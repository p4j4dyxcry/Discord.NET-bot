using System;

namespace TsDiscordBot.Core.Amuse;

public class AmuseChannel
{
    public const string TableName = "amuse_channel";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public DateTime EnabledAtUtc { get; set; }
}
