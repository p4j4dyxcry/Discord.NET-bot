using System.Text;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Core.Messaging;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState;

public class HighLowDecisionGameState : IGameState
{
    private readonly HighLowGameContext _context;
    private readonly Card _drawnCard;
    private readonly int _streak;
    private readonly int _currentPayout;
    private readonly int _nextPayout;

    public HighLowDecisionGameState(HighLowGameContext context, Card drawnCard)
    {
        _context = context;
        _drawnCard = drawnCard;
        _streak = context.Game.Streak;
        _currentPayout = context.Game.CalculatePayout();
        _nextPayout = context.Game.CalculateNextStreakPayout();
    }

    public Task OnEnterAsync()
    {
        return Task.CompletedTask;
    }

    public Task<GameUi> GetGameUiAsync()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<@{_context.Play.UserId}> さん、");
        builder.AppendLine($"正解！カードは{_context.FormatCard(_drawnCard)}!連勝数: {_streak}");
        builder.AppendLine("続ける？それともやめる？");
        builder.AppendLine($"次のゲーム勝てば{_nextPayout}GAL円貰えるよ！");
        builder.AppendLine($"ここでやめたら{_currentPayout}GAL円になるよ！");

        var components = new[]
        {
            Button(HighLowActions.Continue, "続ける", ButtonStyle.Success),
            Button(HighLowActions.Stop, "やめる", ButtonStyle.Danger)
        };

        return Task.FromResult(new GameUi
        {
            Content = builder.ToString(),
            MessageComponents = components
        });
    }

    public Task<IGameState> GetNextStateAsync(string actionId)
    {
        return actionId switch
        {
            HighLowActions.Continue => Task.FromResult<IGameState>(new HighLowGuessGameState(_context)),
            HighLowActions.Stop => Task.FromResult<IGameState>(HighLowResultGameState.CreateWinFromStop(_context)),
            _ => Task.FromResult<IGameState>(this)
        };
    }

    private MessageComponent Button(string action, string label, ButtonStyle style)
    {
        return new MessageComponent
        {
            Kind = ComponentKind.Button,
            Content = label,
            ButtonStyle = style,
            ActionId = GameMessageUtil.MakeActionId(action, _context.Play.MessageId)
        };
    }
}
