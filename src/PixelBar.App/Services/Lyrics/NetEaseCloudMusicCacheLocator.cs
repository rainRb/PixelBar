namespace PixelBar_App.Services.Lyrics;

public static class NetEaseCloudMusicCacheLocator
{
    public static IReadOnlyList<string> GetLyricDirectories(string? customDirectory)
    {
        var directories = new List<string>();
        if (!string.IsNullOrWhiteSpace(customDirectory))
        {
            var trimmed = customDirectory.Trim();
            if (Directory.Exists(trimmed))
                directories.Add(trimmed);
        }

        foreach (var lyricDir in EnumerateDefaultLyricDirectories())
        {
            if (Directory.Exists(lyricDir) && !directories.Contains(lyricDir, StringComparer.OrdinalIgnoreCase))
                directories.Add(lyricDir);
        }

        return directories;
    }

    public static string? GetPrimaryCachePath()
    {
        foreach (var dir in EnumerateDefaultLyricDirectories())
        {
            if (!Directory.Exists(dir))
                continue;

            if (Directory.EnumerateFiles(dir).Any(IsLyricCacheFileCandidate))
                return dir;
        }

        foreach (var dir in EnumerateDefaultLyricDirectories())
        {
            if (Directory.Exists(dir))
                return dir;
        }

        return null;
    }

    private static bool IsLyricCacheFileCandidate(string path) =>
        IsLyricCachePath(path);

    public static bool IsLyricCachePath(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName.Equals("index.dat", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".dat-journal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (extension.Equals(".lrc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yrc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName.Length > 0 && fileName.All(char.IsDigit) && !Path.HasExtension(path))
            return true;

        return IsTempHashFileName(Path.GetFileNameWithoutExtension(path));
    }

    private static bool IsTempHashFileName(string name)
    {
        if (name.Length != 32)
            return false;

        foreach (var c in name)
        {
            if (c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')
                continue;

            return false;
        }

        return true;
    }

    private static IEnumerable<string> EnumerateDefaultLyricDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cloudMusicRoot = Path.Combine(localAppData, "Netease", "CloudMusic");

        yield return Path.Combine(cloudMusicRoot, "Temp");
        yield return Path.Combine(cloudMusicRoot, "webdata", "lyric");
        yield return Path.Combine(cloudMusicRoot, "Cache", "Lyric");
        yield return Path.Combine(cloudMusicRoot, "Cache", "LyricNew");
    }
}
