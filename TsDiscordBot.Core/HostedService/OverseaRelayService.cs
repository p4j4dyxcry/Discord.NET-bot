using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService;

public class OverseaRelayService : IHostedService
{
    private readonly IMessageReceiver _client;
    private readonly IWebHookService _webHookService;
    private readonly ILogger<OverseaRelayService> _logger;
    private readonly DatabaseService _databaseService;

    private OverseaChannel[] _channelCache = [];
    private OverseaUserSetting[] _userCache = [];
    private DateTime _lastQueryTime = DateTime.MinValue;
    private readonly TimeSpan _querySpan = TimeSpan.FromSeconds(5);
    private IDisposable? _subscription;

    public OverseaRelayService(IMessageReceiver client, IWebHookService webHookService, ILogger<OverseaRelayService> logger, DatabaseService databaseService)
    {
        _client = client;
        _webHookService = webHookService;
        _logger = logger;
        _databaseService = databaseService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _client.OnReceivedSubscribe(OnMessageReceivedAsync, nameof(OverseaRelayService));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(IMessageData message)
    {
        try
        {
            if (message.IsBot)
            {
                return;
            }

            if (DateTime.Now - _lastQueryTime > _querySpan)
            {
                _channelCache = _databaseService.FindAll<OverseaChannel>(OverseaChannel.TableName).ToArray();
                _userCache = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName).ToArray();
                _lastQueryTime = DateTime.Now;
            }

            var current = _channelCache.FirstOrDefault(x => x.ChannelId == message.ChannelId);
            if (current is null)
            {
                return;
            }

            var targets = _channelCache
                .Where(x => x.OverseaId == current.OverseaId)
                .ToArray();

            if (!targets.Any())
            {
                return;
            }

            var userSetting = _userCache.FirstOrDefault(x => x.UserId == message.AuthorId);
            bool anonymous = userSetting?.IsAnonymous ?? true;
            string username;
            string? avatarUrl = null;

            if (anonymous)
            {
                var profile = AnonymousProfileProvider.GetProfile(message.AuthorId);
                var discriminator = AnonymousProfileProvider.GetDiscriminator(message.AuthorId);
                var baseName = string.IsNullOrEmpty(userSetting?.AnonymousName)
                    ? profile.Name
                    : userSetting.AnonymousName!;

                baseName = UserNameFixLogic.Fix(baseName);

                username = $"{baseName}#{discriminator}";
                avatarUrl = userSetting?.AnonymousAvatarUrl ?? profile.AvatarUrl;
            }
            else
            {
                username = message.AuthorName;
                avatarUrl = message.AvatarUrl;
            }

            List<Task> tasks =
            [
                Task.Run(async () =>
                {
                    try
                    {
                        await message.DeleteAsync();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to delete original message");
                    }
                })

            ];

            var semaphore = new SemaphoreSlim(4);
            foreach (var target in targets)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var client = await _webHookService.GetOrCreateWebhookClientAsync(message.ChannelId,"oversea-relay");
                        await client.RelayMessageAsync(message, message.Content,username,avatarUrl);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error sending message to oversea channel {ChannelId}", target.ChannelId);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to relay oversea message.");
        }
    }
}

