using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TsDiscordBot.Discord.HostedService;
using Xunit;

namespace TsDiscordBot.Tests;

public class DiscordStartupServiceTests
{
    private class TestDiscordClient : IDiscordBotClient
    {
        public event Func<LogMessage, Task>? Log;
        public TokenType? LoginTokenType { get; private set; }
        public string? LoginToken { get; private set; }
        public bool Started { get; private set; }
        public bool LoggedOut { get; private set; }
        public bool Stopped { get; private set; }

        public Task LoginAsync(TokenType tokenType, string token)
        {
            LoginTokenType = tokenType;
            LoginToken = token;
            return Task.CompletedTask;
        }

        public Task StartAsync()
        {
            Started = true;
            Log?.Invoke(new LogMessage(LogSeverity.Info, "TestDiscordClient", "Started"));
            return Task.CompletedTask;
        }

        public Task LogoutAsync()
        {
            LoggedOut = true;
            Log?.Invoke(new LogMessage(LogSeverity.Info, "Test", "Discord bot logged out."));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Stopped = true;
            Log?.Invoke(new LogMessage(LogSeverity.Info, "Test", "Discord bot is stopped"));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StartAsync_LogsInAndStartsClient()
    {
        var client = new TestDiscordClient();
        Environment.SetEnvironmentVariable("DISCORD_TOKEN", "abc123");
        var config = new ConfigurationBuilder().Build();
        var service = new DiscordStartupService(client, config, NullLogger<IDiscordBotClient>.Instance);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(TokenType.Bot, client.LoginTokenType);
        Assert.Equal("abc123", client.LoginToken);
        Assert.True(client.Started);
    }

    [Fact]
    public async Task StopAsync_LogsOutAndStopsClient()
    {
        var client = new TestDiscordClient();
        var config = new ConfigurationBuilder().Build();
        var service = new DiscordStartupService(client, config, NullLogger<IDiscordBotClient>.Instance);

        await service.StopAsync(CancellationToken.None);

        Assert.True(client.LoggedOut);
        Assert.True(client.Stopped);
    }
}
