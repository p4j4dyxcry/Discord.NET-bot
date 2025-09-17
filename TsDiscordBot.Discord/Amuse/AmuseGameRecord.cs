namespace TsDiscordBot.Discord.Amuse;

public class AmuseGameRecord
{
    public const string TableName = "amuse_game_record";

    public int Id { get; set; }
    public ulong UserId { get; set; }
    public string GameKind { get; set; } = string.Empty;
    public int TotalPlays { get; set; }
    public int WinCount { get; set; }
}

