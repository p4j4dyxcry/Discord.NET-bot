namespace TsDiscordBot.Discord.Data
{
    public class BannedExcludeWord
    {
        public const string TableName = "BannedExcludeWords";
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public string Word { get; set; } = string.Empty;
    }
}
