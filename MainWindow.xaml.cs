using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.Input;
using Melodium.Services;
using Melodium.ViewModels;

namespace Melodium;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public IRelayCommand ToggleWindowVisibilityCommand { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        ToggleWindowVisibilityCommand = new RelayCommand(ToggleWindowVisibility);

        this.InitializeComponent();

        SetupTrayIcon();

        // Assign ViewModel DataContext
        MainContent.DataContext = ViewModel;

        // Custom TitleBar setup
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(MainContent.TitleBarElement);

        // Window size
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));

        // Close to tray logic
        AppWindow.Closing += (s, e) =>
        {
            ViewModel.SavePlaybackSession();

            if (App.IsExiting)
            {
                // Application is already exiting; allow window to close without cancelling
                return;
            }

            if (!ViewModel.IsCloseToTrayEnabled || System.Diagnostics.Debugger.IsAttached)
            {
                e.Cancel = true;
                App.ExitApplication();
            }
            else
            {
                e.Cancel = true;
                AppWindow.Hide();
            }
        };

    }

    public void DisposeTrayIcon()
    {
        try
        {
            TrayIcon?.Dispose();
        }
        catch { }
    }

    public void ToggleWindowVisibility()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
        }
        else
        {
            AppWindow.Show();
            Activate();
        }
    }

    private void OnTrayPlayPauseClick(object sender, RoutedEventArgs e)
    {
        ViewModel.PlayPauseCommand.Execute(null);
    }

    private void OnTrayShowHideClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowVisibility();
    }

    private void OnTrayExitClick(object sender, RoutedEventArgs e)
    {
        App.ExitApplication();
    }

    private void SetupTrayIcon()
    {
        try
        {
            var flyout = new MenuFlyout();

            var playPauseItem = new MenuFlyoutItem
            {
                Text = Loc.Instance.TextPlayPause,
                Icon = new FontIcon { Glyph = "\uE768" }
            };
            playPauseItem.Click += OnTrayPlayPauseClick;
            flyout.Items.Add(playPauseItem);

            var showHideItem = new MenuFlyoutItem
            {
                Text = Loc.Instance.TextShowHideWindow,
                Icon = new FontIcon { Glyph = "\uE737" }
            };
            showHideItem.Click += OnTrayShowHideClick;
            flyout.Items.Add(showHideItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            var exitItem = new MenuFlyoutItem
            {
                Text = Loc.Instance.TextExitButton,
                Icon = new FontIcon { Glyph = "\uE711" }
            };
            exitItem.Click += OnTrayExitClick;
            flyout.Items.Add(exitItem);

            Loc.Instance.PropertyChanged += (s, e) =>
            {
                playPauseItem.Text = Loc.Instance.TextPlayPause;
                showHideItem.Text = Loc.Instance.TextShowHideWindow;
                exitItem.Text = Loc.Instance.TextExitButton;
            };

            TrayIcon.ContextFlyout = flyout;
        }
        catch (Exception ex)
        {
            CrashLoggerService.LogCrash(ex, "MainWindow.SetupTrayIcon");
        }
    }
}
