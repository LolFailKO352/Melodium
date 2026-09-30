using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Composition;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Web.WebView2.Core;
using Melodium.Models;
using Melodium.Services;
using Melodium.ViewModels;

namespace Melodium.Views;

public sealed partial class MainView : UserControl
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;
    public Grid TitleBarElement => AppTitleBar;

    public MainView()
    {
        this.InitializeComponent();

        this.Loaded += (s, e) =>
        {
            // Select Home initially
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            SetupBlurEffect();
            if (ViewModel != null)
            {
                ViewModel.ActiveLyricChanged += OnActiveLyricChanged;
            }
        };
    }

    private void OnActiveLyricChanged(LyricLineModel activeLine)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (LyricsItemsRepeater == null || LyricsScrollViewer == null) return;
                var index = ViewModel?.LyricsLines.IndexOf(activeLine) ?? -1;
                if (index >= 0)
                {
                    var element = LyricsItemsRepeater.GetOrCreateElement(index);
                    element?.StartBringIntoView(new BringIntoViewOptions
                    {
                        VerticalAlignmentRatio = 0.4,
                        AnimationDesired = true
                    });
                }
            }
            catch { }
        });
    }

    private void OnLyricLineClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var line = element?.Tag as LyricLineModel ?? element?.DataContext as LyricLineModel;
        if (line != null && ViewModel != null)
        {
            ViewModel.SeekToLyricCommand.Execute(line);
        }
    }

    private void OnShowQueueClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && ViewModel.IsKaraokeMode)
        {
            _ = ViewModel.ToggleKaraokeModeCommand.ExecuteAsync(null);
        }
    }

    private void OnShowLyricsClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && !ViewModel.IsKaraokeMode)
        {
            _ = ViewModel.ToggleKaraokeModeCommand.ExecuteAsync(null);
        }
    }

    private void SetupBlurEffect()
    {
        try
        {
            var hostVisual = ElementCompositionPreview.GetElementVisual(BlurOverlay);
            var compositor = hostVisual.Compositor;

            var blurEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 60.0f,
                BorderMode = EffectBorderMode.Hard,
                Optimization = EffectOptimization.Speed,
                Source = new CompositionEffectSourceParameter("backdrop")
            };

            var factory = compositor.CreateEffectFactory(blurEffect);
            var brush = factory.CreateBrush();
            brush.SetSourceParameter("backdrop", compositor.CreateBackdropBrush());

            var sprite = compositor.CreateSpriteVisual();
            sprite.Brush = brush;

            var bindSize = compositor.CreateExpressionAnimation("host.Size");
            bindSize.SetReferenceParameter("host", hostVisual);
            sprite.StartAnimation("Size", bindSize);

            ElementCompositionPreview.SetElementChildVisual(BlurOverlay, sprite);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chyba při aplikaci blur efektu: {ex.Message}");
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            ViewModel?.NavigateCommand.Execute(tag);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText) && ViewModel != null)
        {
            ViewModel.SearchQuery = args.QueryText;
            ViewModel.PerformSearchCommand.Execute(null);

            // Odznačit položky v navigaci (vyhledávání má vlastní zobrazení)
            NavView.SelectedItem = null;
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange) return;
        if (string.IsNullOrWhiteSpace(sender.Text) && ViewModel?.CurrentView == "Search")
        {
            OnBackToHomeClick(sender, new RoutedEventArgs());
        }
    }

    private void OnBackToHomeClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.NavigateCommand.Execute("Home");
            var homeItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == "Home");
            if (homeItem != null)
            {
                NavView.SelectedItem = homeItem;
            }
        }
    }

    private void OnSongCardClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var song = element?.Tag as SongModel ?? element?.DataContext as SongModel;
        if (song != null && ViewModel != null)
        {
            _ = ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }

    private void OnPlaylistCardClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var playlist = element?.Tag as PlaylistModel ?? element?.DataContext as PlaylistModel;
        if (playlist != null && ViewModel != null)
        {
            _ = ViewModel.PlayPlaylistCommand.ExecuteAsync(playlist);
        }
    }

    private void OnAlbumCardClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var album = element?.Tag as AlbumModel ?? element?.DataContext as AlbumModel;
        if (album != null && ViewModel != null)
        {
            _ = ViewModel.PlayAlbumCommand.ExecuteAsync(album);
        }
    }

    private void OnArtistCardClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var artist = element?.Tag as ArtistModel ?? element?.DataContext as ArtistModel;
        if (artist != null && ViewModel != null)
        {
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(artist);
        }
    }

    private void OnCurrentSongArtistClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CurrentSong != null)
        {
            if (ViewModel.IsFullScreenPlayerVisible)
            {
                ViewModel.IsFullScreenPlayerVisible = false;
            }
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(ViewModel.CurrentSong);
        }
    }

    private void OnSongArtistClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var song = element?.Tag as SongModel ?? element?.DataContext as SongModel;
        if (song != null && ViewModel != null)
        {
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(song);
        }
    }

    private void OnSongArtistTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
        var element = sender as FrameworkElement;
        var song = element?.Tag as SongModel ?? element?.DataContext as SongModel;
        if (song != null && ViewModel != null)
        {
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(song);
        }
    }

    // --- Context Menu Handlers ---
    private void OnSongContextPlayClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as MenuFlyoutItem)?.CommandParameter as SongModel
                ?? (sender as FrameworkElement)?.Tag as SongModel 
                ?? (sender as FrameworkElement)?.DataContext as SongModel;
        if (song != null && ViewModel != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] Play clicked for song: {song.Title}");
            _ = ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }

    private void OnSongContextPlayNextClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as MenuFlyoutItem)?.CommandParameter as SongModel
                ?? (sender as FrameworkElement)?.Tag as SongModel 
                ?? (sender as FrameworkElement)?.DataContext as SongModel;
        if (song != null && ViewModel != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] Play Next clicked for song: {song.Title}");
            ViewModel.PlaySongNextCommand.Execute(song);
        }
    }

    private void OnSongContextAddToQueueClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as MenuFlyoutItem)?.CommandParameter as SongModel
                ?? (sender as FrameworkElement)?.Tag as SongModel 
                ?? (sender as FrameworkElement)?.DataContext as SongModel;
        if (song != null && ViewModel != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] Add to Queue clicked for song: {song.Title}");
            ViewModel.AddToQueueCommand.Execute(song);
        }
    }

    private void OnSongContextStartMixClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as MenuFlyoutItem)?.CommandParameter as SongModel
                ?? (sender as FrameworkElement)?.Tag as SongModel 
                ?? (sender as FrameworkElement)?.DataContext as SongModel;
        if (song != null && ViewModel != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] Start Mix clicked for song: {song.Title}");
            _ = ViewModel.StartMixCommand.ExecuteAsync(song);
        }
    }

    private void OnSongContextOpenArtistClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as MenuFlyoutItem)?.CommandParameter as SongModel
                ?? (sender as FrameworkElement)?.Tag as SongModel 
                ?? (sender as FrameworkElement)?.DataContext as SongModel;
        if (song != null && ViewModel != null)
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(song);
    }

    private void OnSongContextRemoveFromQueueClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as MenuFlyoutItem)?.CommandParameter as SongModel
                ?? (sender as FrameworkElement)?.Tag as SongModel 
                ?? (sender as FrameworkElement)?.DataContext as SongModel;
        if (song != null && ViewModel != null)
            ViewModel.RemoveFromQueueCommand.Execute(song);
    }

    private void OnAlbumContextPlayClick(object sender, RoutedEventArgs e)
    {
        var album = (sender as MenuFlyoutItem)?.CommandParameter as AlbumModel
                 ?? (sender as FrameworkElement)?.Tag as AlbumModel 
                 ?? (sender as FrameworkElement)?.DataContext as AlbumModel;
        if (album != null && ViewModel != null)
            _ = ViewModel.PlayAlbumCommand.ExecuteAsync(album);
    }

    private void OnAlbumContextOpenArtistClick(object sender, RoutedEventArgs e)
    {
        var album = (sender as MenuFlyoutItem)?.CommandParameter as AlbumModel
                 ?? (sender as FrameworkElement)?.Tag as AlbumModel 
                 ?? (sender as FrameworkElement)?.DataContext as AlbumModel;
        if (album != null && ViewModel != null)
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(album.ArtistName);
    }

    private void OnArtistContextOpenClick(object sender, RoutedEventArgs e)
    {
        var artist = (sender as MenuFlyoutItem)?.CommandParameter as ArtistModel
                  ?? (sender as FrameworkElement)?.Tag as ArtistModel 
                  ?? (sender as FrameworkElement)?.DataContext as ArtistModel;
        if (artist != null && ViewModel != null)
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(artist);
    }

    private void OnPlaylistContextPlayClick(object sender, RoutedEventArgs e)
    {
        var playlist = (sender as MenuFlyoutItem)?.CommandParameter as PlaylistModel
                    ?? (sender as FrameworkElement)?.Tag as PlaylistModel 
                    ?? (sender as FrameworkElement)?.DataContext as PlaylistModel;
        if (playlist != null && ViewModel != null)
            _ = ViewModel.PlayPlaylistCommand.ExecuteAsync(playlist);
    }
    // ----------------------------

    private void OnArtistBackClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.GoBackCommand.Execute(null);
            var item = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == ViewModel.CurrentView);
            if (item != null)
            {
                NavView.SelectedItem = item;
            }
        }
    }

    private void OnSearchArtistSongsClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CurrentArtist != null)
        {
            ViewModel.SearchQuery = ViewModel.CurrentArtist.Name;
            ViewModel.PerformSearchCommand.Execute(null);
            NavView.SelectedItem = null;
        }
    }

    private void OnSongListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SongModel song && ViewModel != null)
        {
            _ = ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }

    private void OnQueueItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SongModel song && ViewModel != null)
        {
            _ = ViewModel.PlayFromQueueCommand.ExecuteAsync(song);
        }
    }

    private void OnLibraryTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is RadioButtons rb && rb.SelectedItem is RadioButton item && item.Tag is string tab && ViewModel != null)
        {
            ViewModel.LibraryTab = tab;
        }
    }

    private void OnSliderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Slider slider && ViewModel != null)
        {
            ViewModel.SeekTo(TimeSpan.FromSeconds(slider.Value));
        }
    }

    private void OnVolumePointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Slider slider && ViewModel != null)
        {
            var delta = e.GetCurrentPoint(slider).Properties.MouseWheelDelta;
            var change = (delta > 0 ? 0.05 : -0.05);
            var newVol = Math.Clamp(slider.Value + change, 0, 1);
            slider.Value = newVol;
            e.Handled = true;
        }
    }

    private void OnGoToLoginClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.CurrentView = "Login";
            var loginItem = NavView.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == "Login");
            if (loginItem != null)
            {
                NavView.SelectedItem = loginItem;
            }
        }
    }

    private async void LoginWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (ViewModel == null) return;
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
                    await ViewModel.SaveSessionAsync(targetCookies);
                    ViewModel.CurrentView = "Home";
                    NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting cookies: {ex.Message}");
        }
    }
}
