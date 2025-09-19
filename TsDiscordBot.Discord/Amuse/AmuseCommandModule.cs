using Discord;
using Discord.Interactions;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

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

    [SlashCommand("amuse-god", "指定したユーザーの所持金を増加させます。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task IncreaseCashAsync(IGuildUser user, long amount)
    {
        if (amount <= 0)
        {
            await RespondAsync("付与する金額は1GAL円以上を指定してね！", ephemeral: true);
            return;
        }

        var utcNow = DateTime.UtcNow;

        var cash = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == user.Id);

        if (cash is null)
        {
            cash = new AmuseCash
            {
                UserId = user.Id,
                Cash = amount,
                LastUpdatedAtUtc = utcNow
            };

            _databaseService.Insert(AmuseCash.TableName, cash);
        }
        else
        {
            cash.Cash += amount;
            cash.LastUpdatedAtUtc = utcNow;
            _databaseService.Update(AmuseCash.TableName, cash);
        }

        await RespondAsync($"{user.Mention}さんに{amount}GAL円を付与しました！現在の所持金は{cash.Cash}GAL円です。");
    }
}
