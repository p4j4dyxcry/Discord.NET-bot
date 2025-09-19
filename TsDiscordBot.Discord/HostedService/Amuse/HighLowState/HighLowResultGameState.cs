using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.HostedService.Amuse;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState;

public class HighLowResultGameState : IGameState
{
    private enum HighLowResultKind
    {
        Win,
        Loss
    }

    private readonly HighLowGameContext _context;
    private readonly HighLowResultKind _resultKind;
    private readonly string _initialContent;
    private readonly bool _combinePreviousContent;
    private readonly int _payout;
    private readonly CancellationTokenSource _timeoutCancellation = new();
    private readonly string _timeoutContent;
    private int _nextBet;

    private HighLowResultGameState(
        HighLowGameContext context,
        HighLowResultKind resultKind,
        string initialContent,
        bool combinePreviousContent,
        int payout)
    {
        _context = context;
        _resultKind = resultKind;
        _initialContent = initialContent.TrimEnd();
        _combinePreviousContent = combinePreviousContent;
        _payout = payout;
        _timeoutContent = BuildTimeoutContent(_initialContent, _combinePreviousContent);
    }

    public static HighLowResultGameState CreateLoss(
        HighLowGameContext context,
        Card previous,
        Card drawn)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<@{context.Play.UserId}> さん、");
        builder.AppendLine($"前のカード: {context.FormatCard(previous)}");
        builder.AppendLine($"引いたカード: {context.FormatCard(drawn)}");
        builder.AppendLine("不正解…あなたの負けです。");

        return new HighLowResultGameState(
            context,
            HighLowResultKind.Loss,
            builder.ToString(),
            true,
            0);
    }

    public static HighLowResultGameState CreateWinFromMax(
        HighLowGameContext context,
        Card previous,
        Card drawn)
    {
        var payout = context.Game.CalculatePayout();

        var builder = new StringBuilder();
        builder.AppendLine($"<@{context.Play.UserId}> さん、");
        builder.AppendLine($"前のカード: {context.FormatCard(previous)}");
        builder.AppendLine($"引いたカード: {context.FormatCard(drawn)}");
        builder.AppendLine("正解！");
        builder.AppendLine($"現在の連勝数: {context.Game.Streak}");
        builder.AppendLine($"{payout}GAL円ゲット！");

        return new HighLowResultGameState(
            context,
            HighLowResultKind.Win,
            builder.ToString(),
            false,
            payout);
    }

    public static HighLowResultGameState CreateWinFromStop(HighLowGameContext context)
    {
        var payout = context.Game.CalculatePayout();

        var builder = new StringBuilder();
        builder.AppendLine($"<@{context.Play.UserId}> さん、");
        builder.AppendLine($"連勝数: {context.Game.Streak}で終了しました。");
        builder.AppendLine($"{payout}GAL円ゲット！");

        return new HighLowResultGameState(
            context,
            HighLowResultKind.Win,
            builder.ToString(),
            false,
            payout);
    }

    public Task OnEnterAsync()
    {
        _nextBet = _context.DetermineReplayBet();
        _context.UpdateGameRecord(_resultKind == HighLowResultKind.Win);

        if (_resultKind == HighLowResultKind.Win && _payout > 0)
        {
            _context.DatabaseService.AddUserCash(_context.Play.UserId, _payout);
        }

        _ = StartTimeoutAsync();

        return Task.CompletedTask;
    }

    public Task<GameUi> GetGameUiAsync()
    {
        var builder = new StringBuilder();

        if (_combinePreviousContent && !string.IsNullOrWhiteSpace(_initialContent))
        {
            builder.AppendLine(_initialContent);
            builder.AppendLine();
        }

        if (_resultKind == HighLowResultKind.Win)
        {
            builder.AppendLine($"はい、{_payout}GAL円だよ！");
        }
        else
        {
            builder.AppendLine("残念ながら今までの賞金は没収だよ！");
        }

        builder.AppendLine();
        builder.AppendLine($"もう一度{_nextBet}GAL円をベットして始めちゃう？");

        var components = new[]
        {
            Button(HighLowActions.Replay, "もう１回", ButtonStyle.Primary),
            Button(HighLowActions.Quit, "やめる", ButtonStyle.Secondary)
        };

        return Task.FromResult(new GameUi
        {
            Content = builder.ToString(),
            MessageComponents = components
        });
    }

    public async Task<IGameState> GetNextStateAsync(string actionId)
    {
        if (actionId == HighLowActions.Replay)
        {
            await CancelTimeoutAsync();

            var bet = _context.DetermineReplayBet();
            _context.DatabaseService.AddUserCash(_context.Play.UserId, -bet);
            _context.ResetGame(bet);
            _context.DatabaseService.Update(AmusePlay.TableName, _context.Play);

            return new HighLowGuessGameState(_context);
        }

        if (actionId == HighLowActions.Quit)
        {
            await CancelTimeoutAsync();
            return new HighLowExitGameState(_timeoutContent);
        }

        return this;
    }

    private async Task CancelTimeoutAsync()
    {
        try
        {
            await _timeoutCancellation.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
        }
        _timeoutCancellation.Dispose();
    }

    private Task StartTimeoutAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), _timeoutCancellation.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (_context.Client.GetChannel(_context.Play.ChannelId) is not IMessageChannel channel)
            {
                return;
            }

            if (await channel.GetMessageAsync(_context.Play.MessageId) is not IUserMessage message)
            {
                return;
            }

            await message.ModifyAsync(msg =>
            {
                msg.Content = _timeoutContent;
                msg.Components = new ComponentBuilder().Build();
            });

            _context.DatabaseService.Delete(AmusePlay.TableName, _context.Play.Id);
        });
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

    private static string BuildTimeoutContent(string initialContent, bool combinePreviousContent)
    {
        if (!combinePreviousContent || string.IsNullOrWhiteSpace(initialContent))
        {
            return "また遊ぼうね！";
        }

        var builder = new StringBuilder();
        builder.AppendLine(initialContent);
        builder.AppendLine("また遊ぼうね！");
        return builder.ToString();
    }
}

public class HighLowExitGameState : QuitGameState
{
    private readonly string _content;

    public HighLowExitGameState(string content)
    {
        _content = string.IsNullOrWhiteSpace(content) ? "また遊ぼうね！" : content;
    }

    public override Task<GameUi> GetGameUiAsync()
    {
        return Task.FromResult(new GameUi
        {
            Content = _content
        });
    }
}
