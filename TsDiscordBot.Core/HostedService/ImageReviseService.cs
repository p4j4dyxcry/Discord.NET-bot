using System;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace TsDiscordBot.Core.HostedService
{
    public class ImageReviseService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<ImageReviseService> _logger;
        public static readonly Channel<SocketMessage> Queue = Channel.CreateUnbounded<SocketMessage>();

        public ImageReviseService(DiscordSocketClient client, ILogger<ImageReviseService> logger)
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

        private Task OnMessageReceivedAsync(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot)
                    return Task.CompletedTask;

                if (!message.Content.StartsWith("!revise "))
                    return Task.CompletedTask;

                Queue.Writer.TryWrite(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue image revise request");
            }

            return Task.CompletedTask;
        }
    }
}

