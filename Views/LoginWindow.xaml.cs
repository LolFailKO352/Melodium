using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Melodium.ViewModels;
using Melodium.Services;

namespace Melodium.Views;

public sealed partial class LoginWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isLoggingIn = false;

    public LoginWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Rozměry a vycentrování okna
        AppWindow.Resize(new Windows.Graphics.SizeInt32(860, 680));
        CenterWindow();

        this.Closed += (s, e) =>
        {
            try
            {
                LoginWebView?.Close();
            }
            catch { }
        };

        _ = InitializeWebViewAsync();
    }

    private void CenterWindow()
    {
        try
        {
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var centeredPosition = AppWindow.Position;
                centeredPosition.X = (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;
                centeredPosition.Y = (displayArea.WorkArea.Height - AppWindow.Size.Height) / 2;
                AppWindow.Move(centeredPosition);
            }
        }
        catch { }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            LoadingRing.IsActive = true;
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            LoginWebView.Visibility = Visibility.Collapsed;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userDataFolder = Path.Combine(localAppData, "Melodium", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);
            await LoginWebView.EnsureCoreWebView2Async();

            LoginWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            LoginWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            LoginWebView.Source = new Uri("https://music.youtube.com/");
            LoginWebView.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorMessageText.Text = $"Chyba při startu přihlašovacího okna: {ex.Message}";
            CrashLoggerService.LogCrash(ex, "LoginWindow.InitializeWebViewAsync");
        }
    }

    private async void LoginWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (_viewModel == null || _isLoggingIn) return;
        try
        {
            if (sender.CoreWebView2 != null)
            {
                var cookieManager = sender.CoreWebView2.CookieManager;
                var cookieList = await cookieManager.GetCookiesAsync("https://music.youtube.com");
                
                var targetCookies = new List<Cookie>();
                bool hasSapisid = false;
                bool hasPapisid = false;
                bool hasSsid = false;
                bool hasHsid = false;
                bool hasSapsid = false;

                foreach (var c in cookieList)
                {
                    targetCookies.Add(new Cookie(c.Name, c.Value, c.Path, c.Domain));
                    if (c.Name == "SAPISID") hasSapisid = true;
                    if (c.Name == "__Secure-3PAPISID") hasPapisid = true;
                    if (c.Name == "SSID") hasSsid = true;
                    if (c.Name == "HSID") hasHsid = true;
                    if (c.Name == "SID") hasSapsid = true;
                }

                if ((hasSapisid || hasPapisid) && (hasSsid || hasHsid || hasSapsid))
                {
                    _isLoggingIn = true;
                    LoadingRing.IsActive = true;
                    LoadingPanel.Visibility = Visibility.Visible;
                    LoadingStatusText.Text = "Přihlášení úspěšné, načítám vaši hudební knihovnu...";

                    await _viewModel.SaveSessionAsync(targetCookies);
                    
                    this.Close();
                }
            }
        }
        catch (Exception ex)
        {
            CrashLoggerService.LogCrash(ex, "LoginWindow.NavigationCompleted");
        }
    }

    private async void OnDownloadWebView2Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var uri = new Uri("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch { }
    }

    private async void OnManualCookieClick(object sender, RoutedEventArgs e)
    {
        var stackPanel = new StackPanel { Spacing = 10, Width = 460 };
        stackPanel.Children.Add(new TextBlock 
        { 
            Text = "Zadejte cookies z vašeho webového prohlížeče (formát: 'SAPISID=xxx; SSID=yyy; ...' nebo JSON):", 
            TextWrapping = TextWrapping.Wrap, 
            FontSize = 13 
        });

        var textBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 130,
            PlaceholderText = "Vložte zkopírované cookies zde...",
            TextWrapping = TextWrapping.Wrap
        };
        stackPanel.Children.Add(textBox);

        var dialog = new ContentDialog
        {
            Title = "Vložit přihlašovací cookies ručně",
            Content = stackPanel,
            PrimaryButtonText = "Přihlásit",
            CloseButtonText = "Zrušit",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var parsedCookies = ParseCookies(textBox.Text);
            bool hasAuth = parsedCookies.Any(c => c.Name == "SAPISID" || c.Name == "__Secure-3PAPISID" || c.Name == "SID");

            if (hasAuth)
            {
                LoadingRing.IsActive = true;
                LoadingPanel.Visibility = Visibility.Visible;
                LoadingStatusText.Text = "Ukládám relaci a načítám knihovnu...";

                await _viewModel.SaveSessionAsync(parsedCookies);
                this.Close();
            }
            else
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Neplatné cookies",
                    Content = "Zadaný text neobsahuje potřebné přihlašovací tokeny (SAPISID, __Secure-3PAPISID ani SID). Ujistěte se, že jste zkopírovali všechny cookies ze stránky music.youtube.com.",
                    CloseButtonText = "Rozumím",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = errorDialog.ShowAsync();
            }
        }
    }

    public static List<Cookie> ParseCookies(string input)
    {
        var cookies = new List<Cookie>();
        if (string.IsNullOrWhiteSpace(input)) return cookies;

        input = input.Trim();

        // 1. Zkusit JSON formát (často exportovaný z doplňků do prohlížeče)
        if (input.StartsWith("[") && input.EndsWith("]"))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(input);
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    string? name = null;
                    string? value = null;
                    string domain = ".youtube.com";
                    string path = "/";

                    if (elem.TryGetProperty("name", out var n)) name = n.GetString();
                    if (elem.TryGetProperty("value", out var v)) value = v.GetString();
                    if (elem.TryGetProperty("domain", out var d)) domain = d.GetString() ?? domain;
                    if (elem.TryGetProperty("path", out var p)) path = p.GetString() ?? path;

                    if (!string.IsNullOrEmpty(name) && value != null)
                    {
                        cookies.Add(new Cookie(name, value, path, domain));
                    }
                }
                if (cookies.Count > 0) return cookies;
            }
            catch { }
        }

        // 2. Standardní formát Cookie hlavičky: Name=Value; Name2=Value2
        var parts = input.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
            {
                var name = trimmed.Substring(0, eqIdx).Trim();
                var value = trimmed.Substring(eqIdx + 1).Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    cookies.Add(new Cookie(name, value, "/", ".youtube.com"));
                }
            }
        }

        return cookies;
    }
}
