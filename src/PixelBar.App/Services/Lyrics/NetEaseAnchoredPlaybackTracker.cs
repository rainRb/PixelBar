namespace PixelBar_App.Services.Lyrics;

/// <summary>
/// 无 SMTC 时：桌面歌词换行锚定时间轴 + 内置计时推进；有 SMTC 时直接透传系统进度。
/// </summary>
internal sealed class NetEaseAnchoredPlaybackTracker
{
    private static readonly TimeSpan DesktopFreezeThreshold = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan SmtcStaleThreshold = TimeSpan.FromMilliseconds(250);

    private string? _trackKey;
    private TimeSpan _anchorPosition;
    private DateTime _anchorUtc = DateTime.UtcNow;
    private string? _lastDesktopLine;
    private DateTime _lastDesktopChangeUtc = DateTime.MinValue;
    private TimeSpan _smtcAnchorPosition;
    private DateTime _smtcAnchorUtc = DateTime.UtcNow;
    private TimeSpan _lastReportedSmtcPosition;

    public NetEasePositionMode LastMode { get; private set; } = NetEasePositionMode.AnchoredTimer;

    public void Reset()
    {
        _trackKey = null;
        _lastDesktopLine = null;
        _anchorPosition = TimeSpan.Zero;
        _anchorUtc = DateTime.UtcNow;
        _lastDesktopChangeUtc = DateTime.MinValue;
        _smtcAnchorPosition = TimeSpan.Zero;
        _smtcAnchorUtc = DateTime.UtcNow;
        _lastReportedSmtcPosition = TimeSpan.Zero;
        LastMode = NetEasePositionMode.AnchoredTimer;
    }

    public TimeSpan ResolvePosition(
        MediaPlaybackInfo? playback,
        DesktopLyricSnapshot? desktop,
        LrcDocument? document,
        string trackKey)
    {
        if (playback?.HasTrustedTimeline == true)
        {
            LastMode = NetEasePositionMode.Smtc;
            if (!string.Equals(_trackKey, trackKey, StringComparison.Ordinal))
            {
                _smtcAnchorPosition = playback.Position;
                _lastReportedSmtcPosition = playback.Position;
                _smtcAnchorUtc = DateTime.UtcNow;
            }

            _trackKey = trackKey;

            if (playback.IsPlaying)
                return ResolveExtrapolatedSmtcPosition(playback);

            _smtcAnchorPosition = playback.Position;
            _lastReportedSmtcPosition = playback.Position;
            _smtcAnchorUtc = DateTime.UtcNow;
            return playback.Position;
        }

        LastMode = NetEasePositionMode.AnchoredTimer;

        if (!string.Equals(_trackKey, trackKey, StringComparison.Ordinal))
        {
            _trackKey = trackKey;
            _lastDesktopLine = null;
            InitializeAnchor(document, desktop);
        }

        if (desktop?.Line is { Length: > 0 } desktopLine
            && NetEaseDesktopLyricsReader.IsUsableLyricLine(desktopLine)
            && !string.Equals(_lastDesktopLine, desktopLine, StringComparison.Ordinal))
        {
            _lastDesktopLine = desktopLine;
            _lastDesktopChangeUtc = DateTime.UtcNow;
            if (document is not null
                && LyricMatchHelper.FindMatchingLineEntry(document, desktopLine) is { } matched)
            {
                _anchorPosition = matched.Time;
                _anchorUtc = DateTime.UtcNow;
            }
        }

        var freezeTimer = desktop is not null
            && !string.IsNullOrWhiteSpace(_lastDesktopLine)
            && DateTime.UtcNow - _lastDesktopChangeUtc > DesktopFreezeThreshold;

        if (freezeTimer)
            return _anchorPosition;

        return _anchorPosition + (DateTime.UtcNow - _anchorUtc);
    }

    /// <summary>
    /// SMTC 约 1 秒上报一次进度；两次上报之间用本地时钟外推，避免歌词系统性滞后（QQ / InfLink 均适用）。
    /// </summary>
    private TimeSpan ResolveExtrapolatedSmtcPosition(MediaPlaybackInfo playback)
    {
        var reported = playback.Position;
        if (reported + TimeSpan.FromSeconds(2) < _lastReportedSmtcPosition
            || reported > _lastReportedSmtcPosition + SmtcStaleThreshold)
        {
            _smtcAnchorPosition = reported;
            _lastReportedSmtcPosition = reported;
            _smtcAnchorUtc = DateTime.UtcNow;
            return reported;
        }

        var extrapolated = _smtcAnchorPosition + (DateTime.UtcNow - _smtcAnchorUtc);
        if (playback.Duration > TimeSpan.FromSeconds(1) && extrapolated > playback.Duration)
            extrapolated = playback.Duration;

        return extrapolated;
    }

    private void InitializeAnchor(LrcDocument? document, DesktopLyricSnapshot? desktop)
    {
        if (document is null || document.Lines.Count == 0)
        {
            _anchorPosition = TimeSpan.Zero;
            _anchorUtc = DateTime.UtcNow;
            return;
        }

        if (desktop?.Line is { Length: > 0 } line
            && NetEaseDesktopLyricsReader.IsUsableLyricLine(line)
            && LyricMatchHelper.FindMatchingLineEntry(document, line) is { } matched)
        {
            _anchorPosition = matched.Time;
            _anchorUtc = DateTime.UtcNow;
            _lastDesktopLine = line;
            _lastDesktopChangeUtc = DateTime.UtcNow;
            return;
        }

        _anchorPosition = FindFirstSingableTime(document) ?? document.Lines[0].Time;
        _anchorUtc = DateTime.UtcNow;
    }

    private static TimeSpan? FindFirstSingableTime(LrcDocument document)
    {
        foreach (var line in document.Lines)
        {
            if (!LyricMetadataFilter.IsMetadataLine(line.Text))
                return line.Time;
        }

        return null;
    }
}

internal enum NetEasePositionMode
{
    Smtc,
    AnchoredTimer,
}
