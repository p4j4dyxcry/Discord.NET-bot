using System.Linq;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class BeRealService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly DatabaseService _databaseService;
    private readonly ILogger<BeRealService> _logger;
    private CancellationTokenSource? _cts;

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

            var config = _databaseService
                .FindAll<BeRealConfig>(BeRealConfig.TableName)
                .FirstOrDefault(x => x.PostChannelId == guildChannel.Id);

            if (config is null)
            {
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
            await webhookClient.RelayMessageAsync(message, message.Content, logger:_logger);

            await message.DeleteAsync();

            if (message.Author is SocketGuildUser guildUser)
            {
                var role = guild.GetRole(config.RoleId);
                if (role is not null)
                {
                    await guildUser.AddRoleAsync(role);
                }

                var existing = _databaseService
                    .FindAll<BeRealParticipant>(BeRealParticipant.TableName)
                    .FirstOrDefault(x => x.GuildId == guild.Id && x.UserId == guildUser.Id);

                if (existing is null)
                {
                    existing = new BeRealParticipant
                    {
                        GuildId = guild.Id,
                        UserId = guildUser.Id,
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
