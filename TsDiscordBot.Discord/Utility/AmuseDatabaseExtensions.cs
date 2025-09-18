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
    }
}