using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
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
    private record ReplaySession(ulong UserId, ulong ChannelId, ulong MessageId, int Bet, string OriginalContent, CancellationTokenSource TimeoutToken);

    private readonly ConcurrentDictionary<ulong, HighLowSession> _sessions = new();
    private readonly ConcurrentDictionary<ulong, ReplaySession> _replaySessions = new();
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

        var action = parts[0];

        if (action is "empty_hl_replay" or "empty_hl_quit")
        {
            if (!_replaySessions.TryGetValue(messageId, out var replay))
            {
                await component.RespondAsync("再戦の情報が見つかりません。", ephemeral: true);
                return;
            }

            if (component.User.Id != replay.UserId)
            {
                return;
            }

            await component.DeferAsync();

            if (_client.GetChannel(replay.ChannelId) is not IMessageChannel replayChannel)
            {
                return;
            }

            if (await replayChannel.GetMessageAsync(replay.MessageId) is not IUserMessage replayMessage)
            {
                return;
            }

            if (action == "empty_hl_replay")
            {
                if (_replaySessions.TryRemove(messageId, out replay))
                {
                    replay.TimeoutToken.Cancel();
                    await StartReplayAsync(replayMessage, replay);
                }
            }
            else
            {
                if (_replaySessions.TryRemove(messageId, out replay))
                {
                    replay.TimeoutToken.Cancel();
                    await CancelReplayAsync(replayMessage, replay, false);
                }
            }

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

        switch (action)
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
                        await FinalizeWinAsync(userMessage, session);
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
                    await FinalizeLossAsync(userMessage, session);
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
                await FinalizeWinAsync(userMessage, session);
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
            builder.AppendLine($"正解！カードは{FormatCard(drawn)}!連勝数: {streak}");
            builder.AppendLine($"続ける？それともやめる？");
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

    private async Task FinalizeWinAsync(IUserMessage message, HighLowSession session)
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

        await ShowReplayPromptAsync(message, session.Play, session.Game.Bet, payout, false);
    }

    private async Task FinalizeLossAsync(IUserMessage message, HighLowSession session)
    {
        UpdateGameRecord(session.Play.UserId, session.Play.GameKind, false);
        _databaseService.Delete(AmusePlay.TableName, session.Play.Id);
        _sessions.TryRemove(session.Play.MessageId, out _);

        await ShowReplayPromptAsync(message, session.Play, session.Game.Bet,0,true);
    }

    private async Task ShowReplayPromptAsync(IUserMessage message, AmusePlay play, int previousBet,int lastPayout,bool combinePreviousContent)
    {
        string originalContent = string.Empty;

        if (combinePreviousContent)
        {
            var latest = await message.Channel.GetMessageAsync(message.Id) as IUserMessage;
            originalContent = latest?.Content ?? message.Content ?? string.Empty;
        }

        var bet = DetermineReplayBet(play.UserId, previousBet);

        if (_replaySessions.TryRemove(play.MessageId, out var existing))
        {
            existing.TimeoutToken.Cancel();
        }

        var builder = new System.Text.StringBuilder(originalContent);
        if (lastPayout > 0)
        {
            builder.AppendLine($"はい、{lastPayout}GAL円だよ！");
        }
        else
        {
            builder.AppendLine("残念ながら今までの賞金は没収だよ！");
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine($"もう一度{bet}GAL円をベットして始めちゃう？");

        var components = new ComponentBuilder()
            .WithButton("もう１回", $"empty_hl_replay:{play.MessageId}", ButtonStyle.Primary)
            .WithButton("やめる", $"empty_hl_quit:{play.MessageId}", ButtonStyle.Secondary);

        await message.ModifyAsync(msg =>
        {
            msg.Content = builder.ToString();
            msg.Components = components.Build();
        });

        var tokenSource = new CancellationTokenSource();
        var replay = new ReplaySession(play.UserId, play.ChannelId, play.MessageId, bet, originalContent, tokenSource);
        _replaySessions[play.MessageId] = replay;
        _ = StartReplayTimeoutAsync(replay);
    }

    private async Task StartReplayAsync(IUserMessage message, ReplaySession replay)
    {
        var createdAt = DateTime.UtcNow;
        var newPlay = new AmusePlay
        {
            UserId = replay.UserId,
            CreatedAtUtc = createdAt,
            GameKind = "HL",
            ChannelId = replay.ChannelId,
            Bet = replay.Bet,
            MessageId = replay.MessageId,
            Started = false
        };

        _databaseService.Insert(AmusePlay.TableName, newPlay);

        var insertedPlay = _databaseService
            .FindAll<AmusePlay>(AmusePlay.TableName)
            .Where(x => x.UserId == replay.UserId && x.MessageId == replay.MessageId && x.GameKind == newPlay.GameKind)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        if (insertedPlay is null)
        {
            await CancelReplayAsync(message, replay, false, "ゲームの再開に失敗しました。");
            return;
        }

        insertedPlay.Started = true;
        _databaseService.Update(AmusePlay.TableName, insertedPlay);

        var cash = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == replay.UserId);

        if (cash is null)
        {
            var newCash = new AmuseCash
            {
                UserId = replay.UserId,
                Cash = 0,
                LastUpdatedAtUtc = DateTime.UtcNow
            };
            _databaseService.Insert(AmuseCash.TableName, newCash);

            cash = _databaseService
                .FindAll<AmuseCash>(AmuseCash.TableName)
                .FirstOrDefault(x => x.UserId == replay.UserId);
        }

        if (cash is not null)
        {
            cash.Cash -= insertedPlay.Bet;
            cash.LastUpdatedAtUtc = DateTime.UtcNow;
            _databaseService.Update(AmuseCash.TableName, cash);
        }

        var game = new HighLowGame(insertedPlay.Bet);
        _sessions[replay.MessageId] = new HighLowSession(insertedPlay, game, SessionState.Guess);

        await ShowGuessAsync(message, insertedPlay, game);
    }

    private async Task CancelReplayAsync(IUserMessage message, ReplaySession replay, bool dueToTimeout, string? additionalLine = null)
    {
        var builder = new System.Text.StringBuilder(replay.OriginalContent);
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.AppendLine();
        }

        var line = additionalLine ?? "また遊ぼうね！";
        builder.AppendLine(line);

        await message.ModifyAsync(msg =>
        {
            msg.Content = builder.ToString();
            msg.Components = new ComponentBuilder().Build();
        });
    }

    private async Task CancelReplayAsync(ReplaySession replay, bool dueToTimeout)
    {
        if (_client.GetChannel(replay.ChannelId) is not IMessageChannel channel)
        {
            return;
        }

        if (await channel.GetMessageAsync(replay.MessageId) is not IUserMessage message)
        {
            return;
        }

        await CancelReplayAsync(message, replay, dueToTimeout);
    }

    private Task StartReplayTimeoutAsync(ReplaySession replay)
    {
        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), replay.TimeoutToken.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (_replaySessions.TryRemove(replay.MessageId, out var removed))
            {
                await CancelReplayAsync(removed, true);
            }
        });
    }

    private int DetermineReplayBet(ulong userId, int previousBet)
    {
        var cash = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == userId);

        if (cash is null || cash.Cash < previousBet)
        {
            return 100;
        }

        return previousBet;
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