using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace PixelBar_App.Services.Lyrics;

/// <summary>读取网易云音乐桌面歌词窗口，或从主窗口标题解析曲目信息。</summary>
public static class NetEaseDesktopLyricsReader
{
    private const string MainWindowClass = "OrpheusBrowserHost";
    private const int MaxChildDepth = 8;

    private static readonly string[] IgnoredWindowTitles =
    [
        "桌面歌词",
        "歌词",
        "网易云音乐",
        "Netease Cloud Music",
        "Desktop Lyric",
        "DesktopLyric",
    ];

    public static DesktopLyricSnapshot? TryRead()
    {
        var processId = FindCloudMusicProcessId();
        if (processId == 0)
            return null;

        var track = TryParseMainWindowTrack();
        DesktopLyricSnapshot? best = null;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out var windowProcessId);
            if (windowProcessId != processId)
                return true;

            if (ReadClassName(hWnd).Equals(MainWindowClass, StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryParseDesktopLyricTitle(ReadWindowText(hWnd), track, out var fromTitle))
            {
                if (best is null || fromTitle.Line.Length > best.Value.Line.Length)
                    best = fromTitle;
                return true;
            }

            var line = ReadBestTextFromWindowTree(hWnd);
            if (line is not { Length: > 0 } lyricLine
                || !IsUsableLyricLine(lyricLine, track?.Title, track?.Artist))
            {
                return true;
            }

            if (best is null || lyricLine.Length > best.Value.Line.Length)
                best = new DesktopLyricSnapshot(lyricLine, track?.Artist, lyricLine);

            return true;
        }, IntPtr.Zero);

        return best ?? TryReadViaAutomation(processId, track);
    }

    /// <summary>桌面歌词窗口已打开（标题可能暂时与歌名相同）。</summary>
    public static bool IsDesktopLyricWindowOpen()
    {
        var processId = FindCloudMusicProcessId();
        if (processId == 0)
            return false;

        var found = false;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out var windowProcessId);
            if (windowProcessId != processId)
                return true;

            if (ReadClassName(hWnd).Equals(MainWindowClass, StringComparison.OrdinalIgnoreCase))
                return true;

            var title = ReadWindowText(hWnd).Trim();
            if (title.Length == 0)
                return true;

            foreach (var ignored in IgnoredWindowTitles)
            {
                if (title.Equals(ignored, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            found = true;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private static DesktopLyricSnapshot? TryReadViaAutomation(uint processId, (string? Title, string? Artist)? track)
    {
        try
        {
            using var automation = new UIA3Automation();
            var cf = automation.ConditionFactory;
            var desktop = automation.GetDesktop();
            string? bestLine = null;
            foreach (var window in desktop.FindAllChildren(cf.ByProcessId((int)processId)))
            {
                var windowName = window.Properties.Name.ValueOrDefault;
                if (TryParseDesktopLyricTitle(windowName, track, out var fromTitle)
                    && (bestLine is null || fromTitle.Line.Length > bestLine.Length))
                {
                    bestLine = fromTitle.Line;
                    continue;
                }

                CollectAutomationText(window, ref bestLine, track?.Title, track?.Artist, 0);
            }

            return bestLine is null
                ? null
                : new DesktopLyricSnapshot(bestLine, track?.Artist, bestLine);
        }
        catch
        {
            return null;
        }
    }

    private static void CollectAutomationText(
        AutomationElement element,
        ref string? bestLine,
        string? trackTitle,
        string? trackArtist,
        int depth)
    {
        if (depth > 14)
            return;

        var name = element.Properties.Name.ValueOrDefault;
        if (IsUsableLyricLine(name, trackTitle, trackArtist)
            && (bestLine is null || name!.Length > bestLine.Length))
        {
            bestLine = name!.Trim();
        }

        foreach (var child in element.FindAllChildren())
            CollectAutomationText(child, ref bestLine, trackTitle, trackArtist, depth + 1);
    }

    public static bool IsUsableLyricLine(string? text, string? trackTitle = null, string? trackArtist = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.Length < 2)
            return false;

        foreach (var ignored in IgnoredWindowTitles)
        {
            if (text.Equals(ignored, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (IsLikelyTrackTitle(text, trackTitle, trackArtist))
            return false;

        if (LyricMetadataFilter.IsMetadataLine(text))
            return false;

        return true;
    }

    public static (string? Title, string? Artist)? TryParseMainWindowTrack()
    {
        var processId = FindCloudMusicProcessId();
        if (processId == 0)
            return null;

        string? mainTitle = null;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out var windowProcessId);
            if (windowProcessId != processId)
                return true;

            if (!ReadClassName(hWnd).Equals(MainWindowClass, StringComparison.OrdinalIgnoreCase))
                return true;

            mainTitle = ReadWindowText(hWnd);
            return false;
        }, IntPtr.Zero);

        if (string.IsNullOrWhiteSpace(mainTitle) || mainTitle == "网易云音乐")
            return null;

        return ParseTrackTitle(mainTitle);
    }

    internal static (string? Title, string? Artist)? ParseTrackTitle(string title)
    {
        title = title.Trim();
        var separator = title.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator <= 0 || separator >= title.Length - 3)
            return (title, null);

        var song = title[..separator].Trim();
        var artist = title[(separator + 3)..].Trim();
        return song.Length == 0 ? null : (song, artist);
    }

    /// <summary>桌面歌词窗口标题常为「当前歌词行 - 歌手」，与 QQ 音乐相同。</summary>
    private static bool TryParseDesktopLyricTitle(
        string? windowTitle,
        (string? Title, string? Artist)? track,
        out DesktopLyricSnapshot snapshot)
    {
        snapshot = default;
        if (string.IsNullOrWhiteSpace(windowTitle))
            return false;

        windowTitle = windowTitle.Trim();
        foreach (var ignored in IgnoredWindowTitles)
        {
            if (windowTitle.Equals(ignored, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var separator = windowTitle.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator <= 0 || separator >= windowTitle.Length - 3)
        {
            if (IsLikelyTrackTitle(windowTitle, track?.Title, track?.Artist))
                return false;

            if (!IsUsableLyricLine(windowTitle, trackTitle: null, trackArtist: null))
                return false;

            snapshot = new DesktopLyricSnapshot(windowTitle, track?.Artist, windowTitle);
            return true;
        }

        var line = windowTitle[..separator].Trim();
        var artist = windowTitle[(separator + 3)..].Trim();
        if (line.Length == 0)
            return false;

        if (track is { Title: var song, Artist: var trackArtist }
            && line.Equals(song, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(trackArtist)
                || artist.Equals(trackArtist, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!IsUsableLyricLine(line, trackTitle: null, trackArtist: null))
            return false;

        snapshot = new DesktopLyricSnapshot(line, artist, windowTitle);
        return true;
    }

    public static bool IsProcessRunning() => FindCloudMusicProcessId() != 0;

    private static bool IsLikelyTrackTitle(string text, string? title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        if (!text.Contains(title, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(artist))
            return text.Equals(title, StringComparison.OrdinalIgnoreCase);

        return text.Contains(artist, StringComparison.OrdinalIgnoreCase)
               || text.Equals($"{title} · {artist}", StringComparison.OrdinalIgnoreCase)
               || text.Equals($"{title} - {artist}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadBestTextFromWindowTree(IntPtr hWnd)
    {
        var best = string.Empty;
        CollectTexts(hWnd, 0, ref best);
        return best.Length > 0 ? best : null;
    }

    private static void CollectTexts(IntPtr hWnd, int depth, ref string best)
    {
        if (depth > MaxChildDepth)
            return;

        ConsiderText(ReadWindowText(hWnd), ref best);
        ConsiderText(ReadControlText(hWnd), ref best);

        var child = GetWindow(hWnd, GW_CHILD);
        while (child != IntPtr.Zero)
        {
            CollectTexts(child, depth + 1, ref best);
            child = GetWindow(child, GW_HWNDNEXT);
        }
    }

    private static void ConsiderText(string? text, ref string best)
    {
        if (!IsUsableLyricLine(text))
            return;

        text = text!.Trim();
        if (text.Length > best.Length)
            best = text;
    }

    private static string ReadControlText(IntPtr hWnd)
    {
        var length = SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (length <= 0)
            return string.Empty;

        var builder = new StringBuilder(length + 1);
        _ = SendMessage(hWnd, WM_GETTEXT, builder.Capacity, builder);
        return builder.ToString();
    }

    private static uint FindCloudMusicProcessId()
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("cloudmusic"))
        {
            try
            {
                return (uint)process.Id;
            }
            finally
            {
                process.Dispose();
            }
        }

        return 0;
    }

    private static string ReadWindowText(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length <= 0)
            return string.Empty;

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(IntPtr hWnd)
    {
        var builder = new StringBuilder(256);
        _ = GetClassName(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private const int WM_GETTEXT = 0x000D;
    private const int WM_GETTEXTLENGTH = 0x000E;
    private const uint GW_CHILD = 5;
    private const uint GW_HWNDNEXT = 2;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, StringBuilder lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
