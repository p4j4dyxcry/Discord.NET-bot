using Discord.WebSocket;
using Lavalink4NET;
using Microsoft.Extensions.Hosting;

namespace TsDiscordBot.Core.HostedService;

public class LavalinkHostedService : IHostedService
{
    private readonly IAudioService _audioService;
    private readonly DiscordSocketClient _client;

    public LavalinkHostedService(IAudioService audioService, DiscordSocketClient client)
    {
        _audioService = audioService;
        _client = client;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Ready += InitializeAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Ready -= InitializeAsync;
        return Task.CompletedTask;
    }

    private Task InitializeAsync()
        => _audioService.InitializeAsync();
}
