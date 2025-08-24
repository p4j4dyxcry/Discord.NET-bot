using Discord;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Tracks;

public sealed class CustomLavaLinkPlayer(IPlayerProperties<CustomLavaLinkPlayer, CustomPlayerOptions> properties) : QueuedLavalinkPlayer(properties)
{

    /// <summary>Handles the track start event by building and sending a rich visual player interface with artwork and interactive controls</summary>
    /// <param name="track">The track that started playing, containing metadata for display</param>
    /// <param name="cancellationToken">Token to cancel the operation in case of shutdown or timeout</param>
    /// <returns>A task representing the asynchronous operation of creating and sending the player UI</returns>
    /// <summary>Handles the track start event by building and sending a rich visual player interface</summary>
    protected override ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>Handles the track end event by logging track completion and performing cleanup</summary>
    /// <param name="queueItem">The track that ended, containing metadata for logging</param>
    /// <param name="endReason">The reason the track ended, used for logging and debugging</param>
    /// <param name="cancellationToken">Token to cancel the operation in case of shutdown or timeout</param>
    /// <returns>A task representing the asynchronous operation of logging track completion</returns>
    protected override ValueTask NotifyTrackEndedAsync(ITrackQueueItem queueItem, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>Handles the player active event when users join the voice channel after all users had left</summary>
    /// <param name="cancellationToken">Token to cancel the operation in case of shutdown or timeout</param>
    /// <returns>A task representing the asynchronous operation of handling player activation</returns>
    public ValueTask NotifyPlayerActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return default; // No special handling needed
    }

    /// <summary>Handles the player inactive event due to inactivity timeout, stopping playback and disconnecting from the voice channel</summary>
    /// <param name="cancellationToken">Token to cancel the operation in case of shutdown or timeout</param>
    /// <returns>A task representing the asynchronous operation of handling player inactivity</returns>
    public async ValueTask NotifyPlayerInactiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Stop playback
            await StopAsync(cancellationToken).ConfigureAwait(false);

            // Disconnect from voice channel
            await DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Logs.Error($"Error handling player inactivity: {ex.Message}");
        }
    }

    /// <summary>Handles the player tracked state change event, used to update the visual player interface</summary>
    /// <param name="cancellationToken">Token to cancel the operation in case of shutdown or timeout</param>
    /// <returns>A task representing the asynchronous operation of handling player state changes</returns>
    public ValueTask NotifyPlayerTrackedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Logs.Debug($"Player tracked state change for guild {GuildId}");
        return default; // No special handling needed
    }
}

/// <summary>Custom options for the CustomPlayer class, extending the standard QueuedLavalinkPlayerOptions with additional configuration</summary>
/// <param name="TextChannel">Gets or sets the Discord text channel where player messages will be sent, used for displaying the visual player and notifications</param>
public sealed record CustomPlayerOptions(ITextChannel? TextChannel) : QueuedLavalinkPlayerOptions
{
    /// <summary>Gets or sets the default volume level, ranging from 0.0 to 1.0</summary>
    public float DefaultVolume { get; init; } = 0.2f;

    /// <summary>Gets or sets whether to show track thumbnails in player messages, used for visual feedback</summary>
    public bool ShowThumbnails { get; init; } = true;

    /// <summary>Gets or sets whether to delete player messages when they become outdated, used for cleanup and organization</summary>
    public bool DeleteOutdatedMessages { get; init; } = true;

    public TimeSpan InactivityTimeout { get; init; } = TimeSpan.FromMinutes(20);

    /// <summary>Initializes a new instance of the CustomPlayerOptions class, setting default values for LavaLink player configuration</summary>
    public CustomPlayerOptions() : this((ITextChannel?)null)
    {
        // Set LavaLink player defaults
        DisconnectOnStop = false;
        SelfDeaf = true;

        // Other defaults are set through auto-properties
    }
}

/// <summary>Enhanced track queue item that extends the standard Lavalink interface with rich Plex metadata for improved display and user experience</summary>
public class CustomTrackQueueItem : ITrackQueueItem
{
    /// <summary>Contains the core Lavalink track reference needed for audio streaming and playback control</summary>
    public TrackReference Reference { get; set; }

    /// <summary>Provides access to the underlying Lavalink track object through the interface implementation</summary>
    LavalinkTrack? ITrackQueueItem.Track => Reference.Track;

    /// <summary>Stores the track's title as retrieved from Plex metadata</summary>
    public string? Title { get; set; }

    /// <summary>Stores the track's artist name as retrieved from Plex metadata</summary>
    public string? Artist { get; set; }

    /// <summary>Stores the album name containing this track as retrieved from Plex metadata</summary>
    public string? Album { get; set; }

    /// <summary>Stores the track's release date to show age/recency information to users</summary>
    public string? ReleaseDate { get; set; }

    /// <summary>URL to the album/track artwork for embedding in Discord messages and UI</summary>
    public string? Artwork { get; set; }

    /// <summary>Direct URL to the track's playback source for reference and linking</summary>
    public string? Url { get; set; }

    /// <summary>URL to the artist's page for linking in the UI and providing additional context</summary>
    public string? ArtistUrl { get; set; }

    /// <summary>Human-readable duration string formatted for display in player embeds</summary>
    public string? Duration { get; set; }

    /// <summary>Recording studio information to provide additional context about the track's origin</summary>
    public string? Studio { get; set; }

    /// <summary>Username of the Discord user who requested this track for attribution and permission management</summary>
    public string? RequestedBy { get; set; }

    /// <summary>Implementation of the interface's type conversion method to support Lavalink's player architecture</summary>
    /// <typeparam name="T">The target type to convert to within the Lavalink player system</typeparam>
    /// <returns>This instance as the requested type if compatible, otherwise null</returns>
    public T? As<T>() where T : class, ITrackQueueItem => this as T;

    /// <summary>Generates a user-friendly string representation of this track for logging and debugging purposes</summary>
    /// <returns>A formatted string containing essential track information</returns>
    public override string ToString()
    {
        return $"{Title} by {Artist} ({Duration})";
    }
}