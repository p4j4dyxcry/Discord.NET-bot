using System;

namespace TsDiscordBot.Core.Data;

public class BeRealChannel
{
    public const string TableName = "be_real_channel";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
}

