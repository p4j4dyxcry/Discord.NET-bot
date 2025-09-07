using System.Collections.Concurrent;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace TsDiscordBot.Core.Services
{
    public class WebHookWrapper
    {
        public static WebHookWrapper Default { get; } = new();

        private readonly ConcurrentDictionary<(ulong,string), DiscordWebhookClient> _webhookCache = new();
        public async Task<WebHookEx> GetOrCreateWebhookClientAsync(ITextChannel channel, string name)
        {
            // 既にキャッシュ済みならそれを返す
            if (_webhookCache.TryGetValue((channel.Id,name), out var cachedClient))
            {
                return new(cachedClient);
            }

            // チャンネルの既存 webhook を探す
            var hooks = await channel.GetWebhooksAsync();
            var hook = hooks.FirstOrDefault(h => h.Name == name)
                       ?? await channel.CreateWebhookAsync(name);

            var client = new DiscordWebhookClient(hook);
            _webhookCache[(channel.Id,name)] = client;

            return new(client);
        }
    }

    public class WebHookEx
    {
        private readonly DiscordWebhookClient _client;

        private ConcurrentDictionary<SocketMessage, IReadOnlyList<(string FileName, string ContentType, byte[] Data)>> _attachmentCache = new();

        public WebHookEx(DiscordWebhookClient client)
        {
            _client = client;
        }

        public async Task RelayMessageAsync(SocketMessage socketMessage, string? content, string? author = null, string? avatarUrl = null, ILogger? logger = null)
        {
            try
            {
                content ??= string.IsNullOrWhiteSpace(socketMessage.Content) ? string.Empty : socketMessage.Content;

                if (author is null)
                {
                    author = (socketMessage.Author as SocketGuildUser)?.Nickname
                             ?? socketMessage.Author.GlobalName
                             ?? socketMessage.Author.Username;
                }

                if (avatarUrl is null)
                {
                    avatarUrl = (socketMessage.Author as SocketGuildUser)?.GetGuildAvatarUrl()
                                    ?? socketMessage.Author.GetAvatarUrl()
                                    ?? socketMessage.Author.GetDefaultAvatarUrl();

                }

                if (!_attachmentCache.TryGetValue(socketMessage, out var attachments))
                {
                    attachments = await DiscordUtility.CorrectAttachmentsAsync(socketMessage, logger);
                    _attachmentCache[socketMessage] = attachments;
                }


                if (attachments is { Count: > 0 })
                {
                    var files = attachments
                        .Select(a => new FileAttachment(new MemoryStream(a.Data), a.FileName, a.ContentType))
                        .ToList();
                    try
                    {
                        await _client.SendFilesAsync(files, text: content, username: author, avatarUrl: avatarUrl);
                    }
                    catch(Exception e)
                    {
                        logger?.LogError(e, "An error occured while sending files.");
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
                        await _client.SendMessageAsync(content ?? string.Empty, username: author, avatarUrl: avatarUrl);
                    }
                    catch(Exception e)
                    {
                        logger?.LogError(e,"An error occured while sending message.");
                    }

                }
            }
            catch(Exception e)
            {
                logger?.LogError(e, "Error while relaying message");
            }
        }
    }
}