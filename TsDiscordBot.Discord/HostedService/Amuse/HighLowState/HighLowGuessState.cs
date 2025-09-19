using System.Text;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.HighLow;
using TsDiscordBot.Core.Messaging;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState;

public class HighLowGuessGameState : IGameState
{
    private readonly HighLowGameContext _context;

    public HighLowGuessGameState(HighLowGameContext context)
    {
        _context = context;
    }

    public Task OnEnterAsync()
    {
        return Task.CompletedTask;
    }

    public Task<GameUi> GetGameUiAsync()
    {
        HighLowUiBuilder builder = new HighLowUiBuilder(_context.Play.MessageId);

        string header = $"High and Low: Bet[{_context.Game.Bet}]";

        string title = $"カードは{_context.FormatCard(_context.Game.CurrentCard)}より大きい？";
        StringBuilder description = new StringBuilder();
        description.AppendLine($"現在の連勝数 {_context.Game.Streak}");
        description.AppendLine($"正解すると {_context.Game.CalculateNextStreakPayout()} GAL円");

        string footer = "ゲームが進行中";

        string? currentCard = _context.EmoteDatabase.FindEmoteByCard(_context.Game.CurrentCard, false)?.Url;
        string? nextCard = _context.EmoteDatabase.FindEmoteByName("BG", string.Empty)?.Url;

        var result = builder
            .WithHeader(header)
            .WithTitle(title)
            .WithDescription(description.ToString())
            .WithFooter(footer)
            .WithCard(currentCard)
            .WithNextCard(nextCard)
            .WithColor(MessageColor.FromRgb(255,217,0))
            .EnableHighLowButton()
            .Build();

        return Task.FromResult(result);
    }

    public Task<IGameState> GetNextStateAsync(string actionId)
    {
        if (actionId == HighLowActions.High || actionId == HighLowActions.Low)
        {
            var prediction = actionId == HighLowActions.High ? GuessPrediction.High : GuessPrediction.Low;
            var previous = _context.Game.CurrentCard;
            var result = _context.Game.Guess(prediction);

            if (result.Correct)
            {
                if (result.MaxReached)
                {
                    return Task.FromResult<IGameState>(
                        new HighLowResultState(_context,
                            previous,
                            result.DrawnCard,
                            _context.Game.CalculatePayout()));
                }

                return Task.FromResult<IGameState>(
                    new HighLowDecisionWinState(
                        _context,
                        previous,
                        result.DrawnCard));
            }

            return Task.FromResult<IGameState>(
                new HighLowDecisionLoseState(
                    _context,
                    previous,
                    result.DrawnCard));
        }

        return Task.FromResult<IGameState>(this);
    }
}
