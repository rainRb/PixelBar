using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixelBar_App.Pages;
using System.Text.Json;

namespace PixelBar_App.Services;

/// <summary>通过命令行显式运行的 WinUI 页面回归检查，不启动歌词推送或托盘。</summary>
internal static class LyricsPageSmokeTest
{
    public static bool IsRequested => Environment.GetCommandLineArgs().Contains("--self-test-lyrics");

    private static readonly string LogPath = Environment.GetEnvironmentVariable("PIXELBAR_SELF_TEST_LOG")
        ?? Path.Combine(Path.GetTempPath(), "PixelBar-LyricsPage-self-test.log");

    private static Window? _window;
    private static bool _failed;

    public static void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(LogPath))!);
        File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {text}{Environment.NewLine}");
    }

    public static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        _failed = true;
        Write($"FAIL unhandled: {args.Message}{Environment.NewLine}{args.Exception}");
        args.Handled = true;
        Environment.ExitCode = 1;
        Application.Current.Exit();
    }

    public static async void Run()
    {
        try
        {
            Write("START LyricsPage constructor and Loaded regression");
            var settingsBefore = JsonSerializer.Serialize(AppSettingsService.Instance.Current);
            _window = new Window();
            for (var iteration = 0; iteration < 2; iteration++)
            {
                Write($"Construct page {iteration + 1}");
                var page = new LyricsPage();
                Write("Constructor OK");
                var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                page.Loaded += (_, _) => loaded.TrySetResult();
                _window.Content = page;
                _window.Activate();
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Delay(250);
                if (_failed)
                    return;
                Write("Loaded OK");
                _window.Content = new Grid();
                await Task.Delay(100);
            }

            var frame = new Frame();
            frame.NavigationFailed += (_, args) =>
            {
                Write($"FAIL Frame.Navigate: {args.Exception}");
                args.Handled = true;
                _failed = true;
            };
            _window.Content = frame;
            for (var iteration = 0; iteration < 2; iteration++)
            {
                Write($"Frame.Navigate LyricsPage {iteration + 1}");
                var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs args)
                {
                    ((Page)args.Content).Loaded += (_, _) => loaded.TrySetResult();
                }
                frame.Navigated += OnNavigated;
                if (!frame.Navigate(typeof(LyricsPage)) || _failed)
                    throw new InvalidOperationException("LyricsPage navigation failed.");
                frame.Navigated -= OnNavigated;
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Delay(250);
                if (_failed)
                    return;
                Write("Frame.Navigate and Loaded OK");
                frame.Content = new Grid();
                await Task.Delay(100);
            }

            if (JsonSerializer.Serialize(AppSettingsService.Instance.Current) != settingsBefore)
                throw new InvalidOperationException("Opening LyricsPage changed saved preferences.");
            Write("PASS direct construction and Frame.Navigate, twice each; preferences unchanged");
            Environment.ExitCode = 0;
        }
        catch (Exception error)
        {
            Write($"FAIL: {error}");
            Environment.ExitCode = 1;
        }
        finally
        {
            _window?.Close();
            Application.Current.Exit();
        }
    }
}
