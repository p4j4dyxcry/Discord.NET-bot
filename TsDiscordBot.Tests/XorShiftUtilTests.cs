using System;
using TsDiscordBot.Core.Utility;
using Xunit;

namespace TsDiscordBot.Tests;

public class XorShiftUtilTests
{
    [Fact]
    public void SameInputsProduceSameValue()
    {
        ulong userId = 123456789UL;
        var date = new DateOnly(2024, 1, 1);

        var value1 = XorShiftUtil.GetValue(userId, date);
        var value2 = XorShiftUtil.GetValue(userId, date);

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void DifferentUserIdProducesDifferentValue()
    {
        var date = new DateOnly(2024, 1, 1);
        ulong userId1 = 1UL;
        ulong userId2 = 2UL;

        var value1 = XorShiftUtil.GetValue(userId1, date);
        var value2 = XorShiftUtil.GetValue(userId2, date);

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void DifferentDateProducesDifferentValue()
    {
        ulong userId = 123UL;
        var date1 = new DateOnly(2024, 1, 1);
        var date2 = new DateOnly(2024, 1, 2);

        var value1 = XorShiftUtil.GetValue(userId, date1);
        var value2 = XorShiftUtil.GetValue(userId, date2);

        Assert.NotEqual(value1, value2);
    }
}

