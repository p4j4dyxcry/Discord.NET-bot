using Discord;
using Discord.Interactions;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class AnonymousCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _databaseService;

    public AnonymousCommandModule(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [SlashCommand("who", "サーバー全体で匿名化するキャラクターを選択します。")]
    public async Task Who()
    {
        var options = AnonymousProfileProvider.GetProfiles()
            .Select(p => new SelectMenuOptionBuilder()
                .WithLabel(p.Name)
                .WithValue(p.Name))
            .Take(25)
            .ToList();

        var component = new ComponentBuilder()
            .WithSelectMenu("who_select", options, "キャラクターを選択してね");

        await RespondAsync("キャラクターを選択してね", components: component.Build(), ephemeral: true);
    }

    [ComponentInteraction("who_select")]
    public async Task WhoSelectHandler(string[] selected)
    {
        var name = selected.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(name))
        {
            await RespondAsync("指定されたキャラクターは見つからなかったよ！", ephemeral: true);
            return;
        }

        var profile = AnonymousProfileProvider.GetProfileByName(name);
        if (profile is null)
        {
            await RespondAsync("指定されたキャラクターは見つからなかったよ！", ephemeral: true);
            return;
        }

        var user = Context.User;
        var guildId = (Context.Guild?.Id) ?? 0;
        var settings = _databaseService.FindAll<AnonymousGuildUserSetting>(AnonymousGuildUserSetting.TableName)
            .Where(x => x.GuildId == guildId && x.UserId == user.Id)
            .ToArray();

        if (settings.Length is 0)
        {
            _databaseService.Insert(AnonymousGuildUserSetting.TableName, new AnonymousGuildUserSetting
            {
                GuildId = guildId,
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
                _databaseService.Update(AnonymousGuildUserSetting.TableName, setting);
            }
        }

        var embed = new EmbedBuilder()
            .WithTitle("設定されたキャラクター")
            .WithDescription($"{profile.Name}として表示されます。")
            .WithImageUrl(profile.AvatarUrl)
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync("匿名キャラクターを設定したよ！", embed: embed, ephemeral: true);
    }

    [SlashCommand("iam", "サーバー全体の匿名化を解除します。")]
    public async Task IAm()
    {
        var user = Context.User;
        var guildId = (Context.Guild?.Id) ?? 0;
        var settings = _databaseService.FindAll<AnonymousGuildUserSetting>(AnonymousGuildUserSetting.TableName)
            .Where(x => x.GuildId == guildId && x.UserId == user.Id)
            .ToArray();

        if (settings.Length is 0)
        {
            _databaseService.Insert(AnonymousGuildUserSetting.TableName, new AnonymousGuildUserSetting
            {
                GuildId = guildId,
                UserId = user.Id,
                IsAnonymous = false,
            });
        }
        else
        {
            foreach (var setting in settings)
            {
                setting.IsAnonymous = false;
                setting.AnonymousName = null;
                setting.AnonymousAvatarUrl = null;
                _databaseService.Update(AnonymousGuildUserSetting.TableName, setting);
            }
        }

        await RespondAsync("匿名化を解除したよ！", ephemeral: true);
    }
}

