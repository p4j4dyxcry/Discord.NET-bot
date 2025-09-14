using System;
using System.Linq;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class PlayDiceService(int bet, DatabaseService databaseService) : IAmuseService
{
    private const string GameKind = "DI";
    private readonly int _bet = bet;
    private readonly DatabaseService _databaseService = databaseService;

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
                await message.ReplyMessageAsync("現在サイコロ勝負をプレイ中です。5分後に再試行してください。");
                return;
            }

            _databaseService.Delete(AmusePlay.TableName, existing.Id);
        }

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

        var bet = _bet;
        if (currentCash <= 0)
        {
            bet = 100;
            currentCash -= bet;
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

        cash.Cash = currentCash;
        cash.LastUpdatedAtUtc = DateTime.UtcNow;
        _databaseService.Update(AmuseCash.TableName, cash);

        var play = new AmusePlay
        {
            UserId = message.AuthorId,
            CreatedAtUtc = DateTime.UtcNow,
            GameKind = GameKind,
            ChannelId = message.ChannelId,
            Bet = bet,
            Started = false
        };
        _databaseService.Insert(AmusePlay.TableName, play);

        var reply = await message.ReplyMessageAsync("サイコロ勝負のゲームを開始します。");

        if (reply is not null)
        {
            play.MessageId = reply.Id;
            _databaseService.Update(AmusePlay.TableName, play);
        }
    }
}

