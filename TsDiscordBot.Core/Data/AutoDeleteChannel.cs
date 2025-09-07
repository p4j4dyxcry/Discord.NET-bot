namespace TsDiscordBot.Core.Data;

public class AutoDeleteChannel
{
    public const string TableName = "auto_delete_channel";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public int DelayMinutes { get; set; }
    public DateTime EnabledAtUtc { get; set; }
    public ulong LastMessageId { get; set; }
}
