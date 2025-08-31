using System;

namespace TsDiscordBot.Core.Data;

public class AutoDeleteMessage
{
    public const string TableName = "auto_delete_message";

    public int Id { get; set; }
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public DateTime DeleteAtUtc { get; set; }
}
