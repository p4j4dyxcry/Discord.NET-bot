using System.Text;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Amuse;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState;

public class HighLowResultState : IGameState
{
    private readonly HighLowGameContext _context;
    private readonly Card _previousCard;
    private readonly Card _drawnCard;
    private readonly int _payOut;

    public HighLowResultState(HighLowGameContext context, Card previousCard, Card drawnCard, int payOut)
    {
        _context = context;
        _previousCard = previousCard;
        _drawnCard = drawnCard;
        _payOut = payOut;
    }

    public Task OnEnterAsync()
    {
        _context.DatabaseService.UpdateGameRecord(_context.Play, true);

        _context.DatabaseService.AddUserCash(_context.Play.UserId, _payOut);

        return Task.CompletedTask;
    }

    public Task<GameUi> GetGameUiAsync()
    {
        HighLowUiBuilder builder = new HighLowUiBuilder(_context.Play.MessageId);

        string header = $"High and Low: Bet[{_context.Game.Bet}]";

        string title = "ゲーム終了！";
        StringBuilder description = new StringBuilder();
        description.AppendLine($"連勝数 {_context.Game.Streak}");
        description.AppendLine($"{_payOut} GAL円 ゲット!");

        var bet = _context.DatabaseService.DetermineReplayBet(_context.Play);
        string footer = $"{bet} GAL円 Betしてもう一度遊ぶ？";

        string? currentCard = _context.EmoteDatabase.FindEmoteByCard(_previousCard, false)?.Url;
        string? nextCard = _context.EmoteDatabase.FindEmoteByCard(_drawnCard, false)?.Url;

        var result = builder
            .WithHeader(header)
            .WithTitle(title)
            .WithDescription(description.ToString())
            .WithFooter(footer)
            .WithCard(currentCard)
            .WithNextCard(nextCard)
            .WithColor(MessageColor.FromRgb(219, 79, 46))
            .EnableRetryButton()
            .Build();

        return Task.FromResult(result);
    }

    public Task<IGameState> GetNextStateAsync(string actionId)
    {
        if (actionId == HighLowActions.Replay)
        {
            var bet = _context.DatabaseService.DetermineReplayBet(_context.Play);
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
