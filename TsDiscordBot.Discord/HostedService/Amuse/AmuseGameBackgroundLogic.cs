using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse
{
    public class AmuseGameBackgroundLogic : IAmuseBackgroundLogic
    {
        private readonly ILogger _logger;
        private readonly AmuseGameManager _amuseGameManager;

        public AmuseGameBackgroundLogic(
            IDatabaseService databaseService,
            ILogger logger,
            DiscordSocketClient client,
            EmoteDatabase emoteDatabase)
        {
            _amuseGameManager = new AmuseGameManager(client, databaseService, emoteDatabase);

            _logger = logger;
        }

        public Task OnButtonExecutedAsync(SocketMessageComponent component)
        {
            return _amuseGameManager.OnUpdateMessageAsync(component);
        }

        public async Task ProcessAsync(AmusePlay[] amusePlays)
        {
            foreach (var amusePlay in amusePlays)
            {
                await _amuseGameManager.UpdateGameAsync(amusePlay);
            }
        }
    }
}