namespace TsDiscordBot.Discord.Data;

public class CommandUsage
{
    public const string TableName = "command_usage";

    public int Id { get; set; }
    public ulong UserId { get; set; }
    public string CommandName { get; set; } = string.Empty;
    public DateTimeOffset UsedAt { get; set; }
}
