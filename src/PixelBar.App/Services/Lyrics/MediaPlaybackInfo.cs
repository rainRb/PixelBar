namespace PixelBar_App.Services.Lyrics;

public sealed record MediaPlaybackInfo(
    string Title,
    string Artist,
    TimeSpan Position,
    TimeSpan Duration,
    bool IsPlaying,
    string AppId,
    bool HasTrustedTimeline = true,
    string? NetEaseSongId = null,
    bool IsInfLinkSession = false);
