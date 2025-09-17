namespace TsDiscordBot.Discord.Amuse;

public class AmusePlay
{
    public const string TableName = "amuse_play";

    public int Id { get; set; }
    public ulong UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string GameKind { get; set; } = string.Empty;
    public ulong MessageId { get; set; }
    public ulong ChannelId { get; set; }
    public int Bet { get; set; }
    public bool Started { get; set; }
}

