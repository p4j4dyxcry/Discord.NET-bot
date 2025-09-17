using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse
{
    public class BlackJackBackgroundLogic : IAmuseBackgroundLogic
    {
        private record GameSession(AmusePlay Play, BlackJackGame Game);
        private record ReplayRequest(ulong UserId, ulong ChannelId, ulong MessageId, int Bet, string OriginalContent, CancellationTokenSource TimeoutToken);

        private readonly ConcurrentDictionary<ulong, GameSession> _games = new();
        private readonly ConcurrentDictionary<ulong, ReplayRequest> _replayRequests = new();
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

                var action = parts[0];

                if (action is "empty_bj_replay" or "empty_bj_quit")
                {
                    if (!_replayRequests.TryGetValue(messageId, out var replay))
                    {
                        await component.RespondAsync("再戦の情報が見つかりません。", ephemeral: true);
                        return;
                    }

                    if (component.User.Id != replay.UserId)
                    {
                        return;
                    }

                    await component.DeferAsync();

                    if (_client.GetChannel(replay.ChannelId) is not IMessageChannel replayChannel)
                    {
                        return;
                    }

                    if (await replayChannel.GetMessageAsync(replay.MessageId) is not IUserMessage replayMessage)
                    {
                        return;
                    }

                    if (action == "empty_bj_replay")
                    {
                        if (_replayRequests.TryRemove(messageId, out replay))
                        {
                            await replay.TimeoutToken.CancelAsync();
                            await StartReplayAsync(replayMessage, replay);
                        }
                    }
                    else
                    {
                        if (_replayRequests.TryRemove(messageId, out replay))
                        {
                            await replay.TimeoutToken.CancelAsync();
                            await CancelReplayAsync(replayMessage, replay, false);
                        }
                    }

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

                switch (action)
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

                await HandleGameStateAsync(session, userMessage);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle blackjack interaction");
            }
        }

        private async Task HandleGameStateAsync(GameSession session, IUserMessage userMessage)
        {
            var game = session.Game;

            if (game.IsFinished && game.Result is not null)
            {
                var result = game.Result;
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
                _games.TryRemove(session.Play.MessageId, out _);
                await ShowReplayPromptAsync(userMessage, session.Play, session.Play.Bet);
                return;
            }

            await UpdateMessageAsync(userMessage, game, session.Play, false);
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
                if (playerScore == 21)
                {
                    components.WithButton("スタンド", $"empty_bj_stand:{play.MessageId}", ButtonStyle.Secondary);
                }
                else
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
                if (play.MessageId == 0)
                {
                    _databaseService.Delete(AmusePlay.TableName, play.Id);
                    continue;
                }

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
                var session = new GameSession(play, game);
                _games[play.MessageId] = session;
                play.Started = true;
                _databaseService.Update(AmusePlay.TableName, play);

                var cash = _databaseService
                    .FindAll<AmuseCash>(AmuseCash.TableName)
                    .First(x => x.UserId == play.UserId);

                cash.Cash -= play.Bet;
                cash.LastUpdatedAtUtc = DateTime.UtcNow;
                _databaseService.Update(AmuseCash.TableName, cash);

                await HandleGameStateAsync(session, userMessage);
            }
        }

        private async Task ShowReplayPromptAsync(IUserMessage message, AmusePlay play, int previousBet)
        {
            var latest = await message.Channel.GetMessageAsync(message.Id) as IUserMessage;
            var originalContent = latest?.Content ?? message.Content ?? string.Empty;

            var bet = DetermineReplayBet(play.UserId, previousBet);

            if (_replayRequests.TryRemove(play.MessageId, out var existing))
            {
                existing.TimeoutToken.Cancel();
            }

            var builder = new System.Text.StringBuilder(originalContent);
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine($"もう一度{bet}GAL円をベットして始めますか？");

            var components = new ComponentBuilder()
                .WithButton("もう一度遊ぶ", $"empty_bj_replay:{play.MessageId}", ButtonStyle.Primary)
                .WithButton("やめる", $"empty_bj_quit:{play.MessageId}", ButtonStyle.Secondary);

            await message.ModifyAsync(msg =>
            {
                msg.Content = builder.ToString();
                msg.Components = components.Build();
            });

            var tokenSource = new CancellationTokenSource();
            var replay = new ReplayRequest(play.UserId, play.ChannelId, play.MessageId, bet, originalContent, tokenSource);
            _replayRequests[play.MessageId] = replay;
            _ = StartReplayTimeoutAsync(replay);
        }

        private async Task StartReplayAsync(IUserMessage message, ReplayRequest replay)
        {
            var createdAt = DateTime.UtcNow;
            var newPlay = new AmusePlay
            {
                UserId = replay.UserId,
                CreatedAtUtc = createdAt,
                GameKind = "BJ",
                ChannelId = replay.ChannelId,
                Bet = replay.Bet,
                MessageId = replay.MessageId,
                Started = false
            };

            _databaseService.Insert(AmusePlay.TableName, newPlay);

            var insertedPlay = _databaseService
                .FindAll<AmusePlay>(AmusePlay.TableName)
                .Where(x => x.UserId == replay.UserId && x.MessageId == replay.MessageId && x.GameKind == "BJ")
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefault();

            if (insertedPlay is null)
            {
                await CancelReplayAsync(message, replay, false, "ゲームの再開に失敗しました。");
                return;
            }

            insertedPlay.Started = true;
            _databaseService.Update(AmusePlay.TableName, insertedPlay);

            var cash = _databaseService
                .FindAll<AmuseCash>(AmuseCash.TableName)
                .FirstOrDefault(x => x.UserId == replay.UserId);

            if (cash is null)
            {
                var newCash = new AmuseCash
                {
                    UserId = replay.UserId,
                    Cash = 0,
                    LastUpdatedAtUtc = DateTime.UtcNow
                };
                _databaseService.Insert(AmuseCash.TableName, newCash);

                cash = _databaseService
                    .FindAll<AmuseCash>(AmuseCash.TableName)
                    .FirstOrDefault(x => x.UserId == replay.UserId);
            }

            if (cash is not null)
            {
                cash.Cash -= insertedPlay.Bet;
                cash.LastUpdatedAtUtc = DateTime.UtcNow;
                _databaseService.Update(AmuseCash.TableName, cash);
            }

            var game = new BlackJackGame(insertedPlay.Bet);
            var session = new GameSession(insertedPlay, game);
            _games[replay.MessageId] = session;

            await HandleGameStateAsync(session, message);
        }

        private async Task CancelReplayAsync(IUserMessage message, ReplayRequest replay, bool dueToTimeout, string? additionalLine = null)
        {
            await message.ModifyAsync(msg =>
            {
                msg.Content = "また遊ぼうね！";
                msg.Components = new ComponentBuilder().Build();
            });
        }

        private async Task CancelReplayAsync(ReplayRequest replay, bool dueToTimeout)
        {
            if (_client.GetChannel(replay.ChannelId) is not IMessageChannel channel)
            {
                return;
            }

            if (await channel.GetMessageAsync(replay.MessageId) is not IUserMessage message)
            {
                return;
            }

            await CancelReplayAsync(message, replay, dueToTimeout);
        }

        private Task StartReplayTimeoutAsync(ReplayRequest replay)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), replay.TimeoutToken.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (_replayRequests.TryRemove(replay.MessageId, out var removed))
                {
                    await CancelReplayAsync(removed, true);
                }
            });
        }

        private int DetermineReplayBet(ulong userId, int previousBet)
        {
            var cash = _databaseService
                .FindAll<AmuseCash>(AmuseCash.TableName)
                .FirstOrDefault(x => x.UserId == userId);

            if (cash is null || cash.Cash < previousBet)
            {
                return 100;
            }

            return previousBet;
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