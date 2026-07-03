using System.Collections.Concurrent;
using System.Text.Json;

namespace PixelBar_App.Services.Lyrics;

public static class NetEaseLyricMetaStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetMetaDirectory()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PixelBar",
            "NetEaseLyricMeta");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static NetEaseLyricMeta? TryRead(string songId)
    {
        var path = Path.Combine(GetMetaDirectory(), $"{songId}.json");
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<NetEaseLyricMeta>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string songId, string? title, string? artist, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(songId))
            return;

        var meta = new NetEaseLyricMeta
        {
            SongId = songId,
            Title = title,
            Artist = artist,
            SourcePath = sourcePath,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        var path = Path.Combine(GetMetaDirectory(), $"{songId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(meta, JsonOptions));
    }

    public static IEnumerable<(string SongId, NetEaseLyricMeta Meta)> EnumerateEntries()
    {
        var dir = GetMetaDirectory();
        foreach (var metaPath in Directory.EnumerateFiles(dir, "*.json"))
        {
            NetEaseLyricMeta? meta;
            try
            {
                meta = JsonSerializer.Deserialize<NetEaseLyricMeta>(File.ReadAllText(metaPath), JsonOptions);
            }
            catch
            {
                continue;
            }

            if (meta?.SongId is { Length: > 0 })
                yield return (meta.SongId, meta);
        }
    }
}

public sealed class NetEaseLyricMeta
{
    public string SongId { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Artist { get; set; }

    public string? SourcePath { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
