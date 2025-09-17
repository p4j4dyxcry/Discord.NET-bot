namespace TsDiscordBot.Discord.Data;

public class BeRealConfig
{
    public const string TableName = "bereal_config";
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong PostChannelId { get; set; }
    public ulong FeedChannelId { get; set; }
    public ulong RoleId { get; set; }
}
