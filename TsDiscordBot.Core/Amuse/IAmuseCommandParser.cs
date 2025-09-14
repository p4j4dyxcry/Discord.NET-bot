namespace TsDiscordBot.Core.Amuse;

public interface IAmuseCommandParser
{
    IAmuseService? Parse(string content);
}
