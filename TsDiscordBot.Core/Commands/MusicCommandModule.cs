using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;

namespace TsDiscordBot.Core.Commands;

public class MusicCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;

    public MusicCommandModule(IAudioService audioService)
    {
        _audioService = audioService;
    }

    [SlashCommand("play", "Play a track in your voice channel")]
    public async Task PlayAsync(string query)
    {
        var user = Context.User as IGuildUser;
        var voiceChannel = user?.VoiceChannel;
        if (voiceChannel is null)
        {
            await RespondAsync("You must be connected to a voice channel.");
            return;
        }

        var player = _audioService.GetPlayer<LavalinkPlayer>(Context.Guild.Id)
            ?? await _audioService.JoinAsync<LavalinkPlayer>(Context.Guild.Id, voiceChannel.Id);

        var track = await _audioService.GetTrackAsync(query, SearchMode.YouTube);
        if (track is null)
        {
            await RespondAsync("Track not found.");
            return;
        }

        await player.PlayAsync(track);
        await RespondAsync($"Now playing: {track.Title}");
    }
}
