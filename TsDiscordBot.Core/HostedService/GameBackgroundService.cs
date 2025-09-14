using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Amuse;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class GameBackgroundService(DiscordSocketClient client, ILogger<GameBackgroundService> logger, DatabaseService databaseService) : BackgroundService
{
    private readonly DiscordSocketClient _client = client;
    private readonly ILogger<GameBackgroundService> _logger = logger;
    private readonly DatabaseService _databaseService = databaseService;
    private readonly ConcurrentDictionary<ulong, GameSession> _games = new();

    private record GameSession(AmusePlay Play, BlackJackGame Game);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.ButtonExecuted += OnButtonExecuted;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var plays = _databaseService
                    .FindAll<AmusePlay>(AmusePlay.TableName)
                    .ToArray();

                await ProcessBlackJackGames(plays);

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to start blackjack game");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        _client.ButtonExecuted -= OnButtonExecuted;
    }

    private async Task ProcessBlackJackGames(AmusePlay[] amusePlays)
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

    private async Task OnButtonExecuted(SocketMessageComponent component)
    {
        try
        {
            if (!component.Data.CustomId.StartsWith("bj_"))
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

            // await component.DeferAsync();

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

    private static async Task UpdateMessageAsync(IUserMessage message, BlackJackGame game, AmusePlay play, bool revealDealer)
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
        builder.AppendLine($"<@{play.UserId}> が {play.Bet} がベットしました！");
        builder.AppendLine($"つむぎ [{dealerScore}]: {dealerCards}");
        builder.AppendLine($"あなた [{playerScore}]: {playerCards}");

        if (game.IsFinished && game.Result is not null)
        {
            var outcome = game.Result.Outcome switch
            {
                GameOutcome.PlayerWin => "勝利",
                GameOutcome.DealerWin => "敗北",
                _ => "引き分け"
            };
            builder.AppendLine($"結果: {outcome}！ {game.Result.Payout}GAL円ゲット！");
        }

        var components = new ComponentBuilder();
        if (!game.IsFinished)
        {
            components.WithButton("ヒット", $"empty_bj_hit:{play.MessageId}", ButtonStyle.Primary);
            components.WithButton("スタンド", $"empty_bj_stand:{play.MessageId}", ButtonStyle.Secondary);
            if (!game.DoubleDowned && game.PlayerCards.Count == 2)
            {
                components.WithButton("ダブルダウン", $"empty_bj_double:{play.MessageId}", ButtonStyle.Danger);
            }
        }

        await message.ModifyAsync(msg =>
        {
            msg.Content = builder.ToString();
            msg.Components = components.Build();
        });
    }
}

