using System.Collections.Generic;

namespace TsDiscordBot.Core.Messaging;

public class MessageEmbed
{
    public string? Author { get; init; }

    public string? AuthorAvatarUrl { get; init; }
    public string? Title { get; init; }
    public string? Url { get; init; }
    public string? Description { get; init; }
    public MessageColor? Color { get; init; }
    public string? ImageUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public IReadOnlyList<EmbedField> Fields { get; init; } = System.Array.Empty<EmbedField>();

    public string? Footer { get; init; }
    public string? FootetIconUrl { get; init; }
}
