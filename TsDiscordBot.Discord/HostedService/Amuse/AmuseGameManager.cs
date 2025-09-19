using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
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
        private TimeSpan _checkSessionInterval = TimeSpan.FromSeconds(5);
        private DateTime _lastChecked = DateTime.MinValue;

        private ConcurrentDictionary<ulong, GameStateMachine> _sessions  = new();
        public AmuseGameManager(
            DiscordSocketClient discordSocketClient,
            IDatabaseService databaseService,
            EmoteDatabase emoteDatabase)
        {
            _discordSocketClient = discordSocketClient;
            _databaseService = databaseService;
            _emoteDatabase = emoteDatabase;
        }

        public Task CheckSessionAsync()
        {
            var now = DateTime.UtcNow;
            if (now - _lastChecked < _checkSessionInterval)
            {
                return Task.CompletedTask;
            }
            _lastChecked = now;

            var sessions = _sessions.ToArray();
            var sessionInDatabase = _databaseService.FindAll<AmusePlay>(AmusePlay.TableName)
                .ToDictionary(x => x.Id);

            foreach (var session in sessions)
            {
                if (!sessionInDatabase.ContainsKey(session.Value.AmusePlay.Id))
                {
                    _sessions.TryRemove(session.Value.AmusePlay.MessageId, out _);
                }
            }

            return Task.CompletedTask;
        }

        public async Task UpdateGameAsync(AmusePlay amusePlay)
        {
            await TryStartNewGameIfNot(amusePlay);
            await CheckSessionAsync();
        }

        public async Task TryStartNewGameIfNot(AmusePlay amusePlay)
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
            }
        }

        private void StopGame(AmusePlay amusePlay)
        {
            _sessions.TryRemove(amusePlay.MessageId, out _);
            _databaseService.Delete(AmusePlay.TableName, amusePlay.Id);
        }
    }
}