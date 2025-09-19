using TsDiscordBot.Core.Database;
using TsDiscordBot.Discord.Amuse;

namespace TsDiscordBot.Discord.HostedService.Amuse
{
    public static class AmuseDatabaseExtensions
    {
        public static bool AddUserCash(this IDatabaseService databaseService, ulong userId, int difference)
        {
            if (difference is 0)
            {
                return false;
            }

            var cash = databaseService
                .FindAll<AmuseCash>(AmuseCash.TableName)
                .FirstOrDefault(x => x.UserId == userId);

            if (cash is null)
            {
                cash = new AmuseCash
                {
                    Cash = 0,
                    UserId = userId,
                };
                databaseService.Insert(AmuseCash.TableName, cash);
            }

            cash.Cash += difference;
            cash.LastUpdatedAtUtc = DateTime.UtcNow;
            databaseService.Update(AmuseCash.TableName, cash);
            return true;
        }

        public static long GetUserCash(this IDatabaseService databaseService, ulong userId)
        {
            var cash = databaseService
                .FindAll<AmuseCash>(AmuseCash.TableName)
                .FirstOrDefault(x => x.UserId == userId);

            if (cash is null)
            {
                cash = new AmuseCash
                {
                    Cash = 0,
                    UserId = userId,
                    LastUpdatedAtUtc = DateTime.UtcNow,
                };
                databaseService.Insert(AmuseCash.TableName, cash);
            }

            return cash.Cash;
        }

        public static void UpdateGameRecord(this IDatabaseService databaseService, AmusePlay play, bool win)
        {
            var record = databaseService
                .FindAll<AmuseGameRecord>(AmuseGameRecord.TableName)
                .FirstOrDefault(x => x.UserId == play.UserId && x.GameKind == play.GameKind);

            if (record is null)
            {
                record = new AmuseGameRecord
                {
                    UserId = play.UserId,
                    GameKind = play.GameKind,
                    TotalPlays = 0,
                    WinCount = 0
                };
                databaseService.Insert(AmuseGameRecord.TableName, record);
            }

            record.TotalPlays++;
            if (win)
            {
                record.WinCount++;
            }

            databaseService.Update(AmuseGameRecord.TableName, record);
        }

        public static int DetermineReplayBet(this IDatabaseService databaseService, AmusePlay play)
        {
            var cash = databaseService
                .FindAll<AmuseCash>(AmuseCash.TableName)
                .FirstOrDefault(x => x.UserId == play.UserId);

            if (cash is null || cash.Cash < play.Bet)
            {
                return 100;
            }

            return play.Bet;
        }
    }
}