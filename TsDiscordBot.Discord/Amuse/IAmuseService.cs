using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Framework;

namespace TsDiscordBot.Discord.Amuse;

public interface IAmuseService
{
    Task ExecuteAsync(IMessageData message);
}
