using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.HostedService;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;
using Xunit;

#nullable enable

namespace TsDiscordBot.Tests;

public class AnonymousRelayServiceTests
{
    private class FakeMessageReceiver : IMessageReceiver
    {
        private Func<IMessageData, Task>? _onReceived;

        public IDisposable OnReceivedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
        {
            _onReceived = onMessageReceived;
            return new DummyDisposable();
        }

        public IDisposable OnEditedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
        {
            return new DummyDisposable();
        }

        public Task PublishAsync(IMessageData message)
        {
            return _onReceived?.Invoke(message) ?? Task.CompletedTask;
        }

        private class DummyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    private class FakeWebHookClient : IWebHookClient
    {
        public IMessageData? LastMessage;
        public string? LastContent;
        public string? LastAuthor;
        public string? LastAvatarUrl;

        public Task<ulong?> RelayMessageAsync(IMessageData message, string? content, string? author = null, string? avatarUrl = null)
        {
            LastMessage = message;
            LastContent = content;
            LastAuthor = author;
            LastAvatarUrl = avatarUrl;
            return Task.FromResult<ulong?>(1);
        }
    }

    private class FakeWebHookService : IWebHookService
    {
        public readonly FakeWebHookClient Client = new();

        public Task<IWebHookClient> GetOrCreateWebhookClientAsync(ulong textChannelId, string name)
        {
            return Task.FromResult<IWebHookClient>(Client);
        }
    }

    private class FakeMessageData : IMessageData
    {
        public string AuthorName { get; set; } = string.Empty;
        public ulong Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong AuthorId { get; set; }
        public bool IsBot { get; set; }
        public bool FromTsumugi => false;
        public bool MentionTsumugi => false;
        public string Content { get; set; } = string.Empty;
        public bool IsReply => false;
        public bool FromAdmin => false;
        public string? AvatarUrl { get; set; }
        public string AuthorMention => string.Empty;
        public string ChannelName => string.Empty;
        public MessageData? ReplySource => null;
        public bool IsDeleted { get; private set; }
        public DateTimeOffset Timestamp => DateTimeOffset.Now;
        public List<AttachmentData> Attachments { get; } = new();

        public bool DeleteCalled { get; private set; }

        public Task<bool> TryAddReactionAsync(string reaction) => Task.FromResult(true);
        public Task<IMessageData?> SendMessageAsyncOnChannel(string content, string? filePath = null) => Task.FromResult<IMessageData?>(null);
        public Task<IMessageData?> ReplyMessageAsync(string content, string? filePath = null) => Task.FromResult<IMessageData?>(null);
        public Task<IMessageData?> ModifyMessageAsync(Func<string, string> modify) => Task.FromResult<IMessageData?>(null);
        public Task<bool> DeleteAsync()
        {
            DeleteCalled = true;
            IsDeleted = true;
            return Task.FromResult(true);
        }
        public Task CreateAttachmentSourceIfNotCachedAsync() => Task.CompletedTask;
    }

    private static DatabaseService CreateDatabase(params AnonymousGuildUserSetting[] settings)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        Environment.SetEnvironmentVariable("LITEDB_PATH", path);
        var db = new DatabaseService(NullLogger<DatabaseService>.Instance, new ConfigurationBuilder().Build());
        foreach (var s in settings)
        {
            db.Insert(AnonymousGuildUserSetting.TableName, s);
        }
        return db;
    }

    [Fact]
    public async Task Relays_Message_When_User_Is_Anonymous()
    {
        using var db = CreateDatabase(new AnonymousGuildUserSetting { GuildId = 1, UserId = 2, IsAnonymous = true });
        var receiver = new FakeMessageReceiver();
        var webhooks = new FakeWebHookService();
        var service = new AnonymousRelayService(receiver, webhooks, NullLogger<AnonymousRelayService>.Instance, db);
        await service.StartAsync(CancellationToken.None);

        var message = new FakeMessageData { GuildId = 1, ChannelId = 10, AuthorId = 2, Content = "hello" };
        await receiver.PublishAsync(message);

        Assert.True(message.DeleteCalled);
        Assert.NotNull(webhooks.Client.LastAuthor);
        var profile = AnonymousProfileProvider.GetProfile(message.AuthorId);
        var discriminator = AnonymousProfileProvider.GetDiscriminator(message.AuthorId);
        var expectedBase = UserNameFixLogic.Fix(profile.Name);
        var expectedUser = $"{expectedBase}#{discriminator}";
        Assert.Equal(expectedUser, webhooks.Client.LastAuthor);
        Assert.Equal("hello", webhooks.Client.LastContent);
    }

    [Fact]
    public async Task Does_Not_Relay_When_User_Not_Anonymous()
    {
        using var db = CreateDatabase();
        var receiver = new FakeMessageReceiver();
        var webhooks = new FakeWebHookService();
        var service = new AnonymousRelayService(receiver, webhooks, NullLogger<AnonymousRelayService>.Instance, db);
        await service.StartAsync(CancellationToken.None);

        var message = new FakeMessageData { GuildId = 1, ChannelId = 10, AuthorId = 2, Content = "hello" };
        await receiver.PublishAsync(message);

        Assert.False(message.DeleteCalled);
        Assert.Null(webhooks.Client.LastAuthor);
    }
}

