namespace PixelBar_App.Services;

internal static class StartupLaunchPolicy
{
    public static bool ShouldStartInTray(string? arguments, bool silentStartup)
    {
        var args = arguments?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (args.Contains("--show", StringComparer.OrdinalIgnoreCase))
            return false;
        return silentStartup || args.Contains("--tray", StringComparer.OrdinalIgnoreCase);
    }
}
