using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Data;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService;

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
        _subscription = _messageReceiver.OnReceivedSubscribe(
            OnMessageReceivedAsync,
            MessageConditions.NotFromBot.And(MessageConditions.NotDeleted),
            nameof(BeRealService));
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

    private async Task OnMessageReceivedAsync(IMessageData message, CancellationToken token)
    {
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
        var user = _client.GetGuild(message.GuildId)
            ?.GetUser(message.AuthorId);

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

    private async Task TimerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var nowUtc = DateTime.UtcNow;
            try
            {
                // 期限切れのみを取れるならこの段階で絞る（推奨）
                var cutoffUtc = DateTime.UtcNow - TimeSpan.FromHours(24);

                var participants = _databaseService
                    .FindAll<BeRealParticipant>(BeRealParticipant.TableName)
                    .Where(x => x.LastPostedAtUtc <= cutoffUtc)
                    .ToArray();

                if (participants.Length == 0)
                {
                    _logger.LogInformation("BeReal: no expired participants at {Now}", nowUtc);
                }

                // ギルドごとにまとめる
                var byGuild = participants.GroupBy(p => p.GuildId).ToArray();

                // ギルド設定をキャッシュ化
                var guildIds = byGuild.Select(g => g.Key)
                    .ToArray();
                var allConfigs = _databaseService
                    .FindAll<BeRealConfig>(BeRealConfig.TableName)
                    .Where(c => guildIds.Contains(c.GuildId))
                    .ToDictionary(c => c.GuildId);

                foreach (var group in byGuild)
                {
                    var guildId = group.Key;
                    var guild = _client.GetGuild(guildId);
                    if (guild is null)
                    {
                        _logger.LogWarning("BeReal: guild not found: {GuildId}", guildId);
                        continue;
                    }

                    if (!allConfigs.TryGetValue(guildId, out var config))
                    {
                        _logger.LogWarning("BeReal: config not found for guild {GuildId}", guildId);
                        continue;
                    }

                    var role = guild.GetRole(config.RoleId);
                    if (role is null)
                    {
                        _logger.LogWarning("BeReal: role {RoleId} not found in guild {GuildId}", config.RoleId, guildId);
                        continue;
                    }

                    foreach (var participant in group)
                    {
                        // 追加ガード（FindAllで絞れていない場合に備える）
                        if (nowUtc - participant.LastPostedAtUtc < TimeSpan.FromHours(24))
                            continue;

                        try
                        {
                            var user = guild.GetUser(participant.UserId);
                            if (user is null)
                            {
                                _logger.LogInformation("BeReal: user not found in cache: {UserId} (guild {GuildId})", participant.UserId, guildId);
                                // 必要なら REST: await guild.GetUserAsync(participant.UserId);
                                // 見つからない場合はDBから消すかは運用方針次第
                                continue;
                            }

                            await user.RemoveRoleAsync(role);
                            _databaseService.Delete(BeRealParticipant.TableName, participant.Id);

                            _logger.LogInformation("BeReal: removed role {RoleId} from {UserId} in guild {GuildId}",
                                role.Id,
                                user.Id,
                                guildId);

                            // 軽いスロットリング（429回避）
                            await Task.Delay(TimeSpan.FromMilliseconds(1000), token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "BeReal: failed to remove role {RoleId} from {UserId} in guild {GuildId}",
                                role.Id,
                                participant.UserId,
                                guildId);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "BeReal: sweep failed at {Now}", nowUtc);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30), token);
            }
            catch (TaskCanceledException)
            {
                /* ignore */
            }
        }
    }
}
