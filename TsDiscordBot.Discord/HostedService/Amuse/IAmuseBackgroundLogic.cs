using Discord.WebSocket;
using TsDiscordBot.Core.Amuse;

namespace TsDiscordBot.Core.HostedService.Amuse
{
    public interface IAmuseBackgroundLogic
    {
        public Task OnButtonExecutedAsync(SocketMessageComponent component);

        public Task ProcessAsync(AmusePlay[] amusePlays);
    }
}