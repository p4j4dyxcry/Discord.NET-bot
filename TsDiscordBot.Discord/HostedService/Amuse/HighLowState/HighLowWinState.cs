using System.Text;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.HighLow;
using TsDiscordBot.Core.Messaging;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState;

public class HighLowWinState : IGameState
{
    private readonly HighLowGameContext _context;
    private readonly Card _previousCard;
    private readonly Card _drawnCard;

    public HighLowWinState(HighLowGameContext context, Card previousCard, Card drawnCard)
    {
        _context = context;
        _previousCard = previousCard;
        _drawnCard = drawnCard;
    }

    public Task OnEnterAsync()
    {
        return Task.CompletedTask;
    }

    public Task<GameUi> GetGameUiAsync()
    {
        HighLowUiBuilder builder = new HighLowUiBuilder(_context.Play.MessageId);

        string header = $"High and Low: Bet[{_context.Game.Bet}]";

        string title = $"正解！ 連勝数 {_context.Game.Streak}";
        StringBuilder description = new StringBuilder();
        description.AppendLine($"次に正解すると {_context.Game.CalculateNextStreakPayout()} GAL円");
        description.AppendLine($"ドロップすると {_context.Game.CalculatePayout()} GAL円");

        string footer = "ハイかローで続行、ドロップで終了";

        string? currentCard = _context.EmoteDatabase.FindEmoteByCard(_previousCard, false)?.Url;
        string? nextCard = _context.EmoteDatabase.FindEmoteByCard(_drawnCard, false)?.Url;

        var result = builder
            .WithHeader(header)
            .WithTitle(title)
            .WithDescription(description.ToString())
            .WithFooter(footer)
            .WithCard(currentCard)
            .WithNextCard(nextCard)
            .WithColor(MessageColor.FromRgb(184, 210, 0))
            .EnableHighLowButton()
            .EnableDropButton()
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
                        new HighLowResultState(
                            _context,
                            previous,
                            result.DrawnCard,
                            _context.Game.CalculatePayout()));
                }

                return Task.FromResult<IGameState>(
                    new HighLowWinState(
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

        if (actionId == HighLowActions.Drop)
        {
            return Task.FromResult<IGameState>(
                new HighLowResultState(
                    _context,
                    _previousCard,
                    _drawnCard,
                    _context.Game.CalculatePayout()));
        }

        return Task.FromResult<IGameState>(this);
    }
}
