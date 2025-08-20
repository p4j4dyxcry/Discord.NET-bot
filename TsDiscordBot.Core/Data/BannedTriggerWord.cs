namespace TsDiscordBot.Core.Data
{
    public class BannedTriggerWord
    {
        public const string TableName = "BannedTriggerWords";
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public string Word { get; set; } = string.Empty;
    }
}