using TsDiscordBot.Interfaces.Game.BlackJack;

namespace TsDiscordBot.Interfaces.Game.HighLow;

public enum GuessPrediction
{
    High,
    Low
}

public sealed record GuessResult(bool Correct, Card DrawnCard, bool MaxReached);

public class HighLowGame
{
    private readonly Deck _deck;

    public int Bet { get; }
    public Card CurrentCard { get; private set; }
    public int Streak { get; private set; }
    public int MaxStreak { get; } = 10;

    public HighLowGame(int bet, Deck? deck = null)
    {
        Bet = bet;
        _deck = deck ?? new Deck();
        CurrentCard = _deck.Draw();
    }

    public GuessResult Guess(GuessPrediction prediction)
    {
        Card next;
        do
        {
            next = _deck.Draw();
        } while (next.Rank == CurrentCard.Rank);

        var correct = prediction == GuessPrediction.High
            ? next.Rank > CurrentCard.Rank
            : next.Rank < CurrentCard.Rank;

        CurrentCard = next;
        if (correct)
        {
            Streak++;
            return new GuessResult(true, next, Streak >= MaxStreak);
        }

        return new GuessResult(false, next, false);
    }


    public int CalculatePayout()
    {
        return CalculatePayout(Streak);
    }

    public int CalculateNextStreakPayout()
    {
        return CalculatePayout(Streak+1);
    }

    public int CalculatePayout(int streak)
    {
        // インデックス = 連勝数 に対応
        int[] table = [0, 1, 2, 4, 7, 10, 15, 20, 30, 50, 100];

        if (streak <= 0)
            return 0; // 0以下は無効

        // テーブル外に出たら最後の要素を使う
        int index = streak < table.Length ? streak : table.Length - 1;

        return Bet * table[index];
    }
}

