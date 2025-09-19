using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using Xunit;

namespace TsDiscordBot.Tests;

public class BlackJackGameTests
{
    [Fact]
    public void Hit_Busts_PlayerLoses()
    {
        var deck = new Deck([
            new Card(Rank.Ten, Suit.Hearts),
            new Card(Rank.Five, Suit.Spades),
            new Card(Rank.Nine, Suit.Clubs),
            new Card(Rank.Six, Suit.Hearts),
            new Card(Rank.Eight, Suit.Diamonds),
            new Card(Rank.Ten, Suit.Diamonds)
        ]);
        var game = new BlackJackGame(10, deck);
        game.Hit();

        Assert.True(game.IsFinished);
        Assert.Equal(GameOutcome.DealerWin, game.Result!.Outcome);
        Assert.Equal(0, game.Result.Payout);
    }

    [Fact]
    public void Hit_Busts_DealerContinuesToDraw()
    {
        var deck = new Deck([
            new Card(Rank.Ten, Suit.Hearts),
            new Card(Rank.Six, Suit.Spades),
            new Card(Rank.Seven, Suit.Clubs),
            new Card(Rank.Six, Suit.Hearts),
            new Card(Rank.Ten, Suit.Diamonds),
            new Card(Rank.Four, Suit.Diamonds),
            new Card(Rank.Five, Suit.Clubs)
        ]);
        var game = new BlackJackGame(10, deck);
        game.Hit();

        Assert.True(game.IsFinished);
        Assert.Equal(4, game.DealerCards.Count);
        Assert.Equal(21, BlackJackGame.CalculateScore(game.DealerCards));
    }

    [Fact]
    public void Hit_BothBusts_ResultsInPush()
    {
        var deck = new Deck([
            new Card(Rank.Ten, Suit.Hearts),
            new Card(Rank.Seven, Suit.Spades),
            new Card(Rank.Nine, Suit.Clubs),
            new Card(Rank.Seven, Suit.Hearts),
            new Card(Rank.King, Suit.Diamonds),
            new Card(Rank.Nine, Suit.Diamonds)
        ]);
        var game = new BlackJackGame(10, deck);
        game.Hit();

        Assert.True(game.IsFinished);
        Assert.Equal(GameOutcome.Push, game.Result!.Outcome);
        Assert.Equal(10, game.Result.Payout);
    }

    [Fact]
    public void Stand_PlayerHigherScore_Wins()
    {
        var deck = new Deck([
            new Card(Rank.Ten, Suit.Hearts),
            new Card(Rank.Nine, Suit.Spades),
            new Card(Rank.Nine, Suit.Clubs),
            new Card(Rank.Six, Suit.Clubs),
            new Card(Rank.Two, Suit.Hearts),
            new Card(Rank.Nine, Suit.Diamonds)
        ]);
        var game = new BlackJackGame(10, deck);
        game.Stand();

        Assert.True(game.IsFinished);
        Assert.Equal(GameOutcome.PlayerWin, game.Result!.Outcome);
        Assert.Equal(20, game.Result.Payout);
    }

    [Fact]
    public void DoubleDown_Win_PayoutQuadrupled()
    {
        var deck = new Deck([
            new Card(Rank.Five, Suit.Hearts),
            new Card(Rank.Nine, Suit.Spades),
            new Card(Rank.Six, Suit.Clubs),
            new Card(Rank.Nine, Suit.Hearts),
            new Card(Rank.Ten, Suit.Diamonds)
        ]);
        var game = new BlackJackGame(10, deck);
        game.DoubleDown();

        Assert.True(game.IsFinished);
        Assert.Equal(GameOutcome.PlayerWin, game.Result!.Outcome);
        Assert.Equal(40, game.Result.Payout);
    }

    [Fact]
    public void Surrender_PlayerLosesWithoutPayout()
    {
        var deck = new Deck([
            new Card(Rank.Ten, Suit.Hearts),
            new Card(Rank.Nine, Suit.Spades),
            new Card(Rank.Nine, Suit.Clubs),
            new Card(Rank.Six, Suit.Clubs)
        ]);
        var game = new BlackJackGame(10, deck);
        game.Surrender();

        Assert.True(game.IsFinished);
        Assert.Equal(GameOutcome.DealerWin, game.Result!.Outcome);
        Assert.Equal(0, game.Result.Payout);
    }
}
