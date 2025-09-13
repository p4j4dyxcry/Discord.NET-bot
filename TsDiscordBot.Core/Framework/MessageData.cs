using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.Framework
{

public class MessageData : IMessageData
{
    private ILogger? Logger = null;

    private const ulong _tsumugiId = 1315441123715579985;
    public static async Task<MessageData> FromIMessageAsync(IMessage message, ILogger? logger = null)
    {
        MessageData? referencedMessage = null;
        if (message.Reference?.MessageId.IsSpecified is true)
        {
            var refMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
            if (refMessage is not null)
            {
                referencedMessage = await FromIMessageAsync(refMessage);
            }
        }

        return new MessageData()
        {
            AuthorId = message.Author.Id,
            Id = message.Id,
            ChannelId = message.Channel.Id,
            GuildId = (message.Channel as SocketGuildChannel)?.Guild.Id ?? 0,
            AuthorName = DiscordUtility.GetAuthorNameFromMessage(message),
            AvatarUrl = DiscordUtility.GetAvatarUrlFromMessage(message),
            Content = message.Content,
            FromAdmin = (message.Author as SocketGuildUser)?.GuildPermissions.Administrator ?? false,
            FromTsumugi = message.Author.Id == _tsumugiId,
            IsReplay = referencedMessage is not null,
            ReplaySource = referencedMessage,
            IsBot = message.Author.IsBot,
            OriginalSocketMessage = message,
            ChannelName = message.Channel.Name,
            AuthorMention = message.Author.Mention,
            MentionTsumugi = message.MentionedUserIds.Contains(_tsumugiId),
            Timestamp = message.Timestamp,
            Logger = logger
        };
    }

    public string AuthorName { get; init; } = string.Empty;
    public ulong Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public ulong AuthorId { get; init; }
    public bool IsBot { get; init; }
    public bool FromTsumugi { get; init; }
    public bool MentionTsumugi { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsReplay { get; init; }
    public bool FromAdmin { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string AuthorMention { get; init; } = string.Empty;
    public string ChannelName { get; init; } = string.Empty;
    public MessageData? ReplaySource { get; private set; }
    public bool IsDeleted { get; private set; }

    public bool CheckedAttachmentIs => _attachments is not null;

    private List<AttachmentData>? _attachments = null;
    public List<AttachmentData> Attachments => _attachments ?? [];

    private IMessage? OriginalSocketMessage { get; init; }

    public async Task<bool> TryAddReactionAsync(string reaction)
    {
        if (IsDeleted)
        {
            return false;
        }

        if (OriginalSocketMessage is null)
        {
            return false;
        }

        try
        {
            if (Emote.TryParse(reaction, out var emote))
            {
                await OriginalSocketMessage.AddReactionAsync(emote);
            }
            else if(Emoji.TryParse(reaction,out var emoji))
            {
                await OriginalSocketMessage.AddReactionAsync(emoji,new RequestOptions());
            }

            return true;
        }
        catch(Exception e)
        {
            Logger?.LogError(e,"Failed to reaction");
        }

        return false;
    }

    public Task<IMessageData?> ReplyMessageAsync(string message, string? filePath = null)
    {
        if (IsDeleted)
        {
            return Task.FromResult<IMessageData?>(null);
        }

        return SendMessageInternalAsync(message, Id, filePath);
    }

    public Task<IMessageData?> SendMessageAsyncOnChannel(string message,string? filePath = null)
    {
        return SendMessageInternalAsync(message, null,filePath);
    }

    public async Task<IMessageData?> ModifyMessageAsync(Func<string,string> modify)
    {
        if (IsDeleted)
        {
            return this;
        }

        if (OriginalSocketMessage is null)
        {
            return this;
        }

        try
        {
            if (OriginalSocketMessage is IUserMessage userMessage)
            {
                await userMessage.ModifyAsync(msg => msg.Content = modify(OriginalSocketMessage.Content));
            }

            return this;
        }
        catch(Exception e)
        {
            Logger?.LogError(e,"Failed to Nauari");
        }

        return this;
    }

    private async Task<IMessageData?> SendMessageInternalAsync(string content, ulong? referenceMessageId, string? filePath = null)
    {
        if (OriginalSocketMessage is null)
        {
            return null;
        }

        try
        {
            MessageReference? reference = null;

            if (referenceMessageId is not null)
            {
                reference = new MessageReference(referenceMessageId);
            }

            IUserMessage? result = null;

            if (filePath is not null)
            {
                result = await OriginalSocketMessage.Channel.SendFileAsync(filePath,content, messageReference:reference);
            }
            else
            {
                result = await OriginalSocketMessage.Channel.SendMessageAsync(content, messageReference:reference);
            }

            return await FromIMessageAsync(result);
        }
        catch(Exception e)
        {
            Logger?.LogError(e,"Failed to Nauari");
        }

        return null;
    }

    public async Task<bool> DeleteAsync()
    {
        if (IsDeleted)
        {
            return false;
        }

        if (OriginalSocketMessage is null)
        {
            return false;
        }

        try
        {
            await OriginalSocketMessage.DeleteAsync();
            IsDeleted = true;
            return true;
        }
        catch(Exception e)
        {
            Logger?.LogError(e, "Failed to delete message");
        }

        return false;
    }

    public async Task CreateAttachmentSourceIfNotCachedAsync()
    {
        if (_attachments is not null)
        {
            return;
        }

        _attachments = new();
        if (OriginalSocketMessage!.Attachments.Any())
        {
            foreach (var a in OriginalSocketMessage.Attachments)
            {
                try
                {
                    var data = await HttpClientStatic.Default.GetByteArrayAsync(a.Url);
                    _attachments.Add(new AttachmentData(a.Filename,a.ContentType, data,a.Width, a.Height));
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to download attachment {Url}", a.Url);
                }
            }
        }
    }
}

}