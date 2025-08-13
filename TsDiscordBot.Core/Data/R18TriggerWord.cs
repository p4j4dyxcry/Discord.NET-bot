namespace TsDiscordBot.Core.Data
{
    public class R18TriggerWord
    {
        public const string TableName = "R18TriggerWords";
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public string Word { get; set; } = string.Empty;
    }
}