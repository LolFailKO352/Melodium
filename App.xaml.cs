using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Melodium.Services;
using Melodium.ViewModels;

namespace Melodium;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;
    public static bool IsExiting { get; set; } = false;

    public App()
    {
        CrashLoggerService.Initialize();

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var webViewFolder = Path.Combine(localAppData, "Melodium", "WebView2");
            Directory.CreateDirectory(webViewFolder);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewFolder);
        }
        catch (Exception ex)
        {
            CrashLoggerService.LogCrash(ex, "App.ConfigureWebView2Folder");
        }

        this.InitializeComponent();

        this.UnhandledException += (s, e) =>
        {
            CrashLoggerService.LogCrash(e.Exception, "WinUI.UnhandledException");
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<MelodiumService>();
        services.AddSingleton<IAudioService, WindowsAudioService>();
        services.AddSingleton<TranslationService>();
        services.AddSingleton<DiscordRpcService>();
        services.AddSingleton<LyricsService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
        Services = services.BuildServiceProvider();

        MainWindow = Services.GetRequiredService<MainWindow>();
        MainThread.Initialize(MainWindow.DispatcherQueue);

        MainWindow.Activate();
    }

    public static void ExitApplication()
    {
        if (IsExiting) return;
        IsExiting = true;

        void CleanupAndExit()
        {
            try { MainWindow?.DisposeTrayIcon(); } catch { }
            try { Services?.GetService<DiscordRpcService>()?.Dispose(); } catch { }
            try { (Services?.GetService<IAudioService>() as IDisposable)?.Dispose(); } catch { }
            try { MainWindow?.Close(); } catch { }
            try { Application.Current?.Exit(); } catch { }
            Environment.Exit(0);
        }

        if (MainWindow?.DispatcherQueue != null && !MainWindow.DispatcherQueue.HasThreadAccess)
        {
            MainWindow.DispatcherQueue.TryEnqueue(() => CleanupAndExit());
        }
        else
        {
            CleanupAndExit();
        }
    }
}