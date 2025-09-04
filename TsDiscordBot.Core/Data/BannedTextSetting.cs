namespace TsDiscordBot.Core.Data
{
    public class BannedTextSetting
    {
        public const string TableName = "BannedTextSettings";
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public BannedTextMode Mode { get; set; } = BannedTextMode.Hide;
    }

    public enum BannedTextMode
    {
        Hide,
        Delete
    }
}
