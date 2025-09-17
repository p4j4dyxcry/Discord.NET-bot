namespace TsDiscordBot.Core.Messaging;

public static class MessageConditions
{
    public static Func<IMessageData, CancellationToken, ValueTask<bool>> True =>
        (m, ct) => ValueTask.FromResult(true);

    public static Func<IMessageData, CancellationToken, ValueTask<bool>> NotFromBot =>
        (m, ct) => ValueTask.FromResult(!m.IsBot);

    public static Func<IMessageData, CancellationToken, ValueTask<bool>> NotDeleted =>
        (m, ct) => ValueTask.FromResult(!m.IsDeleted);

    public static Func<IMessageData, CancellationToken, ValueTask<bool>> And(
        this Func<IMessageData, CancellationToken, ValueTask<bool>> first,
        Func<IMessageData, CancellationToken, ValueTask<bool>> second)
    {
        if (first is null) throw new ArgumentNullException(nameof(first));
        if (second is null) throw new ArgumentNullException(nameof(second));
        return async (msg, token) =>
        {
            if (!await first(msg, token).ConfigureAwait(false))
                return false;
            return await second(msg, token).ConfigureAwait(false);
        };
    }
}
