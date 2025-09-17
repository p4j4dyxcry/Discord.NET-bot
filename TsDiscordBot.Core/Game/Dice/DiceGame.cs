namespace TsDiscordBot.Interfaces.Game.Dice;

public enum DiceOutcome
{
    PlayerWin,
    DealerWin,
    Push
}

public sealed record DiceResult(DiceOutcome Outcome, int Payout, int DealerRoll, int PlayerRoll);

public class DiceGame
{
    private readonly Random _random;

    public int Bet { get; }
    public DiceResult Result { get; }

    public DiceGame(int bet, int? dealerRoll = null, int? playerRoll = null, Random? random = null)
    {
        Bet = bet;
        _random = random ?? new Random();
        var dealer = dealerRoll ?? _random.Next(1, 7);
        var player = playerRoll ?? _random.Next(1, 7);
        var outcome = DetermineOutcome(dealer, player);
        var payout = outcome switch
        {
            DiceOutcome.PlayerWin => Bet * 2,
            DiceOutcome.Push => Bet,
            _ => 0
        };
        Result = new DiceResult(outcome, payout, dealer, player);
    }

    private static DiceOutcome DetermineOutcome(int dealer, int player)
    {
        if (player > dealer)
        {
            return DiceOutcome.PlayerWin;
        }
        if (player < dealer)
        {
            return DiceOutcome.DealerWin;
        }
        return DiceOutcome.Push;
    }
}

