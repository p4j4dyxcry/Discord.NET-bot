using Discord;

namespace TsDiscordBot.Core.HostedService;

public interface IDiscordBotClient
{
    event Func<LogMessage, Task>? Log;
    Task LoginAsync(TokenType tokenType, string token);
    Task StartAsync();
    Task LogoutAsync();
    Task StopAsync();
}
