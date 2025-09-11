using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService
{
    public class BannedMessageCheckerService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<BannedMessageCheckerService> _logger;
        private readonly DatabaseService _databaseService;

        private BannedTriggerWord[] _cache = [];
        private BannedTextSetting[] _settingsCache = [];
        private DateTime _lastFetchTime = DateTime.MinValue;
        private readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);
        private readonly ConcurrentDictionary<ulong, List<DateTime>> _userBannedWordTimestamps = new();

        public BannedMessageCheckerService(
            DiscordSocketClient client,
            ILogger<BannedMessageCheckerService> logger,
            DatabaseService databaseService)
        {
            _client = client;
            _logger = logger;
            _databaseService = databaseService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += OnMessageReceivedAsync;
            _client.MessageUpdated += OnMessageUpdatedAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived -= OnMessageReceivedAsync;
            _client.MessageUpdated -= OnMessageUpdatedAsync;
            return Task.CompletedTask;
        }

        private Task OnMessageReceivedAsync(SocketMessage message) => CheckMessageAsync(message);

        private Task OnMessageUpdatedAsync(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after is null)
                return Task.CompletedTask;

            return CheckMessageAsync(after);
        }

        private async Task HandleBannedWordAsync(SocketGuildUser user, ISocketMessageChannel channel)
        {
            try
            {
                var list = _userBannedWordTimestamps.GetOrAdd(user.Id, _ => new List<DateTime>());
                var shouldTimeout = false;
                lock (list)
                {
                    var now = DateTime.UtcNow;
                    list.Add(now);
                    list.RemoveAll(t => (now - t) > TimeSpan.FromMinutes(5));
                    if (list.Count >= 5)
                    {
                        list.Clear();
                        shouldTimeout = true;
                    }
                }

                if (shouldTimeout)
                {
                    try
                    {
                        await user.SetTimeOutAsync(TimeSpan.FromMinutes(1));
                        _logger.LogInformation("Timed out user {User} for repeated banned words", user.Username);

                        try
                        {
                            await channel.SendMessageAsync($"{user.Mention} さんは不適切な発言が多いため一旦タイムアウトさせてもらったね！");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to notify channel about timeout for {User}", user.Username);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to timeout user {User}", user.Username);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track banned word usage for {User}", user.Username);
            }
        }

        private async Task CheckMessageAsync(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot || message.Channel is not SocketGuildChannel guildChannel)
                    return;

                if (message.Channel.Name.Contains("閲覧注意"))
                    return;

                var currentUser = guildChannel.Guild.CurrentUser;
                if (!currentUser.GetPermissions(guildChannel).ManageMessages)
                {
                    _logger.LogWarning("Missing ManageMessages permission in channel {ChannelName}", guildChannel.Name);
                    return;
                }

                var guildId = guildChannel.Guild.Id;

                if ((DateTime.Now - _lastFetchTime) > CacheDuration)
                {
                    _cache = _databaseService.FindAll<BannedTriggerWord>(BannedTriggerWord.TableName).ToArray();
                    _settingsCache = _databaseService.FindAll<BannedTextSetting>(BannedTextSetting.TableName).ToArray();
                    _lastFetchTime = DateTime.Now;
                }

                var keywords = _cache
                    .Where(x => x.GuildId == guildId)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Word));

                var setting = _settingsCache.FirstOrDefault(x => x.GuildId == guildId);
                if (setting is not null && !setting.IsEnabled)
                {
                    return;
                }

                foreach (var keyword in keywords)
                {
                    if (message.Content.Contains(keyword.Word, StringComparison.OrdinalIgnoreCase))
                    {
                        var mode = setting?.Mode ?? BannedTextMode.Hide;

                        if (mode == BannedTextMode.Delete)
                        {
                            await message.DeleteAsync();
                            _logger.LogInformation($"Deleted banned message from {message.Author.Username}: {keyword.Word}");

                            try
                            {
                                await message.Channel.SendMessageAsync(
                                    $"🔞 {message.Author.Mention} さん、不適切な発言が検出されたためメッセージを削除しました。");
                            }
                            catch (Exception dmEx)
                            {
                                _logger.LogWarning(dmEx, "Failed to send DM to user");
                            }
                        }
                        else
                        {
                            var sanitized = message.Content;
                            foreach (var k in keywords)
                            {
                                sanitized = Regex.Replace(sanitized, Regex.Escape(k.Word), new string('＊', k.Word.Length), RegexOptions.IgnoreCase);
                            }

                            if (message is IUserMessage userMessage && userMessage.Author.Id == _client.CurrentUser.Id)
                            {
                                try
                                {
                                    _logger.LogDebug("Modifying bot's own message for sanitization");
                                    await userMessage.ModifyAsync(m => m.Content = sanitized);
                                }
                                catch (HttpException httpEx) when (httpEx.HttpCode == HttpStatusCode.Forbidden)
                                {
                                    _logger.LogWarning(httpEx, "Missing permissions to modify message");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to modify message, deleting and reposting sanitized copy");
                                    await userMessage.DeleteAsync();
                                    if (message.Channel is ITextChannel editChannel)
                                    {
                                        var username = DiscordUtility.GetAuthorNameFromMessage(message);
                                        var avatarUrl = DiscordUtility.GetAvatarUrlFromMessage(message);
                                        var webhookClient = await WebHookWrapper.Default.GetOrCreateWebhookClientAsync(editChannel, "banned-relay");
                                        await webhookClient.RelayMessageAsync(message,sanitized, author: username, avatarUrl: avatarUrl,_logger);
                                    }
                                    else
                                    {
                                        await message.Channel.SendMessageAsync($"{message.Author.Mention}: {sanitized}");
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Deleting and reposting message not sent by bot");
                                try
                                {
                                    await message.DeleteAsync();
                                    if (message.Channel is ITextChannel channel)
                                    {
                                        var webhookClient = await WebHookWrapper.Default.GetOrCreateWebhookClientAsync(channel, "banned-relay");
                                        await webhookClient.RelayMessageAsync(message,sanitized, logger:_logger);
                                    }
                                    else
                                    {
                                        await message.Channel.SendMessageAsync($"{message.Author.Mention}: {sanitized}");
                                    }
                                }
                                catch (HttpException httpEx) when (httpEx.HttpCode == HttpStatusCode.Forbidden)
                                {
                                    _logger.LogWarning(httpEx, "Missing permissions to delete message or send sanitized copy");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to delete message or send sanitized copy");
                                }
                            }

                            _logger.LogInformation($"Masked banned message from {message.Author.Username}: {keyword.Word}");
                        }

                        if (message.Author is SocketGuildUser guildUser && !guildUser.GuildPermissions.Administrator)
                        {
                            await HandleBannedWordAsync(guildUser, message.Channel);
                        }

                        break; // 1件でもヒットしたら処理終了（重複削除防止）
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in banned message checker");
            }
        }
    }
}
