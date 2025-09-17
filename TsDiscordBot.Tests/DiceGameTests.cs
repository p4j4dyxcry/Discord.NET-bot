using TsDiscordBot.Interfaces.Game.Dice;
using Xunit;

namespace TsDiscordBot.Tests;

public class DiceGameTests
{
    [Fact]
    public void PlayerHigherRoll_Wins()
    {
        var game = new DiceGame(10, dealerRoll: 2, playerRoll: 5);
        Assert.Equal(DiceOutcome.PlayerWin, game.Result.Outcome);
        Assert.Equal(20, game.Result.Payout);
    }

    [Fact]
    public void DealerHigherRoll_PlayerLoses()
    {
        var game = new DiceGame(10, dealerRoll: 6, playerRoll: 1);
        Assert.Equal(DiceOutcome.DealerWin, game.Result.Outcome);
        Assert.Equal(0, game.Result.Payout);
    }

    [Fact]
    public void SameRoll_Push()
    {
        var game = new DiceGame(10, dealerRoll: 3, playerRoll: 3);
        Assert.Equal(DiceOutcome.Push, game.Result.Outcome);
        Assert.Equal(10, game.Result.Payout);
    }
}

