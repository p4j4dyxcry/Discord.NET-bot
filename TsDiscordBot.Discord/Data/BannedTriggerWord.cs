namespace TsDiscordBot.Discord.Data
{
    public class BannedTriggerWord
    {
        public const string TableName = "R18TriggerWords"; // for compatibility
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public string Word { get; set; } = string.Empty;
    }
}