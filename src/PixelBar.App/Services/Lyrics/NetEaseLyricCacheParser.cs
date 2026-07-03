using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PixelBar_App.Services.Lyrics;

public static partial class YrcParser
{
    [GeneratedRegex(@"^\[(?<start>\d+),(?<duration>\d+)\]")]
    private static partial Regex LineHeaderRegex();

    public static LrcDocument? Parse(string yrcData)
    {
        if (string.IsNullOrWhiteSpace(yrcData))
            return null;

        var lines = new List<LyricLine>();
        foreach (var rawLine in yrcData.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('{'))
            {
                if (TryParseJsonLine(line) is { } jsonLine)
                    lines.Add(jsonLine);
                continue;
            }

            if (TryParseLrcStyleLine(line) is { } lrcStyleLine)
                lines.Add(lrcStyleLine);
        }

        if (lines.Count == 0)
            return null;

        lines.Sort(static (a, b) => a.Time.CompareTo(b.Time));
        return new LrcDocument
        {
            Lines = lines,
        };
    }

    private static LyricLine? TryParseJsonLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("t", out var timeElement))
                return null;

            var startMs = timeElement.GetInt64();
            if (!root.TryGetProperty("c", out var charsElement) || charsElement.ValueKind != JsonValueKind.Array)
                return null;

            var textBuilder = new System.Text.StringBuilder();
            foreach (var charElement in charsElement.EnumerateArray())
            {
                if (charElement.TryGetProperty("tx", out var txElement))
                    textBuilder.Append(txElement.GetString());
            }

            var text = textBuilder.ToString().Trim();
            return text.Length == 0 ? null : new LyricLine(TimeSpan.FromMilliseconds(startMs), text);
        }
        catch
        {
            return null;
        }
    }

    private static LyricLine? TryParseLrcStyleLine(string line)
    {
        var header = LineHeaderRegex().Match(line);
        if (!header.Success)
            return null;

        if (!long.TryParse(header.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startMs))
            return null;

        var content = line[(header.Index + header.Length)..];
        var textBuilder = new System.Text.StringBuilder();
        var index = 0;
        while (index < content.Length)
        {
            if (content[index] != '(')
            {
                index++;
                continue;
            }

            var closeParen = content.IndexOf(')', index);
            if (closeParen < 0)
                break;

            var wordStart = closeParen + 1;
            var wordEnd = wordStart;
            while (wordEnd < content.Length && content[wordEnd] != '(')
                wordEnd++;

            textBuilder.Append(content[wordStart..wordEnd]);
            index = wordEnd;
        }

        var text = textBuilder.ToString().Trim();
        return text.Length == 0 ? null : new LyricLine(TimeSpan.FromMilliseconds(startMs), text);
    }
}

public static partial class NetEaseLyricCacheParser
{
    private static readonly string[] LyricJsonPaths =
    [
        "yrc.lyric",
        "lrc.lyric",
        "lyric",
        "klyric.lyric",
    ];

    [GeneratedRegex(@"""musicId""\s*:\s*(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex MusicIdRegex();

    [GeneratedRegex(@"""songId""\s*:\s*(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SongIdRegex();

    public static LrcDocument? TryParseFile(string path)
    {
        try
        {
            var content = File.ReadAllText(path);
            var songId = TryParseSongIdFromPath(path) ?? TryParseSongIdFromContent(content);
            return TryParseContent(content, songId);
        }
        catch
        {
            return null;
        }
    }

    public static LrcDocument? TryParseContent(string content, string? songId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        content = content.Trim();
        if (content.StartsWith('[') && content.Contains(':', StringComparison.Ordinal))
            return LrcParser.Parse(content);

        foreach (var path in LyricJsonPaths)
        {
            var lyricText = TryExtractLyricField(content, path);
            if (string.IsNullOrWhiteSpace(lyricText))
                continue;

            lyricText = UnescapeLyricText(lyricText);
            var parsed = ParseCombinedLyricText(lyricText);
            if (parsed is not null)
                return FinalizeDocument(parsed, songId);
        }

        if (content.Contains('[') && content.Contains(']', StringComparison.Ordinal))
        {
            var unescaped = UnescapeLyricText(content);
            var fallback = LrcParser.Parse(unescaped);
            if (fallback.Lines.Count > 0)
                return FinalizeDocument(fallback, songId);
        }

        return null;
    }

    public static string? TryParseSongIdFromPath(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        name = Path.GetFileNameWithoutExtension(name);
        if (name.Length > 0 && name.All(char.IsDigit))
            return name;

        if (name.Length == 32 && name.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            return name;

        return null;
    }

    public static string? TryParseNumericSongIdFromContent(string content)
    {
        var match = MusicIdRegex().Match(content);
        if (match.Success)
            return match.Groups["id"].Value;

        match = SongIdRegex().Match(content);
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static string? TryParseSongIdFromContent(string content) =>
        TryParseNumericSongIdFromContent(content);

    private static string? TryExtractLyricField(string content, string dottedPath)
    {
        if (TryExtractFromJson(content, dottedPath) is { } jsonValue)
            return jsonValue;

        var parts = dottedPath.Split('.');
        if (parts.Length == 1)
        {
            var pattern = $@"""{Regex.Escape(parts[0])}""\s*:\s*""((?:\\.|[^""\\])*)""";
            var match = Regex.Match(content, pattern, RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : null;
        }

        if (parts.Length == 2)
        {
            var pattern =
                $@"""{Regex.Escape(parts[0])}""\s*:\s*\{{[^{{}}]*""{Regex.Escape(parts[1])}""\s*:\s*""((?:\\.|[^""\\])*)""";
            var match = Regex.Match(content, pattern, RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : null;
        }

        return null;
    }

    private static string? TryExtractFromJson(string content, string dottedPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var element = doc.RootElement;
            foreach (var segment in dottedPath.Split('.'))
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
                    return null;
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Object when element.TryGetProperty("lyric", out var lyricElement) && lyricElement.ValueKind == JsonValueKind.String
                    => lyricElement.GetString(),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string UnescapeLyricText(string value)
    {
        value = value.Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);

        try
        {
            value = Regex.Unescape(value);
        }
        catch
        {
            // ignore invalid escape sequences
        }

        return WebUtility.HtmlDecode(value);
    }

    private static LrcDocument? ParseCombinedLyricText(string lyricText)
    {
        var yrcDoc = YrcParser.Parse(lyricText);
        var lrcDoc = LrcParser.Parse(lyricText);
        var combined = CombineLyricDocuments(yrcDoc, lrcDoc);
        return combined.Lines.Count > 0 ? combined : null;
    }

    private static LrcDocument CombineLyricDocuments(LrcDocument? primary, LrcDocument? secondary)
    {
        var lines = new List<LyricLine>();
        if (primary?.Lines is { Count: > 0 })
            lines.AddRange(primary.Lines);
        if (secondary?.Lines is { Count: > 0 })
            lines.AddRange(secondary.Lines);

        if (lines.Count == 0)
            return new LrcDocument { Lines = lines };

        lines.Sort(static (a, b) => a.Time.CompareTo(b.Time));

        var deduped = new List<LyricLine>(lines.Count);
        foreach (var line in lines)
        {
            if (deduped.Count > 0
                && deduped[^1].Time == line.Time
                && deduped[^1].Text == line.Text)
            {
                continue;
            }

            deduped.Add(line);
        }

        return new LrcDocument
        {
            Title = primary?.Title ?? secondary?.Title,
            Artist = primary?.Artist ?? secondary?.Artist,
            OffsetMs = primary?.OffsetMs ?? secondary?.OffsetMs ?? 0,
            Lines = deduped,
        };
    }

    private static LrcDocument FinalizeDocument(LrcDocument document, string? songId)
    {
        if (document.Lines.Count == 0)
            return document;

        return new LrcDocument
        {
            Title = document.Title,
            Artist = document.Artist,
            OffsetMs = document.OffsetMs,
            Lines = document.Lines,
        };
    }
}
