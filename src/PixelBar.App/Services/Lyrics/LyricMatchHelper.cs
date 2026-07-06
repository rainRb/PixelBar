namespace PixelBar_App.Services.Lyrics;

internal static class LyricMatchHelper
{
    public static int ScoreMatch(IndexedLyricEntry entry, string title, string artist)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(entry.Title) && TextMatch(entry.Title, title))
            score += 60;
        if (!string.IsNullOrWhiteSpace(entry.Artist) && TextMatch(entry.Artist, artist))
            score += 40;
        if (!string.IsNullOrWhiteSpace(entry.Document.Title) && TextMatch(entry.Document.Title, title))
            score += 40;
        if (!string.IsNullOrWhiteSpace(entry.Document.Artist) && TextMatch(entry.Document.Artist, artist))
            score += 30;

        score += ScoreDocumentContent(entry.Document, title, artist);
        return score;
    }

    public static int ScoreDocumentContent(LrcDocument document, string title, string artist)
    {
        var score = 0;
        if (string.IsNullOrWhiteSpace(title))
            return score;

        var normalizedTitle = Normalize(title);
        if (normalizedTitle.Length >= 2 && DocumentContainsText(document, normalizedTitle, exactOnly: normalizedTitle.Length < 4))
            score += 30;

        if (!string.IsNullOrWhiteSpace(artist))
        {
            var normalizedArtist = Normalize(artist);
            if (normalizedArtist.Length >= 2 && DocumentContainsText(document, normalizedArtist, exactOnly: false))
                score += 20;
        }

        return score;
    }

    public static bool DocumentContainsTitle(LrcDocument document, string title) =>
        !string.IsNullOrWhiteSpace(title) && DocumentContainsText(document, Normalize(title), exactOnly: Normalize(title).Length < 4);

    public static bool LinesEquivalent(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && Normalize(left) == Normalize(right);

    public static bool TrackMatches(string? leftTitle, string? leftArtist, string? rightTitle, string? rightArtist)
    {
        if (string.IsNullOrWhiteSpace(leftTitle) || string.IsNullOrWhiteSpace(rightTitle))
            return false;

        if (!TextMatch(leftTitle, rightTitle))
            return false;

        if (string.IsNullOrWhiteSpace(leftArtist) || string.IsNullOrWhiteSpace(rightArtist))
            return true;

        return TextMatch(leftArtist, rightArtist);
    }

    public static bool DocumentMatchesTrack(LrcDocument document, string? title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;

        if (TrackMatches(document.Title, document.Artist, title, artist))
            return true;

        return ScoreDocumentContent(document, title, artist ?? string.Empty) >= 40;
    }

    public static LrcDocument? PickBestMatch(
        IReadOnlyList<IndexedLyricEntry> index,
        string title,
        string artist)
    {
        IndexedLyricEntry? bestEntry = null;
        var bestScore = 0;

        foreach (var entry in index)
        {
            var score = ScoreMatch(entry, title, artist);
            score += VersionConsistencyScore(entry.Title ?? entry.Document.Title, title);
            if (score > bestScore)
            {
                bestScore = score;
                bestEntry = entry;
                continue;
            }

            if (score == bestScore && score >= 60 && bestEntry is not null)
            {
                if (ExactTitleMatch(entry, title) && !ExactTitleMatch(bestEntry, title))
                    bestEntry = entry;
                else if (ExactTitleMatch(entry, title) == ExactTitleMatch(bestEntry, title)
                         && entry.Modified > bestEntry.Modified)
                    bestEntry = entry;
            }
        }

        if (bestScore >= 60)
            return bestEntry?.Document;

        return null;
    }

    private static bool ExactTitleMatch(IndexedLyricEntry entry, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        return TextMatch(entry.Title ?? string.Empty, title)
            || TextMatch(entry.Document.Title ?? string.Empty, title);
    }

    private static int VersionConsistencyScore(string? cachedTitle, string requestedTitle)
    {
        if (string.IsNullOrWhiteSpace(cachedTitle) || string.IsNullOrWhiteSpace(requestedTitle))
            return 0;

        var cachedLive = HasVersionMarker(cachedTitle);
        var requestedLive = HasVersionMarker(requestedTitle);
        if (cachedLive == requestedLive)
            return 0;

        return -30;
    }

    private static bool HasVersionMarker(string title) =>
        title.Contains("live", StringComparison.OrdinalIgnoreCase)
        || title.Contains("现场", StringComparison.OrdinalIgnoreCase)
        || title.Contains("demo", StringComparison.OrdinalIgnoreCase)
        || title.Contains("acoustic", StringComparison.OrdinalIgnoreCase);

    public static string? FindMatchingLine(LrcDocument document, string desktopLine) =>
        FindMatchingLineEntry(document, desktopLine)?.Text;

    public static LyricLine? FindMatchingLineEntry(LrcDocument document, string desktopLine)
    {
        if (document.Lines.Count == 0 || string.IsNullOrWhiteSpace(desktopLine))
            return null;

        var target = Normalize(desktopLine);
        if (target.Length == 0)
            return null;

        LyricLine? best = null;
        var bestScore = 0;
        foreach (var line in document.Lines)
        {
            var candidate = Normalize(line.Text);
            if (candidate.Length == 0)
                continue;

            var score = 0;
            if (candidate.Equals(target, StringComparison.Ordinal))
                score = 100;
            else if (candidate.Contains(target, StringComparison.Ordinal) || target.Contains(candidate, StringComparison.Ordinal))
                score = 80;

            if (score > bestScore)
            {
                bestScore = score;
                best = line;
            }
        }

        return bestScore >= 80 ? best : null;
    }

    private static bool DocumentContainsText(LrcDocument document, string normalizedNeedle, bool exactOnly)
    {
        if (normalizedNeedle.Length == 0)
            return false;

        foreach (var line in document.Lines)
        {
            if (LyricMetadataFilter.IsMetadataLine(line.Text))
                continue;

            var normalizedLine = Normalize(line.Text);
            if (exactOnly)
            {
                if (normalizedLine == normalizedNeedle)
                    return true;

                continue;
            }

            if (normalizedLine.Contains(normalizedNeedle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool TextMatch(string left, string right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a.Length == 0 || b.Length == 0)
            return false;

        return a.Equals(b, StringComparison.Ordinal)
            || a.Contains(b, StringComparison.Ordinal)
            || b.Contains(a, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var chars = value.Where(static c => !char.IsWhiteSpace(c) && c is not '-' and not '_' and not '（' and not '）' and not '(' and not ')').ToArray();
        return new string(chars).ToLowerInvariant();
    }
}
