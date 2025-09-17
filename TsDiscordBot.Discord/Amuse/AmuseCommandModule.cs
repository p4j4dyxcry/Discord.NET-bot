using Discord;
using Discord.Interactions;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class AmuseCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _databaseService;

    public AmuseCommandModule(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [SlashCommand("amuse-enable", "このチャンネルでamuseコマンドを有効にします。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task EnableAsync()
    {
        var guildId = Context.Guild.Id;
        var channelId = Context.Channel.Id;

        var existing = _databaseService
            .FindAll<AmuseChannel>(AmuseChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId && x.ChannelId == channelId);

        if (existing is not null)
        {
            await RespondAsync("このチャンネルでは既にamuseが有効だよ！");
            return;
        }

        var data = new AmuseChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            EnabledAtUtc = DateTime.UtcNow
        };

        _databaseService.Insert(AmuseChannel.TableName, data);
        await RespondAsync("amuseをこのチャンネルで有効にしたよ！");
    }

    [SlashCommand("amuse-disable", "このチャンネルでamuseコマンドを無効にします。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task DisableAsync()
    {
        var guildId = Context.Guild.Id;
        var channelId = Context.Channel.Id;

        var existing = _databaseService
            .FindAll<AmuseChannel>(AmuseChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId && x.ChannelId == channelId);

        if (existing is null)
        {
            await RespondAsync("このチャンネルではamuseは有効になっていないよ！");
            return;
        }

        _databaseService.Delete(AmuseChannel.TableName, existing.Id);
        await RespondAsync("amuseを無効にしたよ！");
    }
}
