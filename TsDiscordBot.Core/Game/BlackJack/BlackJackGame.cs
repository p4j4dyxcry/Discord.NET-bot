namespace TsDiscordBot.Core.Game.BlackJack;

public enum GameOutcome
{
    PlayerWin,
    DealerWin,
    Push
}

public sealed record GameResult(GameOutcome Outcome, int Payout, IReadOnlyList<Card> DealerCards, IReadOnlyList<Card> PlayerCards);

public class BlackJackGame
{
    private readonly Deck _deck;
    private readonly List<Card> _dealerHand = [];
    private readonly List<Card> _playerHand = [];

    public int Bet { get; }
    public bool IsFinished { get; private set; }
    public bool DoubleDowned { get; private set; }
    public GameResult? Result { get; private set; }

    public IReadOnlyList<Card> DealerCards => _dealerHand.AsReadOnly();
    public IReadOnlyList<Card> PlayerCards => _playerHand.AsReadOnly();
    public Card DealerVisibleCard => _dealerHand[0];

    public BlackJackGame(int bet, Deck? deck = null)
    {
        Bet = bet;
        _deck = deck ?? new Deck();
        _playerHand.Add(_deck.Draw());
        _dealerHand.Add(_deck.Draw());
        _playerHand.Add(_deck.Draw());
        _dealerHand.Add(_deck.Draw());
    }

    public bool CanHit => !IsFinished && CalculateScore(PlayerCards) < 21;

    public void Hit()
    {
        if (IsFinished)
        {
            return;
        }

        _playerHand.Add(_deck.Draw());
        if (CalculateScore(_playerHand) > 21)
        {
            PlayDealerTurn();
            IsFinished = true;
            Result = BuildResult(GameOutcome.DealerWin);
        }
    }

    public void Stand()
    {
        if (IsFinished)
        {
            return;
        }

        PlayDealerTurn();
        IsFinished = true;
        Result = BuildResult(DetermineOutcome());
    }

    public void DoubleDown()
    {
        if (IsFinished || DoubleDowned || _playerHand.Count != 2)
        {
            return;
        }

        DoubleDowned = true;
        _playerHand.Add(_deck.Draw());
        if (CalculateScore(_playerHand) > 21)
        {
            PlayDealerTurn();
            IsFinished = true;
            Result = BuildResult(GameOutcome.DealerWin);
            return;
        }

        PlayDealerTurn();
        IsFinished = true;
        Result = BuildResult(DetermineOutcome());
    }

    public void Surrender()
    {
        if (IsFinished)
        {
            return;
        }

        IsFinished = true;
        Result = BuildResult(GameOutcome.DealerWin);
    }

    private void PlayDealerTurn()
    {
        while (CalculateScore(_dealerHand) <= 17)
        {
            _dealerHand.Add(_deck.Draw());
        }
    }

    private GameOutcome DetermineOutcome()
    {
        var player = CalculateScore(_playerHand);
        var dealer = CalculateScore(_dealerHand);

        if (player > 21 && dealer > 21)
        {
            return GameOutcome.Push;
        }

        if (player > 21)
        {
            return GameOutcome.DealerWin;
        }

        if (dealer > 21)
        {
            return GameOutcome.PlayerWin;
        }

        if (player > dealer)
        {
            return GameOutcome.PlayerWin;
        }

        if (player < dealer)
        {
            return GameOutcome.DealerWin;
        }

        return GameOutcome.Push;
    }

    private GameResult BuildResult(GameOutcome outcome)
    {
        var payout = outcome switch
        {
            GameOutcome.PlayerWin => DoubleDowned ? Bet * 4 : Bet * 2,
            GameOutcome.Push => Bet,
            _ => 0
        };

        return new GameResult(outcome, payout, DealerCards, PlayerCards);
    }

    public static int CalculateScore(IEnumerable<Card> hand)
    {
        var score = 0;
        var aceCount = 0;

        foreach (var card in hand)
        {
            switch (card.Rank)
            {
                case Rank.Ace:
                    aceCount++;
                    break;
                case >= Rank.Two and <= Rank.Ten:
                    score += (int)card.Rank;
                    break;
                default:
                    score += 10;
                    break;
            }
        }

        for (var i = 0; i < aceCount; i++)
        {
            if (score + 11 <= 21 - (aceCount - 1 - i))
            {
                score += 11;
            }
            else
            {
                score += 1;
            }
        }

        return score;
    }
}
