using System;
using System.Linq;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public abstract class PlayGameServiceBase(int bet, DatabaseService databaseService) : IAmuseService
{
    private readonly int _bet = bet;
    protected readonly DatabaseService DatabaseService = databaseService;

    protected abstract string GameKind { get; }
    protected abstract string InProgressMessage { get; }
    protected abstract string StartMessage { get; }

    public async Task ExecuteAsync(IMessageData message)
    {
        var existing = DatabaseService
            .FindAll<AmusePlay>(AmusePlay.TableName)
            .FirstOrDefault(x => x.UserId == message.AuthorId && x.GameKind == GameKind);

        if (existing is not null)
        {
            var elapsed = DateTime.UtcNow - existing.CreatedAtUtc;
            if (elapsed < TimeSpan.FromMinutes(5))
            {
                await message.ReplyMessageAsync(InProgressMessage);
                return;
            }

            DatabaseService.Delete(AmusePlay.TableName, existing.Id);
        }

        var cash = DatabaseService
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
            DatabaseService.Insert(AmuseCash.TableName, cash);
        }

        var currentCash = cash.Cash;

        var bet = _bet;
        if (currentCash <= 0)
        {
            bet = 100;
        }
        else
        {
            if (bet == int.MaxValue)
            {
                bet = currentCash > int.MaxValue ? int.MaxValue : (int)currentCash;
            }
            else if (bet <= 0)
            {
                bet = currentCash < 100 ? (int)currentCash : 100;
            }
            else if (bet > currentCash)
            {
                bet = (int)currentCash;
            }
        }


        var reply = await message.ReplyMessageAsync(StartMessage);

        if (reply is not null)
        {
            var play = new AmusePlay
            {
                UserId = message.AuthorId,
                CreatedAtUtc = DateTime.UtcNow,
                GameKind = GameKind,
                ChannelId = message.ChannelId,
                Bet = bet,
                MessageId = reply.Id,
                Started = false
            };
            DatabaseService.Insert(AmusePlay.TableName, play);
        }
    }
}

