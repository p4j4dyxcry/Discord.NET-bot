using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TsDiscordBot.Discord.Services;

public class DatabaseService : IDisposable
{
    private readonly ILogger _logger;
    private readonly LiteDatabase? _litedb;
    private readonly string _databasePath;

    public DatabaseService(ILogger<DatabaseService> logger, IConfiguration configuration)
    {
        _logger = logger;

        string databasePath = Envs.LITEDB_PATH;

        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = configuration["database_path"] ?? "default.db";
        }

        _databasePath = databasePath;

        _logger.LogInformation($"LITEDB_PATH: {Envs.LITEDB_PATH}");
        _logger.LogInformation($"Database path: {_databasePath}");

        try
        {
            _litedb = new LiteDatabase(_databasePath);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to create DB at path: {_databasePath}");
        }
    }
    public void Insert<T>(string tableName, T data)
    {
        if (_litedb is null)
        {
            _logger.LogError($"Failed to insert DB path:{_databasePath} table name:{tableName}");
            return;
        }

        try
        {
            var col = _litedb.GetCollection<T>(tableName);
            _ = col.Insert(data);

        }
        catch(Exception e)
        {
            _logger.LogError(e,$"Failed to insert DB path:{_databasePath} table name:{tableName}");
        }
    }

    public bool Delete(string tableName,int id)
    {
        if (_litedb is null)
        {
            _logger.LogError($"Failed to delete DB path:{_databasePath} tableName:{tableName} id:{id}");
            return false;
        }

        try
        {
            var collection = _litedb.GetCollection(tableName);
            return collection.Delete(id);
        }
        catch(Exception e)
        {
            _logger.LogError(e,$"Failed to delete DB path:{_databasePath} id:{id}");
            return false;
        }
    }

    public void Update<T>(string tableName, T data)
    {
        if (_litedb is null)
        {
            _logger.LogError($"Failed to update DB path:{_databasePath} table name:{tableName}");
            return;
        }

        try
        {
            var col = _litedb.GetCollection<T>(tableName);
            _ = col.Update(data);
        }
        catch (Exception e)
        {
            _logger.LogError(e,$"Failed to update DB path:{_databasePath} table name:{tableName}");
        }
    }

    public IEnumerable<T> FindAll<T>(string tableName)
    {
        if (_litedb is null)
        {
            _logger.LogError($"Failed to find DB path:{_databasePath} table name:{tableName}");
            return ArraySegment<T>.Empty;
        }

        try
        {
            ILiteCollection<T>? col = _litedb.GetCollection<T>(tableName);
            return col?.FindAll() ?? ArraySegment<T>.Empty;
        }
        catch(Exception e)
        {
            _logger.LogError(e,$"Failed to find DB path:{_databasePath} table name:{tableName}");
            return ArraySegment<T>.Empty;
        }
    }

    public virtual Task<IEnumerable<T>> FindAllAsync<T>(string tableName)
    {
        return Task.Run(() => FindAll<T>(tableName));
    }

    public void Dispose()
    {
        _litedb?.Dispose();
    }
}