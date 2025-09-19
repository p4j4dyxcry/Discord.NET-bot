using TsDiscordBot.Core.Messaging;

namespace TsDiscordBot.Core.Game
{
    public record GameUi
    {
        public string Content { get; set; } = string.Empty;
        public MessageComponent[] MessageComponents { get; set; } = [];
        public MessageEmbed[] MessageEmbed { get; set; } = [];
    }

    public interface IGameState
    {
        public Task OnEnterAsync();
        public Task<IGameState> GetNextStateAsync(string actionId);
        public Task<GameUi> GetGameUiAsync();
    }

    public class QuitGameState : IGameState
    {
        public static QuitGameState Default { get; } = new ();

        public Task OnEnterAsync()
        {
            return Task.CompletedTask;
        }

        public Task<IGameState> GetNextStateAsync(string actionId)
        {
            return Task.FromResult<IGameState>(this);
        }

        public virtual Task<GameUi> GetGameUiAsync()
        {
            return Task.FromResult(new GameUi());
        }
    }

    public static class GameMessageUtil
    {
        public static string MakeActionId(string actionIdName, ulong messageId)
        {
            return $"{actionIdName}:{messageId}";
        }
    }
}