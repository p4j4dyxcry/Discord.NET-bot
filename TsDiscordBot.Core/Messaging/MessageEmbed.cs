using System.Collections.Generic;

namespace TsDiscordBot.Core.Messaging;

public class MessageEmbed
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public MessageColor? Color { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyList<EmbedField> Fields { get; init; } = System.Array.Empty<EmbedField>();
}
