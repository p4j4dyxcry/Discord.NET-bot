using TsDiscordBot.Core.Messaging;

namespace TsDiscordBot.Core.Game
{
    public record GameUi
    {
        public string Content { get; set; } = string.Empty;
        public MessageComponent[] MessageComponents { get; set; } = [];
        public MessageEmbed[] MessageEmbed { get; set; } = [];
    }

    public interface IState<T>
    {
        public T Game { get; }
        public Task OnEnterAsync();
        public Task<IState<T>> GetNextStateAsync(string actionId);
        public Task<GameUi> GetGameUiAsync();
    }

    public static class StateMachineUtil
    {
        public static string MakeActionId(string actionIdName, ulong messageId)
        {
            return $"{actionIdName}:{messageId}";
        }
    }
}