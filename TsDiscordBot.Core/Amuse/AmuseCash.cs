using System;

namespace TsDiscordBot.Core.Amuse;

public class AmuseCash
{
    public const string TableName = "amuse_cash";

    public int Id { get; set; }
    public ulong UserId { get; set; }
    public long Cash { get; set; }
    public DateTime? LastEarnedAtUtc { get; set; }
    public DateTime LastUpdatedAtUtc { get; set; }
}

