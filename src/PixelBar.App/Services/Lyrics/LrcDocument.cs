namespace PixelBar_App.Services.Lyrics;

public sealed class LrcDocument
{
    public string? Title { get; init; }

    public string? Artist { get; init; }

    public int OffsetMs { get; init; }

    public required IReadOnlyList<LyricLine> Lines { get; init; }

    public string? GetLineAt(TimeSpan position, int userTimingOffsetMs = 0) =>
        GetLineEntryAt(position, userTimingOffsetMs)?.Text;

    public LyricLine? GetLineEntryAt(TimeSpan position, int userTimingOffsetMs = 0)
    {
        if (Lines.Count == 0)
            return null;

        var targetMs = position.TotalMilliseconds - userTimingOffsetMs + OffsetMs;
        var index = -1;
        for (var i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].Time.TotalMilliseconds <= targetMs)
                index = i;
            else
                break;
        }

        return index >= 0 ? Lines[index] : null;
    }

    /// <summary>跳过作词/作曲等元数据行，返回可展示的歌词。</summary>
    public string? GetSingableLineAt(TimeSpan position, int userTimingOffsetMs = 0)
    {
        if (Lines.Count == 0)
            return null;

        var targetMs = position.TotalMilliseconds - userTimingOffsetMs + OffsetMs;
        var index = -1;
        for (var i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].Time.TotalMilliseconds <= targetMs)
                index = i;
            else
                break;
        }

        if (index < 0)
            return FindNextSingableIndex(0) is var first && first >= 0 ? Lines[first].Text : null;

        while (index >= 0 && LyricMetadataFilter.IsMetadataLine(Lines[index].Text))
            index--;

        if (index < 0)
            index = FindNextSingableIndex(0);

        return index >= 0 && index < Lines.Count ? Lines[index].Text : null;
    }

    private int FindNextSingableIndex(int start)
    {
        for (var i = start; i < Lines.Count; i++)
        {
            if (!LyricMetadataFilter.IsMetadataLine(Lines[i].Text))
                return i;
        }

        return -1;
    }
}

internal static class LyricMetadataFilter
{
    private const int MaxSingableLineLength = 72;

    private static readonly string[] NonSingableKeywords =
    [
        "售卖本工具",
        "要求退款",
        "举报商家",
        "BetterNCM",
        "Better NCM",
        "InfLink",
        "本工具",
        "爱好者",
        "如果你是从任何地方",
        "从其它途径购买",
        "从其他途径购买",
        "没办法为你",
        "TempoHub",
        "网易云音乐",
        "Netease Cloud Music",
        "desktop lyric",
        "未经著作权人许可",
        "不得翻唱",
        "翻录或使用",
        "著作权人",
        "制作发行",
    ];

    public static bool IsMetadataLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        text = text.Trim();
        if (text.Length > MaxSingableLineLength)
            return true;

        ReadOnlySpan<string> prefixes =
        [
            "作词", "作曲", "编曲", "制作人", "监制", "混音", "录音", "母带",
            "吉他", "贝斯", "鼓", "键盘", "和声", "统筹", "出品", "SP:",
            "Lyricist", "Composer", "Producer",
        ];

        foreach (var prefix in prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var keyword in NonSingableKeywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (text.Count(c => c is '，' or ',' or '。' or '！' or '？' or '；' or '「' or '」') >= 3)
            return true;

        return false;
    }

    public static IReadOnlyList<LyricLine> FilterSingableLines(IReadOnlyList<LyricLine> lines)
    {
        if (lines.Count == 0)
            return lines;

        var filtered = new List<LyricLine>(lines.Count);
        foreach (var line in lines)
        {
            if (!IsMetadataLine(line.Text))
                filtered.Add(line);
        }

        return filtered;
    }
}
