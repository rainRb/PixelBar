namespace PixelBar_App.Services.Lyrics;

internal sealed record IndexedLyricEntry(
    string SourceKey,
    string Path,
    DateTime Modified,
    string? Title,
    string? Artist,
    LrcDocument Document,
    string? NumericSongId = null);
