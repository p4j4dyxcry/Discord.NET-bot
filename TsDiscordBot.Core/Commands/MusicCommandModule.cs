using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Options;


public class MusicCommandModule : InteractionModuleBase<SocketInteractionContext>
{
private readonly IAudioService _audio;

    public MusicCommandModule(IAudioService audio) => _audio = audio;

    private ValueTask<LavalinkPlayer> JoinLavalinkPlayerAsync(IVoiceChannel? channel = null)
    {
        channel ??= (Context.User as IGuildUser)?.VoiceChannel;

        if (channel is null)
        {
            throw new Exception("Please join voice channel");
        }

        return _audio.Players.JoinAsync(
            guildId: Context.Guild.Id,
            voiceChannelId: channel.Id,
            playerFactory: PlayerFactory.Default,
            options: Options.Create(new LavalinkPlayerOptions()));
    }

    [SlashCommand("join", "ボイスチャンネルに参加")]
    public async Task JoinAsync(IVoiceChannel? channel = null)
    {
        await DeferAsync(ephemeral: true);

        channel ??= (Context.User as IGuildUser)?.VoiceChannel;
        if (channel is null)
        {
            await FollowupAsync("先にVCへ入ってください。"); return;
        }

        await JoinLavalinkPlayerAsync(channel);

        await FollowupAsync($"✅ 参加: {channel.Name}");
    }

    [SlashCommand("play", "検索 or URL で再生")]
    public async Task PlayAsync([Summary(description: "URL または 検索語")] string query)
    {
        await DeferAsync();

        // プレイヤー取得 or 参加（ユーザーのVCに）
        var vc = (Context.User as IGuildUser)?.VoiceChannel;
        var player = await _audio.Players.GetPlayerAsync<LavalinkPlayer>(Context.Guild.Id) ??
                     (vc is null
                         ? null
                         : await JoinLavalinkPlayerAsync(vc));

        if (player is null)
        {
            await FollowupAsync("VCに参加できませんでした。先に /join を試してください。"); return;
        }

        // URL判定 → 文字列なら ytsearch:
        bool isUrl = Uri.IsWellFormedUriString(query, UriKind.Absolute);
        string identifier = isUrl ? query : $"ytsearch:{query}";

        var track = await _audio.Tracks.LoadTrackAsync(
            identifier,
            searchMode: isUrl ? TrackSearchMode.None : TrackSearchMode.YouTube);
        if (track is null)
        {
            await FollowupAsync("見つかりませんでした。"); return;
        }

        await player.PlayAsync(track);
        await FollowupAsync($"▶️ **{track.Title}**");
    }

    [SlashCommand("pause", "一時停止")]
    public async Task PauseAsync()
    {
        await DeferAsync(ephemeral: true);
        var player = await _audio.Players.GetPlayerAsync<LavalinkPlayer>(Context.Guild.Id);
        if (player is null)
        {
            await FollowupAsync("プレイヤーがありません。"); return;
        }
        await player.PauseAsync();
        await FollowupAsync("⏸ 一時停止");
    }

    [SlashCommand("resume", "再開")]
    public async Task ResumeAsync()
    {
        await DeferAsync(ephemeral: true);
        var player = await _audio.Players.GetPlayerAsync<LavalinkPlayer>(Context.Guild.Id);
        if (player is null)
        {
            await FollowupAsync("プレイヤーがありません。"); return;
        }
        await player.ResumeAsync();
        await FollowupAsync("▶ 再開");
    }

    [SlashCommand("stop", "停止")]
    public async Task StopAsync()
    {
        await DeferAsync(ephemeral: true);
        var player = await _audio.Players.GetPlayerAsync<LavalinkPlayer>(Context.Guild.Id);
        if (player is null)
        {
            await FollowupAsync("プレイヤーがありません。"); return;
        }
        await player.StopAsync();
        await FollowupAsync("⏹ 停止");
    }

    [SlashCommand("leave", "退出")]
    public async Task LeaveAsync()
    {
        await DeferAsync(ephemeral: true);

        var player = await _audio.Players.GetPlayerAsync<LavalinkPlayer>(Context.Guild.Id);
        if (player is null)
        {
            await FollowupAsync("プレイヤーが見つかりません。");
            return;
        }

        // 再生中なら止める（失敗しても無視）
        try { await player.StopAsync(); } catch { /* noop */ }

        // v4.0.27: プレイヤーを破棄 = 退出
        if (player is IAsyncDisposable ad)
            await ad.DisposeAsync();
        else if(player is IDisposable disp)
            disp.Dispose();

        await FollowupAsync("👋 退出しました。");
    }
}