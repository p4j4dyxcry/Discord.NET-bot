using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TsDiscordBot.Core.HostedService
{
    public class NauAriService: IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<NauAriService> _logger;

        public NauAriService(DiscordSocketClient client, ILogger<NauAriService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += OnMessageReceivedAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived -= OnMessageReceivedAsync;
            return Task.CompletedTask;
        }

            private async Task OnMessageReceivedAsync(SocketMessage message)
            {
                try
                {
                    if (message.Author.IsBot || message.Channel is not SocketGuildChannel)
                    {
                        return;
                    }

                    if (message.Content.StartsWith("なう(20"))
                    {
                        await message.Channel.SendMessageAsync("なうあり！");
                    }
                }
                catch(Exception e)
                {
                    _logger.LogError(e,"Failed to Nauari");
                }
            }
    }
}