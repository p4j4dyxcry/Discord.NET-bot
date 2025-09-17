using System;
using System.Reactive.Disposables;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TsDiscordBot.Tests
{
    public class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper? _testOutputHelper;

        public TestLogger(ITestOutputHelper? testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _testOutputHelper?.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return Disposable.Empty;
        }
    }
}