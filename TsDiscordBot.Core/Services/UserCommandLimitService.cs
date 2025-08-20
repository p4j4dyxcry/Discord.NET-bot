using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;

namespace TsDiscordBot.Core.Services;

public interface IUserCommandLimitService
{
    /// <summary>
    /// Try to register a command execution for a user. Returns false if the user has exceeded the limit.
    /// </summary>
    bool TryAdd(ulong userId, string commandName, int limit = 3, TimeSpan? interval = null);
}

public class UserCommandLimitService : IUserCommandLimitService
{
    private readonly DatabaseService _db;
    private readonly ILogger _logger;
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);
    private const int DefaultLimit = 3;

    public UserCommandLimitService(DatabaseService db, ILogger<UserCommandLimitService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public bool TryAdd(ulong userId, string commandName, int limit = DefaultLimit, TimeSpan? interval = null)
    {
        interval ??= DefaultInterval;

        var now = DateTimeOffset.UtcNow;
        var records = _db.FindAll<CommandUsage>(CommandUsage.TableName)
            .Where(x => x.UserId == userId && x.CommandName == commandName)
            .ToList();

        foreach (var record in records)
        {
            if (now - record.UsedAt > interval)
            {
                _db.Delete(CommandUsage.TableName, record.Id);
            }
        }

        var recent = records.Where(r => now - r.UsedAt <= interval).ToList();
        if (recent.Count >= limit)
        {
            return false;
        }

        var usage = new CommandUsage
        {
            UserId = userId,
            CommandName = commandName,
            UsedAt = now,
        };
        try
        {
            _db.Insert(CommandUsage.TableName, usage);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to insert command usage");
            return false;
        }

        return true;
    }
}
