namespace TsDiscordBot.Core.Framework;
public record AttachmentData(string FileName, string ContentType, byte[] Bytes ,int? Width, int? Height);

public interface IMessageData
{
    public string AuthorName { get; }
    public ulong Id { get;  }
    public ulong GuildId { get;  }
    public ulong ChannelId { get;  }
    public ulong AuthorId { get;  }
    public bool IsBot { get;  }
    public bool FromTsumugi { get;  }
    public string Content { get;  }
    public bool IsReplay { get;  }
    public bool FromAdmin { get;  }
    public string? AvatarUrl { get;  }
    public string AuthorMention { get; }
    public string ChannelName { get; }
    public MessageData? ReplaySource { get;  }
    public bool IsDeleted { get; }
    public List<AttachmentData> Attachments { get; }

    public Task<bool> TryAddReactionAsync(string reaction);
    public Task<IMessageData?> SendMessageAsync(string content, string? filePath = null);
    public Task<IMessageData?> ReplyMessageAsync(string content, string? filePath = null);
    public Task<IMessageData?> ModifyMessageAsync(Func<string, string> modify);
    public Task<bool> DeleteAsync();

    public Task CreateAttachmentSourceIfNotCachedAsync();
}
