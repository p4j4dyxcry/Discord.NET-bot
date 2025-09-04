using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        private async Task CheckMessageAsync(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot || message.Channel is not SocketGuildChannel guildChannel)
                    return;

                if (message.Channel.Name.Contains("閲覧注意"))
                    return;

                var guildId = guildChannel.Guild.Id;

                if ((DateTime.Now - _lastFetchTime) > CacheDuration)
                {
                    var list = _databaseService.FindAll<BannedTriggerWord>(BannedTriggerWord.TableName);
                    _cache = list.ToArray();
                    _lastFetchTime = DateTime.Now;
                }

                var keywords = _cache
                    .Where(x => x.GuildId == guildId)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Word));

                foreach (var keyword in keywords)
                {
                    if (message.Content.Contains(keyword.Word, StringComparison.OrdinalIgnoreCase))
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