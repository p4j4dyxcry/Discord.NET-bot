using TsDiscordBot.Core.Messaging;

namespace TsDiscordBot.Discord.Framework
{
    public interface IWebHookService
    {
        Task<IWebHookClient> GetOrCreateWebhookClientAsync(ulong textChannelId, string name);
    }

    public interface IWebHookClient
    {
        public Task<ulong?> RelayMessageAsync(IMessageData message, string? content, string? author = null, string? avatarUrl = null);
    }
}