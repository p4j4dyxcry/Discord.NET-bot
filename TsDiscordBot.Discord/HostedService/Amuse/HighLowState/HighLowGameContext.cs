using System.Linq;
using Discord.WebSocket;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game.BlackJack;
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
        var result = EmoteDatabase.GetEmote(card);
        if (!string.IsNullOrWhiteSpace(result))
        {
            return result;
        }

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

    public int DetermineReplayBet()
    {
        var cash = DatabaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == Play.UserId);

        if (cash is null || cash.Cash < Play.Bet)
        {
            return 100;
        }

        return Play.Bet;
    }

    public void UpdateGameRecord(bool win)
    {
        var record = DatabaseService
            .FindAll<AmuseGameRecord>(AmuseGameRecord.TableName)
            .FirstOrDefault(x => x.UserId == Play.UserId && x.GameKind == Play.GameKind);

        if (record is null)
        {
            record = new AmuseGameRecord
            {
                UserId = Play.UserId,
                GameKind = Play.GameKind,
                TotalPlays = 0,
                WinCount = 0
            };
            DatabaseService.Insert(AmuseGameRecord.TableName, record);
        }

        record.TotalPlays++;
        if (win)
        {
            record.WinCount++;
        }

        DatabaseService.Update(AmuseGameRecord.TableName, record);
    }
}
