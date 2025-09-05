using System;

namespace TsDiscordBot.Core.Data;

public class BeRealPost
{
    public const string TableName = "be_real_post";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime PostedAtUtc { get; set; }
}

