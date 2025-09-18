using TsDiscordBot.Core;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse.BlackJackState
{
    public class BlackJackResultState : IState<BlackJackGame>
    {
        private readonly AmusePlay _play;
        private readonly IDatabaseService _databaseService;
        private readonly EmoteDatabase _emoteDatabase;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private int _nextBet = 100;
        public BlackJackGame Game { get; }

        public BlackJackResultState(
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

        public Task OnEnterAsync()
        {
            _nextBet = _play.Bet;
            var previousBet = _play.Bet;
            var userCash = _databaseService.GetUserCash(_play.UserId);
            if (userCash < previousBet)
            {
                _nextBet = previousBet;
            }

            // Pay bet.
            _databaseService.AddUserCash(_play.UserId, Game.Result?.Payout ?? 0);

            // 1分後自動キャンセル
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                _databaseService.Delete(AmusePlay.TableName, _play.Id);
            });

            return Task.CompletedTask;
        }

        public async Task<IState<BlackJackGame>> GetNextStateAsync(string actionId)
        {
            await _cancellationTokenSource.CancelAsync();
            if (actionId == BlackJackActions.Replay)
            {
                _play.Bet = _nextBet;
                _databaseService.Update(AmusePlay.TableName, _play);

                return new BlackJackInitState(_nextBet, _play, _databaseService, _emoteDatabase);
            }

            if (actionId == BlackJackActions.Quit)
            {
                _databaseService.Delete(AmusePlay.TableName, _play.Id);
                return new BlackJackExitState();
            }

            return this;
        }

        public Task<GameUi> GetGameUiAsync()
        {
            var result = new BlackJackUIBuilder(Game, _emoteDatabase, _play.MessageId)
                .WithFooter($"{_nextBet}GAL円をBETして再選する？")
                .EnableRetryButton()
                .EnableQuitButton()
                .Build();

            return Task.FromResult(result);
        }
    }

    public class BlackJackExitState : IState<BlackJackGame>
    {
        public BlackJackGame Game { get; } = new(0);
        public Task OnEnterAsync()
        {
            return Task.CompletedTask;
        }

        public Task<IState<BlackJackGame>> GetNextStateAsync(string actionId)
        {
            return Task.FromResult<IState<BlackJackGame>>(this);
        }

        public Task<GameUi> GetGameUiAsync()
        {
            return Task.FromResult(new GameUi()
            {
                Content = "また遊んでね！"
            });
        }
    }
}