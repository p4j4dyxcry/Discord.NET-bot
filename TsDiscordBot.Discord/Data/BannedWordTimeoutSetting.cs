namespace TsDiscordBot.Core.Data
{
    public class BannedWordTimeoutSetting
    {
        public const string TableName = "BannedWordTimeoutSettings";
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int Count { get; set; } = 5;
        public int WindowMinutes { get; set; } = 5;
        public int TimeoutMinutes { get; set; } = 1;
    }
}
