namespace TsDiscordBot.Core.Data;

public class Poll
{
    public const string TableName = "poll";

    public int Id { get; set; }
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
}

