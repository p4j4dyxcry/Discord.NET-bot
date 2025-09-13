using System.Collections.Concurrent;
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

    private record ServiceRegistration(Func<MessageData, Task> Event, string ServiceName, string EventName)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string ServiceName { get; } = ServiceName;
        public string EventName { get; } = EventName;
        public Func<MessageData, Task> Event { get; } = Event;
    }

    private readonly ConcurrentDictionary<ServicePriority, List<ServiceRegistration>> ReceivedServiceList = new();
    private readonly ConcurrentDictionary<ServicePriority, List<ServiceRegistration>> EditedServiceList = new();

    /// <inheritdoc />
    public IDisposable OnReceivedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
    {
        if (!ReceivedServiceList.ContainsKey(priority))
        {
            ReceivedServiceList[priority] = [];
        }
        ServiceRegistration registration = new ServiceRegistration(onMessageReceived,serviceName,"MessageReceived");
        ReceivedServiceList[priority].Add(registration);

        return Disposable.Create(() =>
        {
            ReceivedServiceList[priority].Remove(registration);
        });
    }

    /// <inheritdoc />
    public IDisposable OnEditedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal)
    {
        if (!EditedServiceList.ContainsKey(priority))
        {
            EditedServiceList[priority] = [];
        }
        ServiceRegistration registration = new ServiceRegistration(onMessageReceived,serviceName,"MessageUpdated");
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

                List<ServiceRegistration>[] priorityGroup = ReceivedServiceList.OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToArray();

                foreach (var priority in priorityGroup)
                {
                    foreach (var subscription in priority)
                    {
                        try
                        {
                            await subscription.Event(data);
                        }
                        catch(Exception e)
                        {
                            _logger.LogError(e, $"An exception occured while processing message in {subscription.ServiceName}");
                        }
                    }
                }
            });
        }
        catch(Exception e)
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

                List<ServiceRegistration>[] priorityGroup = EditedServiceList.OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToArray();

                foreach (var priority in priorityGroup)
                {
                    foreach (var subscription in priority)
                    {
                        await subscription.Event(data);
                    }
                }
            });
        }
        catch(Exception e)
        {
            _logger.LogError(e, "An exception occurred while processing a message");
        }
    }
}