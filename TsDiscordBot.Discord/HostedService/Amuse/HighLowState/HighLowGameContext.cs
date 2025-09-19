using Discord.WebSocket;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.HighLow;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState;

public class HighLowGameContext
{
    public AmusePlay Play { get; }
    public HighLowGame Game { get; private set; }
    public IDatabaseService DatabaseService { get; }
    public EmoteDatabase EmoteDatabase { get; }
    public DiscordSocketClient Client { get; }

    public HighLowGameContext(
        AmusePlay play,
        IDatabaseService databaseService,
        EmoteDatabase emoteDatabase,
        DiscordSocketClient client)
    {
        Play = play;
        DatabaseService = databaseService;
        EmoteDatabase = emoteDatabase;
        Client = client;
        Game = new HighLowGame(play.Bet);
    }

    public void ResetGame(int bet)
    {
        Play.Bet = bet;
        Game = new HighLowGame(bet);
    }

    public string FormatCard(Card card)
    {
        var rank = card.Rank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            _ => ((int)card.Rank).ToString()
        };

        var suit = card.Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            _ => "♠"
        };

        return rank + suit;
    }
}
