using TsDiscordBot.Core.Framework;

namespace TsDiscordBot.Core.Amuse;

public interface IAmuseService
{
    Task ExecuteAsync(IMessageData message);
}
