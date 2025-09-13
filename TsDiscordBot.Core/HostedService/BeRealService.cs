using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService;

public class BeRealService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly DatabaseService _databaseService;
    private readonly ILogger<BeRealService> _logger;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<ulong, BeRealConfig> _configCache = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), BeRealParticipant> _participantCache = new();

    public BeRealService(DiscordSocketClient client, DatabaseService databaseService, ILogger<BeRealService> logger)
    {
        _client = client;
        _databaseService = databaseService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceived += OnMessageReceivedAsync;
        _cts = new CancellationTokenSource();
        _ = Task.Run(RefreshCacheAsync);
        _ = Task.Run(() => TimerLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceived -= OnMessageReceivedAsync;
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        try
        {
            if (message.Channel is not SocketGuildChannel guildChannel)
            {
                return;
            }

            if (message.Author.IsBot)
            {
                return;
            }

            if (!_configCache.TryGetValue(guildChannel.Id, out var config))
            {
                _ = Task.Run(RefreshConfigCacheAsync);
                return;
            }

            if (!message.Attachments.Any(a => a.Width.HasValue))
            {
                return;
            }

            var guild = guildChannel.Guild;
            var feedChannel = guild.GetTextChannel(config.FeedChannelId);
            if (feedChannel is null)
            {
                return;
            }

            var webhookClient = await WebHookWrapper.Default.GetOrCreateWebhookClientAsync(feedChannel, "be-real-relay");
            var feedMessageId = await webhookClient.RelayMessageAsync(message, message.Content, logger:_logger);

            await message.DeleteAsync();

            if (feedMessageId is { } id)
            {
                var link = $"https://discord.com/channels/{guild.Id}/{feedChannel.Id}/{id}";
                if (message.Channel is IMessageChannel postChannel)
                {
                    await postChannel.SendMessageAsync($"{DiscordUtility.GetAuthorNameFromMessage(message)}さんがBeRealに画像を投稿したよ！{link}");
                }
            }

            if (message.Author is SocketGuildUser guildUser)
            {
                var role = guild.GetRole(config.RoleId);
                if (role is not null)
                {
                    await guildUser.AddRoleAsync(role);
                }

                if (!_participantCache.TryGetValue((guild.Id, guildUser.Id), out var existing))
                {
                    _ = Task.Run(RefreshParticipantCacheAsync);
                    existing = new BeRealParticipant
                    {
                        GuildId = guild.Id,
                        UserId = guildUser.Id,
                        LastPostedAtUtc = DateTime.UtcNow
                    };
                    _participantCache[(guild.Id, guildUser.Id)] = existing;
                    _ = Task.Run(() => _databaseService.Insert(BeRealParticipant.TableName, existing));
                }
                else
                {
                    existing.LastPostedAtUtc = DateTime.UtcNow;
                    _participantCache[(guild.Id, guildUser.Id)] = existing;
                    _ = Task.Run(() => _databaseService.Update(BeRealParticipant.TableName, existing));
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process be-real post");
        }
    }

    private async Task RefreshCacheAsync()
    {
        await Task.WhenAll(RefreshConfigCacheAsync(), RefreshParticipantCacheAsync());
    }

    private async Task RefreshConfigCacheAsync()
    {
        try
        {
            var configs = await Task.Run(() => _databaseService
                .FindAll<BeRealConfig>(BeRealConfig.TableName)
                .ToArray());
            foreach (var config in configs)
            {
                _configCache[config.PostChannelId] = config;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to refresh be-real config cache");
        }
    }

    private async Task RefreshParticipantCacheAsync()
    {
        try
        {
            var participants = await Task.Run(() => _databaseService
                .FindAll<BeRealParticipant>(BeRealParticipant.TableName)
                .ToArray());
            foreach (var participant in participants)
            {
                _participantCache[(participant.GuildId, participant.UserId)] = participant;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to refresh be-real participant cache");
        }
    }

    private async Task TimerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await RefreshCacheAsync();
                var participants = _participantCache.Values.ToArray();

                foreach (var participant in participants)
                {
                    if (DateTime.UtcNow - participant.LastPostedAtUtc >= TimeSpan.FromHours(24))
                    {
                        var guild = _client.GetGuild(participant.GuildId);
                        var config = _configCache.Values.FirstOrDefault(x => x.GuildId == participant.GuildId);
                        if (guild != null && config != null)
                        {
                            var user = guild.GetUser(participant.UserId);
                            var role = guild.GetRole(config.RoleId);
                            if (user != null && role != null)
                            {
                                try
                                {
                                    await user.RemoveRoleAsync(role);
                                    _participantCache.TryRemove((participant.GuildId, participant.UserId), out _);
                                    _ = Task.Run(() => _databaseService.Delete(BeRealParticipant.TableName, participant.Id));
                                }
                                catch(Exception e)
                                {
                                    _logger.LogError(e, "Failed to remove role");
                                }

                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to revoke be-real roles");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30), token);
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}
