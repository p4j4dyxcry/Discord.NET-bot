using Discord.WebSocket;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TsDiscordBot.Core.HostedService;

public class InviteTrackingService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<InviteTrackingService> _logger;
    private readonly ulong _notificationChannelId;
    private readonly Dictionary<ulong, Dictionary<string, int>> _cachedInvites = new();

    public InviteTrackingService(DiscordSocketClient client, ILogger<InviteTrackingService> logger,
        IConfiguration configuration)
    {
        _client = client;
        _logger = logger;
        if (ulong.TryParse(configuration["invite_tracking_channel_id"], out var channelId))
        {
            _notificationChannelId = channelId;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Ready += OnReadyAsync;
        _client.UserJoined += OnUserJoinedAsync;
        _client.InviteCreated += OnInviteCreatedAsync;
        _client.InviteDeleted += OnInviteDeletedAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Ready -= OnReadyAsync;
        _client.UserJoined -= OnUserJoinedAsync;
        _client.InviteCreated -= OnInviteCreatedAsync;
        _client.InviteDeleted -= OnInviteDeletedAsync;
        return Task.CompletedTask;
    }

    private async Task OnReadyAsync()
    {
        foreach (var guild in _client.Guilds)
        {
            var invites = await guild.GetInvitesAsync();
            _cachedInvites[guild.Id] = invites.ToDictionary(i => i.Code, i => i.Uses ?? 0);
        }
    }

    private async Task OnInviteCreatedAsync(SocketInvite invite)
    {
        if (!_cachedInvites.TryGetValue(invite.Guild.Id, out var guildInvites))
        {
            guildInvites = new Dictionary<string, int>();
            _cachedInvites[invite.Guild.Id] = guildInvites;
        }
        guildInvites[invite.Code] = invite.Uses;

        await SendNotificationAsync($"Invite {invite.Code} created by {invite.Inviter?.Username}");
    }

    private Task OnInviteDeletedAsync(SocketGuildChannel channel, string code)
    {
        if (_cachedInvites.TryGetValue(channel.Guild.Id, out var guildInvites))
        {
            guildInvites.Remove(code);
        }
        return Task.CompletedTask;
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        var guild = user.Guild;
        var invites = await guild.GetInvitesAsync();

        if (!_cachedInvites.TryGetValue(guild.Id, out var cached))
        {
            cached = new Dictionary<string, int>();
            _cachedInvites[guild.Id] = cached;
        }

        RestInviteMetadata? usedInvite = null;
        foreach (var invite in invites)
        {
            var uses = invite.Uses ?? 0;
            var previous = cached.GetValueOrDefault(invite.Code);
            if (uses > previous)
            {
                usedInvite = invite;
                break;
            }
        }

        _cachedInvites[guild.Id] = invites.ToDictionary(i => i.Code, i => i.Uses ?? 0);

        if (usedInvite is not null)
        {
            _logger.LogInformation("{User} joined using invite {Code} created by {Inviter}",
                user.Username, usedInvite.Code, usedInvite.Inviter?.Username);
            await SendNotificationAsync($"{user.Username} joined using {usedInvite.Code} created by {usedInvite.Inviter?.Username}");
        }
        else
        {
            _logger.LogInformation("{User} joined but invite could not be determined", user.Username);
            await SendNotificationAsync($"{user.Username} joined but invite could not be determined");
        }
    }

    private Task SendNotificationAsync(string message)
    {
        if (_notificationChannelId != 0 && _client.GetChannel(_notificationChannelId) is ISocketMessageChannel channel)
        {
            return channel.SendMessageAsync(message);
        }

        return Task.CompletedTask;
    }
}

