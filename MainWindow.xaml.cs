using System;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Input;
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
            if (System.Diagnostics.Debugger.IsAttached || App.IsExiting || !ViewModel.IsCloseToTrayEnabled)
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

        // Initialize session and recommendations
        _ = ViewModel.LoadSavedSessionAsync();
        _ = ViewModel.LoadHomeRecommendationsAsync();
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
}
