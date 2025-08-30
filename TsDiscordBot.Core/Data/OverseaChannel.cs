using System;

namespace TsDiscordBot.Core.Data;

public class OverseaChannel
{
    public const string TableName = "oversea_channel";

    public int Id { get; set; }
    public int OverseaId { get; set; }
    public ulong ChannelId { get; set; }
}

