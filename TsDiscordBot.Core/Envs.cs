using Microsoft.Extensions.Logging;

namespace TsDiscordBot.Core
{
    public static class Envs
    {
        public static string OPENAI_API_KEY => Environment.GetEnvironmentVariable(nameof(OPENAI_API_KEY)) ?? string.Empty;
        public static string OPENAI_PROMPT => Environment.GetEnvironmentVariable(nameof(OPENAI_PROMPT)) ?? string.Empty;
        public static string DISCORD_TOKEN => Environment.GetEnvironmentVariable(nameof(DISCORD_TOKEN)) ?? string.Empty;
        public static string LITEDB_PATH => Environment.GetEnvironmentVariable(nameof(LITEDB_PATH)) ?? string.Empty;
        public static string APP_DATA_PATH => Environment.GetEnvironmentVariable(nameof(APP_DATA_PATH)) ?? string.Empty;
        public static string LAVALINK_WS => Environment.GetEnvironmentVariable(nameof(LAVALINK_WS)) ?? string.Empty;
        public static string LAVALINK_SERVER_PASSWORD => Environment.GetEnvironmentVariable(nameof(LAVALINK_SERVER_PASSWORD)) ?? string.Empty;

        public static void LogEnvironmentVariables()
        {
            Console.WriteLine(
                "ENV present: OPENAI_API_KEY={0}, DISCORD_TOKEN={1}, LAVALINK_SERVER_PASSWORD={2}",
                !string.IsNullOrWhiteSpace(OPENAI_API_KEY),
                !string.IsNullOrWhiteSpace(DISCORD_TOKEN),
                !string.IsNullOrWhiteSpace(LAVALINK_SERVER_PASSWORD));

            Console.WriteLine("ENV values: OPENAI_PROMPT={0}, LITEDB_PATH={1}, APP_DATA_PATH={2}, LAVALINK_WS={3}" ,
                OPENAI_PROMPT,
                LITEDB_PATH,
                APP_DATA_PATH,
                LAVALINK_WS);
        }
    }
}