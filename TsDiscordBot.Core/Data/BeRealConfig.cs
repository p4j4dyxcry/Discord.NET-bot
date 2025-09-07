namespace TsDiscordBot.Core.Data;

public class BeRealConfig
{
    public const string TableName = "bereal-config";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong PostChannelId { get; set; }
    public ulong FeedChannelId { get; set; }
    public ulong PinsChannelId { get; set; }
    public ulong RoleId { get; set; }
}
