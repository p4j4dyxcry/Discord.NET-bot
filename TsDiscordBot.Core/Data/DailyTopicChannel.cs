namespace TsDiscordBot.Core.Data;

public class DailyTopicChannel
{
    public const string TableName = "daily_topic_channel";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public TimeSpan PostAtJst { get; set; }
    public DateTime LastPostedUtc { get; set; }
}
