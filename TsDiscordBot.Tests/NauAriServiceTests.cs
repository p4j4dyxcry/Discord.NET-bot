using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.HostedService;
using Xunit;

#nullable enable

namespace TsDiscordBot.Tests;

public class NauAriServiceTests
{
    private class TestMessageReceiver : IMessageReceiver
    {
        public Func<IMessageData, Task>? Handler { get; private set; }
        public TestDisposable? Subscription { get; private set; }

        public IDisposable OnReceivedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
        {
            Handler = onMessageReceived;
            Subscription = new TestDisposable();
            return Subscription;
        }

        public IDisposable OnEditedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
            => new TestDisposable();

        public class TestDisposable : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }
    }

    private class TestMessage : IMessageData
    {
        public string AuthorName { get; set; } = string.Empty;
        public ulong Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong AuthorId { get; set; }
        public bool IsBot { get; set; }
        public bool FromTsumugi { get; set; }
        public bool MentionTsumugi { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsReply { get; set; }
        public bool FromAdmin { get; set; }
        public string? AvatarUrl { get; set; }
        public string AuthorMention { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public MessageData? ReplySource { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public List<AttachmentData> Attachments { get; set; } = new();

        public string? SentMessage { get; private set; }

        public Task<bool> TryAddReactionAsync(string reaction) => Task.FromResult(false);
        public Task<IMessageData?> SendMessageAsyncOnChannel(string content, string? filePath = null)
        {
            SentMessage = content;
            return Task.FromResult<IMessageData?>(null);
        }
        public Task<IMessageData?> SendMessageAsyncOnChannel(Embed embed, AllowedMentions? allowedMentions = null)
            => Task.FromResult<IMessageData?>(null);
        public Task<IMessageData?> ReplyMessageAsync(string content, string? filePath = null) => Task.FromResult<IMessageData?>(null);
        public Task<IMessageData?> ReplyMessageAsync(Embed embed, AllowedMentions? allowedMentions = null)
            => Task.FromResult<IMessageData?>(null);
        public Task<IMessageData?> ModifyMessageAsync(Func<string, string> modify) => Task.FromResult<IMessageData?>(null);
        public Task<bool> DeleteAsync() => Task.FromResult(false);
        public Task CreateAttachmentSourceIfNotCachedAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task OnMessageReceived_NauMessage_SendsResponse()
    {
        var receiver = new TestMessageReceiver();
        var service = new NauAriService(receiver, NullLogger<NauAriService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var message = new TestMessage { Content = "なう(2024/04/01", IsBot = false };
        await receiver.Handler!(message);

        Assert.Equal("なうあり！", message.SentMessage);
    }

    [Fact]
    public async Task OnMessageReceived_BotMessage_DoesNotRespond()
    {
        var receiver = new TestMessageReceiver();
        var service = new NauAriService(receiver, NullLogger<NauAriService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var message = new TestMessage { Content = "なう(2024/04/01", IsBot = true };
        await receiver.Handler!(message);

        Assert.Null(message.SentMessage);
    }

    [Fact]
    public async Task OnMessageReceived_NonNauMessage_DoesNotRespond()
    {
        var receiver = new TestMessageReceiver();
        var service = new NauAriService(receiver, NullLogger<NauAriService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var message = new TestMessage { Content = "hello", IsBot = false };
        await receiver.Handler!(message);

        Assert.Null(message.SentMessage);
    }

    [Fact]
    public async Task StopAsync_DisposesSubscription()
    {
        var receiver = new TestMessageReceiver();
        var service = new NauAriService(receiver, NullLogger<NauAriService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.True(receiver.Subscription?.Disposed);
    }
}

