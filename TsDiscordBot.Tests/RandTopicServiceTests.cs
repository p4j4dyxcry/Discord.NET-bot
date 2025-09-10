using System;
using TsDiscordBot.Core.Services;
using Xunit;

namespace TsDiscordBot.Tests;

public class RandTopicServiceTests
{
    [Fact]
    public void GetTopic_ReturnsTopicForKnownDate()
    {
        var service = new RandTopicService();
        var topic = service.GetTopic(new DateTime(2024, 1, 1));
        Assert.Equal("お正月マジぱねぇ🎍✨みんなお年玉どんくらい貰ってた？💸てか今なら何に使うん？", topic);
    }
}
