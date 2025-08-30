using System.Linq;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class OverseaCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public OverseaCommandModule(ILogger<OverseaCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("oversea-register", "当該チャンネルマルチサーバー用に登録します。")]
    public async Task Register(int id)
    {
        var channelId = Context.Channel.Id;
        var existing = _databaseService.FindAll<OverseaChannel>(OverseaChannel.TableName)
            .FirstOrDefault(x => x.ChannelId == channelId);

        if (existing is not null)
        {
            existing.OverseaId = id;
            _databaseService.Update(OverseaChannel.TableName, existing);
        }
        else
        {
            _databaseService.Insert(OverseaChannel.TableName, new OverseaChannel
            {
                OverseaId = id,
                ChannelId = channelId
            });
        }

        await RespondAsync($"このチャンネルをマルチサーバー{id}に登録したよ！");
    }

    [SlashCommand("oversea-leave", "当該チャンネルに登録されているマルチサーバーを解除します。")]
    public async Task Leave()
    {
        var channelId = Context.Channel.Id;
        var channels = _databaseService.FindAll<OverseaChannel>(OverseaChannel.TableName)
            .Where(x => x.ChannelId == channelId)
            .ToArray();

        if (channels.Length is 0)
        {
            await RespondAsync("このチャンネルは登録されていないよ！");
            return;
        }

        foreach (var ch in channels)
        {
            _databaseService.Delete(OverseaChannel.TableName, ch.Id);
        }

        await RespondAsync("このチャンネルの登録を解除したよ！");
    }

    [SlashCommand("oversea-enable-anonymous", "投稿者を匿名化します。(標準は匿名化されます)")]
    public async Task EnableAnonymous(ulong userId)
    {
        var existing = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName)
            .FirstOrDefault(x => x.UserId == userId);

        if (existing is null)
        {
            _databaseService.Insert(OverseaUserSetting.TableName, new OverseaUserSetting
            {
                UserId = userId,
                IsAnonymous = true
            });
        }
        else
        {
            existing.IsAnonymous = true;
            _databaseService.Update(OverseaUserSetting.TableName, existing);
        }

        await RespondAsync($"<@{userId}>を匿名化したよ！");
    }

    [SlashCommand("oversea-disable-anonymous", "投稿者の匿名化を解除します。")]
    public async Task DisableAnonymous(ulong userId)
    {
        var existing = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName)
            .FirstOrDefault(x => x.UserId == userId);

        if (existing is null)
        {
            _databaseService.Insert(OverseaUserSetting.TableName, new OverseaUserSetting
            {
                UserId = userId,
                IsAnonymous = false
            });
        }
        else
        {
            existing.IsAnonymous = false;
            _databaseService.Update(OverseaUserSetting.TableName, existing);
        }

        await RespondAsync($"<@{userId}>の匿名化を解除したよ！");
    }
}

