using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
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

    [SlashCommand("llhealth", "Lavalinkの疎通確認",runMode:RunMode.Async)]
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

    public async Task<QueuedLavalinkPlayer?> GetPlayerAsync(IDiscordInteraction interaction, bool connectToVoiceChannel = true,
        CancellationToken cancellationToken = default)
    {
        // Check if the user is in a voice channel
        if (interaction.User is not IGuildUser user || user.VoiceChannel == null)
        {
            await interaction.FollowupAsync("You must be in a voice channel to use the music player.", ephemeral: true);
            return null;
        }

        try
        {
            // Get guild and channel information
            ulong guildId = user.Guild.Id;
            ulong voiceChannelId = user.VoiceChannel.Id;
            // Determine channel behavior based on connectToVoiceChannel parameter
            PlayerChannelBehavior channelBehavior = connectToVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None;
            PlayerRetrieveOptions retrieveOptions = new(channelBehavior);

            float defaultVolume = 0.2f;
            // Create player options
            CustomPlayerOptions playerOptions = new()
            {
                DisconnectOnStop = false,
                SelfDeaf = true,
                // Get text channel based on interaction type
                TextChannel = interaction is SocketInteraction socketInteraction
                    ? socketInteraction.Channel as ITextChannel
                    : null,
                DefaultVolume = defaultVolume,
            };
            // Wrap options for DI
            var optionsWrapper = Options.Create(playerOptions);
            // Retrieve or create the player
            PlayerResult<CustomLavaLinkPlayer> result = await _audio.Players
                .RetrieveAsync<CustomLavaLinkPlayer, CustomPlayerOptions>(guildId,
                    voiceChannelId,
                    (properties, token) => ValueTask.FromResult(new CustomLavaLinkPlayer(properties)),
                    optionsWrapper,
                    retrieveOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            // Handle retrieval failures
            if (!result.IsSuccess)
            {
                string errorMessage = result.Status switch
                {
                    PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                    PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected to a voice channel.",
                    _ => "An unknown error occurred while trying to retrieve the player."
                };
                await interaction.FollowupAsync(errorMessage, ephemeral: true);
                return null;
            }

            // Set volume if it's a new player
            if (result.Status == PlayerRetrieveStatus.Success)
            {
                await result.Player.SetVolumeAsync(defaultVolume, cancellationToken);
                _logger.LogInformation($"Created new player for guild {guildId} with volume {defaultVolume * 100:F0}%");
            }

            return result.Player;
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Error getting player: {ex.Message}");
            throw;
        }
    }

    private async ValueTask<LavalinkPlayer> JoinLavalinkPlayerAsync(IVoiceChannel? channel = null)
    {
        channel ??= (Context.User as IGuildUser)?.VoiceChannel;

        if (channel is null)
        {
            throw new Exception("Please join voice channel");
        }

        return await _audio.Players.JoinAsync(
            guildId: Context.Guild.Id,
            voiceChannelId: channel.Id,
            playerFactory: PlayerFactory.Default,
            options: _playerOptions)
            .ConfigureAwait(false);
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

            await GetPlayerAsync(Context.Interaction,true);

            await FollowupAsync($"✅ 参加: {channel.Name}");
        }
        catch(Exception e)
        {
            await TryInternalLeaveAsync();
            _logger.LogError(e,"Failed to Join");
            await FollowupAsync("接続に失敗しました。");
        }
    }

    [SlashCommand("play", "検索 or URL で再生", runMode:RunMode.Async)]
    public async Task PlayAsync([Summary(description: "URL または 検索語")] string query)
    {
        try
        {
            await DeferAsync();

            // プレイヤー取得 or 参加（ユーザーのVCに）
            var vc = (Context.User as IGuildUser)?.VoiceChannel;
            var player = await GetPlayerAsync(Context.Interaction);

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

            await player.PlayAsync(track).ConfigureAwait(false);
            await FollowupAsync($"▶️ **{track.Title}**");
        }
        catch(Exception e)
        {
            await TryInternalLeaveAsync();
            _logger.LogError(e,"Failed to Play");
            await FollowupAsync("再生に失敗しました。");
        }
    }

    [SlashCommand("pause", "一時停止", runMode:RunMode.Async)]
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

    [SlashCommand("resume", "再開",runMode:RunMode.Async)]
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