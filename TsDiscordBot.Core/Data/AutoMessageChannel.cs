using System;

namespace TsDiscordBot.Core.Data;

public class AutoMessageChannel
{
    public const string TableName = "auto_message_channel";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public int IntervalHours { get; set; }
    public DateTime LastPostedUtc { get; set; }
}
