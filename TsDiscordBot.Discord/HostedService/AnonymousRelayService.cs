using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Data;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;
using TsDiscordBot.Discord.Utility;

namespace TsDiscordBot.Discord.HostedService;

public class AnonymousRelayService : IHostedService
{
    private readonly IMessageReceiver _client;
    private readonly IWebHookService _webHookService;
    private readonly ILogger<AnonymousRelayService> _logger;
    private readonly DatabaseService _databaseService;

    private AnonymousGuildUserSetting[] _userCache = [];
    private OverseaChannel[] _overseaChannelCache = [];
    private DateTime _lastQueryTime = DateTime.MinValue;
    private readonly TimeSpan _querySpan = TimeSpan.FromSeconds(5);

    private IDisposable? _subscription;

    public AnonymousRelayService(IMessageReceiver client, IWebHookService webHookService, ILogger<AnonymousRelayService> logger, DatabaseService databaseService)
    {
        _client = client;
        _webHookService = webHookService;
        _logger = logger;
        _databaseService = databaseService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _client.OnReceivedSubscribe(
            OnMessageReceivedAsync,
            MessageConditions.NotFromBot.And(MessageConditions.NotDeleted),
            nameof(AnonymousRelayService));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(IMessageData message, CancellationToken token)
    {
        if (DateTime.Now - _lastQueryTime > _querySpan)
        {
            _userCache = _databaseService.FindAll<AnonymousGuildUserSetting>(AnonymousGuildUserSetting.TableName).ToArray();
            _overseaChannelCache = _databaseService.FindAll<OverseaChannel>(OverseaChannel.TableName).ToArray();
            _lastQueryTime = DateTime.Now;
        }

        if (_overseaChannelCache.Any(x => x.ChannelId == message.ChannelId))
        {
            return; // Oversea relay has priority
        }

        var setting = _userCache.FirstOrDefault(x => x.GuildId == message.GuildId && x.UserId == message.AuthorId);
        if (setting is null || !setting.IsAnonymous)
        {
            return;
        }

        var profile = AnonymousProfileProvider.GetProfile(message.AuthorId);
        var discriminator = AnonymousProfileProvider.GetDiscriminator(message.AuthorId);
        var baseName = string.IsNullOrEmpty(setting.AnonymousName) ? profile.Name : setting.AnonymousName!;
        baseName = UserNameFixLogic.Fix(baseName);
        var username = $"{baseName}#{discriminator}";
        var avatarUrl = setting.AnonymousAvatarUrl ?? profile.AvatarUrl;

        string? content = string.IsNullOrWhiteSpace(message.Content) ? null : message.Content;

        await message.DeleteAsync();

        var client = await _webHookService.GetOrCreateWebhookClientAsync(message.ChannelId, "anonymous-relay");
        await client.RelayMessageAsync(message, content ?? string.Empty, username, avatarUrl);
    }
}
