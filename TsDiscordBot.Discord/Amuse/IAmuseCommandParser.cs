namespace TsDiscordBot.Discord.Amuse;

public interface IAmuseCommandParser
{
    IAmuseService? Parse(string content);
}
