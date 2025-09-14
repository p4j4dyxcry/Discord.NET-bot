using System;
using System.Linq;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class PlayBlackJackService : IAmuseService
{
    private const string GameKind = "BJ";
    private readonly int _bet;
    private readonly DatabaseService _databaseService;

    public PlayBlackJackService(int bet, DatabaseService databaseService)
    {
        _bet = bet;
        _databaseService = databaseService;
    }

    public async Task ExecuteAsync(IMessageData message)
    {
        var existing = _databaseService
            .FindAll<AmusePlay>(AmusePlay.TableName)
            .FirstOrDefault(x => x.UserId == message.AuthorId && x.GameKind == GameKind);

        if (existing is not null)
        {
            var elapsed = DateTime.UtcNow - existing.CreatedAtUtc;
            if (elapsed < TimeSpan.FromMinutes(5))
            {
                await message.ReplyMessageAsync("現在ブラックジャックをプレイ中です。5分後に再試行してください。");
                return;
            }

            _databaseService.Delete(AmusePlay.TableName, existing.Id);
        }

        var play = new AmusePlay
        {
            UserId = message.AuthorId,
            CreatedAtUtc = DateTime.UtcNow,
            GameKind = GameKind
        };
        _databaseService.Insert(AmusePlay.TableName, play);
        // Load or create cash record
        var cash = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == message.AuthorId);

        if (cash is null)
        {
            cash = new AmuseCash
            {
                UserId = message.AuthorId,
                Cash = 0,
                LastUpdatedAtUtc = DateTime.UtcNow
            };
            _databaseService.Insert(AmuseCash.TableName, cash);
        }

        var currentCash = cash.Cash;

        // Determine bet according to spec
        var bet = _bet;
        if (currentCash <= 0)
        {
            bet = 100;
            currentCash -= bet; // borrow 100
        }
        else
        {
            if (bet <= 0)
            {
                bet = currentCash < 100 ? (int)currentCash : 100;
            }
            else if (bet > currentCash)
            {
                bet = (int)currentCash;
            }

            currentCash -= bet;
        }

        // Update cash after placing bet
        cash.Cash = currentCash;
        cash.LastUpdatedAtUtc = DateTime.UtcNow;
        _databaseService.Update(AmuseCash.TableName, cash);

        // Play game (auto resolve without user interaction)
        var game = new BlackJackGame(bet);
        game.Stand();
        var result = game.Result!;

        cash.Cash += result.Payout;
        cash.LastUpdatedAtUtc = DateTime.UtcNow;
        _databaseService.Update(AmuseCash.TableName, cash);

        string FormatCard(Card c)
        {
            var rank = c.Rank switch
            {
                Rank.Ace => "A",
                Rank.King => "K",
                Rank.Queen => "Q",
                Rank.Jack => "J",
                _ => ((int)c.Rank).ToString()
            };
            var suit = c.Suit switch
            {
                Suit.Clubs => "♣",
                Suit.Diamonds => "♦",
                Suit.Hearts => "♥",
                _ => "♠"
            };
            return rank + suit;
        }

        var dealerCards = string.Join(" ", result.DealerCards.Select(FormatCard));
        var playerCards = string.Join(" ", result.PlayerCards.Select(FormatCard));
        var dealerScore = BlackJackGame.CalculateScore(result.DealerCards);
        var playerScore = BlackJackGame.CalculateScore(result.PlayerCards);
        var outcome = result.Outcome switch
        {
            GameOutcome.PlayerWin => "勝ち",
            GameOutcome.DealerWin => "負け",
            _ => "引き分け"
        };

        var finalCash = cash.Cash;

        var messageText = $"{message.AuthorMention}の手札: {playerCards} (計{playerScore})\n" +
                          $"ディーラーの手札: {dealerCards} (計{dealerScore})\n" +
                          $"結果: {outcome}\n現在の所持金: {finalCash}GAL円";

        var reply = await message.ReplyMessageAsync(messageText);

        if (reply is not null)
        {
            play.MessageId = reply.Id;
            _databaseService.Update(AmusePlay.TableName, play);
        }

        _databaseService.Delete(AmusePlay.TableName, play.Id);

        return;
    }
}
