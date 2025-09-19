using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse.BlackJackState
{

    public class BlackJackInitGameState : IGameState
    {
        public BlackJackGame Game { get; }

        private readonly int _bet;
        private readonly AmusePlay _play;
        private readonly IDatabaseService _databaseService;
        private readonly EmoteDatabase _emoteDatabase;
        private BlackJackInProgressGameState _innerGameState;
        public BlackJackInitGameState(int bet, AmusePlay play,
            IDatabaseService databaseService,
            EmoteDatabase emoteDatabase)
        {
            _bet = bet;
            _play = play;
            _databaseService = databaseService;
            _emoteDatabase = emoteDatabase;
            Game = new BlackJackGame(bet);
            _innerGameState = new BlackJackInProgressGameState(Game, play, _databaseService, emoteDatabase);
        }

        public Task<IGameState> GetNextStateAsync(string actionId)
        {
            if (actionId == BlackJackActions.DoubleDown)
            {
                if (_databaseService.AddUserCash(_play.UserId, -_bet))
                {
                    Game.DoubleDown();
                }
            }

            return _innerGameState.GetNextStateAsync(actionId);
        }

        public Task<GameUi> GetGameUiAsync()
        {
            var builder = new BlackJackUIBuilder(Game, _emoteDatabase, _play.MessageId);

            if (Game.CanHit)
            {
                builder.EnableHitButton();
            }

            builder.EnableStandButton();

            if (CanDoubleDown())
            {
                builder.EnableDoubleDownButton();
            }

            return Task.FromResult(builder.Build());
        }

        private bool CanDoubleDown()
        {
            if (Game is not
                {
                    DoubleDowned: false,
                    PlayerCards.Count: 2
                })
            {
                return false;
            }

            long cash = _databaseService.GetUserCash(_play.UserId);

            if (cash >= _bet)
            {
                return true;
            }

            return false;
        }

        public Task OnEnterAsync()
        {
            return Task.CompletedTask;
        }
    }
}