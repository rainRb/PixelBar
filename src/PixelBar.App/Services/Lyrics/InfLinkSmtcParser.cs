using System.Text.RegularExpressions;

namespace PixelBar_App.Services.Lyrics;

internal static partial class InfLinkSmtcParser
{
    [GeneratedRegex(@"NCM-(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SongIdRegex();

    /// <summary>InfLink-rs 将歌曲 ID 写入 SMTC 的 Genre 字段，格式为 NCM-{id}。</summary>
    public static string? TryParseSongId(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : SongIdRegex().Match(value.Trim()) is { Success: true } match
                ? match.Groups["id"].Value
                : null;

    public static bool IsNumericSongId(string? songId) =>
        !string.IsNullOrWhiteSpace(songId) && songId.All(char.IsDigit);

    public static string? TryParseSongIdFromSession(
        IReadOnlyList<string>? genres,
        string? title,
        string? artist,
        string? albumTitle,
        string? albumArtist)
    {
        if (genres is not null)
        {
            foreach (var genre in genres)
            {
                if (TryParseSongId(genre) is { } id)
                    return id;
            }
        }

        return TryParseSongId(title)
            ?? TryParseSongId(artist)
            ?? TryParseSongId(albumTitle)
            ?? TryParseSongId(albumArtist);
    }
}
