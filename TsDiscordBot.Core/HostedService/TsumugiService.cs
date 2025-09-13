using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService
{
    public class TsumugiService : IHostedService
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly IMessageReceiver _client;
        private readonly ILogger<NauAriService> _logger;
        private readonly OpenAIService _openAiService;

        private readonly ConcurrentDictionary<ulong, ConvertedMessage[]> _firstHistory = new();

        private IDisposable? _subscription;

        public TsumugiService(DiscordSocketClient discordSocketClient, IMessageReceiver client, ILogger<NauAriService> logger, OpenAIService openAiService)
        {
            _discordSocketClient = discordSocketClient;
            _client = client;
            _logger = logger;
            _openAiService = openAiService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _subscription = _client.OnReceivedSubscribe(OnMessageReceivedAsync, nameof(TsumugiService));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscription?.Dispose();
            return Task.CompletedTask;
        }

        private async Task OnMessageReceivedAsync(IMessageData message)
        {
            if (message.IsDeleted || message.IsBot)
            {
                return;
            }

            if (message.Content.StartsWith("!revise"))
            {
                return;
            }

            try
            {
                var channel = await _discordSocketClient.GetChannelAsync(message.ChannelId) as ISocketMessageChannel;

                if (channel is null)
                {
                    return;
                }

                if (!_firstHistory.TryGetValue(message.ChannelId, out var cache))
                {
                    var firstMessages = await channel.GetMessagesAsync()
                        .FlattenAsync();
                    var convertedFirst = new List<ConvertedMessage>();
                    foreach (var m in firstMessages)
                    {
                        var c = await MessageData.FromIMessageAsync(m,_logger);
                        convertedFirst.Add(ConvertMessageAsync(c));
                    }
                    _firstHistory[message.ChannelId] = convertedFirst.ToArray();
                }

                if (message.MentionTsumugi || message.Content.StartsWith("!つむぎ"))
                {
                    var previousMessagesTasks = channel.GetCachedMessages(100)
                        .Where(m => m.Id != message.Id)
                        .Select(async x => await MessageData.FromIMessageAsync(x))
                        .Select(async x => ConvertMessageAsync(await x));

                    var previousMessages = await Task.WhenAll(previousMessagesTasks);

                    var current = ConvertMessageAsync(message);

                    previousMessages = _firstHistory[message.ChannelId]
                        .Concat(previousMessages)
                        .Concat(new[] { current })
                        .OrderBy(x => x.Date)
                        .TakeLast(30)
                        .ToArray();

                    string result = await _openAiService.GetResponse(message.GuildId, null, previousMessages);

                    await message.SendMessageAsyncOnChannel(result);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to Nauari");
            }
        }

        private ConvertedMessage ConvertMessageAsync(IMessageData message)
        {
            return DiscordToOpenAIMessageConverter.ConvertFromDiscord(message);
        }
    }
}