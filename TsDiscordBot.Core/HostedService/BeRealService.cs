using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class BeRealService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly IMessageReceiver _messageReceiver;
    private readonly IWebHookService _webHookService;
    private readonly DatabaseService _databaseService;
    private readonly ILogger<BeRealService> _logger;
    private CancellationTokenSource? _cts;
    private IDisposable? _subscription = null;

    public BeRealService(DiscordSocketClient client, IMessageReceiver messageReceiver, IWebHookService webHookService, DatabaseService databaseService, ILogger<BeRealService> logger)
    {
        _client = client;
        _messageReceiver = messageReceiver;
        _webHookService = webHookService;
        _databaseService = databaseService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _messageReceiver.OnReceivedSubscribe(OnMessageReceivedAsync,nameof(BeRealService));
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => TimerLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(IMessageData message)
    {
        try
        {
            if (message.IsBot || message.IsDeleted)
            {
                return;
            }

            var config = _databaseService
                .FindAll<BeRealConfig>(BeRealConfig.TableName)
                .FirstOrDefault(x => x.PostChannelId == message.ChannelId);

            if (config is null)
            {
                return;
            }

            await message.CreateAttachmentSourceIfNotCachedAsync();

            if (!message.Attachments.Any(a => a.Width.HasValue))
            {
                return;
            }

            var webhookClient = await _webHookService.GetOrCreateWebhookClientAsync(config.FeedChannelId, "be-real-relay");
            var feedMessageId = await webhookClient.RelayMessageAsync(message, message.Content);

            await message.DeleteAsync();

            if (feedMessageId is { } id)
            {
                var link = $"https://discord.com/channels/{message.GuildId}/{config.FeedChannelId}/{id}";
                await message.SendMessageAsyncOnChannel($"{message.AuthorName}さんがBeRealに画像を投稿したよ！{link}");
            }

            var guild = _client.GetGuild(message.GuildId);
            var user = _client.GetGuild(message.GuildId)?.GetUser(message.AuthorId);

            var role = guild.GetRole(config.RoleId);
            if (role is not null && user is not null)
            {
                await user.AddRoleAsync(role);
            }

            var existing = _databaseService
                .FindAll<BeRealParticipant>(BeRealParticipant.TableName)
                .FirstOrDefault(x => x.GuildId == guild.Id && x.UserId == message.AuthorId);

            if (existing is null)
            {
                existing = new BeRealParticipant
                {
                    GuildId = guild.Id,
                    UserId = message.AuthorId,
                    LastPostedAtUtc = DateTime.UtcNow
                };
                _databaseService.Insert(BeRealParticipant.TableName, existing);
            }
            else
            {
                existing.LastPostedAtUtc = DateTime.UtcNow;
                _databaseService.Update(BeRealParticipant.TableName, existing);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process be-real post");
        }
    }

    private async Task TimerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var participants = _databaseService
                    .FindAll<BeRealParticipant>(BeRealParticipant.TableName)
                    .ToArray();

                foreach (var participant in participants)
                {
                    if (DateTime.UtcNow - participant.LastPostedAtUtc >= TimeSpan.FromHours(24))
                    {
                        var guild = _client.GetGuild(participant.GuildId);
                        var config = _databaseService
                            .FindAll<BeRealConfig>(BeRealConfig.TableName)
                            .FirstOrDefault(x => x.GuildId == participant.GuildId);
                        if (guild != null && config != null)
                        {
                            var user = guild.GetUser(participant.UserId);
                            var role = guild.GetRole(config.RoleId);
                            if (user != null && role != null)
                            {
                                try
                                {
                                    await user.RemoveRoleAsync(role);
                                    _databaseService.Delete(BeRealParticipant.TableName, participant.Id);
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
