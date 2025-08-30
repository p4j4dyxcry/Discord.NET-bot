using TsDiscordBot.Core.Utility;
using Xunit;

namespace TsDiscordBot.Tests;

public class UserNameFixLogicTests
{
    [Theory]
    [InlineData("ソフィア#3040", "ソフィア")]
    [InlineData("Alice", "Alice")]
    [InlineData("", "")]
    public void Fix_RemovesDiscriminator(string input, string expected)
    {
        Assert.Equal(expected, UserNameFixLogic.Fix(input));
    }
}
