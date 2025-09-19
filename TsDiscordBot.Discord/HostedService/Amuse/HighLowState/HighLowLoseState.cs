using System.Text;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Amuse;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState
{
    public class HighLowDecisionLoseState : IGameState
    {
        private readonly HighLowGameContext _context;
        private readonly Card _previousCard;
        private readonly Card _drawnCard;

        public HighLowDecisionLoseState(HighLowGameContext context, Card previousCard, Card drawnCard)
        {
            _context = context;
            _previousCard = previousCard;
            _drawnCard = drawnCard;
        }
        public Task OnEnterAsync()
        {
            _context.UpdateGameRecord(false);
            return Task.CompletedTask;
        }

        public Task<GameUi> GetGameUiAsync()
        {
            HighLowUiBuilder builder = new HighLowUiBuilder(_context.Play.MessageId);

            string header = $"High and Low: Bet[{_context.Game.Bet}]";

            string title = "外れ！ゲーム終了 ";
            StringBuilder description = new StringBuilder();
            description.AppendLine($"{_context.Game.CalculatePayout()} GAL円は没収です!");

            var bet = _context.DetermineReplayBet();
            string footer = $"{bet} GAL円 Betしてもう一度遊ぶ？";

            string? currentCard = _context.EmoteDatabase.FindEmoteByCard(_previousCard, false)?.Url;
            string? nextCard = _context.EmoteDatabase.FindEmoteByCard(_drawnCard, true)?.Url;

            var result = builder
                .WithHeader(header)
                .WithTitle(title)
                .WithDescription(description.ToString())
                .WithFooter(footer)
                .WithCard(currentCard)
                .WithNextCard(nextCard)
                .WithColor(MessageColor.FromRgb(184, 210, 0))
                .EnableRetryButton()
                .Build();

            return Task.FromResult(result);
        }

        public Task<IGameState> GetNextStateAsync(string actionId)
        {
            if (actionId == HighLowActions.Replay)
            {
                var bet = _context.DetermineReplayBet();
                _context.DatabaseService.AddUserCash(_context.Play.UserId, -bet);
                _context.ResetGame(bet);
                _context.DatabaseService.Update(AmusePlay.TableName, _context.Play);

                return Task.FromResult<IGameState>(new HighLowGuessGameState(_context));
            }

            if (actionId == HighLowActions.Quit)
            {
                return Task.FromResult<IGameState>(new QuitGameState());
            }

            return Task.FromResult<IGameState>(this);
        }
    }
}