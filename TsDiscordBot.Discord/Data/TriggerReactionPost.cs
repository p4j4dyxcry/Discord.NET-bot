namespace TsDiscordBot.Discord.Data;

public class TriggerReactionPost
{
    public const string TableName = "trigger_reaction";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public string TriggerWord { get; set; } = string.Empty;
    public string Reaction { get; set; } = string.Empty;
}