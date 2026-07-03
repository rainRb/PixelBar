using System.Collections.Concurrent;

namespace PixelBar_App.Services.Lyrics;

public sealed class NetEaseLyricProvider
{
    private readonly ConcurrentDictionary<string, IndexedLyricEntry?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _indexLock = new();
    private DateTime _indexBuiltAt = DateTime.MinValue;
    private IReadOnlyList<IndexedLyricEntry> _index = [];
    private Dictionary<string, IndexedLyricEntry> _numericSongIdToEntry = new(StringComparer.Ordinal);

    public IReadOnlyList<string> GetSearchDirectories(string? customDirectory) =>
        NetEaseCloudMusicCacheLocator.GetLyricDirectories(customDirectory);

    public int IndexedFileCount
    {
        get
        {
            lock (_indexLock)
                return _index.Count;
        }
    }

    public void InvalidateIndex()
    {
        lock (_indexLock)
        {
            _indexBuiltAt = DateTime.MinValue;
            _index = [];
            _numericSongIdToEntry = new Dictionary<string, IndexedLyricEntry>(StringComparer.Ordinal);
        }

        _cache.Clear();
    }

    public LrcDocument? TryFindLyrics(string title, string artist, string? customDirectory)
    {
        EnsureIndex(customDirectory);
        return LyricMatchHelper.PickBestMatch(_index, title, artist);
    }

    public async Task<(LrcDocument? Document, LyricSource Source)> TryFindLyricsForTrackAsync(
        string? title,
        string? artist,
        string? netEaseSongId,
        string? customDirectory,
        CancellationToken cancellationToken = default)
    {
        if (InfLinkSmtcParser.IsNumericSongId(netEaseSongId))
        {
            var local = TryFindLyricsByNumericSongId(netEaseSongId!, customDirectory, title, artist);
            if (local is not null)
                return (local, LyricSource.NetEaseCache);

            var fromApi = await NetEaseLyricApiClient.TryFetchLyricsAsync(
                netEaseSongId!,
                title,
                artist,
                cancellationToken).ConfigureAwait(false);
            if (fromApi is not null)
                return (fromApi, LyricSource.NetEaseApi);

            return (null, LyricSource.None);
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var byTitle = TryFindLyrics(title, artist ?? string.Empty, customDirectory);
            if (byTitle is not null)
                return (byTitle, LyricSource.NetEaseCache);
        }

        return (null, LyricSource.None);
    }

    private LrcDocument? TryFindLyricsByNumericSongId(
        string numericSongId,
        string? customDirectory,
        string? title,
        string? artist)
    {
        var meta = NetEaseLyricMetaStore.TryRead(numericSongId);
        if (meta?.SourcePath is { Length: > 0 } metaPath && File.Exists(metaPath))
        {
            var fromMeta = LoadEntry(metaPath)?.Document;
            if (fromMeta is not null && IsExpectedTrack(fromMeta, title, meta.Title))
                return fromMeta;
        }

        var apiCachePath = Path.Combine(NetEaseLyricApiClient.GetApiCacheDirectory(), $"{numericSongId}.json");
        if (File.Exists(apiCachePath))
        {
            var fromApiCache = LoadEntry(apiCachePath)?.Document;
            if (fromApiCache is not null)
                return fromApiCache;
        }

        foreach (var dir in GetSearchDirectories(customDirectory))
        {
            var candidate = Path.Combine(dir, numericSongId);
            if (!File.Exists(candidate))
                continue;

            var fromFile = LoadEntry(candidate)?.Document;
            if (fromFile is not null)
                return fromFile;
        }

        EnsureIndex(customDirectory);
        if (_numericSongIdToEntry.TryGetValue(numericSongId, out var indexedEntry))
            return indexedEntry.Document;

        var recent = TryFindRecentLocalByNumericId(numericSongId, customDirectory, TimeSpan.FromSeconds(90));
        return recent;
    }

    private LrcDocument? TryFindRecentLocalByNumericId(
        string numericSongId,
        string? customDirectory,
        TimeSpan maxAge)
    {
        EnsureIndex(customDirectory);
        var cutoff = DateTime.UtcNow - maxAge;
        return _index
            .Where(entry => entry.Modified >= cutoff)
            .OrderByDescending(entry => entry.Modified)
            .Select(entry => entry)
            .FirstOrDefault(entry =>
                string.Equals(entry.NumericSongId, numericSongId, StringComparison.Ordinal))
            ?.Document;
    }

    public void RememberTrackMetadata(string? title, string? artist, string? netEaseSongId, string? customDirectory)
    {
        EnsureIndex(customDirectory);

        if (InfLinkSmtcParser.IsNumericSongId(netEaseSongId))
        {
            var path = ResolveLocalPathForNumericId(netEaseSongId!, customDirectory)
                ?? FindRecentCachePath(customDirectory, TimeSpan.FromSeconds(90));

            if (path is not null && File.Exists(path))
                NetEaseLyricMetaStore.Write(netEaseSongId!, title, artist, path);
            return;
        }

        if (string.IsNullOrWhiteSpace(title))
            return;

        var matchedEntry = _index
            .Select(entry => (Entry: entry, Score: LyricMatchHelper.ScoreMatch(entry, title, artist ?? string.Empty)))
            .Where(item => item.Score >= 80)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Entry.Modified)
            .Select(item => item.Entry)
            .FirstOrDefault(entry => LyricMatchHelper.DocumentContainsTitle(entry.Document, title));

        if (matchedEntry?.Path is not { Length: > 0 } matchedPath || !File.Exists(matchedPath))
            return;

        var hashId = NetEaseLyricCacheParser.TryParseSongIdFromPath(matchedPath);
        if (hashId is not null && !InfLinkSmtcParser.IsNumericSongId(hashId))
            NetEaseLyricMetaStore.Write(hashId, title, artist, matchedPath);
    }

    private string? ResolveLocalPathForNumericId(string numericSongId, string? customDirectory)
    {
        var metaPath = NetEaseLyricMetaStore.TryRead(numericSongId)?.SourcePath;
        if (metaPath is not null && File.Exists(metaPath))
            return metaPath;

        var apiCachePath = Path.Combine(NetEaseLyricApiClient.GetApiCacheDirectory(), $"{numericSongId}.json");
        if (File.Exists(apiCachePath))
            return apiCachePath;

        EnsureIndex(customDirectory);
        if (_numericSongIdToEntry.TryGetValue(numericSongId, out var entry))
            return entry.Path;

        foreach (var dir in GetSearchDirectories(customDirectory))
        {
            var candidate = Path.Combine(dir, numericSongId);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindRecentCachePath(string? customDirectory, TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var dir in NetEaseCloudMusicCacheLocator.GetLyricDirectories(customDirectory))
        {
            try
            {
                var recent = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(NetEaseCloudMusicCacheLocator.IsLyricCachePath)
                    .Select(path => new FileInfo(path))
                    .Where(info => info.LastWriteTimeUtc >= cutoff)
                    .OrderByDescending(info => info.LastWriteTimeUtc)
                    .Select(info => info.FullName)
                    .FirstOrDefault();
                if (recent is not null)
                    return recent;
            }
            catch
            {
                // ignore unreadable directories
            }
        }

        return null;
    }

    private static bool IsExpectedTrack(LrcDocument document, string? expectedTitle, string? metaTitle)
    {
        if (string.IsNullOrWhiteSpace(expectedTitle))
            return true;

        if (LyricMatchHelper.DocumentContainsTitle(document, expectedTitle))
            return true;

        return !string.IsNullOrWhiteSpace(metaTitle)
            && LyricMatchHelper.TrackMatches(metaTitle, null, expectedTitle, null);
    }

    private void EnsureIndex(string? customDirectory)
    {
        lock (_indexLock)
        {
            if (DateTime.UtcNow - _indexBuiltAt < TimeSpan.FromSeconds(20))
                return;

            var entries = new List<IndexedLyricEntry>();
            var numericMap = new Dictionary<string, IndexedLyricEntry>(StringComparer.Ordinal);
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in GetSearchDirectories(customDirectory))
            {
                try
                {
                    foreach (var path in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        if (!NetEaseCloudMusicCacheLocator.IsLyricCachePath(path))
                            continue;

                        var entry = LoadEntry(path);
                        if (entry?.Document is null || !seenKeys.Add(entry.SourceKey))
                            continue;

                        entries.Add(entry);
                        if (entry.NumericSongId is { Length: > 0 })
                            numericMap.TryAdd(entry.NumericSongId, entry);
                    }
                }
                catch
                {
                    // ignore unreadable directories
                }
            }

            var apiCacheDir = NetEaseLyricApiClient.GetApiCacheDirectory();
            if (Directory.Exists(apiCacheDir))
            {
                try
                {
                    foreach (var path in Directory.EnumerateFiles(apiCacheDir, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var entry = LoadEntry(path);
                        if (entry?.Document is null || !seenKeys.Add(entry.SourceKey))
                            continue;

                        entries.Add(entry);
                        if (entry.NumericSongId is { Length: > 0 })
                            numericMap.TryAdd(entry.NumericSongId, entry);
                    }
                }
                catch
                {
                    // ignore unreadable directories
                }
            }

            _index = entries;
            _numericSongIdToEntry = numericMap;
            _indexBuiltAt = DateTime.UtcNow;
        }
    }

    private IndexedLyricEntry? LoadEntry(string path)
    {
        try
        {
            var modified = File.GetLastWriteTimeUtc(path);
            var memoryKey = $"{path}|{modified.Ticks}";
            if (_cache.TryGetValue(memoryKey, out var cached))
                return cached;

            var pathSongId = NetEaseLyricCacheParser.TryParseSongIdFromPath(path);
            string? content = null;
            try
            {
                content = File.ReadAllText(path);
            }
            catch
            {
                _cache[memoryKey] = null;
                return null;
            }

            var contentSongId = NetEaseLyricCacheParser.TryParseNumericSongIdFromContent(content);
            var numericSongId = InfLinkSmtcParser.IsNumericSongId(pathSongId)
                ? pathSongId
                : contentSongId;

            var meta = pathSongId is null ? null : NetEaseLyricMetaStore.TryRead(pathSongId);
            var document = NetEaseLyricCacheParser.TryParseContent(content, pathSongId ?? numericSongId);
            if (document is null || document.Lines.Count == 0)
            {
                _cache[memoryKey] = null;
                return null;
            }

            var singableLines = LyricMetadataFilter.FilterSingableLines(document.Lines);
            if (singableLines.Count == 0)
            {
                _cache[memoryKey] = null;
                return null;
            }

            var metaTitle = meta?.Title;
            var metaArtist = meta?.Artist;
            if (!string.IsNullOrWhiteSpace(metaTitle)
                && !LyricMatchHelper.DocumentContainsTitle(
                    new LrcDocument { Lines = singableLines },
                    metaTitle))
            {
                metaTitle = null;
                metaArtist = null;
            }

            document = new LrcDocument
            {
                Title = document.Title ?? metaTitle,
                Artist = document.Artist ?? metaArtist,
                OffsetMs = document.OffsetMs,
                Lines = singableLines,
            };

            var indexed = new IndexedLyricEntry(
                $"netease:{path}|{modified.Ticks}",
                path,
                modified,
                document.Title,
                document.Artist,
                document,
                numericSongId);

            _cache[memoryKey] = indexed;
            return indexed;
        }
        catch
        {
            return null;
        }
    }
}
