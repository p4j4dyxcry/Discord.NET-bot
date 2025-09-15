using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Framework;

namespace TsDiscordBot.Core.HostedService
{
    public class NauAriService : IHostedService
    {
        private readonly IMessageReceiver _client;
        private readonly ILogger<NauAriService> _logger;
        private IDisposable? _subscription;
        public NauAriService(IMessageReceiver client, ILogger<NauAriService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _subscription = _client.OnReceivedSubscribe(
                OnMessageReceived,
                MessageConditions.NotFromBot,
                nameof(NauAriService),
                ServicePriority.Low);
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscription?.Dispose();
            return Task.CompletedTask;
        }

        private async Task OnMessageReceived(IMessageData message, CancellationToken token)
        {
            if (message.Content.StartsWith("なう(20"))
            {
                await message.SendMessageAsyncOnChannel("なうあり！");
            }
        }
    }
}