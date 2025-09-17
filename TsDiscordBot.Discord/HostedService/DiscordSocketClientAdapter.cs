using Discord;
using Discord.WebSocket;

namespace TsDiscordBot.Core.HostedService;

public class DiscordSocketClientAdapter : IDiscordBotClient
{
    private readonly DiscordSocketClient _client;

    public DiscordSocketClientAdapter(DiscordSocketClient client)
    {
        _client = client;
    }

    public event Func<LogMessage, Task>? Log
    {
        add { _client.Log += value; }
        remove { _client.Log -= value; }
    }

    public Task LoginAsync(TokenType tokenType, string token)
        => _client.LoginAsync(tokenType, token);

    public Task StartAsync()
        => _client.StartAsync();

    public Task LogoutAsync()
        => _client.LogoutAsync();

    public Task StopAsync()
        => _client.StopAsync();
}
