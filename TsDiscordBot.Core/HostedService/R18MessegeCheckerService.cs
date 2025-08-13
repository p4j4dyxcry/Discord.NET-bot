using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService
{
    public class R18MessageCheckerService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<R18MessageCheckerService> _logger;
        private readonly DatabaseService _databaseService;

        private R18TriggerWord[] _cache = [];
        private DateTime _lastFetchTime = DateTime.MinValue;
        private readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

        public R18MessageCheckerService(
            DiscordSocketClient client,
            ILogger<R18MessageCheckerService> logger,
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
                    var list = _databaseService.FindAll<R18TriggerWord>(R18TriggerWord.TableName);
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
                        _logger.LogInformation($"Deleted R18 message from {message.Author.Username}: {keyword.Word}");

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
                _logger.LogError(ex, "Error in R18 message checker");
            }
        }
    }
}