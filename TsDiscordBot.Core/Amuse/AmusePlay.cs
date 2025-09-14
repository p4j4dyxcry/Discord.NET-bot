using System;

namespace TsDiscordBot.Core.Amuse;

public class AmusePlay
{
    public const string TableName = "amuse_play";

    public int Id { get; set; }
    public ulong UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string GameKind { get; set; } = string.Empty;
    public ulong MessageId { get; set; }
}

