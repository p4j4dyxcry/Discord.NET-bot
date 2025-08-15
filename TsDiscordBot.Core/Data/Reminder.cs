namespace TsDiscordBot.Core.Data;

public class Reminder
{
    public const string TableName = "reminder";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
    public DateTime RemindAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
}
