using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TsDiscordBot.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace TsDiscordBot.Tests;

public class DatabaseServiceTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public DatabaseServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private const string Table = "test";

    private class TestRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Insert_FindAll_Update_Delete_Work()
    {
        using var service = TestDB.Crate(null,_testOutputHelper);

        var item = new TestRecord { Name = "foo" };
        service.Insert(Table, item);

        var all = service.FindAll<TestRecord>(Table).ToList();
        Assert.Single(all);
        var stored = all[0];
        Assert.Equal("foo", stored.Name);
        Assert.True(stored.Id > 0);

        stored.Name = "bar";
        service.Update(Table, stored);
        var updated = service.FindAll<TestRecord>(Table).Single();
        Assert.Equal("bar", updated.Name);

        Assert.True(service.Delete(Table, updated.Id));
        Assert.Empty(service.FindAll<TestRecord>(Table));
    }
}

