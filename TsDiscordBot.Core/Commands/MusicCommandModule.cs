using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TsDiscordBot.Core;


public class MusicCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audio;
    private readonly ILogger<MusicCommandModule> _logger;
    private readonly IOptions<LavalinkPlayerOptions> _playerOptions;

    public MusicCommandModule(IAudioService audio, ILogger<MusicCommandModule> logger, IOptions<LavalinkPlayerOptions> playerOptions)
    {
        _audio = audio;
        _logger = logger;
        _playerOptions = playerOptions;
    }

    public override async void OnModuleBuilding(InteractionService commandService, ModuleInfo module)
    {
        try
        {
            base.OnModuleBuilding(commandService, module);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", Envs.LAVALINK_SERVER_PASSWORD);
            var s = await client.GetStringAsync("http://lavalink4net.railway.internal:2333/version");
            _logger.LogInformation($"Music bot health check:{s}");
        }
        catch (Exception e)
        {
            _logger.LogError(e,"failed to connect to Lavalink");
        }
    }

    [SlashCommand("llhealth", "Lavalinkの疎通確認")]
    public async Task LavalinkHealthAsync()
    {
        string body = string.Empty;
        try
        {
            await DeferAsync(ephemeral: true);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", Envs.LAVALINK_SERVER_PASSWORD);
            body = await client.GetStringAsync($"{Envs.LAVALINK_BASE_ADDRESS}/version");
            _logger.LogInformation($"Music bot health check:{body}");
        }
        catch (Exception e)
        {
            _logger.LogError(e,"failed to connect to Lavalink");
            await FollowupAsync($"`/version` ng: `{e}`");
        }
        await FollowupAsync($"`/version` ok: `{body}`");
    }


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
            options: _playerOptions);
    }

    [SlashCommand("join", "ボイスチャンネルに参加")]
    public async Task JoinAsync(IVoiceChannel? channel = null)
    {
        try
        {
            await DeferAsync(ephemeral: true);

            channel ??= (Context.User as IGuildUser)?.VoiceChannel;
            if (channel is null)
            {
                await FollowupAsync("先にVCへ入ってください。");
                return;
            }

            await JoinLavalinkPlayerAsync(channel);

            await FollowupAsync($"✅ 参加: {channel.Name}");
        }
        catch(Exception e)
        {
            await TryInternalLeaveAsync();
            _logger.LogError(e,"Failed to Join");
            await FollowupAsync("接続に失敗しました。");
        }
    }

    [SlashCommand("play", "検索 or URL で再生")]
    public async Task PlayAsync([Summary(description: "URL または 検索語")] string query)
    {
        try
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
        catch(Exception e)
        {
            await TryInternalLeaveAsync();
            _logger.LogError(e,"Failed to Play");
            await FollowupAsync("再生に失敗しました。");
        }
    }

    [SlashCommand("pause", "一時停止")]
    public async Task PauseAsync()
    {
        try
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
        catch(Exception e)
        {
            await TryInternalLeaveAsync();
            _logger.LogError(e,"Failed to Play");
            await FollowupAsync("一時停止に失敗しました。");
        }
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

        await TryInternalLeaveAsync();

        await FollowupAsync("👋 退出しました。");
    }

    private async Task TryInternalLeaveAsync()
    {
        try
        {
            var player = await _audio.Players.GetPlayerAsync<LavalinkPlayer>(Context.Guild.Id);

            if (player is null)
            {
                return;
            }

            // 再生中なら止める（失敗しても無視）
            try
            {
                await player.StopAsync();
            }
            catch
            {
                /* noop */
            }

            // v4.0.27: プレイヤーを破棄 = 退出
            if (player is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else if (player is IDisposable disp)
            {
                disp.Dispose();
            }
        }
        catch(Exception e)
        {
            _logger.LogError(e,"Failed to TryInternalLeaveAsync");
        }
    }
}