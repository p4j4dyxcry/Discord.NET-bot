namespace TsDiscordBot.Discord.Data;

public class BeRealParticipant
{
    public const string TableName = "bereal-participant";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public DateTime LastPostedAtUtc { get; set; }
}
