using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Amuse;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService.Amuse
{
    public class BlackJackBackgroundLogic : IAmuseBackgroundLogic
    {
        private record GameSession(AmusePlay Play, BlackJackGame Game);

        private readonly ConcurrentDictionary<ulong, GameSession> _games = new();
        private readonly DatabaseService _databaseService;
        private readonly ILogger _logger;
        private readonly DiscordSocketClient _client;

        public BlackJackBackgroundLogic(DatabaseService databaseService, ILogger logger, DiscordSocketClient client)
        {
            _databaseService = databaseService;
            _logger = logger;
            _client = client;
        }

        public async Task OnButtonExecutedAsync(SocketMessageComponent component)
        {
            try
            {
                if (!component.Data.CustomId.StartsWith("empty_bj_"))
                {
                    return;
                }

                var parts = component.Data.CustomId.Split(':');
                if (parts.Length != 2)
                {
                    return;
                }

                if (!ulong.TryParse(parts[1], out var messageId))
                {
                    return;
                }

                if (!_games.TryGetValue(messageId, out var session))
                {
                    await component.RespondAsync("ゲームが見つかりません。", ephemeral: true);
                    return;
                }

                if (component.User.Id != session.Play.UserId)
                {
                    return;
                }

                await component.DeferAsync();

                var game = session.Game;

                switch (parts[0])
                {
                    case "empty_bj_hit":
                        game.Hit();
                        break;
                    case "empty_bj_stand":
                        game.Stand();
                        break;
                    case "empty_bj_double":
                        if (!game.DoubleDowned && game.PlayerCards.Count == 2)
                        {
                            var cash = _databaseService
                                .FindAll<AmuseCash>(AmuseCash.TableName)
                                .FirstOrDefault(x => x.UserId == session.Play.UserId);
                            if (cash is not null && cash.Cash >= session.Play.Bet)
                            {
                                cash.Cash -= session.Play.Bet;
                                cash.LastUpdatedAtUtc = DateTime.UtcNow;
                                _databaseService.Update(AmuseCash.TableName, cash);
                                game.DoubleDown();
                            }
                        }

                        break;
                }

                if (_client.GetChannel(session.Play.ChannelId) is not IMessageChannel channel)
                {
                    return;
                }

                if (await channel.GetMessageAsync(session.Play.MessageId) is not IUserMessage userMessage)
                {
                    return;
                }

                if (game.IsFinished)
                {
                    var result = game.Result!;
                    var cash = _databaseService
                        .FindAll<AmuseCash>(AmuseCash.TableName)
                        .FirstOrDefault(x => x.UserId == session.Play.UserId);
                    if (cash is not null)
                    {
                        cash.Cash += result.Payout;
                        cash.LastUpdatedAtUtc = DateTime.UtcNow;
                        _databaseService.Update(AmuseCash.TableName, cash);
                    }

                    UpdateGameRecord(session.Play.UserId, session.Play.GameKind,
                        result.Outcome == GameOutcome.PlayerWin);

                    await UpdateMessageAsync(userMessage, game, session.Play, true);

                    _databaseService.Delete(AmusePlay.TableName, session.Play.Id);
                    _games.TryRemove(messageId, out _);
                }
                else
                {
                    await UpdateMessageAsync(userMessage, game, session.Play, false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle blackjack interaction");
            }
        }

        private async Task UpdateMessageAsync(IUserMessage message, BlackJackGame game, AmusePlay play, bool revealDealer)
        {
            var dealerCards = revealDealer
                ? string.Join(" ", game.DealerCards.Select(FormatCard))
                : $"{FormatCard(game.DealerVisibleCard)} ??";
            var dealerScore = revealDealer
                ? BlackJackGame.CalculateScore(game.DealerCards).ToString()
                : BlackJackGame.CalculateScore([game.DealerCards[0]]).ToString();

            var playerCards = string.Join(" ", game.PlayerCards.Select(FormatCard));
            var playerScore = BlackJackGame.CalculateScore(game.PlayerCards);

            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"<@{play.UserId}> さん、");
            builder.AppendLine($"{play.Bet}GAL円 賭けて勝負だよ！！");
            builder.AppendLine($"- つむぎ [{dealerScore}]: {dealerCards}");
            builder.AppendLine($"- あなた [{playerScore}]: {playerCards}");

            if (game.IsFinished && game.Result is not null)
            {
                var outcome = game.Result.Outcome switch
                {
                    GameOutcome.PlayerWin => "勝利",
                    GameOutcome.DealerWin => "敗北",
                    _ => "引き分け"
                };
                builder.AppendLine($"結果: {outcome}！");

                if (game.Result.Outcome == GameOutcome.PlayerWin)
                {
                    builder.AppendLine($"{game.Result.Payout}GAL円ゲット！");
                }
            }

            var components = new ComponentBuilder();
            if (!game.IsFinished)
            {
                components.WithButton("ヒット", $"empty_bj_hit:{play.MessageId}", ButtonStyle.Primary);
                components.WithButton("スタンド", $"empty_bj_stand:{play.MessageId}", ButtonStyle.Secondary);
                if (!game.DoubleDowned && game.PlayerCards.Count == 2)
                {
                    var cash = _databaseService
                        .FindAll<AmuseCash>(AmuseCash.TableName)
                        .FirstOrDefault(x => x.UserId == play.UserId);
                    if (cash is not null && cash.Cash >= play.Bet)
                    {
                        components.WithButton("ダブルダウン", $"empty_bj_double:{play.MessageId}", ButtonStyle.Danger);
                    }
                }
            }

            await message.ModifyAsync(msg =>
            {
                msg.Content = builder.ToString();
                msg.Components = components.Build();
            });
        }

        public async Task ProcessAsync(AmusePlay[] amusePlays)
        {
            foreach (var play in amusePlays.Where(x => x.GameKind == "BJ" && !x.Started))
            {
                if (_games.ContainsKey(play.MessageId))
                {
                    continue;
                }

                if (_client.GetChannel(play.ChannelId) is not IMessageChannel channel)
                {
                    continue;
                }

                if (await channel.GetMessageAsync(play.MessageId) is not IUserMessage userMessage)
                {
                    continue;
                }

                var game = new BlackJackGame(play.Bet);
                _games[play.MessageId] = new GameSession(play, game);
                play.Started = true;
                _databaseService.Update(AmusePlay.TableName, play);
                await UpdateMessageAsync(userMessage, game, play, false);
            }
        }

        private void UpdateGameRecord(ulong userId, string gameKind, bool win)
        {
            var record = _databaseService
                .FindAll<AmuseGameRecord>(AmuseGameRecord.TableName)
                .FirstOrDefault(x => x.UserId == userId && x.GameKind == gameKind);

            if (record is null)
            {
                record = new AmuseGameRecord
                {
                    UserId = userId,
                    GameKind = gameKind,
                    TotalPlays = 0,
                    WinCount = 0
                };
                _databaseService.Insert(AmuseGameRecord.TableName, record);
            }

            record.TotalPlays++;
            if (win)
            {
                record.WinCount++;
            }

            _databaseService.Update(AmuseGameRecord.TableName, record);
        }

        private static string FormatCard(Card c)
        {
            var rank = c.Rank switch
            {
                Rank.Ace => "A",
                Rank.King => "K",
                Rank.Queen => "Q",
                Rank.Jack => "J",
                _ => ((int)c.Rank).ToString()
            };
            var suit = c.Suit switch
            {
                Suit.Clubs => "♣",
                Suit.Diamonds => "♦",
                Suit.Hearts => "♥",
                _ => "♠"
            };
            return rank + suit;
        }
    }
}