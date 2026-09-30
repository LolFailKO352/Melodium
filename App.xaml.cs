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
        this.InitializeComponent();

        this.UnhandledException += (s, e) =>
        {
            try
            {
                var crashPath = Path.Combine(Path.GetTempPath(), "ytm_crash.txt");
                File.WriteAllText(crashPath, $"WinUI Unhandled: {e.Message}\nException: {e.Exception}\nStackTrace: {e.Exception?.StackTrace}");
            }
            catch { }
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
}