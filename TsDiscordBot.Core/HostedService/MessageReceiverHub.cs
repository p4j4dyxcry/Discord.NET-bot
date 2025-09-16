using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Disposables;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Framework;

namespace TsDiscordBot.Core.HostedService;

/// <summary>
/// Acts as a central hub that dispatches Discord message events to subscribed
/// services based on their configured priorities.
/// </summary>
public class MessageReceiverHub : IHostedService, IMessageReceiver
{
    private readonly DiscordSocketClient _discord;
    private readonly ILogger<MessageReceiverHub> _logger;

    public MessageReceiverHub(DiscordSocketClient discord, ILogger<MessageReceiverHub> logger)
    {
        _discord = discord;
        _logger = logger;
    }

    private record ServiceRegistration(
        Func<MessageData, CancellationToken, Task> Event,
        Func<MessageData, CancellationToken, ValueTask<bool>> Condition,
        string ServiceName,
        string EventName)
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private readonly ConcurrentDictionary<ServicePriority, List<ServiceRegistration>> ReceivedServiceList = new();
    private readonly ConcurrentDictionary<ServicePriority, List<ServiceRegistration>> EditedServiceList = new();

    /// <inheritdoc />
    public IDisposable OnReceivedSubscribe(
        Func<IMessageData, CancellationToken, Task> onMessageReceived,
        Func<MessageData, CancellationToken, ValueTask<bool>> condition,
        string serviceName = "",
        ServicePriority priority = ServicePriority.Normal)
    {
        if (condition is null) throw new ArgumentNullException(nameof(condition));
        if (!ReceivedServiceList.ContainsKey(priority))
        {
            ReceivedServiceList[priority] = [];
        }
        ServiceRegistration registration = new(
            (m, ct) => onMessageReceived(m, ct),
            condition,
            serviceName,
            "MessageReceived");
        ReceivedServiceList[priority].Add(registration);

        return Disposable.Create(() =>
        {
            ReceivedServiceList[priority].Remove(registration);
        });
    }

    /// <inheritdoc />
    public IDisposable OnEditedSubscribe(
        Func<IMessageData, CancellationToken, Task> onMessageReceived,
        Func<MessageData, CancellationToken, ValueTask<bool>> condition,
        string serviceName = "",
        ServicePriority priority = ServicePriority.Normal)
    {
        if (condition is null) throw new ArgumentNullException(nameof(condition));
        if (!EditedServiceList.ContainsKey(priority))
        {
            EditedServiceList[priority] = [];
        }
        ServiceRegistration registration = new(
            (m, ct) => onMessageReceived(m, ct),
            condition,
            serviceName,
            "MessageUpdated");
        EditedServiceList[priority].Add(registration);

        return Disposable.Create(() =>
        {
            EditedServiceList[priority].Remove(registration);
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _discord.MessageReceived += OnMessageReceivedAsync;
        _discord.MessageUpdated += OnMessageUpdatedAsync;
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _discord.MessageReceived -= OnMessageReceivedAsync;
        _discord.MessageUpdated -= OnMessageUpdatedAsync;
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(SocketMessage arg)
    {
        try
        {
            await Task.Run(async () =>
            {
                MessageData data = await MessageData.FromIMessageAsync(arg);

                var priorityGroup = ReceivedServiceList
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToArray();

                foreach (var priority in priorityGroup)
                {
                    foreach (var subscription in priority.ToArray())
                    {
                        var token = CancellationToken.None;
                        bool shouldRun;
                        try
                        {
                            shouldRun = await subscription.Condition(data, token);
                        }
                        catch (OperationCanceledException ex)
                        {
                            _logger.LogWarning(ex, "Condition cancelled for {Service}", subscription.ServiceName);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Condition error for {Service}", subscription.ServiceName);
                            continue;
                        }

                        if (!shouldRun)
                        {
                            continue;
                        }

                        var sw = Stopwatch.StartNew();
                        try
                        {
                            await subscription.Event(data, token);
                            sw.Stop();
                            _logger.LogInformation(
                                "Executed {Service}/{Event} for {Guild}/{Channel}/{Message} in {Elapsed}ms result=Success",
                                subscription.ServiceName,
                                subscription.EventName,
                                data.GuildId,
                                data.ChannelId,
                                data.Id,
                                sw.ElapsedMilliseconds);
                        }
                        catch (OperationCanceledException ex)
                        {
                            sw.Stop();
                            _logger.LogWarning(
                                ex,
                                "Executed {Service}/{Event} cancelled for {Guild}/{Channel}/{Message} after {Elapsed}ms",
                                subscription.ServiceName,
                                subscription.EventName,
                                data.GuildId,
                                data.ChannelId,
                                data.Id,
                                sw.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            _logger.LogError(
                                ex,
                                "Executed {Service}/{Event} failed for {Guild}/{Channel}/{Message} after {Elapsed}ms",
                                subscription.ServiceName,
                                subscription.EventName,
                                data.GuildId,
                                data.ChannelId,
                                data.Id,
                                sw.ElapsedMilliseconds);
                        }
                    }
                }
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while processing a message");
        }
    }

    private async Task OnMessageUpdatedAsync(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
    {
        try
        {
            await Task.Run(async () =>
            {
                MessageData data = await MessageData.FromIMessageAsync(arg2);

                var priorityGroup = EditedServiceList
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToArray();

                foreach (var priority in priorityGroup)
                {
                    foreach (var subscription in priority.ToArray())
                    {
                        var token = CancellationToken.None;
                        bool shouldRun;
                        try
                        {
                            shouldRun = await subscription.Condition(data, token);
                        }
                        catch (OperationCanceledException ex)
                        {
                            _logger.LogWarning(ex, "Condition cancelled for {Service}", subscription.ServiceName);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Condition error for {Service}", subscription.ServiceName);
                            continue;
                        }

                        if (!shouldRun)
                        {
                            continue;
                        }

                        var sw = Stopwatch.StartNew();
                        try
                        {
                            await subscription.Event(data, token);
                            sw.Stop();
                            _logger.LogInformation(
                                "Executed {Service}/{Event} for {Guild}/{Channel}/{Message} in {Elapsed}ms result=Success",
                                subscription.ServiceName,
                                subscription.EventName,
                                data.GuildId,
                                data.ChannelId,
                                data.Id,
                                sw.ElapsedMilliseconds);
                        }
                        catch (OperationCanceledException ex)
                        {
                            sw.Stop();
                            _logger.LogWarning(
                                ex,
                                "Executed {Service}/{Event} cancelled for {Guild}/{Channel}/{Message} after {Elapsed}ms",
                                subscription.ServiceName,
                                subscription.EventName,
                                data.GuildId,
                                data.ChannelId,
                                data.Id,
                                sw.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            _logger.LogError(
                                ex,
                                "Executed {Service}/{Event} failed for {Guild}/{Channel}/{Message} after {Elapsed}ms",
                                subscription.ServiceName,
                                subscription.EventName,
                                data.GuildId,
                                data.ChannelId,
                                data.Id,
                                sw.ElapsedMilliseconds);
                        }

                    }
                }
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An exception occurred while processing a message");
        }
    }
}