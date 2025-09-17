using System.Collections.Concurrent;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Framework;

namespace TsDiscordBot.Discord.Services
{
    public class WebHookService : IWebHookService
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly ILogger<WebHookService> _logger;

        public WebHookService(DiscordSocketClient discordSocketClient, ILogger<WebHookService> logger)
        {
            _discordSocketClient = discordSocketClient;
            _logger = logger;
        }

        private readonly ConcurrentDictionary<(ulong,string), DiscordWebhookClient> _webhookCache = new();
        public async Task<IWebHookClient> GetOrCreateWebhookClientAsync(ulong textChannelId, string name)
        {
            // 既にキャッシュ済みならそれを返す
            if (_webhookCache.TryGetValue((textChannelId,name), out var cachedClient))
            {
                return new WebHookEx(cachedClient, _logger);
            }

            var channel = await _discordSocketClient.GetChannelAsync(textChannelId) as ITextChannel;

            if (channel is null)
            {
                throw new NullReferenceException($"Channel {textChannelId} not found");
            }

            // チャンネルの既存 webhook を探す
            var hooks = await channel.GetWebhooksAsync();
            var hook = hooks.FirstOrDefault(h => h.Name == name)
                       ?? await channel.CreateWebhookAsync(name);

            var client = new DiscordWebhookClient(hook);
            _webhookCache[(textChannelId,name)] = client;

            return new WebHookEx(client, _logger);
        }
    }

    public class WebHookEx : IWebHookClient
    {
        private readonly DiscordWebhookClient _client;
        private readonly ILogger? _logger;

        public WebHookEx(DiscordWebhookClient client,ILogger? logger = null)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<ulong?> RelayMessageAsync(IMessageData message, string? content, string? author = null, string? avatarUrl = null)
        {
            try
            {
                content ??= string.IsNullOrWhiteSpace(message.Content) ? string.Empty : message.Content;

                if (author is null)
                {
                    author = message.AuthorName;
                }

                if (avatarUrl is null)
                {
                    avatarUrl = message.AvatarUrl;
                }

                ulong? messageId = null;

                await message.CreateAttachmentSourceIfNotCachedAsync();

                if (message.Attachments is { Count: > 0 })
                {
                    var files = message.Attachments
                        .Select(a => new FileAttachment(new MemoryStream(a.Bytes), a.FileName))
                        .ToList();
                    try
                    {
                        messageId = await _client.SendFilesAsync(files, text: content, username: author, avatarUrl: avatarUrl);
                    }
                    catch(Exception e)
                    {
                        _logger?.LogError(e, "An error occured while sending files.");
                    }
                    finally
                    {
                        foreach (var f in files)
                        {
                            await f.Stream.DisposeAsync();
                        }
                    }
                }
                else
                {
                    try
                    {
                        messageId = await _client.SendMessageAsync(content ?? string.Empty, username: author, avatarUrl: avatarUrl);
                    }
                    catch(Exception e)
                    {
                        _logger?.LogError(e,"An error occured while sending message.");
                    }

                }

                return messageId;
            }
            catch(Exception e)
            {
                _logger?.LogError(e, "Error while relaying message");
                return null;
            }
        }
    }
}