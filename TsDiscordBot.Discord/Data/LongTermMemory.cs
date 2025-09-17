namespace TsDiscordBot.Discord.Data;

public class LongTermMemory
{
    public const string TableName = "long_term_memory";

    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}