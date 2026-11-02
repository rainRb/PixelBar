using Microsoft.UI.Xaml;
using PixelBar_App.Services;
using PixelBar_App.Services.Lyrics;

namespace PixelBar_App;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();
        if (LyricsPageSmokeTest.IsRequested)
            UnhandledException += LyricsPageSmokeTest.OnUnhandledException;
        AppSettingsService.Instance.Load();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (LyricsPageSmokeTest.IsRequested)
        {
            LyricsPageSmokeTest.Run();
            return;
        }

        var settings = AppSettingsService.Instance.Current;
        var startInTray = StartupLaunchPolicy.ShouldStartInTray(args.Arguments, settings.SilentStartup);
        if (settings.RunAtStartup)
        {
            try { StartupRegistrationService.Apply(settings); }
            catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
        }

        MainWindowInstance = new MainWindow();
        // 静默启动没有页面 Loaded，也要为歌词等后台功能完成设备选择。
        try { PixelBarService.Instance.EnsureDefaultDevice(); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
        ClockTimeSyncService.Instance.Initialize();
        TrayIconService.Instance.Initialize(
            () => MainWindowInstance?.ShowFromTray(),
            () => MainWindowInstance?.RequestExit());

        if (startInTray)
        {
            try { MainWindowInstance.HideToTray(showNotification: false); }
            catch { MainWindowInstance.Activate(); }
        }
        else
            MainWindowInstance.Activate();

        LyricsSyncService.Instance.ApplySettings();
    }

}
