using System;
using System.Threading;
using System.Threading.Tasks;

namespace TsDiscordBot.Core.Framework;

public enum ServicePriority
{
    Urgent = 0,
    Default = 1,
    Normal = 2,
    Low = 3,
}

/// <summary>
/// Provides methods to subscribe to Discord message events.
/// </summary>
public interface IMessageReceiver
{
    /// <summary>
    /// Register a callback that is invoked when a new message is received.
    /// </summary>
    /// <param name="onMessageReceived">Handler executed on message arrival.</param>
    /// <param name="condition">Filtering condition to determine execution.</param>
    /// <param name="serviceName">Optional service name for logging.</param>
    /// <param name="priority">Invocation priority among subscribers.</param>
    /// <returns>An <see cref="IDisposable"/> used to cancel the subscription.</returns>
    IDisposable OnReceivedSubscribe(
        Func<IMessageData, CancellationToken, Task> onMessageReceived,
        Func<MessageData, CancellationToken, ValueTask<bool>> condition,
        string serviceName = "",
        ServicePriority priority = ServicePriority.Normal);

    /// <summary>
    /// Register a callback that is invoked when a message is edited.
    /// </summary>
    /// <param name="onMessageReceived">Handler executed on message edit.</param>
    /// <param name="condition">Filtering condition to determine execution.</param>
    /// <param name="serviceName">Optional service name for logging.</param>
    /// <param name="priority">Invocation priority among subscribers.</param>
    /// <returns>An <see cref="IDisposable"/> used to cancel the subscription.</returns>
    IDisposable OnEditedSubscribe(
        Func<IMessageData, CancellationToken, Task> onMessageReceived,
        Func<MessageData, CancellationToken, ValueTask<bool>> condition,
        string serviceName = "",
        ServicePriority priority = ServicePriority.Normal);
}
