namespace TsDiscordBot.Core.Database
{
    public interface IDatabaseService
    {
        public void Insert<T>(string tableName, T data);

        public bool Delete(string tableName, int id);

        public void Update<T>(string tableName, T data);

        public IEnumerable<T> FindAll<T>(string tableName);
    }
}