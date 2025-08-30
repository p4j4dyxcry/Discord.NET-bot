using System.Linq;
using Discord;
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
    public async Task EnableAnonymous(IUser? who = null, string? displayName = null, string? avatarUrl = null)
    {
        IUser user = who ?? Context.User;
        var settings = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName)
            .Where(x => x.UserId == user.Id)
            .ToArray();

        if (settings.Length is 0)
        {
            _databaseService.Insert(OverseaUserSetting.TableName, new OverseaUserSetting
            {
                UserId = user.Id,
                IsAnonymous = true,
                AnonymousName = displayName,
                AnonymousAvatarUrl = avatarUrl
            });
        }

        foreach (var setting in settings)
        {
            setting.IsAnonymous = true;
            setting.AnonymousName = displayName;
            setting.AnonymousAvatarUrl = avatarUrl;
            _databaseService.Update(OverseaUserSetting.TableName, setting);
        }

        await RespondAsync($"{user.Mention}を匿名化したよ！");
    }

    [SlashCommand("oversea-set-name", "マルチサーバーで利用する名前を設定します。")]
    public async Task SetName(string displayName)
    {
        IUser user = Context.User;
        var settings = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName)
            .Where(x => x.UserId == user.Id)
            .ToArray();

        if (settings.Length is 0)
        {
            _databaseService.Insert(OverseaUserSetting.TableName, new OverseaUserSetting
            {
                UserId = user.Id,
                IsAnonymous = true,
                AnonymousName = displayName
            });
        }
        else
        {
            foreach (var setting in settings)
            {
                setting.IsAnonymous = true;
                setting.AnonymousName = displayName;
                _databaseService.Update(OverseaUserSetting.TableName, setting);
            }
        }

        await RespondAsync($"{user.Mention}を匿名化したよ！");
    }

    [SlashCommand("oversea-set-name", "マルチサーバーで利用する名前を設定します。")]
    public async Task SetIcon(IAttachment attachment)
    {
        if ( !attachment.ContentType.StartsWith("image/"))
        {
            await RespondAsync("画像を添付してください。");
            return;
        }

        string url = attachment.Url;

        IUser user = Context.User;
        var settings = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName)
            .Where(x => x.UserId == user.Id)
            .ToArray();

        if (settings.Length is 0)
        {
            _databaseService.Insert(OverseaUserSetting.TableName, new OverseaUserSetting
            {
                UserId = user.Id,
                IsAnonymous = true,
                AnonymousAvatarUrl = url,
            });
        }
        else
        {
            foreach (var setting in settings)
            {
                setting.IsAnonymous = true;
                setting.AnonymousAvatarUrl = url;
                _databaseService.Update(OverseaUserSetting.TableName, setting);
            }
        }

        await RespondAsync($"{user.Mention}を匿名化したよ！");
    }

    [SlashCommand("oversea-disable-anonymous", "投稿者の匿名化を解除します。")]
    public async Task DisableAnonymous(IUser? who = null)
    {
        IUser user = who ?? Context.User;
        var settings = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName)
            .Where(x => x.UserId == user.Id)
            .ToArray();

        if (settings.Length is 0)
        {
            _databaseService.Insert(OverseaUserSetting.TableName, new OverseaUserSetting
            {
                UserId = user.Id,
                IsAnonymous = false
            });
        }
        else
        {
            foreach (var setting in settings)
            {
                setting.IsAnonymous = false;
                setting.AnonymousName = null;
                setting.AnonymousAvatarUrl = null;
                _databaseService.Update(OverseaUserSetting.TableName, setting);
            }
        }

        await RespondAsync($"{user.Mention}の匿名化を解除したよ！");
    }
}

