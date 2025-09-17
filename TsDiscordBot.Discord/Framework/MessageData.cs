using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Utility;

namespace TsDiscordBot.Discord.Framework
{

/// <summary>
/// Immutable representation of a Discord message with helper methods for
/// replying, editing and managing attachments.
/// </summary>
public class MessageData : IMessageData
{
    private ILogger? Logger = null;

    private const ulong _tsumugiId = 1315441123715579985;

    /// <summary>
    /// Creates a <see cref="MessageData"/> from an <see cref="IMessage"/>.
    /// </summary>
    public static async Task<MessageData> FromIMessageAsync(IMessage message, ILogger? logger = null)
    {
        MessageData? repliedMessage = null;
        if (message.Reference?.MessageId.IsSpecified is true)
        {
            var refMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
            if (refMessage is not null)
            {
                repliedMessage = await FromIMessageAsync(refMessage);
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
            IsReply = repliedMessage is not null,
            ReplySource = repliedMessage,
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
    /// <inheritdoc />
    public bool IsReply { get; init; }
    public bool FromAdmin { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string AuthorMention { get; init; } = string.Empty;
    public string ChannelName { get; init; } = string.Empty;
    /// <inheritdoc />
    public IMessageData? ReplySource { get; private set; }
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

        var options = new MessageSendOptions
        {
            Content = message,
            FilePath = filePath,
        };

        return SendMessageInternalAsync(Id, options);
    }

    public Task<IMessageData?> ReplyMessageAsync(MessageSendOptions options)
    {
        if (IsDeleted)
        {
            return Task.FromResult<IMessageData?>(null);
        }

        return SendMessageInternalAsync(Id, options);
    }

    public Task<IMessageData?> SendMessageAsyncOnChannel(string message, string? filePath = null)
    {
        var options = new MessageSendOptions
        {
            Content = message,
            FilePath = filePath,
        };

        return SendMessageInternalAsync(null, options);
    }

    public Task<IMessageData?> SendMessageAsyncOnChannel(MessageSendOptions options)
    {
        return SendMessageInternalAsync(null, options);
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

    private async Task<IMessageData?> SendMessageInternalAsync(
        ulong? referenceMessageId,
        MessageSendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

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

            var allowedMentions = CreateAllowedMentions(options);
            var embed = BuildEmbed(options);

            if (!string.IsNullOrEmpty(options.FilePath))
            {
                result = await OriginalSocketMessage.Channel.SendFileAsync(
                    options.FilePath,
                    options.Content ?? string.Empty,
                    embed: embed,
                    allowedMentions: allowedMentions,
                    messageReference: reference);
            }
            else
            {
                result = await OriginalSocketMessage.Channel.SendMessageAsync(
                    options.Content,
                    embed: embed,
                    allowedMentions: allowedMentions,
                    messageReference: reference);
            }

            return await FromIMessageAsync(result);
        }
        catch(Exception e)
        {
            Logger?.LogError(e,"Failed to Nauari");
        }

        return null;
    }

    private static AllowedMentions? CreateAllowedMentions(MessageSendOptions options)
    {
        return options.MentionHandling switch
        {
            MentionHandling.SuppressAll => AllowedMentions.None,
            _ => null,
        };
    }

    private static Embed? BuildEmbed(MessageSendOptions options)
    {
        var embedOptions = options.Embed;
        if (embedOptions is null)
        {
            return null;
        }

        var builder = new EmbedBuilder();

        if (!string.IsNullOrWhiteSpace(embedOptions.Title))
        {
            builder.WithTitle(embedOptions.Title);
        }

        if (!string.IsNullOrWhiteSpace(embedOptions.Description))
        {
            builder.WithDescription(embedOptions.Description);
        }

        if (embedOptions.Color is MessageColor color)
        {
            builder.WithColor(new Color(color.R, color.G, color.B));
        }

        if (!string.IsNullOrWhiteSpace(embedOptions.ImageUrl))
        {
            builder.WithImageUrl(embedOptions.ImageUrl);
        }

        if (embedOptions.Fields.Count > 0)
        {
            foreach (var field in embedOptions.Fields)
            {
                builder.AddField(field.Name, field.Value, field.Inline);
            }
        }

        return builder.Build();
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