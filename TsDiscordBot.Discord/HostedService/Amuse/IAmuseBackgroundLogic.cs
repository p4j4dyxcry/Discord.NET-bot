using Discord.WebSocket;
using TsDiscordBot.Discord.Amuse;

namespace TsDiscordBot.Discord.HostedService.Amuse
{
    public interface IAmuseBackgroundLogic
    {
        public Task OnButtonExecutedAsync(SocketMessageComponent component);

        public Task ProcessAsync(AmusePlay[] amusePlays);
    }
}