using System.Collections.Concurrent;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.Webhook;
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
    [RequireUserPermission(GuildPermission.Administrator)]
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

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"このチャンネルをマルチサーバー{id}に登録したよ！");
        builder.AppendLine("マルチサーバーとは「匿名」で、他のどこかのサーバーの人たちと交流できます。");
        builder.AppendLine("\ud83d\udd39 投稿は匿名化されて送信されます。");
        builder.AppendLine("\ud83d\udd39 /oversea-set-name でマルチサーバー上での名前を変更");
        builder.AppendLine("\ud83d\udd39 /oversea-set-icon でマルチサーバー上でのアイコンを変更");

        await RespondAsync(builder.ToString());
    }

    [SlashCommand("oversea-leave", "当該チャンネルに登録されているマルチサーバーを解除します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
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
    public async Task EnableAnonymous(string? displayName = null, IAttachment? avatarUrl = null)
    {
        await EnableAnonymous(Context.User,displayName, avatarUrl);
    }

    [SlashCommand("oversea-force-enable-anonymous", "投稿者を匿名化します。(標準は匿名化されます)")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task EnableAnonymous(IUser? who, string? displayName = null, IAttachment? avatarUrl = null)
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
                AnonymousAvatarUrl = avatarUrl?.Url
            });
        }

        foreach (var setting in settings)
        {
            setting.IsAnonymous = true;
            setting.AnonymousName = displayName;
            setting.AnonymousAvatarUrl = avatarUrl?.Url;
            _databaseService.Update(OverseaUserSetting.TableName, setting);
        }

        await RespondAsync($"{user.Mention}を匿名化したよ！");
    }

    [SlashCommand("oversea-set-name", "マルチサーバーで利用する専用の名前を設定します。")]
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

        await RespondAsync($"{user.Mention}の表示名を{displayName}にしたよ！");
    }

    [SlashCommand("oversea-set-icon", "マルチサーバーで利用する匿名アイコンを設定します。")]
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

        var embed = new EmbedBuilder()
            .WithTitle("設定されたアイコン")
            .WithDescription("設定されたアイコンです。")
            .WithImageUrl(url)  // ← URL の画像をプレビュー
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync("匿名用にアイコンを設定したよ！", embed:embed);
    }

    [SlashCommand("cc", "匿名キャラクターを選択します。")]
    public async Task ChooseCharacter([Autocomplete(typeof(AnonymousProfileAutocompleteHandler))] string name)
    {
        var profile = AnonymousProfileProvider.GetProfileByName(name);
        if (profile is null)
        {
            await RespondAsync("指定されたキャラクターは見つからなかったよ！");
            return;
        }

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
                AnonymousName = profile.Name,
                AnonymousAvatarUrl = profile.AvatarUrl
            });
        }
        else
        {
            foreach (var setting in settings)
            {
                setting.IsAnonymous = true;
                setting.AnonymousName = profile.Name;
                setting.AnonymousAvatarUrl = profile.AvatarUrl;
                _databaseService.Update(OverseaUserSetting.TableName, setting);
            }
        }

        var embed = new EmbedBuilder()
            .WithTitle("設定されたキャラクター")
            .WithDescription($"{profile.Name}として表示されます。")
            .WithImageUrl(profile.AvatarUrl)
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync("匿名キャラクターを設定したよ！", embed: embed);
    }

    [SlashCommand("oversea-disable-anonymous", "投稿者の匿名化を解除します。")]
    public async Task DisableAnonymous()
    {
        await DisableAnonymous(Context.User);
    }

    [SlashCommand("oversea-force-disable-anonymous", "投稿者の匿名化を強制解除します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task DisableAnonymous(IUser? who)
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

