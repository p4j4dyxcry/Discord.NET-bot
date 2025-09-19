using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse.BlackJackState
{
    public class BlackJackResultGameState : IGameState
    {
        private readonly AmusePlay _play;
        private readonly IDatabaseService _databaseService;
        private readonly EmoteDatabase _emoteDatabase;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private int _nextBet = 100;
        public BlackJackGame Game { get; }

        public BlackJackResultGameState(
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
            // Payout.
            _databaseService.AddUserCash(_play.UserId, Game.Result?.Payout ?? 0);

            _nextBet = _play.Bet;
            var previousBet = _play.Bet;
            var userCash = _databaseService.GetUserCash(_play.UserId);
            if (userCash < previousBet)
            {
                _nextBet = 100;
            }

            _databaseService.UpdateGameRecord(_play, Game.Result?.Outcome == GameOutcome.PlayerWin);

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

        public async Task<IGameState> GetNextStateAsync(string actionId)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            if (actionId == BlackJackActions.Replay)
            {
                _play.Bet = _nextBet;
                _databaseService.Update(AmusePlay.TableName, _play);
                _databaseService.AddUserCash(_play.UserId, -_play.Bet);

                return new BlackJackInitGameState(_nextBet, _play, _databaseService, _emoteDatabase);
            }

            if (actionId == BlackJackActions.Quit)
            {
                _databaseService.Delete(AmusePlay.TableName, _play.Id);
                return new QuitGameState();
            }

            return this;
        }

        public Task<GameUi> GetGameUiAsync()
        {
            string title = string.Empty;

            if (Game.Result?.Outcome == GameOutcome.PlayerWin)
            {
                title = $"あなたの勝ち!{Game.Result.Payout}GAL円GET";
            }
            else if (Game.Result?.Outcome == GameOutcome.DealerWin)
            {
                title = $"あなたの負け";
            }
            else if (Game.Result?.Outcome == GameOutcome.Push)
            {
                title = $"引き分け";
            }

            var result = new BlackJackUIBuilder(Game, _emoteDatabase, _play.MessageId)
                .WithTitle(title)
                .WithFooter($"{_nextBet}GAL円をBETして再戦する？")
                .EnableRetryButton()
                .EnableQuitButton()
                .Build();

            return Task.FromResult(result);
        }
    }
}