using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using TsDiscordBot.Core.Services;
using Xunit.Abstractions;

namespace TsDiscordBot.Tests
{
    public class TestDB
    {
        public static DatabaseService Crate(string connectionString = null, ITestOutputHelper? testOutputHelper = null, Action<DatabaseService> setup = null)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["database_path"] = connectionString ?? ":memory:"
            }).Build();

            var db = new DatabaseService(new TestLogger<DatabaseService>(testOutputHelper), config);
            setup?.Invoke(db);

            return db;
        }
    }
}