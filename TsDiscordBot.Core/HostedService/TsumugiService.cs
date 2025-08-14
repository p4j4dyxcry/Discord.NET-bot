using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService
{
    public class TsumugiService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<NauAriService> _logger;
        private readonly OpenAIService _openAiService;

        private readonly ConcurrentDictionary<ulong, ConvertedMessage[]> _firstHistory = new();

        public TsumugiService(DiscordSocketClient client, ILogger<NauAriService> logger, OpenAIService openAiService)
        {
            _client = client;
            _logger = logger;
            _openAiService = openAiService;
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
                if (!_firstHistory.TryGetValue(message.Channel.Id, out var cache))
                {
                    _firstHistory[message.Channel.Id] = (await message.Channel.GetMessagesAsync()
                            .FlattenAsync())
                        .Select(DiscordToOpenAIMessageConverter.ConvertFromDiscord)
                        .ToArray();
                }

                if (message.Author.IsBot || message.Channel is not SocketGuildChannel guildChannel)
                {
                    return;
                }

                if (message.MentionedUsers.Any(x => x.Id == _client.CurrentUser.Id) ||
                    message.Content.StartsWith("!つむぎ"))
                {
                    var reply = await GetReplyReferenceAsync(message);
                    var messageStruct = DiscordToOpenAIMessageConverter.ConvertFromDiscord(message, reply);

                    var previousMessages = message.Channel.GetCachedMessages(100)
                        .Select(DiscordToOpenAIMessageConverter.ConvertFromDiscord)
                        .ToArray();

                    previousMessages = _firstHistory[message.Channel.Id]
                        .Concat(previousMessages)
                        .OrderBy(x => x.Date)
                        .TakeLast(30)
                        .ToArray();

                    string result = await _openAiService.GetResponse(guildChannel.Guild.Id, messageStruct, previousMessages);

                    await message.Channel.SendMessageAsync(result);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to Nauari");
            }
        }

        private async Task<ConvertedMessage?> GetReplyReferenceAsync(IMessage message)
        {
            ConvertedMessage? reply = null;
            if (message.Reference?.MessageId.IsSpecified == true)
            {
                var referencedMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);

                if (referencedMessage != null)
                {
                    reply = DiscordToOpenAIMessageConverter.ConvertFromDiscord(referencedMessage);
                }
            }

            return reply;
        }
    }
}