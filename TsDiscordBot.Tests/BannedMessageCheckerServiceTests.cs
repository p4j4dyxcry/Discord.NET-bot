using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.HostedService;
using TsDiscordBot.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace TsDiscordBot.Tests;

public class BannedMessageCheckerServiceTests
{
    private readonly ITestOutputHelper _logger;

    public BannedMessageCheckerServiceTests(ITestOutputHelper logger)
    {
        _logger = logger;
    }

    private BannedMessageCheckerService CreateService(DatabaseService db, FakeWebHookService webhook)
    {
        var discordClient = new DiscordSocketClient();
        var receiver = new DummyMessageReceiver();
        var logger = NullLogger<BannedMessageCheckerService>.Instance;
        return new BannedMessageCheckerService(discordClient, receiver, webhook, logger, db);
    }

    [Fact]
    public async Task CheckMessageAsync_BannedWord_DeletesAndRelaysSanitizedMessage()
    {
        using var webhook = new FakeWebHookService();
        using var db = TestDB.Crate(null,_logger,
            database =>
        {
            database.Insert(BannedTriggerWord.TableName, new BannedTriggerWord
            {
                GuildId = 1,
                Word = "bad"
            });
        });

        var service = CreateService(db, webhook);
        var message = new FakeMessage
        {
            GuildId = 1,
            ChannelId = 10,
            Content = "This is bad",
            AuthorName = "user",
            ChannelName = "general"
        };

        var method = typeof(BannedMessageCheckerService).GetMethod("CheckMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(service, new object?[] { message, CancellationToken.None })!;

        Assert.True(message.DeleteCalled);
        Assert.Equal("This is ＊＊＊", webhook.Client.LastContent);
    }

    [Fact]
    public async Task CheckMessageAsync_ExcludedWord_DoesNotDelete()
    {
        using var webhook = new FakeWebHookService();
        using var db = TestDB.Crate(null,_logger,
            database =>
        {
            database.Insert(BannedTriggerWord.TableName, new BannedTriggerWord
            {
                GuildId = 1,
                Word = "bad"
            });
            database.Insert(BannedExcludeWord.TableName, new BannedExcludeWord
            {
                GuildId = 1,
                Word = "bad dog"
            });
        });

        var service = CreateService(db, webhook);
        var message = new FakeMessage
        {
            GuildId = 1,
            ChannelId = 10,
            Content = "bad dog",
            AuthorName = "user",
            ChannelName = "general"
        };

        var method = typeof(BannedMessageCheckerService).GetMethod("CheckMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(service, new object?[] { message, CancellationToken.None })!;

        Assert.False(message.DeleteCalled);
        Assert.Null(webhook.Client.LastContent);
    }

    private class DummyMessageReceiver : IMessageReceiver
    {
        private class DummyDisposable : IDisposable { public void Dispose() { } }
        public IDisposable OnReceivedSubscribe(Func<IMessageData, CancellationToken, Task> onMessageReceived, Func<MessageData, CancellationToken, ValueTask<bool>> condition, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
            => new DummyDisposable();
        public IDisposable OnEditedSubscribe(Func<IMessageData, CancellationToken, Task> onMessageReceived, Func<MessageData, CancellationToken, ValueTask<bool>> condition, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
            => new DummyDisposable();
    }

    private class FakeWebHookService : IWebHookService, IDisposable
    {
        public CapturingWebHookClient Client { get; } = new();
        public Task<IWebHookClient> GetOrCreateWebhookClientAsync(ulong textChannelId, string name)
            => Task.FromResult<IWebHookClient>(Client);
        public void Dispose() { }
        public class CapturingWebHookClient : IWebHookClient
        {
            public string? LastContent { get; private set; }
            public Task<ulong?> RelayMessageAsync(IMessageData message, string? content, string? author = null, string? avatarUrl = null)
            {
                LastContent = content;
                return Task.FromResult<ulong?>(null);
            }
        }
    }

    private class FakeMessage : IMessageData
    {
        public string AuthorName { get; set; } = string.Empty;
        public ulong Id { get; set; } = 1;
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong AuthorId { get; set; } = 123;
        public bool IsBot { get; set; }

        public bool FromTsumugi => false;
        public bool MentionTsumugi => false;
        public string Content { get; set; } = string.Empty;
        public bool IsReply => false;
        public bool FromAdmin => false;
        public string? AvatarUrl => null;
        public string AuthorMention { get; set; } = "@user";
        public string ChannelName { get; set; } = string.Empty;
        public IMessageData? ReplySource => null;
        public bool IsDeleted => DeleteCalled;
        public DateTimeOffset Timestamp => DateTimeOffset.Now;
        public List<AttachmentData> Attachments { get; } = new();
        public bool DeleteCalled { get; private set; }
        public bool SendMessageCalled { get; private set; }
        public Task<bool> TryAddReactionAsync(string reaction) => Task.FromResult(true);
        public Task<IMessageData?> SendMessageAsyncOnChannel(string content, string? filePath = null)
        {
            SendMessageCalled = true;
            return Task.FromResult<IMessageData?>(null);
        }
        public Task<IMessageData?> SendMessageAsyncOnChannel(Embed embed, AllowedMentions? allowedMentions = null)
        {
            SendMessageCalled = true;
            return Task.FromResult<IMessageData?>(null);
        }
        public Task<IMessageData?> ReplyMessageAsync(string content, string? filePath = null) => Task.FromResult<IMessageData?>(null);
        public Task<IMessageData?> ReplyMessageAsync(Embed embed, AllowedMentions? allowedMentions = null) => Task.FromResult<IMessageData?>(null);
        public Task<IMessageData?> ModifyMessageAsync(Func<string, string> modify) => Task.FromResult<IMessageData?>(this);
        public Task<bool> DeleteAsync()
        {
            DeleteCalled = true;
            return Task.FromResult(true);
        }
        public Task CreateAttachmentSourceIfNotCachedAsync() => Task.CompletedTask;
    }
}
