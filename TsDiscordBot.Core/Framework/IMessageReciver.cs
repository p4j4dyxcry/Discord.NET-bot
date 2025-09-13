namespace TsDiscordBot.Core.Framework
{
    public enum ServicePriority
    {
        Urgent = 0,
        Default = 1,
        Normal = 2,
        Low = 3,
    }

    public interface IMessageReceiver
    {
        IDisposable OnReceivedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal);
        IDisposable OnEditedSubscribe(Func<IMessageData, Task> onMessageReceived, string serviceName = "", ServicePriority priority = ServicePriority.Normal);
    }
}