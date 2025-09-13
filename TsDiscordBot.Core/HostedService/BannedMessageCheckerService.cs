using TsDiscordBot.Core.Framework;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService
{
    public class BannedMessageCheckerService : IHostedService
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly IMessageReceiver _client;
        private readonly IWebHookService _webHookService;
        private readonly ILogger<BannedMessageCheckerService> _logger;
        private readonly DatabaseService _databaseService;

        private BannedTriggerWord[] _cache = [];
        private BannedTextSetting[] _settingsCache = [];
        private BannedWordTimeoutSetting[] _timeoutSettingsCache = [];
        private BannedExcludeWord[] _excludeCache = [];
        private DateTime _lastFetchTime = DateTime.MinValue;
        private readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);
        private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), List<DateTime>> _userBannedWordTimestamps = new();

        private IDisposable? _subscription1;
        private IDisposable? _subscription2;

        public BannedMessageCheckerService(
            DiscordSocketClient discordSocketClient,
            IMessageReceiver client,
            IWebHookService webHookService,
            ILogger<BannedMessageCheckerService> logger,
            DatabaseService databaseService)
        {
            _discordSocketClient = discordSocketClient;
            _client = client;
            _webHookService = webHookService;
            _logger = logger;
            _databaseService = databaseService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _subscription1 = _client.OnReceivedSubscribe(CheckMessageAsync,nameof(BannedMessageCheckerService),ServicePriority.Urgent);
            _subscription2 = _client.OnEditedSubscribe(CheckMessageAsync,nameof(BannedMessageCheckerService),ServicePriority.Urgent);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscription1?.Dispose();
            _subscription2?.Dispose();
            return Task.CompletedTask;
        }

        private async Task HandleBannedWordAsync(IMessageData messageData, BannedWordTimeoutSetting? timeoutSetting)
        {
            try
            {
                if (timeoutSetting is not null && !timeoutSetting.IsEnabled)
                    return;

                var threshold = timeoutSetting?.Count ?? 5;
                var window = TimeSpan.FromMinutes(timeoutSetting?.WindowMinutes ?? 5);
                var duration = TimeSpan.FromMinutes(timeoutSetting?.TimeoutMinutes ?? 1);

                var key = (messageData.GuildId, messageData.AuthorId);
                var list = _userBannedWordTimestamps.GetOrAdd(key, _ => new List<DateTime>());
                var shouldTimeout = false;
                lock (list)
                {
                    var now = DateTime.UtcNow;
                    list.Add(now);
                    list.RemoveAll(t => (now - t) > window);
                    if (list.Count >= threshold)
                    {
                        list.Clear();
                        shouldTimeout = true;
                    }
                }

                if (shouldTimeout)
                {
                    var user = _discordSocketClient.GetGuild(messageData.GuildId)?.GetUser(messageData.AuthorId);

                    if (user is null)
                    {
                        return;
                    }

                    try
                    {
                        await user.SetTimeOutAsync(duration);
                        _logger.LogInformation("Timed out user {User} for repeated banned words", user.Username);

                        try
                        {
                            await messageData.SendMessageAsyncOnChannel($"{messageData.AuthorMention} さんは不適切な発言が多いため一旦タイムアウトさせてもらったね！");
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
                _logger.LogWarning(ex, "Failed to track banned word usage for {User}", messageData.AuthorName);
            }
        }

        private async Task CheckMessageAsync(IMessageData message)
        {
            try
            {
                if (message.IsBot || message.IsDeleted)
                {
                    return;
                }

                if (message.ChannelName.Contains("閲覧注意"))
                {
                    return;
                }


                if ((DateTime.Now - _lastFetchTime) > CacheDuration)
                {
                    _cache = _databaseService.FindAll<BannedTriggerWord>(BannedTriggerWord.TableName).ToArray();
                    _settingsCache = _databaseService.FindAll<BannedTextSetting>(BannedTextSetting.TableName).ToArray();
                    _timeoutSettingsCache = _databaseService.FindAll<BannedWordTimeoutSetting>(BannedWordTimeoutSetting.TableName).ToArray();
                    _excludeCache = _databaseService.FindAll<BannedExcludeWord>(BannedExcludeWord.TableName).ToArray();
                    _lastFetchTime = DateTime.Now;
                }

                var keywords = _cache
                    .Where(x => x.GuildId == message.GuildId)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Word));

                var excludeWords = _excludeCache
                    .Where(x => x.GuildId == message.GuildId)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Word))
                    .Select(x => x.Word)
                    .ToArray();

                var setting = _settingsCache.FirstOrDefault(x => x.GuildId == message.GuildId);
                if (setting is not null && !setting.IsEnabled)
                {
                    return;
                }

                var timeoutSetting = _timeoutSettingsCache.FirstOrDefault(x => x.GuildId == message.GuildId);

                var content = message.Content;
                var placeholders = new Dictionary<string, string>();
                var idx = 0;
                foreach (var e in excludeWords)
                {
                    var placeholder = $"__ALLOW{idx++}__";
                    placeholders[placeholder] = e;
                    content = Regex.Replace(content, Regex.Escape(e), placeholder, RegexOptions.IgnoreCase);
                }

                foreach (var keyword in keywords)
                {
                    if (content.Contains(keyword.Word, StringComparison.OrdinalIgnoreCase))
                    {
                        var mode = setting?.Mode ?? BannedTextMode.Hide;

                        if (mode == BannedTextMode.Delete)
                        {
                            await message.DeleteAsync();
                            _logger.LogInformation($"Deleted banned message from {message.AuthorName}: {keyword.Word}");

                            try
                            {
                                await message.SendMessageAsyncOnChannel($"🔞 {message.AuthorMention} さん、不適切な発言が検出されたためメッセージを削除しました。");
                            }
                            catch (Exception dmEx)
                            {
                                _logger.LogWarning(dmEx, "Failed to send DM to user");
                            }
                        }
                        else
                        {
                            var sanitized = content;
                            foreach (var k in keywords)
                            {
                                sanitized = Regex.Replace(sanitized, Regex.Escape(k.Word), new string('＊', k.Word.Length), RegexOptions.IgnoreCase);
                            }
                            foreach (var kv in placeholders)
                            {
                                sanitized = sanitized.Replace(kv.Key, kv.Value);
                            }

                            await message.DeleteAsync();
                            var webhookClient = await _webHookService.GetOrCreateWebhookClientAsync(message.ChannelId, "banned-relay");
                            await webhookClient.RelayMessageAsync(message,sanitized);

                        }

                        if (!message.FromAdmin)
                        {
                            await HandleBannedWordAsync(message, timeoutSetting);
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
