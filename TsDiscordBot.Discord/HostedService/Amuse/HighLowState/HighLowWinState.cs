using System.Text;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Messaging;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState;

public class HighLowDecisionWinState : IGameState
{
    private readonly HighLowGameContext _context;
    private readonly Card _previousCard;
    private readonly Card _drawnCard;

    public HighLowDecisionWinState(HighLowGameContext context, Card previousCard, Card drawnCard)
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
        description.AppendLine($"ここでやめると {_context.Game.CalculatePayout()} GAL円");

        string footer = "ゲームを続行する？";

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
            .EnableContinueButton()
            .Build();

        return Task.FromResult(result);
    }

    public Task<IGameState> GetNextStateAsync(string actionId)
    {
        return actionId switch
        {
            HighLowActions.Continue => Task.FromResult<IGameState>(new HighLowGuessGameState(_context)),
            HighLowActions.Stop => Task.FromResult<IGameState>(new HighLowResultState(_context,_previousCard,_drawnCard, _context.Game.CalculatePayout())),
            _ => Task.FromResult<IGameState>(this)
        };
    }
}
