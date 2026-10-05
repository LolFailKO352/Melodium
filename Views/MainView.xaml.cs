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
            if (tag == "Login")
            {
                if (ViewModel?.IsLoggedIn == true)
                {
                    AccountNavItem?.ContextFlyout?.ShowAt(AccountNavItem);
                }
                else
                {
                    OpenLoginWindow();
                    var activeItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == ViewModel?.CurrentView);
                    if (activeItem != null)
                    {
                        NavView.SelectedItem = activeItem;
                    }
                }
                return;
            }
            ViewModel?.NavigateCommand.Execute(tag);
        }
    }

    private DispatcherTimer? _searchDebounceTimer;

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _searchDebounceTimer?.Stop();
        string query = !string.IsNullOrWhiteSpace(args.QueryText) ? args.QueryText : sender.Text;
        ExecuteSearch(query);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange) return;

        if (string.IsNullOrWhiteSpace(sender.Text))
        {
            _searchDebounceTimer?.Stop();
            if (ViewModel?.CurrentView == "Search")
            {
                OnBackToHomeClick(sender, new RoutedEventArgs());
            }
            return;
        }

        // Live vyhledávání při psaní (debounce 450 ms)
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer?.Stop();
                ExecuteSearch(sender.Text);
            };
            _searchDebounceTimer.Start();
        }
    }

    private void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _searchDebounceTimer?.Stop();
            ExecuteSearch(SearchBox.Text);
            e.Handled = true;
        }
    }

    private void ExecuteSearch(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || ViewModel == null) return;
        _searchDebounceTimer?.Stop();
        ViewModel.SearchQuery = query.Trim();
        ViewModel.PerformSearchCommand.Execute(null);

        // Odznačit položky v navigaci (vyhledávání má vlastní zobrazení)
        NavView.SelectedItem = null;
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
        var playlist = element?.Tag as PlaylistModel 
                    ?? element?.DataContext as PlaylistModel
                    ?? (sender as MenuFlyoutItem)?.CommandParameter as PlaylistModel;
        if (playlist != null && ViewModel != null)
        {
            _ = ViewModel.OpenPlaylistCommand.ExecuteAsync(playlist);
        }
    }

    private void OnHomeItemClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var item = element?.Tag as HomeItemModel 
                ?? element?.DataContext as HomeItemModel 
                ?? (sender as MenuFlyoutItem)?.CommandParameter as HomeItemModel;

        if (item == null || ViewModel == null) return;

        switch (item.Type)
        {
            case HomeItemType.Song:
                if (item.Song != null)
                {
                    _ = ViewModel.PlaySongCommand.ExecuteAsync(item.Song);
                }
                break;
            case HomeItemType.Playlist:
                if (item.Playlist != null)
                {
                    _ = ViewModel.OpenPlaylistCommand.ExecuteAsync(item.Playlist);
                }
                break;
            case HomeItemType.Album:
                if (item.Album != null)
                {
                    _ = ViewModel.PlayAlbumCommand.ExecuteAsync(item.Album);
                }
                break;
            case HomeItemType.Artist:
                if (item.Artist != null)
                {
                    _ = ViewModel.OpenArtistCommand.ExecuteAsync(item.Artist);
                }
                break;
        }
    }

    private void OnHomeItemPlayClick(object sender, RoutedEventArgs e)
    {
        var item = (sender as FrameworkElement)?.Tag as HomeItemModel 
                ?? (sender as FrameworkElement)?.DataContext as HomeItemModel 
                ?? (sender as MenuFlyoutItem)?.CommandParameter as HomeItemModel;
        if (item == null || ViewModel == null) return;

        if (item.IsSong && item.Song != null)
        {
            _ = ViewModel.PlaySongCommand.ExecuteAsync(item.Song);
        }
        else if (item.IsPlaylist && item.Playlist != null)
        {
            _ = ViewModel.PlayPlaylistCommand.ExecuteAsync(item.Playlist);
        }
        else if (item.IsAlbum && item.Album != null)
        {
            _ = ViewModel.PlayAlbumCommand.ExecuteAsync(item.Album);
        }
        else if (item.IsArtist && item.Artist != null)
        {
            _ = ViewModel.OpenArtistCommand.ExecuteAsync(item.Artist);
        }
    }

    private void OnHomeItemPlayNextClick(object sender, RoutedEventArgs e)
    {
        var item = (sender as FrameworkElement)?.Tag as HomeItemModel 
                ?? (sender as FrameworkElement)?.DataContext as HomeItemModel 
                ?? (sender as MenuFlyoutItem)?.CommandParameter as HomeItemModel;
        if (item?.Song != null && ViewModel != null)
        {
            ViewModel.PlaySongNextCommand.Execute(item.Song);
        }
    }

    private void OnHomeItemAddToQueueClick(object sender, RoutedEventArgs e)
    {
        var item = (sender as FrameworkElement)?.Tag as HomeItemModel 
                ?? (sender as FrameworkElement)?.DataContext as HomeItemModel 
                ?? (sender as MenuFlyoutItem)?.CommandParameter as HomeItemModel;
        if (item?.Song != null && ViewModel != null)
        {
            ViewModel.AddToQueueCommand.Execute(item.Song);
        }
    }

    private void OnHomeItemAddToPlaylistClick(object sender, RoutedEventArgs e)
    {
        var item = (sender as FrameworkElement)?.Tag as HomeItemModel 
                ?? (sender as FrameworkElement)?.DataContext as HomeItemModel 
                ?? (sender as MenuFlyoutItem)?.CommandParameter as HomeItemModel;
        if (item?.Song != null)
        {
            var dummyItem = new MenuFlyoutItem { CommandParameter = item.Song };
            OnSongContextAddToPlaylistClick(dummyItem, e);
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

    private void OnMoodCardClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var mood = element?.Tag as MoodModel ?? element?.DataContext as MoodModel;
        if (mood != null && ViewModel != null)
        {
            _ = ViewModel.PlayMoodCommand.ExecuteAsync(mood);
        }
    }

    private void OnMoodFilterClick(object sender, RoutedEventArgs e)
    {
        var element = sender as FrameworkElement;
        var filter = element?.Tag as MoodFilterModel ?? element?.DataContext as MoodFilterModel;
        if (filter != null && ViewModel != null)
        {
            _ = ViewModel.SelectMoodAsync(filter.Title);
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

    private async void OnSongContextAddToPlaylistClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as MenuFlyoutItem)?.CommandParameter as SongModel
                ?? (sender as FrameworkElement)?.Tag as SongModel
                ?? (sender as FrameworkElement)?.DataContext as SongModel;
        if (song == null || ViewModel == null) return;

        // Pokud nemáme žádné upravitelné playlisty nebo knihovna není ještě načtena, zkusit načíst
        if (ViewModel.EditablePlaylists.Count == 0 || ViewModel.LibraryPlaylists.Count == 0)
        {
            await ViewModel.RefreshEditablePlaylistsAsync(song.VideoId);
        }
        else
        {
            ViewModel.UpdateEditablePlaylists();
        }

        if (ViewModel.EditablePlaylists.Count == 0)
        {
            // Druhý pokus – načtení přímo knihovních playlistů
            await ViewModel.RefreshEditablePlaylistsAsync();
        }

        if (ViewModel.EditablePlaylists.Count == 0)
        {
            var noPlaylistsDialog = new ContentDialog
            {
                Title = "Přidat do playlistu",
                Content = "V knihovně zatím nemáte žádné playlisty, do kterých lze přidávat skladby. Chcete nyní vytvořit nový playlist na YouTube Music a přidat do něj tuto skladbu?",
                PrimaryButtonText = "Vytvořit nový playlist",
                CloseButtonText = "Zrušit",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var askResult = await noPlaylistsDialog.ShowAsync();
            if (askResult == ContentDialogResult.Primary)
            {
                await PromptCreatePlaylistAndAddSongAsync(song);
            }
            return;
        }

        var stackPanel = new StackPanel { Spacing = 8 };
        stackPanel.Children.Add(new TextBlock { Text = $"Vyberte playlist pro přidání skladby '{song.Title}':", Margin = new Thickness(0, 0, 0, 8) });

        var listView = new ListView
        {
            ItemsSource = ViewModel.EditablePlaylists,
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 300
        };

        if (ViewModel.EditablePlaylists.Count > 0)
        {
            listView.SelectedIndex = 0;
        }

        listView.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
            <DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                <Grid ColumnDefinitions=""40, *"" Margin=""0,4"">
                    <Border Grid.Column=""0"" CornerRadius=""4"">
                        <Image Source=""{Binding ThumbnailUrl}"" Width=""36"" Height=""36"" Stretch=""UniformToFill"" />
                    </Border>
                    <StackPanel Grid.Column=""1"" Margin=""10,0,0,0"" VerticalAlignment=""Center"">
                        <TextBlock Text=""{Binding Title}"" FontWeight=""SemiBold"" FontSize=""13"" TextTrimming=""CharacterEllipsis"" />
                        <TextBlock Text=""{Binding Creator}"" FontSize=""11"" Foreground=""{ThemeResource TextFillColorSecondaryBrush}"" />
                    </StackPanel>
                </Grid>
            </DataTemplate>");

        stackPanel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = "Přidat do playlistu",
            Content = stackPanel,
            PrimaryButtonText = "Přidat",
            SecondaryButtonText = "Nový playlist",
            CloseButtonText = "Zrušit",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && listView.SelectedItem is PlaylistModel selectedPlaylist)
        {
            var addResult = await ViewModel.AddSongToPlaylistAsync(selectedPlaylist, song);
            if (addResult.IsDuplicate)
            {
                var dupDialog = new ContentDialog
                {
                    Title = "Skladba již v playlistu je",
                    Content = addResult.Message,
                    CloseButtonText = "Rozumím",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                await dupDialog.ShowAsync();
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await PromptCreatePlaylistAndAddSongAsync(song);
        }
    }

    private async Task PromptCreatePlaylistAndAddSongAsync(SongModel? song = null)
    {
        if (ViewModel == null) return;

        var inputPanel = new StackPanel { Spacing = 10 };
        var titleBox = new TextBox 
        { 
            PlaceholderText = "Název playlistu",
            Margin = new Thickness(0, 4, 0, 0)
        };
        var descBox = new TextBox 
        { 
            PlaceholderText = "Popis (volitelné)",
            AcceptsReturn = false
        };

        inputPanel.Children.Add(new TextBlock { Text = "Zadejte název nového playlistu na YouTube Music:" });
        inputPanel.Children.Add(titleBox);
        inputPanel.Children.Add(descBox);

        var createDialog = new ContentDialog
        {
            Title = "Nový playlist",
            Content = inputPanel,
            PrimaryButtonText = "Vytvořit",
            CloseButtonText = "Zrušit",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var res = await createDialog.ShowAsync();
        if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(titleBox.Text))
        {
            var newPlaylist = await ViewModel.CreatePlaylistAsync(titleBox.Text.Trim(), descBox.Text?.Trim() ?? "");
            if (newPlaylist != null && song != null)
            {
                await ViewModel.AddSongToPlaylistAsync(newPlaylist, song);
            }
        }
    }

    private async void OnCreatePlaylistClick(object sender, RoutedEventArgs e)
    {
        await PromptCreatePlaylistAndAddSongAsync(null);
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

    private void OnPlaylistBackClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ClosePlaylistCommand.Execute(null);
            var item = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == ViewModel.CurrentView);
            if (item != null)
            {
                NavView.SelectedItem = item;
            }
        }
    }

    private void OnPlaylistSongItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SongModel song && ViewModel != null)
        {
            _ = ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }

    private void OnPlaylistSongMoveUpClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as FrameworkElement)?.Tag as SongModel
                ?? (sender as MenuFlyoutItem)?.CommandParameter as SongModel;
        if (song != null && ViewModel != null)
        {
            _ = ViewModel.MoveSongUpCommand.ExecuteAsync(song);
        }
    }

    private void OnPlaylistSongMoveDownClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as FrameworkElement)?.Tag as SongModel
                ?? (sender as MenuFlyoutItem)?.CommandParameter as SongModel;
        if (song != null && ViewModel != null)
        {
            _ = ViewModel.MoveSongDownCommand.ExecuteAsync(song);
        }
    }

    private void OnPlaylistSongRemoveClick(object sender, RoutedEventArgs e)
    {
        var song = (sender as FrameworkElement)?.Tag as SongModel
                ?? (sender as MenuFlyoutItem)?.CommandParameter as SongModel;
        if (song != null && ViewModel != null)
        {
            _ = ViewModel.RemoveSongFromCurrentPlaylistCommand.ExecuteAsync(song);
        }
    }

    private void OnAddSongSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText) && ViewModel != null)
        {
            _ = ViewModel.SearchSongsToAddToPlaylistCommand.ExecuteAsync(args.QueryText);
        }
    }

    private void OnAddSongSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(sender.Text) && ViewModel != null)
        {
            _ = ViewModel.SearchSongsToAddToPlaylistCommand.ExecuteAsync(sender.Text);
        }
    }

    private async void OnSearchResultItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SongModel song && ViewModel != null)
        {
            if (ViewModel.CurrentPlaylistSongs.Any(s => s.VideoId == song.VideoId))
            {
                var dupDialog = new ContentDialog
                {
                    Title = "Skladba již v playlistu je",
                    Content = $"Skladba '{song.Title}' se v playlistu již nachází.",
                    CloseButtonText = "Rozumím",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                await dupDialog.ShowAsync();
                return;
            }

            await ViewModel.AddSongToCurrentPlaylistCommand.ExecuteAsync(song);
        }
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

    private void OnLikedSongListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SongModel song && ViewModel != null)
        {
            _ = ViewModel.PlayLikedSongItemCommand.ExecuteAsync(song);
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

    private LoginWindow? _loginWindow;

    private void OpenLoginWindow()
    {
        if (ViewModel == null) return;
        if (_loginWindow != null)
        {
            _loginWindow.Activate();
            return;
        }

        _loginWindow = new LoginWindow(ViewModel);
        _loginWindow.Closed += (s, e) =>
        {
            _loginWindow = null;
        };
        _loginWindow.Activate();
    }

    private void OnGoToLoginClick(object sender, RoutedEventArgs e)
    {
        OpenLoginWindow();
    }

    private async void OnMainViewManualCookieClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

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
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var parsedCookies = LoginWindow.ParseCookies(textBox.Text);
            bool hasAuth = parsedCookies.Any(c => c.Name == "SAPISID" || c.Name == "__Secure-3PAPISID" || c.Name == "SID");

            if (hasAuth)
            {
                await ViewModel.SaveSessionAsync(parsedCookies);
                ViewModel.CurrentView = "Home";
                NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            }
            else
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Neplatné cookies",
                    Content = "Zadaný text neobsahuje potřebné přihlašovací tokeny (SAPISID, __Secure-3PAPISID ani SID).",
                    CloseButtonText = "Rozumím",
                    XamlRoot = this.XamlRoot
                };
                _ = errorDialog.ShowAsync();
            }
        }
    }
}
