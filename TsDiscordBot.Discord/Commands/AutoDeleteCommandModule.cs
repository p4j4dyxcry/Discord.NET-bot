using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Data;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Commands;

public class AutoDeleteCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public AutoDeleteCommandModule(ILogger<AutoDeleteCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("auto-delete-enable", "メッセージを一定時間後に自動削除するように設定します。")]
    public async Task EnableAutoDelete([Summary("m", "メッセージを削除するまでの時間(分)")] int m)
    {
        var channelId = Context.Channel.Id;
        var guildId = Context.Guild.Id;
        var now = DateTime.UtcNow;
        var snowflake = SnowflakeUtils.ToSnowflake(now);

        var existing = _databaseService
            .FindAll<AutoDeleteChannel>(AutoDeleteChannel.TableName)
            .FirstOrDefault(x => x.ChannelId == channelId);

        if (existing is not null)
        {
            existing.DelayMinutes = m;
            existing.EnabledAtUtc = now;
            existing.LastMessageId = snowflake;
            _databaseService.Update(AutoDeleteChannel.TableName, existing);
            await RespondAsync($"{m}分後にメッセージを自動削除するよう更新したよ！");
            return;
        }

        var data = new AutoDeleteChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            DelayMinutes = m,
            EnabledAtUtc = now,
            LastMessageId = snowflake
        };

        _databaseService.Insert(AutoDeleteChannel.TableName, data);
        await RespondAsync($"{m}分後にメッセージを自動削除するよう設定したよ！");
    }

    [SlashCommand("auto-delete-disable", "このチャンネルの自動削除設定を解除します。")]
    public async Task DisableAutoDelete()
    {
        var channelId = Context.Channel.Id;

        var existing = _databaseService
            .FindAll<AutoDeleteChannel>(AutoDeleteChannel.TableName)
            .FirstOrDefault(x => x.ChannelId == channelId);

        if (existing is null)
        {
            await RespondAsync("このチャンネルでは自動削除は設定されていないよ！");
            return;
        }

        _databaseService.Delete(AutoDeleteChannel.TableName, existing.Id);

        var messages = _databaseService
            .FindAll<AutoDeleteMessage>(AutoDeleteMessage.TableName)
            .Where(x => x.ChannelId == channelId)
            .ToArray();

        foreach (var msg in messages)
        {
            _databaseService.Delete(AutoDeleteMessage.TableName, msg.Id);
        }

        await RespondAsync("このチャンネルでの自動削除設定を解除したよ！");
    }

    [SlashCommand("auto-delete-next", "次に削除されるメッセージを表示します。")]
    public async Task ShowNextAutoDelete()
    {
        async Task RespondAndDeleteAsync(string text)
        {
            await RespondAsync(text);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));

                try
                {
                    await Context.Interaction.DeleteOriginalResponseAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to delete /auto-delete-next response.");
                }
            });
        }

        var channelId = Context.Channel.Id;

        var next = _databaseService
            .FindAll<AutoDeleteMessage>(AutoDeleteMessage.TableName)
            .Where(x => x.ChannelId == channelId)
            .OrderBy(x => x.DeleteAtUtc)
            .FirstOrDefault();

        if (next is null)
        {
            await RespondAndDeleteAsync("このチャンネルで削除予定のメッセージはないよ！");
            return;
        }

        var message = await Context.Channel.GetMessageAsync(next.MessageId);
        if (message is null)
        {
            _databaseService.Delete(AutoDeleteMessage.TableName, next.Id);
            await RespondAndDeleteAsync("このチャンネルで削除予定のメッセージはないよ！");
            return;
        }

        var remaining = next.DeleteAtUtc - DateTime.UtcNow;
        var minutes = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
        var content = string.IsNullOrWhiteSpace(message.Content) ? "(embed or attachment)" : message.Content;
        if (content.Length > 20)
        {
            content = content[..20];
        }

        await RespondAndDeleteAsync($"次に削除されるメッセージ: \"{content}\" {minutes}分後に削除されるよ。");
    }
}
