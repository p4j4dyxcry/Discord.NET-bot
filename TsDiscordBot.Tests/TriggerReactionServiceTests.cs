using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TsDiscordBot.Discord.Services;
using Xunit;

namespace TsDiscordBot.Tests;

public class TriggerReactionServiceTests
{
    private class SlowDatabaseService : DatabaseService
    {
        public SlowDatabaseService() : base(NullLogger<DatabaseService>.Instance,
            new ConfigurationBuilder().Build())
        {
        }

        public override Task<IEnumerable<T>> FindAllAsync<T>(string tableName)
        {
            return Task.Run(async () =>
            {
                await Task.Delay(200);
                return Enumerable.Empty<T>();
            });
        }
    }

    [Fact]
    public async Task FindAllAsync_DoesNotBlockCaller()
    {
        var service = new SlowDatabaseService();

        var sw = Stopwatch.StartNew();
        Task<IEnumerable<int>> task = service.FindAllAsync<int>("dummy");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 100);
        await task;
    }
}
