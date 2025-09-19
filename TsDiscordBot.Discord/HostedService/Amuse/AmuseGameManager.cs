using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.Dice;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.HostedService.Amuse.BlackJackState;
using TsDiscordBot.Discord.HostedService.Amuse.HighLowState;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse
{
public class AmuseGameManager
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly IDatabaseService _databaseService;
        private readonly EmoteDatabase _emoteDatabase;
        private readonly ConcurrentDictionary<ulong, DiceSession> _diceSessions = new();
        private readonly TimeSpan _checkSessionInterval = TimeSpan.FromSeconds(5);
        private DateTime _lastChecked = DateTime.MinValue;

        private readonly ConcurrentDictionary<ulong, GameStateMachine> _sessions  = new();
        private record DiceSession(AmusePlay Play, DiceGame Game, int Step, DateTime LastUpdate);

        private static readonly string[] DiceCharacters =
        {
            "\u2680",
            "\u2681",
            "\u2682",
            "\u2683",
            "\u2684",
            "\u2685"
        };
        public AmuseGameManager(
            DiscordSocketClient discordSocketClient,
            IDatabaseService databaseService,
            EmoteDatabase emoteDatabase)
        {
            _discordSocketClient = discordSocketClient;
            _databaseService = databaseService;
            _emoteDatabase = emoteDatabase;
        }

        public async Task ProcessAsync(AmusePlay[] amusePlays)
        {
            foreach (var amusePlay in amusePlays)
            {
                if (amusePlay.GameKind == "DI")
                {
                    await ProcessDiceGameAsync(amusePlay);
                    continue;
                }

                await TryStartNewGameIfNot(amusePlay);
            }

            await CheckSessionAsync(amusePlays);
        }

        private Task CheckSessionAsync(IEnumerable<AmusePlay> amusePlays)
        {
            var now = DateTime.UtcNow;
            if (now - _lastChecked < _checkSessionInterval)
            {
                return Task.CompletedTask;
            }
            _lastChecked = now;

            var activeSessions = amusePlays.ToDictionary(x => x.Id);

            foreach (var session in _sessions.ToArray())
            {
                if (!activeSessions.ContainsKey(session.Value.AmusePlay.Id))
                {
                    _sessions.TryRemove(session.Value.AmusePlay.MessageId, out _);
                }
            }

            foreach (var diceSession in _diceSessions.ToArray())
            {
                if (!activeSessions.ContainsKey(diceSession.Value.Play.Id))
                {
                    _diceSessions.TryRemove(diceSession.Key, out _);
                }
            }

            return Task.CompletedTask;
        }

        private async Task TryStartNewGameIfNot(AmusePlay amusePlay)
        {
            if (amusePlay.Started)
            {
                return;
            }

            if (!CanBuildGame(amusePlay))
            {
                return;
            }

            if (_discordSocketClient.GetChannel(amusePlay.ChannelId) is not IMessageChannel channel)
            {
                return;
            }

            if (await channel.GetMessageAsync(amusePlay.MessageId) is not IUserMessage userMessage)
            {
                return;
            }

            // Starting game.
            amusePlay.Started = true;
            _databaseService.Update(AmusePlay.TableName, amusePlay);

            // Pay bet.
            _databaseService.AddUserCash(amusePlay.UserId, -amusePlay.Bet);

            IGameState game = BuildGame(amusePlay);

            var result = new GameStateMachine(game, amusePlay);
            await result.OnGameEnter(userMessage);

            _sessions[amusePlay.MessageId] = result;
        }

        private bool CanBuildGame(AmusePlay amusePlay)
        {
            return amusePlay.GameKind is "BJ" or "HL";
        }

        private IGameState BuildGame(AmusePlay amusePlay)
        {
            return amusePlay.GameKind switch
            {
                "BJ" => new BlackJackInitGameState(amusePlay.Bet, amusePlay, _databaseService, _emoteDatabase),
                "HL" => new HighLowGuessGameState(new HighLowGameContext(amusePlay, _databaseService, _emoteDatabase, _discordSocketClient)),
                _ => QuitGameState.Default
            };
        }

        public async Task OnUpdateMessageAsync(SocketMessageComponent component)
        {
           var sessions = _sessions.ToArray();

           Task<(ulong,GameProgress)>[] tasks = sessions
               .Select(x => x.Value.OnUpdateMessageAsync(component))
               .ToArray();

           (ulong id, GameProgress result)[] results = await Task.WhenAll(tasks);

           foreach (var exit in results.Where(x=>x.result == GameProgress.Exit))
           {
               StopGame(exit.id);
           }
        }

        private void StopGame(ulong messageId)
        {
            if (_sessions.TryRemove(messageId, out var data))
            {
                _databaseService.Delete(AmusePlay.TableName, data.AmusePlay.Id);
                return;
            }

            if (_diceSessions.TryRemove(messageId, out var diceSession))
            {
                _databaseService.Delete(AmusePlay.TableName, diceSession.Play.Id);
            }
        }

        private void StopGame(AmusePlay amusePlay)
        {
            _sessions.TryRemove(amusePlay.MessageId, out _);
            _diceSessions.TryRemove(amusePlay.MessageId, out _);
            _databaseService.Delete(AmusePlay.TableName, amusePlay.Id);
        }

        private static async Task UpdateDiceMessageAsync(IUserMessage message, DiceGame game, AmusePlay play, bool revealPlayer)
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

        private async Task ProcessDiceGameAsync(AmusePlay play)
        {
            if (play.MessageId == 0)
            {
                _databaseService.Delete(AmusePlay.TableName, play.Id);
                return;
            }

            if (!_diceSessions.TryGetValue(play.MessageId, out var session))
            {
                if (play.Started)
                {
                    return;
                }

                var game = new DiceGame(play.Bet);
                session = new DiceSession(play, game, 0, DateTime.UtcNow);
                _diceSessions[play.MessageId] = session;
                play.Started = true;
                _databaseService.Update(AmusePlay.TableName, play);

                _databaseService.AddUserCash(play.UserId, -play.Bet);
                return;
            }

            if (DateTime.UtcNow - session.LastUpdate < TimeSpan.FromSeconds(1))
            {
                return;
            }

            if (_discordSocketClient.GetChannel(session.Play.ChannelId) is not IMessageChannel channel)
            {
                return;
            }

            if (await channel.GetMessageAsync(session.Play.MessageId) is not IUserMessage userMessage)
            {
                return;
            }

            if (session.Step == 0)
            {
                await UpdateDiceMessageAsync(userMessage, session.Game, session.Play, false);
                _diceSessions[play.MessageId] = session with { Step = 1, LastUpdate = DateTime.UtcNow };
                return;
            }

            if (session.Step == 1)
            {
                await UpdateDiceMessageAsync(userMessage, session.Game, session.Play, true);

                var result = session.Game.Result;
                if (result.Payout != 0)
                {
                    _databaseService.AddUserCash(session.Play.UserId, result.Payout);
                }

                _databaseService.UpdateGameRecord(session.Play, result.Outcome == DiceOutcome.PlayerWin);

                _databaseService.Delete(AmusePlay.TableName, session.Play.Id);
                _diceSessions.TryRemove(session.Play.MessageId, out _);
            }
        }
    }
}