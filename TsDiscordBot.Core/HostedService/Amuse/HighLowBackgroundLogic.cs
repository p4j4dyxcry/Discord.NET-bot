using System.Collections.Concurrent;
using System.Linq;
using Discord;
using Discord.WebSocket;
using TsDiscordBot.Core.Amuse;
using TsDiscordBot.Core.Game.HighLow;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService.Amuse;

public class HighLowBackgroundLogic(DatabaseService databaseService, DiscordSocketClient client) : IAmuseBackgroundLogic
{
    private enum SessionState
    {
        Guess,
        Decision
    }

    private record HighLowSession(AmusePlay Play, HighLowGame Game, SessionState State);

    private readonly ConcurrentDictionary<ulong, HighLowSession> _sessions = new();
    private readonly DatabaseService _databaseService = databaseService;
    private readonly DiscordSocketClient _client = client;

    public async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        if (!component.Data.CustomId.StartsWith("empty_hl_"))
        {
            return;
        }

        var parts = component.Data.CustomId.Split(':');
        if (parts.Length != 2)
        {
            return;
        }

        if (!ulong.TryParse(parts[1], out var messageId))
        {
            return;
        }

        if (!_sessions.TryGetValue(messageId, out var session))
        {
            await component.RespondAsync("ゲームが見つかりません。", ephemeral: true);
            return;
        }

        if (component.User.Id != session.Play.UserId)
        {
            return;
        }

        await component.DeferAsync();

        if (_client.GetChannel(session.Play.ChannelId) is not IMessageChannel channel)
        {
            return;
        }

        if (await channel.GetMessageAsync(session.Play.MessageId) is not IUserMessage userMessage)
        {
            return;
        }

        switch (parts[0])
        {
            case "empty_hl_high":
            case "empty_hl_low":
                if (session.State != SessionState.Guess)
                {
                    return;
                }

                var prediction = parts[0] == "empty_hl_high" ? GuessPrediction.High : GuessPrediction.Low;
                var previous = session.Game.CurrentCard;
                var result = session.Game.Guess(prediction);
                if (result.Correct)
                {
                    if (result.MaxReached)
                    {
                        await ShowResultAsync(userMessage, session.Play, previous, result.DrawnCard, true, false, session.Game.Streak, true, session.Game.CalculatePayout(), session.Game.CalculateNextStreakPayout());
                        FinalizeWin(session);
                    }
                    else
                    {
                        await ShowResultAsync(userMessage, session.Play, previous, result.DrawnCard, true, true, session.Game.Streak, false, session.Game.CalculatePayout(), session.Game.CalculateNextStreakPayout());
                        _sessions[messageId] = session with { State = SessionState.Decision };
                    }
                }
                else
                {
                    await ShowResultAsync(userMessage, session.Play, previous, result.DrawnCard, false, false, session.Game.Streak, false, session.Game.CalculatePayout(), session.Game.CalculateNextStreakPayout());
                    FinalizeLoss(session);
                }

                break;
            case "empty_hl_continue":
                if (session.State != SessionState.Decision)
                {
                    return;
                }

                await ShowGuessAsync(userMessage, session.Play, session.Game);
                _sessions[messageId] = session with { State = SessionState.Guess };
                break;
            case "empty_hl_stop":
                if (session.State != SessionState.Decision)
                {
                    return;
                }

                await ShowStopAsync(userMessage, session.Play, session.Game);
                FinalizeWin(session);
                break;
        }
    }

    public async Task ProcessAsync(AmusePlay[] amusePlays)
    {
        foreach (var play in amusePlays.Where(x => x.GameKind == "HL" && !x.Started))
        {
            if (play.MessageId == 0)
            {
                _databaseService.Delete(AmusePlay.TableName, play.Id);
                continue;
            }

            if (_sessions.ContainsKey(play.MessageId))
            {
                continue;
            }

            if (_client.GetChannel(play.ChannelId) is not IMessageChannel channel)
            {
                continue;
            }

            if (await channel.GetMessageAsync(play.MessageId) is not IUserMessage userMessage)
            {
                continue;
            }

            var game = new HighLowGame(play.Bet);
            _sessions[play.MessageId] = new HighLowSession(play, game, SessionState.Guess);
            play.Started = true;
            _databaseService.Update(AmusePlay.TableName, play);

            var cash = _databaseService
                .FindAll<AmuseCash>(AmuseCash.TableName)
                .First(x => x.UserId == play.UserId);

            cash.Cash -= play.Bet;
            cash.LastUpdatedAtUtc = DateTime.UtcNow;
            _databaseService.Update(AmuseCash.TableName, cash);

            await ShowGuessAsync(userMessage, play, game);
        }
    }

    private async Task ShowGuessAsync(IUserMessage message, AmusePlay play, HighLowGame game)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"<@{play.UserId}> さん、");
        builder.AppendLine($"{game.CalculateNextStreakPayout()}GAL円 賭けて勝負だよ！！");
        builder.AppendLine($"現在のカード: {FormatCard(game.CurrentCard)}");
        builder.AppendLine($"現在の連勝数: {game.Streak}");
        builder.AppendLine("次のカードはハイ？ロー？");

        var components = new ComponentBuilder()
            .WithButton("ハイ", $"empty_hl_high:{play.MessageId}", ButtonStyle.Primary)
            .WithButton("ロー", $"empty_hl_low:{play.MessageId}", ButtonStyle.Primary);

        await message.ModifyAsync(msg =>
        {
            msg.Content = builder.ToString();
            msg.Components = components.Build();
        });
    }

    private async Task ShowResultAsync(IUserMessage message, AmusePlay play, Card previous, Card drawn, bool correct, bool allowContinue, int streak, bool isPayout, int currentPayout, int nextPayout)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"<@{play.UserId}> さん、");
        builder.AppendLine($"前のカード: {FormatCard(previous)}");
        builder.AppendLine($"引いたカード: {FormatCard(drawn)}");

        if (correct)
        {
            builder.AppendLine("正解！");
            builder.AppendLine($"現在の連勝数: {streak}");
            if (isPayout)
            {
                builder.AppendLine($"{currentPayout}GAL円ゲット！");
            }
        }
        else
        {
            builder.AppendLine("不正解…あなたの負けです。");
        }

        var components = new ComponentBuilder();
        if (correct && allowContinue)
        {
            builder.Clear();
            builder.AppendLine($"<@{play.UserId}> さん、");
            builder.AppendLine($"現在の連勝数: {streak}");
            builder.AppendLine($"続ける？それともやめる？: 次のカード{FormatCard(drawn)}");
            builder.AppendLine($"次のゲーム勝てば{nextPayout}GAL円貰えるよ！");
            builder.AppendLine($"ここでやめたら{currentPayout}GAL円になるよ！");
            components.WithButton("続ける", $"empty_hl_continue:{play.MessageId}", ButtonStyle.Success)
                .WithButton("やめる", $"empty_hl_stop:{play.MessageId}", ButtonStyle.Danger);
        }

        await message.ModifyAsync(msg =>
        {
            msg.Content = builder.ToString();
            msg.Components = components.Build();
        });
    }

    private async Task ShowStopAsync(IUserMessage message, AmusePlay play, HighLowGame game)
    {
        var payout = game.CalculatePayout();
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"<@{play.UserId}> さん、");
        builder.AppendLine($"連勝数: {game.Streak}で終了しました。");
        builder.AppendLine($"{payout}GAL円ゲット！");

        await message.ModifyAsync(msg =>
        {
            msg.Content = builder.ToString();
            msg.Components = new ComponentBuilder().Build();
        });
    }

    private void FinalizeWin(HighLowSession session)
    {
        var payout = session.Game.CalculatePayout();
        var cash = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == session.Play.UserId);
        if (cash is not null)
        {
            cash.Cash += payout;
            cash.LastUpdatedAtUtc = DateTime.UtcNow;
            _databaseService.Update(AmuseCash.TableName, cash);
        }

        UpdateGameRecord(session.Play.UserId, session.Play.GameKind, session.Game.Streak > 0);
        _databaseService.Delete(AmusePlay.TableName, session.Play.Id);
        _sessions.TryRemove(session.Play.MessageId, out _);
    }

    private void FinalizeLoss(HighLowSession session)
    {
        UpdateGameRecord(session.Play.UserId, session.Play.GameKind, false);
        _databaseService.Delete(AmusePlay.TableName, session.Play.Id);
        _sessions.TryRemove(session.Play.MessageId, out _);
    }

    private void UpdateGameRecord(ulong userId, string gameKind, bool win)
    {
        var record = _databaseService
            .FindAll<AmuseGameRecord>(AmuseGameRecord.TableName)
            .FirstOrDefault(x => x.UserId == userId && x.GameKind == gameKind);

        if (record is null)
        {
            record = new AmuseGameRecord
            {
                UserId = userId,
                GameKind = gameKind,
                TotalPlays = 0,
                WinCount = 0
            };
            _databaseService.Insert(AmuseGameRecord.TableName, record);
        }

        record.TotalPlays++;
        if (win)
        {
            record.WinCount++;
        }

        _databaseService.Update(AmuseGameRecord.TableName, record);
    }

    private static string FormatCard(Card c)
    {
        var rank = c.Rank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            _ => ((int)c.Rank).ToString()
        };
        var suit = c.Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            _ => "♠"
        };
        return rank + suit;
    }
}