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
        var builder = new StringBuilder();
        builder.AppendLine($"<@{_context.Play.UserId}> さん、");
        builder.AppendLine($"{_context.Game.CalculateNextStreakPayout()}GAL円 賭けて勝負だよ！！");
        builder.AppendLine($"現在のカード: {_context.FormatCard(_context.Game.CurrentCard)}");
        builder.AppendLine($"現在の連勝数: {_context.Game.Streak}");
        builder.AppendLine("次のカードはハイ？ロー？");

        var components = new[]
        {
            CreateButton(HighLowActions.High, "ハイ", ButtonStyle.Primary),
            CreateButton(HighLowActions.Low, "ロー", ButtonStyle.Primary)
        };

        return Task.FromResult(new GameUi
        {
            Content = builder.ToString(),
            MessageComponents = components
        });
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
                        HighLowResultGameState.CreateWinFromMax(
                            _context,
                            previous,
                            result.DrawnCard));
                }

                return Task.FromResult<IGameState>(
                    new HighLowDecisionGameState(
                        _context,
                        result.DrawnCard));
            }

            return Task.FromResult<IGameState>(
                HighLowResultGameState.CreateLoss(
                    _context,
                    previous,
                    result.DrawnCard));
        }

        return Task.FromResult<IGameState>(this);
    }

    private MessageComponent CreateButton(string action, string label, ButtonStyle style)
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
