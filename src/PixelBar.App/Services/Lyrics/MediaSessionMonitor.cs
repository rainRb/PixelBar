using Windows.Media.Control;

namespace PixelBar_App.Services.Lyrics;

public sealed class MediaSessionMonitor
{
    private string? _lastSessionKey;
    private TimeSpan _lastPosition;
    private int _stalePositionTicks;
    public Task<MediaPlaybackInfo?> TryGetQqMusicPlaybackAsync(CancellationToken cancellationToken = default) =>
        TryGetPlaybackAsync(IsQqMusic, cancellationToken);

    public Task<MediaPlaybackInfo?> TryGetNetEasePlaybackAsync(CancellationToken cancellationToken = default) =>
        TryGetPlaybackAsync(IsNetEaseCloudMusic, cancellationToken);

    public async Task<MediaPlaybackInfo?> TryGetPlaybackAsync(
        LyricsMusicProvider provider,
        CancellationToken cancellationToken = default) =>
        provider switch
        {
            LyricsMusicProvider.QqMusic => await TryGetQqMusicPlaybackAsync(cancellationToken).ConfigureAwait(false),
            LyricsMusicProvider.NetEaseCloudMusic => await TryGetNetEasePlaybackAsync(cancellationToken).ConfigureAwait(false),
            _ => null,
        };

    private async Task<MediaPlaybackInfo?> TryGetPlaybackAsync(
        Func<string, bool> matcher,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in manager.GetSessions())
            {
                var appId = session.SourceAppUserModelId ?? string.Empty;
                if (!matcher(appId))
                    continue;

                return await ReadSessionAsync(session, appId).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public static bool IsQqMusic(string appUserModelId) =>
        appUserModelId.Contains("QQMusic", StringComparison.OrdinalIgnoreCase)
        || appUserModelId.Contains("QQ音乐", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetEaseCloudMusic(string appUserModelId) =>
        appUserModelId.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase)
        || appUserModelId.Contains("CloudMusic", StringComparison.OrdinalIgnoreCase)
        || appUserModelId.Contains("Netease", StringComparison.OrdinalIgnoreCase)
        || appUserModelId.Contains("NetEase", StringComparison.OrdinalIgnoreCase)
        || appUserModelId.Contains("Orpheus", StringComparison.OrdinalIgnoreCase)
        || appUserModelId.Contains("网易云", StringComparison.OrdinalIgnoreCase);

    private async Task<MediaPlaybackInfo?> ReadSessionAsync(
        GlobalSystemMediaTransportControlsSession session,
        string appId)
    {
        try
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask().ConfigureAwait(false);
            if (properties is null)
                return null;

            var title = string.IsNullOrWhiteSpace(properties.Title) ? "未知歌曲" : properties.Title.Trim();
            var artist = string.IsNullOrWhiteSpace(properties.Artist) ? "未知歌手" : properties.Artist.Trim();
            var netEaseSongId = InfLinkSmtcParser.TryParseSongIdFromSession(
                properties.Genres,
                properties.Title,
                properties.Artist,
                properties.AlbumTitle,
                properties.AlbumArtist);
            var timeline = session.GetTimelineProperties();
            var playback = session.GetPlaybackInfo();
            var isPlaying = playback.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var isInfLink = netEaseSongId is not null || DetectInfLinkTimeline(title, artist, timeline, isPlaying);

            var sessionKey = $"{title}|{artist}";
            if (string.Equals(_lastSessionKey, sessionKey, StringComparison.Ordinal)
                && timeline.Position + TimeSpan.FromSeconds(2) < _lastPosition)
            {
                _stalePositionTicks = 0;
            }

            if (!string.Equals(_lastSessionKey, sessionKey, StringComparison.Ordinal))
                _stalePositionTicks = 0;

            var info = new MediaPlaybackInfo(
                title,
                artist,
                timeline.Position,
                timeline.EndTime,
                isPlaying,
                appId,
                HasTrustedTimeline: isInfLink
                    ? HasInfLinkTrustedTimeline(timeline, playback)
                    : HasTrustedTimeline(title, artist, timeline, playback),
                NetEaseSongId: netEaseSongId,
                IsInfLinkSession: isInfLink);

            _lastSessionKey = sessionKey;
            _lastPosition = timeline.Position;
            return info;
        }
        catch
        {
            return null;
        }
    }

    private bool _infLinkLatched;

    private bool DetectInfLinkTimeline(
        string title,
        string artist,
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline,
        bool isPlaying)
    {
        if (!isPlaying || timeline.EndTime <= TimeSpan.FromSeconds(10))
            return _infLinkLatched;

        var sessionKey = $"{title}|{artist}";
        if (string.Equals(_lastSessionKey, sessionKey, StringComparison.Ordinal)
            && timeline.Position > _lastPosition + TimeSpan.FromMilliseconds(400))
        {
            _infLinkLatched = true;
        }

        if (!string.Equals(_lastSessionKey, sessionKey, StringComparison.Ordinal))
            _infLinkLatched = false;

        return _infLinkLatched;
    }

    private static bool HasInfLinkTrustedTimeline(
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline,
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playback)
    {
        if (playback.PlaybackStatus is not GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            return true;

        return timeline.EndTime > TimeSpan.FromSeconds(1);
    }

    private bool HasTrustedTimeline(
        string title,
        string artist,
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline,
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playback)
    {
        if (playback.PlaybackStatus is not GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            return true;

        if (timeline.EndTime <= TimeSpan.FromSeconds(5))
            return true;

        var position = timeline.Position;
        if (position < TimeSpan.FromSeconds(1))
            return false;

        var sessionKey = $"{title}|{artist}";
        if (string.Equals(_lastSessionKey, sessionKey, StringComparison.Ordinal)
            && position <= _lastPosition + TimeSpan.FromMilliseconds(300))
        {
            _stalePositionTicks++;
        }
        else
        {
            _stalePositionTicks = 0;
        }

        if (_stalePositionTicks >= 4 && position < TimeSpan.FromSeconds(12))
            return false;

        return true;
    }
}
