using TsDiscordBot.Core.Framework;

namespace TsDiscordBot.Core.Amuse;

public class PlayBlackJackService : IAmuseService
{
    private readonly int _bet;

    public PlayBlackJackService(int bet)
    {
        _bet = bet;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        return message.ReplyMessageAsync("test message");
    }
}
