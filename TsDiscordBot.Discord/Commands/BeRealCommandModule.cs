using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class BeRealCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger _logger;

    public BeRealCommandModule(DatabaseService databaseService, ILogger<BeRealCommandModule> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    [SlashCommand("be-real-initialize", "be realのチャンネルとロールを作成します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task Initialize()
    {
        var guild = Context.Guild;
        if (guild is null)
        {
            await RespondAsync("This command must be used in a guild.");
            return;
        }

        var existing = _databaseService
            .FindAll<BeRealConfig>(BeRealConfig.TableName)
            .FirstOrDefault(x => x.GuildId == guild.Id);

        if (existing is not null)
        {
            await RespondAsync("be-real はすでに設定されています。");
            return;
        }

        var role = await guild.CreateRoleAsync("BeReal-24h", GuildPermissions.None, null, false, false);
        var everyone = guild.EveryoneRole;

        var postChannel = await guild.CreateTextChannelAsync("be-real-post", props =>
        {
            props.PermissionOverwrites = new[]
            {
                new Overwrite(everyone.Id, PermissionTarget.Role,
                    new OverwritePermissions(viewChannel: PermValue.Allow,
                                             sendMessages: PermValue.Allow))
            };
        });

        var feedChannel = await guild.CreateTextChannelAsync("be-real-feed", props =>
        {
            props.PermissionOverwrites = new[]
            {
                new Overwrite(everyone.Id, PermissionTarget.Role,
                    new OverwritePermissions(viewChannel: PermValue.Deny)),
                new Overwrite(role.Id, PermissionTarget.Role,
                    new OverwritePermissions(viewChannel: PermValue.Allow,
                                             sendMessages: PermValue.Deny,
                                             readMessageHistory: PermValue.Allow))
            };
        });

        var postMsg = await postChannel.SendMessageAsync(
            "画像を投稿すると 24 時間 の間 他の人が投稿した画像を閲覧できるよ！");
        await postMsg.PinAsync();

        var feedMsg = await feedChannel.SendMessageAsync("投稿された画像が　24時間 確認できるよ！");
        await feedMsg.PinAsync();

        var config = new BeRealConfig
        {
            GuildId = guild.Id,
            PostChannelId = postChannel.Id,
            FeedChannelId = feedChannel.Id,
            RoleId = role.Id
        };

        _databaseService.Insert(BeRealConfig.TableName, config);

        await RespondAsync("BeReal を初期化しました。");
    }

    [SlashCommand("be-real-destroy", "be real の設定を解除します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task Destroy()
    {
        var guild = Context.Guild;
        if (guild is null)
        {
            await RespondAsync("This command must be used in a guild.");
            return;
        }

        var config = _databaseService
            .FindAll<BeRealConfig>(BeRealConfig.TableName)
            .FirstOrDefault(x => x.GuildId == guild.Id);

        if (config is null)
        {
            await RespondAsync("be-real は設定されていません。");
            return;
        }

        if (guild.GetRole(config.RoleId) is SocketRole role)
        {
            await role.DeleteAsync();
        }

        if (guild.GetTextChannel(config.PostChannelId) is SocketTextChannel post)
        {
            await post.DeleteAsync();
        }

        if (guild.GetTextChannel(config.FeedChannelId) is SocketTextChannel feed)
        {
            await feed.DeleteAsync();
        }

        var participants = _databaseService
            .FindAll<BeRealParticipant>(BeRealParticipant.TableName)
            .Where(x => x.GuildId == guild.Id)
            .ToArray();
        foreach (var p in participants)
        {
            _databaseService.Delete(BeRealParticipant.TableName, p.Id);
        }

        _databaseService.Delete(BeRealConfig.TableName, config.Id);

        await RespondAsync("be-real の設定を削除しました。");
    }
}
