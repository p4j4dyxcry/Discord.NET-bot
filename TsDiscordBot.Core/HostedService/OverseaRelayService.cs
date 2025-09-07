using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Discord;
using Discord.WebSocket;
using Discord.Webhook;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService;

public class OverseaRelayService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<OverseaRelayService> _logger;
    private readonly DatabaseService _databaseService;

    private OverseaChannel[] _channelCache = [];
    private OverseaUserSetting[] _userCache = [];
    private DateTime _lastQueryTime = DateTime.MinValue;
    private readonly TimeSpan _querySpan = TimeSpan.FromSeconds(5);

    public OverseaRelayService(DiscordSocketClient client, ILogger<OverseaRelayService> logger, DatabaseService databaseService)
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
            if (message.Source != MessageSource.User || message.Channel is not SocketGuildChannel)
            {
                return;
            }

            if (DateTime.Now - _lastQueryTime > _querySpan)
            {
                _channelCache = _databaseService.FindAll<OverseaChannel>(OverseaChannel.TableName).ToArray();
                _userCache = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName).ToArray();
                _lastQueryTime = DateTime.Now;
            }

            var current = _channelCache.FirstOrDefault(x => x.ChannelId == message.Channel.Id);
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

            var userSetting = _userCache.FirstOrDefault(x => x.UserId == message.Author.Id);
            bool anonymous = userSetting?.IsAnonymous ?? true;
            string username;
            string? avatarUrl = null;

            if (anonymous)
            {
                var profile = AnonymousProfileProvider.GetProfile(message.Author.Id);
                var discriminator = AnonymousProfileProvider.GetDiscriminator(message.Author.Id);
                var baseName = string.IsNullOrEmpty(userSetting?.AnonymousName)
                    ? profile.Name
                    : userSetting.AnonymousName!;

                baseName = UserNameFixLogic.Fix(baseName);

                username = $"{baseName}#{discriminator}";
                avatarUrl = userSetting?.AnonymousAvatarUrl ?? profile.AvatarUrl;
            }
            else
            {
                username = message.Author.Username;
                if (message.Author is SocketGuildUser guildUser)
                {
                    username = guildUser.Nickname ?? message.Author.GlobalName ?? message.Author.Username;
                    avatarUrl = guildUser.GetGuildAvatarUrl() ??
                                guildUser.GetAvatarUrl() ??
                                guildUser.GetDefaultAvatarUrl();
                }
                else
                {
                    avatarUrl = message.Author.GetAvatarUrl() ??
                                message.Author.GetDefaultAvatarUrl();
                }
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
                        if (await _client.GetChannelAsync(target.ChannelId) is ITextChannel channel)
                        {
                            var client = await WebHookWrapper.Default.GetOrCreateWebhookClientAsync(channel,"oversea-relay");
                            await client.RelayMessageAsync(message, message.Content,username,avatarUrl,_logger);
                        }
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

