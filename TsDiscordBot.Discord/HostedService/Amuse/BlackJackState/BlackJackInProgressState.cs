using TsDiscordBot.Core;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse.BlackJackState
{
    public class BlackJackInProgressState : IState<BlackJackGame>
    {
        private readonly AmusePlay _play;
        private readonly IDatabaseService _databaseService;
        private readonly EmoteDatabase _emoteDatabase;
        public BlackJackGame Game { get; }
        public BlackJackInProgressState(
            BlackJackGame game,
            AmusePlay play,
            IDatabaseService databaseService,
            EmoteDatabase emoteDatabase)
        {
            _play = play;
            _databaseService = databaseService;
            _emoteDatabase = emoteDatabase;
            Game = game;
        }

        public Task<IState<BlackJackGame>> GetNextStateAsync(string actionId)
        {
            if (actionId == BlackJackActions.Hit)
            {
                Game.Hit();
            }
            else if (actionId == BlackJackActions.Stand)
            {
                Game.Stand();
            }

            if (Game.IsFinished && Game.Result is not null)
            {
                return Task.FromResult<IState<BlackJackGame>>(new BlackJackResultState(Game, _play, _databaseService, _emoteDatabase));
            }
            else
            {
                return Task.FromResult<IState<BlackJackGame>>(this);
            }
        }

        public Task<GameUi> GetGameUiAsync()
        {
            var builder = new BlackJackUIBuilder(Game, _emoteDatabase, _play.MessageId);

            if (Game.CanHit)
            {
                builder.EnableHitButton();
            }

            builder.EnableStandButton();

            return Task.FromResult(builder.Build());
        }

        public Task OnEnterAsync()
        {
            return Task.CompletedTask;
        }
    }
}