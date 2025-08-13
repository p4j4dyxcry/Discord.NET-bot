using System;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService;

public class DiscordStartupService : IHostedService
{
    private readonly DiscordSocketClient _discord;
    private readonly IConfiguration _config;

    public DiscordStartupService(DiscordSocketClient discord, IConfiguration config, ILogger<DiscordSocketClient> logger)
    {
        _discord = discord;
        _config = config;

        _discord.Log += msg => LogHelper.OnLogAsync(logger, msg);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = Envs.DISCORD_TOKEN;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = _config["token"];
        }

        await _discord.LoginAsync(TokenType.Bot, token);
        await _discord.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _discord.LogoutAsync();
        await _discord.StopAsync();
    }
}