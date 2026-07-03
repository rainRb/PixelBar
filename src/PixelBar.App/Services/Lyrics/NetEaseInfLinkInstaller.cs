using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace PixelBar_App.Services.Lyrics;

public enum NetEaseInfLinkInstallState
{
    Installed,
    MissingInfLink,
    MissingBetterNcm,
}

public sealed class NetEaseInfLinkInstallResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool RequiresCloudMusicRestart { get; init; }
}

/// <summary>
/// 一键安装 BetterNCM + apoint123/InfLink-rs（优先使用内置 Assets，否则从 GitHub 下载）。
/// </summary>
public static class NetEaseInfLinkInstaller
{
    private const string BundledAssetsFolder = "Assets/NetEaseInfLink";
    private const string BetterNcmInstallerName = "betterncm_installer.exe";
    private const string InfLinkPluginName = "InfLink-rs.plugin";

    private const string BetterNcmInstallerUrl =
        "https://github.com/std-microblock/BetterNCM-Installer/releases/download/1.2.0/betterncm_installer.exe";

    private const string InfLinkLatestReleaseApi =
        "https://api.github.com/repos/apoint123/inflink-rs/releases/latest";

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "PixelBar-App" } },
    };

    public static NetEaseInfLinkInstallState GetInstallState()
    {
        var pluginsDir = ResolvePluginsDirectory();
        if (pluginsDir is null)
            return NetEaseInfLinkInstallState.MissingBetterNcm;

        return IsInfLinkInstalled(pluginsDir)
            ? NetEaseInfLinkInstallState.Installed
            : NetEaseInfLinkInstallState.MissingInfLink;
    }

    public static string GetStatusText() => GetInstallState() switch
    {
        NetEaseInfLinkInstallState.Installed => "InfLink-rs 已安装",
        NetEaseInfLinkInstallState.MissingInfLink => "InfLink-rs 未安装（网易云歌词同步必需）",
        _ => "BetterNCM 未安装（网易云歌词同步必需）",
    };

    public static async Task<NetEaseInfLinkInstallResult> InstallAsync(
        IProgress<string>? progress = null,
        bool forceReinstall = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report("正在检查 BetterNCM…");
            var pluginsDir = ResolvePluginsDirectory();
            if (pluginsDir is null)
            {
                progress?.Report("正在安装 BetterNCM…");
                var betterNcmResult = await InstallBetterNcmAsync(progress, cancellationToken).ConfigureAwait(false);
                if (!betterNcmResult.Success)
                    return betterNcmResult;

                pluginsDir = ResolvePluginsDirectory();
                if (pluginsDir is null)
                {
                    return new NetEaseInfLinkInstallResult
                    {
                        Success = false,
                        Message = "BetterNCM 安装程序已运行，请完成后重新点击「一键安装 InfLink-rs」。",
                    };
                }
            }

            if (!forceReinstall && IsInfLinkInstalled(pluginsDir))
            {
                return new NetEaseInfLinkInstallResult
                {
                    Success = true,
                    Message = "InfLink-rs 已安装，请重启网易云音乐后使用。",
                    RequiresCloudMusicRestart = true,
                };
            }

            progress?.Report("正在安装 InfLink-rs 插件…");
            Directory.CreateDirectory(pluginsDir);
            var targetPath = Path.Combine(pluginsDir, InfLinkPluginName);
            var bundled = GetBundledAssetPath(InfLinkPluginName);
            if (File.Exists(bundled))
            {
                File.Copy(bundled, targetPath, overwrite: true);
            }
            else
            {
                var downloadUrl = await ResolveInfLinkDownloadUrlAsync(cancellationToken).ConfigureAwait(false);
                await DownloadFileAsync(downloadUrl, targetPath, cancellationToken).ConfigureAwait(false);
            }

            return new NetEaseInfLinkInstallResult
            {
                Success = true,
                Message = forceReinstall
                    ? "InfLink-rs 已重新安装。请完全退出并重新打开网易云音乐。"
                    : "InfLink-rs 已安装。请完全退出并重新打开网易云音乐，然后在诊断中确认显示「InfLink-rs（进度 …）」。",
                RequiresCloudMusicRestart = true,
            };
        }
        catch (Exception ex)
        {
            return new NetEaseInfLinkInstallResult
            {
                Success = false,
                Message = $"安装失败：{ex.Message}",
            };
        }
    }

    private static async Task<NetEaseInfLinkInstallResult> InstallBetterNcmAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PixelBar",
            "NetEaseInfLink");
        Directory.CreateDirectory(cacheDir);

        var installerPath = Path.Combine(cacheDir, BetterNcmInstallerName);
        var bundled = GetBundledAssetPath(BetterNcmInstallerName);
        if (File.Exists(bundled))
        {
            File.Copy(bundled, installerPath, overwrite: true);
        }
        else if (!File.Exists(installerPath))
        {
            progress?.Report("正在下载 BetterNCM 安装程序…");
            await DownloadFileAsync(BetterNcmInstallerUrl, installerPath, cancellationToken).ConfigureAwait(false);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
        });

        return new NetEaseInfLinkInstallResult
        {
            Success = true,
            Message = "已启动 BetterNCM 安装程序。请按提示完成安装后，再次点击「一键安装 InfLink-rs」。",
        };
    }

    private static string? ResolvePluginsDirectory()
    {
        foreach (var dir in EnumeratePluginsDirectories())
        {
            if (Directory.Exists(dir))
                return dir;
        }

        var defaultDir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "betterncm", "plugins");
        if (Directory.Exists(Path.GetDirectoryName(defaultDir)!))
            return defaultDir;

        return null;
    }

    private static IEnumerable<string> EnumeratePluginsDirectories()
    {
        yield return Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "betterncm", "plugins");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, "BetterNCM", "plugins");
    }

    private static bool IsInfLinkInstalled(string pluginsDir)
    {
        if (!Directory.Exists(pluginsDir))
            return false;

        foreach (var file in Directory.EnumerateFiles(pluginsDir))
        {
            var name = Path.GetFileName(file);
            if (name.Contains("InfLink", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".plugin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(pluginsDir))
        {
            if (!Path.GetFileName(dir).Contains("InfLink", StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(Path.Combine(dir, "manifest.json")))
                return true;
        }

        return false;
    }

    private static string GetBundledAssetPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, BundledAssetsFolder, fileName);

    private static async Task<string> ResolveInfLinkDownloadUrlAsync(CancellationToken cancellationToken)
    {
        await using var stream = await HttpClient.GetStreamAsync(InfLinkLatestReleaseApi, cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (string.Equals(name, InfLinkPluginName, StringComparison.OrdinalIgnoreCase))
                return asset.GetProperty("browser_download_url").GetString()!;
        }

        throw new InvalidOperationException("未在 InfLink-rs 最新 Release 中找到 InfLink-rs.plugin。");
    }

    private static async Task DownloadFileAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        await using var stream = await HttpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        await using var file = File.Create(targetPath);
        await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
    }
}
