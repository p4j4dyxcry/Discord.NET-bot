namespace TsDiscordBot.Discord.Data;

public class AnonymousGuildUserSetting
{
    public const string TableName = "anonymous_guild_user_setting";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public bool IsAnonymous { get; set; }
    public string? AnonymousName { get; set; }
    public string? AnonymousAvatarUrl { get; set; }
}
