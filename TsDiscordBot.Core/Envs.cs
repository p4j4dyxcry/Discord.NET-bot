namespace TsDiscordBot.Core
{
    public static class Envs
    {
        public static string OPENAI_API_KEY => Environment.GetEnvironmentVariable(nameof(OPENAI_API_KEY)) ?? string.Empty;
        public static string OPENAI_PROMPT => Environment.GetEnvironmentVariable(nameof(OPENAI_PROMPT)) ?? string.Empty;
        public static string DISCORD_TOKEN => Environment.GetEnvironmentVariable(nameof(DISCORD_TOKEN)) ?? string.Empty;
        public static string LITEDB_PATH => Environment.GetEnvironmentVariable(nameof(LITEDB_PATH)) ?? string.Empty;
    }
}