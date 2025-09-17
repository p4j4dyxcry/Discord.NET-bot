namespace TsDiscordBot.Core.Messaging;

public class MessageSendOptions
{
    public string? Content { get; init; }
    public string? FilePath { get; init; }
    public MessageEmbed? Embed { get; init; }
    public MentionHandling MentionHandling { get; init; } = MentionHandling.Default;
}
