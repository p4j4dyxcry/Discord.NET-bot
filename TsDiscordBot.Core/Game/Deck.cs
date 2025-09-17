namespace TsDiscordBot.Interfaces.Game.BlackJack;

public class Deck
{
    private Stack<Card> _cards;
    private static readonly Random Rng = new();

    public Deck() : this(CreateShuffledDeck())
    {
    }

    public Deck(IEnumerable<Card> cards)
    {
        _cards = new Stack<Card>(cards.Reverse());
    }

    private static IEnumerable<Card> CreateShuffledDeck()
    {
        var cards = new List<Card>();
        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                cards.Add(new Card(rank, suit));
            }
        }

        return cards.OrderBy(_ => Rng.Next());
    }

    public Card Draw()
    {
        if (_cards.Count == 0)
        {
            _cards = new Stack<Card>(CreateShuffledDeck().Reverse());
        }

        return _cards.Pop();
    }
}
