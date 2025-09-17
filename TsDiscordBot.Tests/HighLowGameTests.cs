using TsDiscordBot.Interfaces.Game.BlackJack;
using TsDiscordBot.Interfaces.Game.HighLow;
using Xunit;

namespace TsDiscordBot.Tests;

public class HighLowGameTests
{
    [Fact]
    public void CorrectHighGuess_IncrementsStreak()
    {
        var deck = new Deck(new[]
        {
            new Card(Rank.Five, Suit.Clubs),
            new Card(Rank.Seven, Suit.Clubs)
        });
        var game = new HighLowGame(10, deck);
        var result = game.Guess(GuessPrediction.High);
        Assert.True(result.Correct);
        Assert.Equal(Rank.Seven, result.DrawnCard.Rank);
        Assert.Equal(1, game.Streak);
    }

    [Fact]
    public void IncorrectGuess_EndsWithoutStreak()
    {
        var deck = new Deck(new[]
        {
            new Card(Rank.Eight, Suit.Clubs),
            new Card(Rank.Three, Suit.Clubs)
        });
        var game = new HighLowGame(10, deck);
        var result = game.Guess(GuessPrediction.High);
        Assert.False(result.Correct);
        Assert.Equal(0, game.Streak);
    }

    [Fact]
    public void TieIsIgnored()
    {
        var deck = new Deck(new[]
        {
            new Card(Rank.Four, Suit.Clubs),
            new Card(Rank.Four, Suit.Diamonds),
            new Card(Rank.Six, Suit.Hearts)
        });
        var game = new HighLowGame(10, deck);
        var result = game.Guess(GuessPrediction.High);
        Assert.True(result.Correct);
        Assert.Equal(Rank.Six, result.DrawnCard.Rank);
    }
}

