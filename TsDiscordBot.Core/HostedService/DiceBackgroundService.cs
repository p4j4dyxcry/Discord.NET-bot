using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Amuse;
using TsDiscordBot.Core.Game.Dice;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class DiceBackgroundService(
    DiscordSocketClient client,
    ILogger<DiceBackgroundService> logger,
    DatabaseService databaseService) : BackgroundService
{
    private readonly DiscordSocketClient _client = client;
    private readonly ILogger<DiceBackgroundService> _logger = logger;
    private readonly DatabaseService _databaseService = databaseService;
    private readonly ConcurrentDictionary<ulong, DiceSession> _diceGames = new();

    private record DiceSession(AmusePlay Play, DiceGame Game, int Step, DateTime LastUpdate);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var plays = _databaseService
                    .FindAll<AmusePlay>(AmusePlay.TableName)
                    .Where(x => x.GameKind == "DI")
                    .ToArray();

                foreach (var play in plays)
                {
                    if (!_diceGames.TryGetValue(play.MessageId, out var session))
                    {
                        if (play.Started)
                        {
                            continue;
                        }

                        var game = new DiceGame(play.Bet);
                        session = new DiceSession(play, game, 0, DateTime.UtcNow);
                        _diceGames[play.MessageId] = session;
                        play.Started = true;
                        _databaseService.Update(AmusePlay.TableName, play);
                        continue;
                    }

                    if (DateTime.UtcNow - session.LastUpdate < TimeSpan.FromSeconds(1))
                    {
                        continue;
                    }

                    if (_client.GetChannel(session.Play.ChannelId) is not IMessageChannel channel)
                    {
                        continue;
                    }

                    if (await channel.GetMessageAsync(session.Play.MessageId) is not IUserMessage userMessage)
                    {
                        continue;
                    }

                    if (session.Step == 0)
                    {
                        await UpdateDiceMessageAsync(userMessage, session.Game, session.Play, false);
                        _diceGames[play.MessageId] = session with { Step = 1, LastUpdate = DateTime.UtcNow };
                    }
                    else if (session.Step == 1)
                    {
                        await UpdateDiceMessageAsync(userMessage, session.Game, session.Play, true);

                        var result = session.Game.Result;
                        var cash = _databaseService
                            .FindAll<AmuseCash>(AmuseCash.TableName)
                            .FirstOrDefault(x => x.UserId == session.Play.UserId);
                        if (cash is not null)
                        {
                            cash.Cash += result.Payout;
                            cash.LastUpdatedAtUtc = DateTime.UtcNow;
                            _databaseService.Update(AmuseCash.TableName, cash);
                        }

                        _databaseService.Delete(AmusePlay.TableName, session.Play.Id);
                        _diceGames.TryRemove(session.Play.MessageId, out _);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process dice game");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private static readonly string[] DiceCharacters =
    {
        "\u2680",
        "\u2681",
        "\u2682",
        "\u2683",
        "\u2684",
        "\u2685"
    };

    private static async Task UpdateDiceMessageAsync(
        IUserMessage message,
        DiceGame game,
        AmusePlay play,
        bool revealPlayer)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"<@{play.UserId}> さん、");
        builder.AppendLine($"{play.Bet}GAL円 賭けて勝負だよ！！");
        builder.AppendLine($"- つむぎ: {DiceCharacters[game.Result.DealerRoll - 1]}[{game.Result.DealerRoll}]");

        if (revealPlayer)
        {
            builder.AppendLine($"- あなた: {DiceCharacters[game.Result.PlayerRoll - 1]}[{game.Result.PlayerRoll}]");
            var outcome = game.Result.Outcome switch
            {
                DiceOutcome.PlayerWin => "勝利",
                DiceOutcome.DealerWin => "敗北",
                _ => "引き分け"
            };
            builder.AppendLine($"結果: {outcome}！");
            if (game.Result.Outcome == DiceOutcome.PlayerWin)
            {
                builder.AppendLine($"{game.Result.Payout}GAL円ゲット！");
            }
        }
        else
        {
            builder.AppendLine("- あなた: ??");
        }

        await message.ModifyAsync(msg =>
        {
            msg.Content = builder.ToString();
            msg.Components = new ComponentBuilder().Build();
        });
    }
}

