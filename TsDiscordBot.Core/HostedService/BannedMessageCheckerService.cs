using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService
{
    public class BannedMessageCheckerService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<BannedMessageCheckerService> _logger;
        private readonly DatabaseService _databaseService;

        private BannedTriggerWord[] _cache = [];
        private BannedTextSetting[] _modeCache = [];
        private DateTime _lastFetchTime = DateTime.MinValue;
        private readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

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
                if (message.Author.IsBot || message.Channel is not SocketGuildChannel guildChannel)
                    return;

                if(message.Channel.Name.Contains("閲覧注意"))
                    return;

                var guildId = guildChannel.Guild.Id;

                if ((DateTime.Now - _lastFetchTime) > CacheDuration)
                {
                    _cache = _databaseService.FindAll<BannedTriggerWord>(BannedTriggerWord.TableName).ToArray();
                    _modeCache = _databaseService.FindAll<BannedTextSetting>(BannedTextSetting.TableName).ToArray();
                    _lastFetchTime = DateTime.Now;
                }

                var keywords = _cache
                    .Where(x => x.GuildId == guildId)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Word));

                foreach (var keyword in keywords)
                {
                    if (message.Content.Contains(keyword.Word, StringComparison.OrdinalIgnoreCase))
                    {
                        var mode = _modeCache.FirstOrDefault(x => x.GuildId == guildId)?.Mode ?? BannedTextMode.Hide;

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

                            try
                            {
                                if (message is IUserMessage userMessage)
                                {
                                    await userMessage.ModifyAsync(m => m.Content = sanitized);
                                }
                                else
                                {
                                    await message.DeleteAsync();
                                    await message.Channel.SendMessageAsync($"{message.Author.Mention}: {sanitized}");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to modify message, sending sanitized copy instead.");
                                await message.DeleteAsync();
                                await message.Channel.SendMessageAsync($"{message.Author.Mention}: {sanitized}");
                            }

                            _logger.LogInformation($"Masked banned message from {message.Author.Username}: {keyword.Word}");
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