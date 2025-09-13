namespace TsDiscordBot.Core.Framework;
public record AttachmentData(string FileName, string ContentType, byte[] Bytes ,int? Width, int? Height);

/// <summary>
/// Represents a simplified Discord message with helper methods for replying,
/// editing and deleting.
/// </summary>
public interface IMessageData
{
    public string AuthorName { get; }
    public ulong Id { get; }
    public ulong GuildId { get; }
    public ulong ChannelId { get; }
    public ulong AuthorId { get; }
    public bool IsBot { get; }
    public bool FromTsumugi { get; }
    public bool MentionTsumugi { get; }
    public string Content { get; }
    /// <summary>Whether this message is a reply to another message.</summary>
    public bool IsReply { get; }
    public bool FromAdmin { get; }
    public string? AvatarUrl { get; }
    public string AuthorMention { get; }
    public string ChannelName { get; }
    /// <summary>The source message this message replied to, if any.</summary>
    public MessageData? ReplySource { get; }
    public bool IsDeleted { get; }
    public DateTimeOffset Timestamp { get; }
    public List<AttachmentData> Attachments { get; }

    public Task<bool> TryAddReactionAsync(string reaction);
    public Task<IMessageData?> SendMessageAsyncOnChannel(string content, string? filePath = null);
    public Task<IMessageData?> ReplyMessageAsync(string content, string? filePath = null);
    public Task<IMessageData?> ModifyMessageAsync(Func<string, string> modify);
    public Task<bool> DeleteAsync();

    public Task CreateAttachmentSourceIfNotCachedAsync();
}
