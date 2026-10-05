using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Melodium.Models;
using Melodium.Services;
using System.Globalization;
using System.Diagnostics;

namespace Melodium.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MelodiumService _ytService;
    private readonly IAudioService _audioService;
    private readonly TranslationService _translationService;
    private readonly UpdateService _updateService;
    private UpdateCheckResult? _lastUpdateResult;
    private System.Threading.CancellationTokenSource? _updateDownloadCts;
    private System.Threading.CancellationTokenSource? _downloadCts;
    private System.Threading.CancellationTokenSource? _prefetchCts;
    private readonly List<SongModel> _originalQueue = new();

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; } = "Připraveno. Zvol sekci a hraj.";

    // Navigace a přihlášení
    [ObservableProperty]
    public partial string CurrentView { get; set; } = "Home"; // Home, Search, Library, Login

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoggedIn))]
    [NotifyPropertyChangedFor(nameof(UserAccountTooltip))]
    public partial bool IsLoggedIn { get; set; }

    public bool IsNotLoggedIn => !IsLoggedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserAccountTooltip))]
    public partial string UserProfileName { get; set; } = "Nepřihlášen";

    public string UserAccountTooltip => IsLoggedIn
        ? $"Přihlášen jako: {UserProfileName}"
        : "Nepřihlášen (kliknutím se přihlásíte)";

    // --- Lokalizace ---
    public ObservableCollection<CultureInfo> Languages { get; } = new();

    [ObservableProperty]
    public partial CultureInfo? SelectedLanguage { get; set; }

    partial void OnSelectedLanguageChanged(CultureInfo? value)
    {
        if (value != null)
        {
            Preferences.Default.Set("AppLanguage", value.Name);
            _ = UpdateLocalizedStringsAsync(value);
            LanguageChanged?.Invoke(this, value);
        }
    }
    
    public static event EventHandler<CultureInfo>? LanguageChanged;

    [ObservableProperty] public partial string TextHome { get; set; } = "Home";
    [ObservableProperty] public partial string TextExplore { get; set; } = "Explore 🎶";
    [ObservableProperty] public partial string TextSearch { get; set; } = "Search";
    [ObservableProperty] public partial string TextLibrary { get; set; } = "Library";
    [ObservableProperty] public partial string TextQueue { get; set; } = "Queue";
    [ObservableProperty] public partial string TextAccount { get; set; } = "Account";
    [ObservableProperty] public partial string TextLogout { get; set; } = "Log out";
    [ObservableProperty] public partial string TextLogin { get; set; } = "Log in";
    [ObservableProperty] public partial string TextSettings { get; set; } = "Settings";
    [ObservableProperty] public partial string TextLanguageSelection { get; set; } = "Language Selection";
    
    [ObservableProperty] public partial bool IsDiscordRpcEnabled { get; set; }
    [ObservableProperty] public partial string TextEnableDiscordRpc { get; set; } = "Display activity on Discord";

    partial void OnIsDiscordRpcEnabledChanged(bool value)
    {
        Preferences.Default.Set("IsDiscordRpcEnabled", value);
        _discordRpcService?.HandleSettingsChanged();
    }

    [ObservableProperty] public partial bool IsCloseToTrayEnabled { get; set; } = false;
    [ObservableProperty] public partial string TextCloseToTray { get; set; } = "Minimize to system tray";
    [ObservableProperty] public partial string TextCloseToTrayDesc { get; set; } = "When closing the window, keep Melodium running in the notification area.";

    partial void OnIsCloseToTrayEnabledChanged(bool value)
    {
        Preferences.Default.Set("IsCloseToTrayEnabled", value);
    }

    [ObservableProperty] public partial string TextExitApp { get; set; } = "Exit Application";
    [ObservableProperty] public partial string TextExitAppDesc { get; set; } = "Completely exits Melodium and frees all background processes.";
    [ObservableProperty] public partial string TextExitButton { get; set; } = "Exit application";

    // --- Aktualizace aplikace ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppVersionDisplay))]
    public partial string CurrentAppVersion { get; set; } = "1.6";

    public string AppVersionDisplay => $"Version {CurrentAppVersion} (Windows App SDK)";

    [ObservableProperty] public partial string LatestAppVersion { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartUpdateCheck))]
    public partial bool IsUpdateCheckInProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartUpdateCheck))]
    [NotifyPropertyChangedFor(nameof(CanStartDownload))]
    public partial bool IsUpdateDownloading { get; set; }

    public bool CanStartUpdateCheck => !IsUpdateCheckInProgress && !IsUpdateDownloading;
    public bool CanStartDownload => !IsUpdateDownloading;

    [ObservableProperty] public partial double UpdateDownloadProgress { get; set; }
    [ObservableProperty] public partial string UpdateDownloadProgressText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsUpdateAvailable { get; set; }
    [ObservableProperty] public partial bool IsUpToDate { get; set; }
    [ObservableProperty] public partial bool HasUpdateError { get; set; }
    [ObservableProperty] public partial string UpdateStatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string UpdateReleaseNotes { get; set; } = string.Empty;
    [ObservableProperty] public partial string UpdateReleaseUrl { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsUpdateNotificationOpen { get; set; }
    [ObservableProperty] public partial string UpdateNotificationTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string UpdateNotificationMessage { get; set; } = string.Empty;

    [ObservableProperty] public partial string TextUpdateSettings { get; set; } = "Application Updates";
    [ObservableProperty] public partial string TextCheckForUpdates { get; set; } = "Check for updates";
    [ObservableProperty] public partial string TextCheckForUpdatesDesc { get; set; } = "Checks for the latest Melodium release on GitHub.";
    [ObservableProperty] public partial string TextCheckingForUpdates { get; set; } = "Checking for updates...";
    [ObservableProperty] public partial string TextAppUpToDate { get; set; } = "Melodium is up to date.";
    [ObservableProperty] public partial string TextUpdateAvailable { get; set; } = "A new update is available!";
    [ObservableProperty] public partial string TextDownloadAndInstall { get; set; } = "Download and update";
    [ObservableProperty] public partial string TextDownloadAndInstallDesc { get; set; } = "Downloads and launches the update installer.";
    [ObservableProperty] public partial string TextDownloadingUpdate { get; set; } = "Downloading update...";
    [ObservableProperty] public partial string TextCurrentVersionLabel { get; set; } = "Installed version:";
    [ObservableProperty] public partial string TextLatestVersionLabel { get; set; } = "Latest version on GitHub:";
    [ObservableProperty] public partial string TextChangelogLabel { get; set; } = "Changelog:";
    [ObservableProperty] public partial string TextOpenOnGitHub { get; set; } = "View on GitHub";
    
    [ObservableProperty] public partial string TextSearchPlaceholder { get; set; } = "Search songs, artists, albums...";
    [ObservableProperty] public partial string TextLanguageDescription { get; set; } = "Select your preferred language. All world languages are supported.";
    [ObservableProperty] public partial string TextHeroSubtitle { get; set; } = "Listen to music without limits and without ads";
    [ObservableProperty] public partial string TextStartListening { get; set; } = "Start listening";
    [ObservableProperty] public partial string TextRecommendedMusic { get; set; } = "Recommended music >";
    [ObservableProperty] public partial string TextLoadingRecommendations { get; set; } = "Loading recommendations...";
    [ObservableProperty] public partial string TextPersonalizedSongs { get; set; } = "Recommended tracks just for you";
    [ObservableProperty] public partial string TextLockedLibrary { get; set; } = "Locked Library";
    [ObservableProperty] public partial string TextLockedLibraryDesc { get; set; } = "To view your songs, playlists, and albums from Melodium, please log in.";
    [ObservableProperty] public partial string TextGoToLogin { get; set; } = "Go to login";
    [ObservableProperty] public partial string TextMusic { get; set; } = "Music";
    [ObservableProperty] public partial string TextLikedSongs { get; set; } = "Liked Songs";
    [ObservableProperty] public partial string TextSongs { get; set; } = "Songs";
    [ObservableProperty] public partial string TextPlaylists { get; set; } = "Playlists";
    [ObservableProperty] public partial string TextAlbums { get; set; } = "Albums";
    [ObservableProperty] public partial string TextArtists { get; set; } = "Artists";
    [ObservableProperty] public partial string TextAddFolder { get; set; } = "Add folder";
    [ObservableProperty] public partial string TextShuffleAndPlay { get; set; } = "Shuffle & Play";
    [ObservableProperty] public partial string TextSortBy { get; set; } = "Sort by: Title";
    [ObservableProperty] public partial string TextUnknownGenre { get; set; } = "Unknown genre";
    [ObservableProperty] public partial string TextPlay { get; set; } = "Play";
    [ObservableProperty] public partial string TextPlayAll { get; set; } = "Play all";
    [ObservableProperty] public partial string TextLoginInstructions { get; set; } = "Login Instructions";
    [ObservableProperty] public partial string TextLoginInstruction1 { get; set; } = "1. Sign in to your Google / YouTube account in the window below.";
    [ObservableProperty] public partial string TextLoginInstruction2 { get; set; } = "2. Once signed in, Melodium will automatically connect and load your personal library.";
    [ObservableProperty] public partial string TextPlaybackQueue { get; set; } = "Playback Queue";
    [ObservableProperty] public partial string TextClearQueue { get; set; } = "Clear queue";
    [ObservableProperty] public partial string TextEmptyQueue { get; set; } = "Queue is empty";
    [ObservableProperty] public partial string TextEmptyQueueDesc { get; set; } = "Find some songs and start listening.";
    [ObservableProperty] public partial string TextRemoveFromQueue { get; set; } = "Remove from queue";
    [ObservableProperty] public partial string TextSongsCountLabel { get; set; } = "Songs:";
    [ObservableProperty] public partial string TextReleaseYearLabel { get; set; } = "Year:";
    [ObservableProperty] public partial string TextSubscribersLabel { get; set; } = "Subscribers:";
    [ObservableProperty] public partial string TextSongsInQueueLabel { get; set; } = "Songs in queue:";
    [ObservableProperty] public partial string TextStatusReady { get; set; } = "Ready. Select music and enjoy.";
    [ObservableProperty] public partial string TextStatusLibraryEmpty { get; set; } = "Library is empty, playing recommendations...";
    [ObservableProperty] public partial string TextStatusShuffleFailed { get; set; } = "Cannot shuffle - no songs available.";
    [ObservableProperty] public partial string TextStatusLibraryLoaded { get; set; } = "Your library and recommendations were loaded.";

    [ObservableProperty] public partial string TextStartMix { get; set; } = "Start radio";
    [ObservableProperty] public partial string TextPlayNext { get; set; } = "Play next";
    [ObservableProperty] public partial string TextAddToQueue { get; set; } = "Add to queue";
    [ObservableProperty] public partial string TextSaveToPlaylist { get; set; } = "Add to playlist";
    [ObservableProperty] public partial string TextGoToAlbum { get; set; } = "Go to album";
    [ObservableProperty] public partial string TextGoToArtist { get; set; } = "Go to artist";
    [ObservableProperty] public partial string TextShare { get; set; } = "Share";
    [ObservableProperty] public partial string TextShuffle { get; set; } = "Shuffle";

    [ObservableProperty] public partial string TextDiscoverNewMusic { get; set; } = "Discover new music";
    [ObservableProperty] public partial string TextDiscoverSubtitle { get; set; } = "Latest hits, global charts, and playlists for every mood.";
    [ObservableProperty] public partial string TextPlayCharts { get; set; } = "Play charts 🎶";
    [ObservableProperty] public partial string TextShuffleCharts { get; set; } = "Shuffle charts 🔀";
    [ObservableProperty] public partial string TextMoodsAndGenres { get; set; } = "Moods & Genres";
    [ObservableProperty] public partial string TextChartsAndTrends { get; set; } = "Charts & Trends 🎶";
    [ObservableProperty] public partial string TextRefresh { get; set; } = "Refresh";
    [ObservableProperty] public partial string TextSearchResults { get; set; } = "Search results";
    [ObservableProperty] public partial string TextBackToHome { get; set; } = "Back to home";

    [ObservableProperty] public partial string TextFilterAll { get; set; } = "All";
    [ObservableProperty] public partial string TextFilterRelax { get; set; } = "Relax";
    [ObservableProperty] public partial string TextFilterEnergy { get; set; } = "Energy";
    [ObservableProperty] public partial string TextFilterWorkout { get; set; } = "Workout";
    [ObservableProperty] public partial string TextFilterFocus { get; set; } = "Focus";
    [ObservableProperty] public partial string TextAddSongToPlaylist { get; set; } = "Add song to playlist";
    [ObservableProperty] public partial string TextAddSong { get; set; } = "Add song";
    [ObservableProperty] public partial string TextNoResults { get; set; } = "No results found";
    [ObservableProperty] public partial string TextNoResultsDesc { get; set; } = "Try searching for a different artist, song title, or album.";
    [ObservableProperty] public partial string TextOpenFullPage { get; set; } = "Open full page";
    [ObservableProperty] public partial string TextOpenPlaylist { get; set; } = "Open playlist";
    [ObservableProperty] public partial string TextEditable { get; set; } = "• Editable";
    [ObservableProperty] public partial string TextPopularSongs { get; set; } = "Popular songs";
    [ObservableProperty] public partial string TextShowAll { get; set; } = "Show all";
    [ObservableProperty] public partial string TextSinglesAndEps { get; set; } = "Singles & EPs";
    [ObservableProperty] public partial string TextBack { get; set; } = "Back";
    [ObservableProperty] public partial string TextEditablePlaylist { get; set; } = "Editable playlist";
    [ObservableProperty] public partial string TextReadOnlyPlaylist { get; set; } = "Shared playlist • Read-only";
    [ObservableProperty] public partial string TextSearchAndAddSong { get; set; } = "Search and add song";
    [ObservableProperty] public partial string TextSearchSongInYtMusic { get; set; } = "Search songs on YouTube Music...";
    [ObservableProperty] public partial string TextSongsInPlaylist { get; set; } = "Songs in playlist";
    [ObservableProperty] public partial string TextMoveUp { get; set; } = "Move up";
    [ObservableProperty] public partial string TextMoveDown { get; set; } = "Move down";
    [ObservableProperty] public partial string TextRemoveFromPlaylist { get; set; } = "Remove from playlist";
    [ObservableProperty] public partial string TextNoSongPlaying { get; set; } = "No song playing";
    [ObservableProperty] public partial string TextPreviousSong { get; set; } = "Previous track";
    [ObservableProperty] public partial string TextNextSong { get; set; } = "Next track";
    [ObservableProperty] public partial string TextPlayPause { get; set; } = "Play / Pause";
    [ObservableProperty] public partial string TextRepeatMode { get; set; } = "Repeat mode";
    [ObservableProperty] public partial string TextMute { get; set; } = "Mute / Unmute";
    [ObservableProperty] public partial string TextFullScreen { get; set; } = "Full screen";
    [ObservableProperty] public partial string TextCloseFullScreen { get; set; } = "Close full screen";
    [ObservableProperty] public partial string TextLyrics { get; set; } = "Lyrics";
    [ObservableProperty] public partial string TextLikeSong { get; set; } = "Like";
    [ObservableProperty] public partial string TextDislikeSong { get; set; } = "Dislike";
    [ObservableProperty] public partial string TextAutoplayTooltip { get; set; } = "Infinite Radio (Autoplay) 🎶 - Automatically plays similar songs when queue ends";
    [ObservableProperty] public partial string TextReloadLyrics { get; set; } = "Reload lyrics";
    [ObservableProperty] public partial string TextNoLyricsFound { get; set; } = "No lyrics found for this song.";
    [ObservableProperty] public partial string TextTryAgainOrSelectOther { get; set; } = "Try again or select another song.";
    [ObservableProperty] public partial string TextTryAgain { get; set; } = "Try again";
    [ObservableProperty] public partial string TextLoadingLyrics { get; set; } = "Loading lyrics...";
    [ObservableProperty] public partial string TextShowHideWindow { get; set; } = "Show / Hide window";

    private async Task UpdateLocalizedStringsAsync(CultureInfo culture)
    {
        string targetLang = culture.TwoLetterISOLanguageName;
        var dict = await _translationService.GetDictionaryAsync(targetLang);

        // Aktualizovat centrální Loc instanci pro DataTemplates
        Loc.Instance.ApplyDictionary(dict);

        // Aktualizovat properties ve ViewModelu na MainThread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var type = this.GetType();
            foreach (var kvp in dict)
            {
                var prop = type.GetProperty(kvp.Key);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(this, kvp.Value);
                }
            }

            RefreshMoodFiltersForLanguage();
        });
    }

    private void RefreshMoodFiltersForLanguage()
    {
        if (MoodFilters.Count >= 5)
        {
            MoodFilters[0].Title = TextFilterAll;
            MoodFilters[1].Title = TextFilterRelax;
            MoodFilters[2].Title = TextFilterEnergy;
            MoodFilters[3].Title = TextFilterWorkout;
            MoodFilters[4].Title = TextFilterFocus;
        }
    }
    // --- Konec lokalizace ---

    // Přehrávač
    [ObservableProperty]
    public partial bool IsFullScreenPlayerVisible { get; set; }

    [RelayCommand]
    private void ToggleFullScreenPlayer()
    {
        IsFullScreenPlayerVisible = !IsFullScreenPlayerVisible;
    }

    [RelayCommand]
    private void ExitApplication()
    {
        App.ExitApplication();
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        if (IsUpdateCheckInProgress || IsUpdateDownloading) return;

        IsUpdateCheckInProgress = true;
        HasUpdateError = false;
        IsUpdateAvailable = false;
        IsUpToDate = false;
        UpdateStatusMessage = TextCheckingForUpdates;

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            _lastUpdateResult = result;

            if (!result.IsSuccess)
            {
                HasUpdateError = true;
                UpdateStatusMessage = result.ErrorMessage ?? "Chyba při kontrole aktualizací.";
            }
            else
            {
                LatestAppVersion = result.LatestVersionTag ?? result.LatestVersion?.ToString() ?? "";
                UpdateReleaseNotes = result.ReleaseNotes ?? string.Empty;
                UpdateReleaseUrl = result.ReleaseUrl ?? string.Empty;

                if (result.IsUpdateAvailable)
                {
                    IsUpdateAvailable = true;
                    UpdateStatusMessage = $"{TextUpdateAvailable} ({LatestAppVersion})";

                    UpdateNotificationTitle = $"K dispozici je nová verze {LatestAppVersion}";
                    UpdateNotificationMessage = !string.IsNullOrWhiteSpace(result.ReleaseTitle) && result.ReleaseTitle != LatestAppVersion
                        ? $"{result.ReleaseTitle} – kliknutím na tlačítko spustíte stažení a automatickou instalaci."
                        : "Byla nalezena nová verze aplikace Melodium. Chcete ji nyní stáhnout a aktualizovat?";
                    IsUpdateNotificationOpen = true;
                }
                else
                {
                    IsUpToDate = true;
                    UpdateStatusMessage = $"{TextAppUpToDate} ({CurrentAppVersion})";
                    IsUpdateNotificationOpen = false;
                }
            }
        }
        catch (Exception ex)
        {
            HasUpdateError = true;
            UpdateStatusMessage = $"Chyba: {ex.Message}";
        }
        finally
        {
            IsUpdateCheckInProgress = false;
        }
    }

    public async Task CheckForUpdatesOnStartupAsync()
    {
        // Počkáme pár sekund po startu, aby aplikace hladce načetla rozhraní a knihovnu
        await Task.Delay(4000);

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            if (result != null && result.IsSuccess && result.IsUpdateAvailable)
            {
                _lastUpdateResult = result;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LatestAppVersion = result.LatestVersionTag ?? result.LatestVersion?.ToString() ?? "";
                    UpdateReleaseNotes = result.ReleaseNotes ?? string.Empty;
                    UpdateReleaseUrl = result.ReleaseUrl ?? string.Empty;
                    IsUpdateAvailable = true;
                    UpdateStatusMessage = $"{TextUpdateAvailable} ({LatestAppVersion})";

                    UpdateNotificationTitle = $"Dostupná nová verze {LatestAppVersion}";
                    UpdateNotificationMessage = !string.IsNullOrWhiteSpace(result.ReleaseTitle) && result.ReleaseTitle != LatestAppVersion
                        ? $"{result.ReleaseTitle} – kliknutím na tlačítko spustíte stažení a automatickou instalaci."
                        : "Byla vydána nová verze aplikace Melodium. Chcete ji nyní stáhnout a aktualizovat?";
                    IsUpdateNotificationOpen = true;
                });
            }
        }
        catch
        {
            // Tichá ignorace při chybě během startu (např. offline režim)
        }
    }

    [RelayCommand]
    public async Task DownloadAndInstallUpdateAsync()
    {
        if (IsUpdateDownloading) return;
        if (_lastUpdateResult == null || string.IsNullOrWhiteSpace(_lastUpdateResult.DownloadUrl))
        {
            OpenUpdateOnGitHub();
            return;
        }

        IsUpdateDownloading = true;
        UpdateDownloadProgress = 0;
        UpdateDownloadProgressText = "Příprava stahování...";
        HasUpdateError = false;
        UpdateStatusMessage = "Stahuji aktualizaci...";

        _updateDownloadCts = new System.Threading.CancellationTokenSource();

        var progress = new Progress<UpdateDownloadProgressInfo>(info =>
        {
            UpdateDownloadProgress = info.ProgressPercentage;
            string receivedMb = (info.BytesReceived / 1024.0 / 1024.0).ToString("0.0");
            string totalMb = info.TotalBytes > 0 ? (info.TotalBytes / 1024.0 / 1024.0).ToString("0.0") : "?";
            UpdateDownloadProgressText = $"{info.ProgressPercentage:0.0} % ({receivedMb} MB / {totalMb} MB)";
        });

        try
        {
            string downloadedFile = await _updateService.DownloadUpdateAsync(
                _lastUpdateResult.DownloadUrl,
                _lastUpdateResult.DownloadFileName ?? "Melodium-Setup.msi",
                progress,
                _updateDownloadCts.Token);

            UpdateStatusMessage = "Aktualizace stažena. Spouštím instalaci a restartuji aplikaci...";
            UpdateDownloadProgressText = "100 % – Instaluji...";
            await Task.Delay(400);

            _updateService.LaunchInstallerAndExit(downloadedFile);
        }
        catch (OperationCanceledException)
        {
            IsUpdateDownloading = false;
            UpdateStatusMessage = "Stahování aktualizace bylo zrušeno.";
        }
        catch (Exception ex)
        {
            HasUpdateError = true;
            UpdateStatusMessage = $"Chyba při stahování aktualizace: {ex.Message}";
            IsUpdateDownloading = false;
        }
    }

    [RelayCommand]
    public void OpenUpdateOnGitHub()
    {
        try
        {
            string url = !string.IsNullOrWhiteSpace(UpdateReleaseUrl)
                ? UpdateReleaseUrl
                : "https://github.com/LolFailKO352/Melodium/releases";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    public partial bool IsPlaying { get; set; }

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentSongNotNull))]
    [NotifyPropertyChangedFor(nameof(IsCurrentSongNull))]
    public partial SongModel? CurrentSong { get; set; }

    partial void OnCurrentSongChanged(SongModel? value)
    {
        _discordRpcService?.UpdatePlaybackState(value, IsPlaying);
    }

    public bool IsCurrentSongNotNull => CurrentSong != null;
    public bool IsCurrentSongNull => CurrentSong == null;

    [ObservableProperty]
    public partial SongModel? SelectedSong { get; set; }

    partial void OnSelectedSongChanged(SongModel? value)
    {
        if (value != null)
        {
            _ = PlaySongAsync(value);
            SelectedSong = null;
        }
    }

    [ObservableProperty]
    public partial string PlayPauseIcon { get; set; } = "▶";

    partial void OnIsPlayingChanged(bool value)
    {
        PlayPauseIcon = value ? "⏸" : "▶";
        _discordRpcService?.UpdatePlaybackState(CurrentSong, value);
    }

    [ObservableProperty]
    public partial string LibraryTab { get; set; } = "Liked"; // Liked, Songs, Playlists, Albums, Artists

    [RelayCommand]
    private void SetLibraryTab(string tabName)
    {
        LibraryTab = tabName;
    }

    [ObservableProperty]
    public partial double Volume { get; set; } = 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeGlyph))]
    public partial string VolumeIcon { get; set; } = "\uE767";

    public string VolumeGlyph => VolumeIcon;

    [ObservableProperty]
    public partial string VolumePercentageText { get; set; } = "50 %";

    partial void OnVolumeChanged(double value)
    {
        _audioService.Volume = (float)value;
        VolumePercentageText = $"{(int)(value * 100)} %";
        
        if (value <= 0)
            VolumeIcon = "\uE74F"; // Mute
        else if (value < 0.33)
            VolumeIcon = "\uE992"; // Volume 1
        else if (value < 0.66)
            VolumeIcon = "\uE993"; // Volume 2
        else
            VolumeIcon = "\uE767"; // Volume 3
    }

    [ObservableProperty]
    public partial string PositionText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string DurationText { get; set; } = "0:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressRatio))]
    public partial double PositionSeconds { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressRatio))]
    public partial double DurationSeconds { get; set; }

    public double ProgressRatio => DurationSeconds > 0 ? PositionSeconds / DurationSeconds : 0;

    // Kolekce
    public ObservableCollection<SongModel> SearchResults { get; } = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchResultsEmpty))]
    public partial bool HasSearched { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchResultsEmpty))]
    public partial bool HasSongResults { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchResultsEmpty))]
    public partial bool HasAlbumResults { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchResultsEmpty))]
    public partial bool HasArtistResults { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchResultsEmpty))]
    public partial bool HasPlaylistResults { get; set; }

    public bool IsSearchResultsEmpty => HasSearched && !IsBusy &&
        !HasSongResults &&
        !HasAlbumResults &&
        !HasArtistResults &&
        !HasPlaylistResults;

    [ObservableProperty]
    public partial ArtistDetailsModel? CurrentArtist { get; set; }

    [ObservableProperty]
    public partial bool IsArtistLoading { get; set; }

    [ObservableProperty]
    public partial PlaylistModel? CurrentPlaylist { get; set; }

    [ObservableProperty]
    public partial bool IsPlaylistLoading { get; set; }

    public ObservableCollection<SongModel> CurrentPlaylistSongs { get; } = new();

    [ObservableProperty]
    public partial string PlaylistSearchQuery { get; set; } = string.Empty;

    public ObservableCollection<SongModel> PlaylistSearchResults { get; } = new();

    [ObservableProperty]
    public partial bool IsSearchingSongsToAdd { get; set; }

    private string? _previousView;
    public ObservableCollection<HomeSectionModel> HomeSections { get; } = new();

    [ObservableProperty]
    public partial bool HasHomeSections { get; set; }

    public ObservableCollection<SongModel> HomeRecommendations { get; } = new();
    public ObservableCollection<SongModel> LikedSongs { get; } = new();

    [ObservableProperty]
    public partial bool IsLikedSongsLoading { get; set; }

    [ObservableProperty]
    public partial int LikedSongsCount { get; set; }

    public ObservableCollection<SongModel> LibrarySongs { get; } = new();
    public ObservableCollection<PlaylistModel> LibraryPlaylists { get; } = new();
    public ObservableCollection<PlaylistModel> EditablePlaylists { get; } = new();
    public ObservableCollection<AlbumModel> LibraryAlbums { get; } = new();
    public ObservableCollection<ArtistModel> LibraryArtists { get; } = new();
    public ObservableCollection<SongModel> PlaybackQueue { get; } = new();
    public ObservableCollection<SongModel> LibraryBasedRecommendations { get; } = new();

    [ObservableProperty]
    public partial bool HasPersonalizedRecommendations { get; set; }

    [ObservableProperty]
    public partial int CurrentQueueIndex { get; set; } = -1;

    [ObservableProperty]
    public partial bool IsShuffle { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatGlyph))]
    [NotifyPropertyChangedFor(nameof(IsRepeatActive))]
    public partial int RepeatMode { get; set; } // 0 = Off, 1 = Repeat Queue, 2 = Repeat Song

    public bool IsRepeatActive => RepeatMode > 0;

    public string RepeatGlyph => RepeatMode switch
    {
        1 => "\uE8EE",
        2 => "\uE8ED",
        _ => "\uE8EE"
    };

    public string QueueCountText => $"{TextSongsInQueueLabel} {PlaybackQueue.Count}";

    // --- Přehrávač: Autoplay & Like/Dislike 🎶 ---
    [ObservableProperty] public partial bool IsAutoplayEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSongLikeGlyph))]
    public partial bool IsCurrentSongLiked { get; set; }

    public string CurrentSongLikeGlyph => IsCurrentSongLiked ? "\uEB52" : "\uEB51";

    // --- Explore & Náladové kategorie 🎶 ---
    [ObservableProperty] public partial ObservableCollection<SongModel> ExploreCharts { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<MoodModel> MoodCategories { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<MoodFilterModel> MoodFilters { get; set; } = new();
    [ObservableProperty] public partial string SelectedMood { get; set; } = "Vše";
    [ObservableProperty] public partial bool IsExploreLoading { get; set; }

    // --- Rozšířené vyhledávací filtry ---
    [ObservableProperty] public partial string SelectedSearchFilter { get; set; } = "Vše";
    [ObservableProperty] public partial ObservableCollection<AlbumModel> SearchResultsAlbums { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<ArtistModel> SearchResultsArtists { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<PlaylistModel> SearchResultsPlaylists { get; set; } = new();

    [ObservableProperty]
    public partial bool IsKaraokeMode { get; set; }

    [ObservableProperty]
    public partial bool IsLyricsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLyrics { get; set; }

    [ObservableProperty]
    public partial bool IsSyncedLyrics { get; set; }

    [ObservableProperty]
    public partial string? LyricsSource { get; set; }

    [ObservableProperty]
    public partial LyricLineModel? CurrentLyricLine { get; set; }

    public ObservableCollection<LyricLineModel> LyricsLines { get; } = new();

    public event Action<LyricLineModel>? ActiveLyricChanged;

    [ObservableProperty]
    public partial SongModel? SelectedQueueSong { get; set; }

    partial void OnSelectedQueueSongChanged(SongModel? value)
    {
        if (value != null && value != CurrentSong)
        {
            _ = PlayFromQueueAsync(value);
        }
    }

    [RelayCommand]
    private async Task NotImplemented(string? featureName)
    {
        StatusMessage = $"{await _translationService.TranslateAsync("Zatím nepodporováno:", SelectedLanguage?.TwoLetterISOLanguageName ?? "cs")} {featureName ?? "tato funkce"}";
    }

    [RelayCommand]
    public async Task ShuffleAndPlayLibraryAsync()
    {
        if (LibrarySongs.Count == 0)
        {
            StatusMessage = TextStatusLibraryEmpty;
            PlaybackQueue.Clear();
            _originalQueue.Clear();
            
            // Fallback: Use HomeRecommendations if LibrarySongs is empty
            var fallbackSongs = HomeRecommendations.Count > 0 ? HomeRecommendations.ToList() : SearchResults.ToList();
            
            if (fallbackSongs.Count == 0)
            {
                StatusMessage = TextStatusShuffleFailed;
                return;
            }

            foreach (var song in fallbackSongs)
            {
                PlaybackQueue.Add(song);
                _originalQueue.Add(song);
            }
        }
        else
        {
            PlaybackQueue.Clear();
            _originalQueue.Clear();
            
            foreach (var song in LibrarySongs)
            {
                PlaybackQueue.Add(song);
                _originalQueue.Add(song);
            }
        }

        CurrentQueueIndex = 0;
        IsShuffle = true;
        ApplyShuffle();
        
        await PlayQueueCurrentSongAsync();
        IsBusy = false;
    }

    [RelayCommand]
    public async Task PlayLikedSongsAsync()
    {
        if (LikedSongs.Count == 0) return;

        PlaybackQueue.Clear();
        _originalQueue.Clear();

        foreach (var song in LikedSongs)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }

        CurrentQueueIndex = 0;
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    public async Task ShuffleLikedSongsAsync()
    {
        if (LikedSongs.Count == 0) return;

        PlaybackQueue.Clear();
        _originalQueue.Clear();

        foreach (var song in LikedSongs)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }

        CurrentQueueIndex = 0;
        IsShuffle = true;
        ApplyShuffle();
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    public async Task OpenLikedSongsPlaylistAsync()
    {
        var likedPlaylist = new PlaylistModel
        {
            Id = "VLLM",
            Title = "Oblíbené skladby",
            Creator = "Automatický playlist",
            Description = "Skladby, které jste označili jako oblíbené v YouTube Music",
            ThumbnailUrl = "https://www.gstatic.com/youtube/media/ytm/images/pbg/liked-songs-delhi-1200.png",
            SongCount = LikedSongsCount > 0 ? LikedSongsCount : LikedSongs.Count,
            CanEdit = true
        };
        await OpenPlaylistAsync(likedPlaylist);
    }

    [RelayCommand]
    public async Task PlayLikedSongItemAsync(SongModel? song)
    {
        if (song == null) return;

        PlaybackQueue.Clear();
        _originalQueue.Clear();

        foreach (var s in LikedSongs)
        {
            PlaybackQueue.Add(s);
            _originalQueue.Add(s);
        }

        int index = LikedSongs.IndexOf(song);
        CurrentQueueIndex = index >= 0 ? index : 0;
        await PlayQueueCurrentSongAsync();
    }

    private readonly DiscordRpcService _discordRpcService;
    private readonly LyricsService _lyricsService;

    public MainViewModel(MelodiumService ytService, IAudioService audioService, TranslationService translationService, DiscordRpcService discordRpcService, LyricsService lyricsService, UpdateService updateService)
    {
        _ytService = ytService;
        _audioService = audioService;
        _translationService = translationService;
        _discordRpcService = discordRpcService;
        _lyricsService = lyricsService;
        _updateService = updateService;

        CurrentAppVersion = UpdateService.GetCurrentVersionDisplay();

        IsDiscordRpcEnabled = Preferences.Default.Get("IsDiscordRpcEnabled", false);
        IsCloseToTrayEnabled = Preferences.Default.Get("IsCloseToTrayEnabled", false);
        _discordRpcService.HandleSettingsChanged();

        _audioService.Volume = (float)Volume;

        // Hook up audio service events
        _audioService.PositionChanged += OnAudioPositionChanged;
        _audioService.MediaEnded += OnAudioMediaEnded;
        _audioService.PlaybackStateChanged += OnAudioPlaybackStateChanged;
        _audioService.PlaybackError += OnAudioPlaybackError;

        // Načtení jazyků
        var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .OrderBy(c => c.NativeName)
            .ToList();
        foreach (var c in cultures)
        {
            if (!string.IsNullOrEmpty(c.NativeName))
            {
                Languages.Add(c);
            }
        }

        var savedLang = Preferences.Default.Get("AppLanguage", "en");
        SelectedLanguage = Languages.FirstOrDefault(c => c.TwoLetterISOLanguageName.Equals(savedLang, StringComparison.OrdinalIgnoreCase))
                        ?? Languages.FirstOrDefault(c => c.Name.Equals(savedLang, StringComparison.OrdinalIgnoreCase))
                        ?? Languages.FirstOrDefault(c => c.TwoLetterISOLanguageName == "en")
                        ?? Languages.FirstOrDefault();

        InitializeMoodFilters();

        // Načteme uložené přihlášení a knihovnu při startu
        _ = Task.Run(LoadSavedSessionAsync);

        // Zkontrolujeme dostupnost aktualizací na pozadí při startu
        _ = Task.Run(CheckForUpdatesOnStartupAsync);
    }

    private void InitializeMoodFilters()
    {
        MoodFilters.Clear();
        MoodFilters.Add(new() { Title = TextFilterAll, Icon = "🎶", IsSelected = true });
        MoodFilters.Add(new() { Title = TextFilterRelax, Icon = "🧘", IsSelected = false });
        MoodFilters.Add(new() { Title = TextFilterEnergy, Icon = "⚡", IsSelected = false });
        MoodFilters.Add(new() { Title = TextFilterWorkout, Icon = "🏋️", IsSelected = false });
        MoodFilters.Add(new() { Title = TextFilterFocus, Icon = "🧠", IsSelected = false });
        MoodFilters.Add(new() { Title = "Party", Icon = "🎉", IsSelected = false });
        MoodFilters.Add(new() { Title = "Rock", Icon = "🎸", IsSelected = false });
        MoodFilters.Add(new() { Title = "Pop", Icon = "🎤", IsSelected = false });
        MoodFilters.Add(new() { Title = "Hip-Hop", Icon = "🎧", IsSelected = false });
    }

    private void OnAudioPositionChanged(TimeSpan position, TimeSpan duration)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PositionSeconds = position.TotalSeconds;
            DurationSeconds = duration.TotalSeconds;
            PositionText = position.ToString(@"m\:ss");
            DurationText = duration.TotalSeconds > 0 ? duration.ToString(@"m\:ss") : "0:00";

            if (IsKaraokeMode && IsSyncedLyrics && LyricsLines.Count > 0)
            {
                UpdateActiveLyricLine(position);
            }
        });
    }

    private void UpdateActiveLyricLine(TimeSpan position)
    {
        if (LyricsLines.Count == 0) return;

        LyricLineModel? activeLine = null;
        var offsetPosition = position + TimeSpan.FromMilliseconds(150);

        for (int i = 0; i < LyricsLines.Count; i++)
        {
            var line = LyricsLines[i];
            var nextTime = (i + 1 < LyricsLines.Count) ? LyricsLines[i + 1].Timestamp : TimeSpan.MaxValue;
            if (offsetPosition >= line.Timestamp && offsetPosition < nextTime)
            {
                activeLine = line;
                break;
            }
        }

        if (activeLine != null && activeLine != CurrentLyricLine)
        {
            foreach (var line in LyricsLines)
            {
                line.IsActive = (line == activeLine);
            }
            CurrentLyricLine = activeLine;
            ActiveLyricChanged?.Invoke(activeLine);
        }
    }

    [RelayCommand]
    public async Task ToggleKaraokeModeAsync()
    {
        IsKaraokeMode = !IsKaraokeMode;
        if (IsKaraokeMode && LyricsLines.Count == 0 && CurrentSong != null)
        {
            await LoadLyricsForCurrentSongAsync();
        }
    }

    [RelayCommand]
    public void SeekToLyric(LyricLineModel? line)
    {
        if (line != null && line.Timestamp > TimeSpan.Zero)
        {
            SeekTo(line.Timestamp);
        }
    }

    [RelayCommand]
    public async Task LoadLyricsForCurrentSongAsync()
    {
        if (CurrentSong == null)
        {
            LyricsLines.Clear();
            HasLyrics = false;
            return;
        }

        IsLyricsLoading = true;
        HasLyrics = false;
        LyricsLines.Clear();
        CurrentLyricLine = null;

        try
        {
            var result = await _lyricsService.GetLyricsAsync(
                CurrentSong.Title,
                CurrentSong.Artist,
                DurationSeconds,
                CurrentSong.VideoId);

            if (result != null && result.Lines.Count > 0)
            {
                IsSyncedLyrics = result.IsSynced;
                LyricsSource = result.Source;
                foreach (var l in result.Lines)
                {
                    LyricsLines.Add(l);
                }
                HasLyrics = true;

                if (IsSyncedLyrics)
                {
                    UpdateActiveLyricLine(TimeSpan.FromSeconds(PositionSeconds));
                }
            }
            else
            {
                HasLyrics = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chyba při stahování textu: {ex.Message}");
            HasLyrics = false;
        }
        finally
        {
            IsLyricsLoading = false;
        }
    }

    private void OnAudioMediaEnded()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (RepeatMode == 2) // Repeat Song
            {
                _ = PlayQueueCurrentSongAsync();
            }
            else
            {
                _ = PlayNextSongAsync();
            }
        });
    }

    private void OnAudioPlaybackStateChanged(bool playing)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = playing;
        });
    }

    private void OnAudioPlaybackError(string errorMessage)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            StatusMessage = $"{await _translationService.TranslateAsync("CHYBA:", SelectedLanguage?.TwoLetterISOLanguageName ?? "cs")} {errorMessage}";
        });
    }

    [RelayCommand]
    private void Navigate(string viewName)
    {
        if (CurrentView != viewName && CurrentView != "Artist" && CurrentView != "Playlist")
        {
            _previousView = CurrentView;
        }
        CurrentView = viewName;

        if (viewName == "Explore")
        {
            _ = LoadExploreDataAsync();
        }
    }

    [RelayCommand]
    public void GoBack()
    {
        CurrentView = !string.IsNullOrEmpty(_previousView) ? _previousView : "Home";
    }

    [RelayCommand]
    public async Task OpenArtistAsync(object? param)
    {
        string? artistId = null;
        string? artistName = null;

        if (param is ArtistModel artistModel)
        {
            artistId = artistModel.Id;
            artistName = artistModel.Name;
        }
        else if (param is SongModel songModel)
        {
            artistId = songModel.ArtistId;
            artistName = songModel.Artist;
        }
        else if (param is AlbumModel albumModel)
        {
            artistName = albumModel.ArtistName;
        }
        else if (param is string str)
        {
            if (str.StartsWith("UC"))
            {
                artistId = str;
            }
            else
            {
                artistName = str;
            }
        }

        if (string.IsNullOrWhiteSpace(artistId) && string.IsNullOrWhiteSpace(artistName))
        {
            return;
        }

        if (CurrentView != "Artist")
        {
            _previousView = CurrentView;
        }

        CurrentView = "Artist";
        IsArtistLoading = true;
        StatusMessage = $"Načítám interpreta {artistName ?? artistId}...";

        var details = await _ytService.GetArtistDetailsAsync(artistId, artistName);
        if (details != null)
        {
            CurrentArtist = details;
            StatusMessage = $"Zobrazen interpret: {details.Name}";
        }
        else
        {
            StatusMessage = "Nepodařilo se načíst informace o interpretovi.";
        }

        IsArtistLoading = false;
    }

    [RelayCommand]
    public async Task PlayArtistTopSongsAsync()
    {
        if (CurrentArtist == null || CurrentArtist.TopSongs.Count == 0) return;

        PlaybackQueue.Clear();
        _originalQueue.Clear();

        foreach (var song in CurrentArtist.TopSongs)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }

        CurrentQueueIndex = 0;
        IsShuffle = false;
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    public async Task ShuffleArtistSongsAsync()
    {
        if (CurrentArtist == null || CurrentArtist.TopSongs.Count == 0) return;

        PlaybackQueue.Clear();
        _originalQueue.Clear();

        foreach (var song in CurrentArtist.TopSongs)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }

        CurrentQueueIndex = 0;
        IsShuffle = true;
        ApplyShuffle();
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    private async Task PerformSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        string query = SearchQuery.Trim();
        CurrentView = "Search";
        IsBusy = true;
        StatusMessage = $"Vyhledávám: {query}...";

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SearchResults.Clear();
            SearchResultsAlbums.Clear();
            SearchResultsArtists.Clear();
            SearchResultsPlaylists.Clear();
            HasSongResults = false;
            HasAlbumResults = false;
            HasArtistResults = false;
            HasPlaylistResults = false;
        });

        try
        {
            await _ytService.EnsureInitializedAsync();

            bool isAll = string.Equals(SelectedSearchFilter, "Vše", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(SelectedSearchFilter, "Vse", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(SelectedSearchFilter, "All", StringComparison.OrdinalIgnoreCase);

            var songTask = (isAll || string.Equals(SelectedSearchFilter, "Skladby", StringComparison.OrdinalIgnoreCase) || string.Equals(SelectedSearchFilter, "Songs", StringComparison.OrdinalIgnoreCase))
                ? _ytService.SearchSongsAsync(query)
                : Task.FromResult(new List<SongModel>());

            var albumTask = (isAll || string.Equals(SelectedSearchFilter, "Alba", StringComparison.OrdinalIgnoreCase) || string.Equals(SelectedSearchFilter, "Albums", StringComparison.OrdinalIgnoreCase))
                ? _ytService.SearchAlbumsAsync(query)
                : Task.FromResult(new List<AlbumModel>());

            var artistTask = (isAll || string.Equals(SelectedSearchFilter, "Interpreti", StringComparison.OrdinalIgnoreCase) || string.Equals(SelectedSearchFilter, "Artists", StringComparison.OrdinalIgnoreCase))
                ? _ytService.SearchArtistsAsync(query)
                : Task.FromResult(new List<ArtistModel>());

            var playlistTask = (isAll || string.Equals(SelectedSearchFilter, "Playlisty", StringComparison.OrdinalIgnoreCase) || string.Equals(SelectedSearchFilter, "Playlists", StringComparison.OrdinalIgnoreCase))
                ? _ytService.SearchPlaylistsAsync(query)
                : Task.FromResult(new List<PlaylistModel>());

            await Task.WhenAll(songTask, albumTask, artistTask, playlistTask);

            var songs = await songTask;
            var albums = await albumTask;
            var artists = await artistTask;
            var playlists = await playlistTask;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SearchResults.Clear();
                foreach (var song in songs) SearchResults.Add(song);
                HasSongResults = SearchResults.Count > 0;

                SearchResultsAlbums.Clear();
                foreach (var alb in albums) SearchResultsAlbums.Add(alb);
                HasAlbumResults = SearchResultsAlbums.Count > 0;

                SearchResultsArtists.Clear();
                foreach (var art in artists) SearchResultsArtists.Add(art);
                HasArtistResults = SearchResultsArtists.Count > 0;

                SearchResultsPlaylists.Clear();
                foreach (var pl in playlists) SearchResultsPlaylists.Add(pl);
                HasPlaylistResults = SearchResultsPlaylists.Count > 0;

                HasSearched = true;
                OnPropertyChanged(nameof(IsSearchResultsEmpty));
                StatusMessage = $"Hledání dokončeno 🎶.";
                IsBusy = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerformSearchAsync Error]: {ex}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HasSearched = true;
                OnPropertyChanged(nameof(IsSearchResultsEmpty));
                StatusMessage = $"Chyba při vyhledávání: {ex.Message}";
                IsBusy = false;
            });
        }
    }

    [RelayCommand]
    public async Task PlaySongAsync(SongModel? song)
    {
        if (song == null) return;

        PlaybackQueue.Clear();
        PlaybackQueue.Add(song);
        CurrentQueueIndex = 0;

        _originalQueue.Clear();
        _originalQueue.Add(song);
        IsShuffle = false;

        await PlayQueueCurrentSongAsync();
    }

    public void UpdateEditablePlaylists()
    {
        EditablePlaylists.Clear();
        foreach (var p in LibraryPlaylists)
        {
            bool isEditable = p.CanEdit || 
                _ytService.IsUserCreator(p.Creator) || 
                (!string.IsNullOrEmpty(UserProfileName) && 
                 UserProfileName != "Nepřihlášen" && 
                 UserProfileName != "Můj účet" && 
                 (string.Equals(p.Creator, UserProfileName, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(p.Creator, UserProfileName.TrimStart('@'), StringComparison.OrdinalIgnoreCase)));

            if (!isEditable && !string.IsNullOrEmpty(p.Creator))
            {
                if (p.Creator.Equals("Vy", StringComparison.OrdinalIgnoreCase) ||
                    p.Creator.Equals("You", StringComparison.OrdinalIgnoreCase) ||
                    p.Creator.Equals("Já", StringComparison.OrdinalIgnoreCase) ||
                    p.Creator.Equals("Me", StringComparison.OrdinalIgnoreCase))
                {
                    isEditable = true;
                }
            }

            if (isEditable)
            {
                p.CanEdit = true;
                EditablePlaylists.Add(p);
            }
        }

        // Pokud stále nemáme žádný upravitelný playlist, ale knihovna má uživatelské playlisty (neoficiální, ne-rádia, ne-alba)
        if (EditablePlaylists.Count == 0)
        {
            foreach (var p in LibraryPlaylists.Where(p => 
                !p.Id.StartsWith("OLAK") && 
                !p.Id.StartsWith("RD") && 
                !p.Id.StartsWith("MPREb_") && 
                !string.Equals(p.Creator, "YouTube Music", StringComparison.OrdinalIgnoreCase)))
            {
                p.CanEdit = true;
                EditablePlaylists.Add(p);
            }
        }
    }

    public async Task RefreshEditablePlaylistsAsync(string? videoId = null)
    {
        try
        {
            List<PlaylistModel> options = new();
            if (!string.IsNullOrEmpty(videoId))
            {
                options = await _ytService.GetAddToPlaylistOptionsAsync(videoId);
            }

            if (options.Count > 0)
            {
                foreach (var opt in options)
                {
                    var existing = LibraryPlaylists.FirstOrDefault(p => p.Id == opt.Id);
                    if (existing != null)
                    {
                        existing.CanEdit = true;
                    }
                    else
                    {
                        LibraryPlaylists.Add(opt);
                    }
                }
            }
            else if (LibraryPlaylists.Count == 0)
            {
                var libraryPlaylists = await _ytService.GetLibraryPlaylistsAsync();
                foreach (var p in libraryPlaylists)
                {
                    if (!LibraryPlaylists.Any(x => x.Id == p.Id))
                    {
                        LibraryPlaylists.Add(p);
                    }
                }
            }

            UpdateEditablePlaylists();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chyba při obnově upravitelných playlistů: {ex.Message}");
        }
    }

    public async Task<PlaylistModel?> CreatePlaylistAsync(string title, string description = "")
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        StatusMessage = $"Vytvářím playlist '{title}' na YouTube Music...";
        try
        {
            string? playlistId = await _ytService.CreatePlaylistAsync(title, description);
            if (!string.IsNullOrEmpty(playlistId))
            {
                var newPlaylist = new PlaylistModel
                {
                    Id = playlistId,
                    Title = title,
                    Description = description,
                    Creator = !string.IsNullOrEmpty(UserProfileName) && UserProfileName != "Nepřihlášen" && UserProfileName != "Můj účet" ? UserProfileName : "Vy",
                    CanEdit = true,
                    SongCount = 0
                };

                if (MainThread.IsMainThread)
                {
                    LibraryPlaylists.Insert(0, newPlaylist);
                    UpdateEditablePlaylists();
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LibraryPlaylists.Insert(0, newPlaylist);
                        UpdateEditablePlaylists();
                    });
                }

                StatusMessage = $"Playlist '{title}' byl úspěšně vytvořen na YouTube Music.";
                return newPlaylist;
            }
            else
            {
                StatusMessage = "Vytvoření playlistu na YouTube Music se nezdařilo.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při vytváření playlistu: {ex.Message}";
        }
        return null;
    }

    [RelayCommand]
    public async Task OpenPlaylistAsync(object? param)
    {
        PlaylistModel? playlist = null;
        if (param is PlaylistModel pm)
        {
            playlist = pm;
        }

        if (playlist == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentView != "Playlist")
            {
                _previousView = CurrentView;
            }

            CurrentView = "Playlist";
            CurrentPlaylist = playlist;
            CurrentPlaylistSongs.Clear();
            IsPlaylistLoading = true;
            StatusMessage = $"Načítám playlist {playlist.Title}...";
        });

        try
        {
            var details = await _ytService.GetPlaylistDetailsAsync(playlist.Id);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (details != null)
                    {
                        if (details.Playlist != null)
                        {
                            if (!string.IsNullOrWhiteSpace(details.Playlist.Title)) playlist.Title = details.Playlist.Title;
                            if (!string.IsNullOrWhiteSpace(details.Playlist.ThumbnailUrl)) playlist.ThumbnailUrl = details.Playlist.ThumbnailUrl;
                            if (!string.IsNullOrWhiteSpace(details.Playlist.Creator)) playlist.Creator = details.Playlist.Creator;
                            playlist.SongCount = details.Songs.Count;
                            playlist.Description = details.Playlist.Description;
                            playlist.CanEdit = details.Playlist.CanEdit;
                            playlist.IsCollaborative = details.Playlist.IsCollaborative;
                        }

                        CurrentPlaylistSongs.Clear();
                        foreach (var song in details.Songs)
                        {
                            CurrentPlaylistSongs.Add(song);
                        }

                        UpdateEditablePlaylists();
                        StatusMessage = $"Playlist '{playlist.Title}' načten ({CurrentPlaylistSongs.Count} skladeb).";
                    }
                    else
                    {
                        StatusMessage = $"Playlist '{playlist.Title}' se nepodařilo načíst.";
                    }
                }
                catch (Exception ex)
                {
                    CrashLoggerService.LogCrash(ex, "OpenPlaylistAsync.UIUpdate");
                    StatusMessage = $"Chyba při zobrazení skladeb: {ex.Message}";
                }
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusMessage = $"Chyba při načítání playlistu: {ex.Message}";
            });
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsPlaylistLoading = false;
            });
        }
    }

    [RelayCommand]
    public void ClosePlaylist()
    {
        CurrentView = !string.IsNullOrEmpty(_previousView) ? _previousView : "Library";
    }

    [RelayCommand]
    public async Task PlayCurrentPlaylistAsync()
    {
        if (CurrentPlaylistSongs.Count == 0) return;

        PlaybackQueue.Clear();
        _originalQueue.Clear();
        foreach (var song in CurrentPlaylistSongs)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }
        CurrentQueueIndex = 0;
        if (IsShuffle)
        {
            ApplyShuffle();
        }
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    public async Task ShuffleCurrentPlaylistAsync()
    {
        if (CurrentPlaylistSongs.Count == 0) return;

        PlaybackQueue.Clear();
        _originalQueue.Clear();
        foreach (var song in CurrentPlaylistSongs)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }
        IsShuffle = true;
        ApplyShuffle();
        CurrentQueueIndex = 0;
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    public async Task RemoveSongFromCurrentPlaylistAsync(SongModel? song)
    {
        if (CurrentPlaylist == null || song == null || !CurrentPlaylist.CanEdit) return;

        bool removed = CurrentPlaylistSongs.Remove(song);
        if (removed)
        {
            CurrentPlaylist.SongCount = CurrentPlaylistSongs.Count;
            StatusMessage = $"Odebírám skladbu '{song.Title}' z YouTube Music...";
            bool success = await _ytService.RemoveSongFromPlaylistAsync(CurrentPlaylist.Id, song.SetVideoId, song.VideoId);
            if (success)
            {
                StatusMessage = $"Skladba '{song.Title}' byla úspěšně odebrána z playlistu.";
            }
            else
            {
                StatusMessage = "Chyba při odebírání skladby na serveru YouTube Music.";
            }
        }
    }

    [RelayCommand]
    public async Task MoveSongUpAsync(SongModel? song)
    {
        if (CurrentPlaylist == null || song == null || !CurrentPlaylist.CanEdit) return;

        int index = CurrentPlaylistSongs.IndexOf(song);
        if (index <= 0) return;

        var precedingSong = CurrentPlaylistSongs[index - 1];
        CurrentPlaylistSongs.Move(index, index - 1);

        StatusMessage = $"Ukládám pořadí skladby '{song.Title}'...";
        bool success = await _ytService.MoveSongInPlaylistAsync(CurrentPlaylist.Id, song.SetVideoId ?? "", precedingSong.SetVideoId, null);
        if (success)
        {
            StatusMessage = $"Skladba '{song.Title}' posunuta nahoru.";
        }
        else
        {
            StatusMessage = "Změna pořadí se nemusela uložit na serveru.";
        }
    }

    [RelayCommand]
    public async Task MoveSongDownAsync(SongModel? song)
    {
        if (CurrentPlaylist == null || song == null || !CurrentPlaylist.CanEdit) return;

        int index = CurrentPlaylistSongs.IndexOf(song);
        if (index < 0 || index >= CurrentPlaylistSongs.Count - 1) return;

        var followingSong = CurrentPlaylistSongs[index + 1];
        CurrentPlaylistSongs.Move(index, index + 1);

        StatusMessage = $"Ukládám pořadí skladby '{song.Title}'...";
        bool success = await _ytService.MoveSongInPlaylistAsync(CurrentPlaylist.Id, song.SetVideoId ?? "", null, followingSong.SetVideoId);
        if (success)
        {
            StatusMessage = $"Skladba '{song.Title}' posunuta dolů.";
        }
        else
        {
            StatusMessage = "Změna pořadí se nemusela uložit na serveru.";
        }
    }

    [RelayCommand]
    public async Task AddSongToCurrentPlaylistAsync(SongModel? song)
    {
        if (CurrentPlaylist == null || song == null || !CurrentPlaylist.CanEdit) return;

        // Kontrola, zda už skladba v playlistu není
        if (CurrentPlaylistSongs.Any(s => s.VideoId == song.VideoId))
        {
            StatusMessage = $"Skladba '{song.Title}' se v playlistu již nachází.";
            return;
        }

        StatusMessage = $"Přidávám '{song.Title}' do playlistu '{CurrentPlaylist.Title}'...";
        bool success = await _ytService.AddSongToPlaylistAsync(CurrentPlaylist.Id, song.VideoId);
        if (success)
        {
            CurrentPlaylistSongs.Add(song);
            CurrentPlaylist.SongCount = CurrentPlaylistSongs.Count;
            StatusMessage = $"Skladba '{song.Title}' byla úspěšně přidána do playlistu.";
        }
        else
        {
            StatusMessage = "Chyba: Nepodařilo se přidat skladbu do playlistu.";
        }
    }

    public async Task<(bool Success, bool IsDuplicate, string Message)> AddSongToPlaylistAsync(PlaylistModel playlist, SongModel song)
    {
        if (playlist == null || song == null) return (false, false, "Neplatný požadavek.");

        // Kontrola, zda už skladba v playlistu není
        bool isDuplicate = false;
        if (CurrentPlaylist?.Id == playlist.Id && CurrentPlaylistSongs.Count > 0)
        {
            isDuplicate = CurrentPlaylistSongs.Any(s => s.VideoId == song.VideoId);
        }
        else
        {
            try
            {
                StatusMessage = $"Ověřuji skladby v playlistu '{playlist.Title}'...";
                var existing = await _ytService.GetPlaylistSongsAsync(playlist.Id);
                if (existing != null && existing.Any(s => s.VideoId == song.VideoId))
                {
                    isDuplicate = true;
                }
            }
            catch { }
        }

        if (isDuplicate)
        {
            string dupMsg = $"Skladba '{song.Title}' se v playlistu '{playlist.Title}' již nachází.";
            StatusMessage = dupMsg;
            return (false, true, dupMsg);
        }

        playlist.CanEdit = true;
        StatusMessage = $"Přidávám '{song.Title}' do playlistu '{playlist.Title}'...";
        bool success = await _ytService.AddSongToPlaylistAsync(playlist.Id, song.VideoId);
        if (success)
        {
            playlist.SongCount++;
            if (CurrentPlaylist?.Id == playlist.Id)
            {
                CurrentPlaylistSongs.Add(song);
            }
            string okMsg = $"Skladba '{song.Title}' byla přidána do '{playlist.Title}'.";
            StatusMessage = okMsg;
            return (true, false, okMsg);
        }
        else
        {
            string errMsg = "Chyba: Nepodařilo se přidat skladbu do playlistu na YouTube Music.";
            StatusMessage = errMsg;
            return (false, false, errMsg);
        }
    }

    [RelayCommand]
    public async Task SearchSongsToAddToPlaylistAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        IsSearchingSongsToAdd = true;
        try
        {
            var results = await _ytService.SearchSongsAsync(query);
            PlaylistSearchResults.Clear();
            foreach (var r in results.Take(15))
            {
                PlaylistSearchResults.Add(r);
            }
        }
        catch { }
        finally
        {
            IsSearchingSongsToAdd = false;
        }
    }

    [RelayCommand]
    public async Task PlayPlaylistAsync(PlaylistModel? playlist)
    {
        if (playlist == null) return;

        IsBusy = true;
        StatusMessage = $"Načítám skladby z playlistu: {playlist.Title}...";

        var songs = await _ytService.GetPlaylistSongsAsync(playlist.Id);
        if (songs.Count > 0)
        {
            PlaybackQueue.Clear();
            _originalQueue.Clear();
            foreach (var song in songs)
            {
                PlaybackQueue.Add(song);
                _originalQueue.Add(song);
            }
            CurrentQueueIndex = 0;
            if (IsShuffle)
            {
                ApplyShuffle();
            }
            await PlayQueueCurrentSongAsync();
        }
        else
        {
            StatusMessage = "Playlist neobsahuje žádné skladby.";
        }
        IsBusy = false;
    }

    [RelayCommand]
    public async Task PlayAlbumAsync(AlbumModel? album)
    {
        if (album == null) return;

        IsBusy = true;
        StatusMessage = $"Načítám skladby z alba: {album.Title}...";

        var songs = await _ytService.GetAlbumSongsAsync(album.Id);
        if (songs.Count > 0)
        {
            PlaybackQueue.Clear();
            _originalQueue.Clear();
            foreach (var song in songs)
            {
                PlaybackQueue.Add(song);
                _originalQueue.Add(song);
            }
            CurrentQueueIndex = 0;
            if (IsShuffle)
            {
                ApplyShuffle();
            }
            await PlayQueueCurrentSongAsync();
        }
        else
        {
            StatusMessage = "Album neobsahuje žádné skladby.";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (CurrentSong == null) return;

        if (IsPlaying)
        {
            _audioService.Pause();
        }
        else
        {
            _audioService.Play();
        }
    }

    [RelayCommand]
    public async Task PlayNextSongAsync()
    {
        if (PlaybackQueue.Count == 0) return;

        if (CurrentQueueIndex + 1 < PlaybackQueue.Count)
        {
            CurrentQueueIndex++;
            await PlayQueueCurrentSongAsync();
        }
        else if (RepeatMode == 1) // Repeat Queue
        {
            CurrentQueueIndex = 0;
            await PlayQueueCurrentSongAsync();
        }
        else if (IsAutoplayEnabled && CurrentSong != null)
        {
            // Autoplay: Nekonečné rádio 🎶
            StatusMessage = "Nekonečné rádio 🎶: Načítám další skladby...";
            try
            {
                var nextSongs = await _ytService.GetUpNextRadioAsync(CurrentSong.VideoId);
                if (nextSongs != null && nextSongs.Count > 0)
                {
                    int added = 0;
                    foreach (var s in nextSongs)
                    {
                        if (!PlaybackQueue.Any(q => q.VideoId == s.VideoId))
                        {
                            PlaybackQueue.Add(s);
                            _originalQueue.Add(s);
                            added++;
                        }
                    }
                    if (added > 0 && CurrentQueueIndex + 1 < PlaybackQueue.Count)
                    {
                        CurrentQueueIndex++;
                        await PlayQueueCurrentSongAsync();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                VmLog($"Autoplay failed: {ex.Message}");
            }
            StatusMessage = "Konec fronty přehrávání.";
        }
        else
        {
            StatusMessage = "Konec fronty přehrávání.";
        }
    }

    [RelayCommand]
    public async Task PlayPreviousSongAsync()
    {
        if (PlaybackQueue.Count == 0) return;

        if (PositionSeconds > 3)
        {
            await PlayQueueCurrentSongAsync();
            return;
        }

        if (CurrentQueueIndex - 1 >= 0)
        {
            CurrentQueueIndex--;
            await PlayQueueCurrentSongAsync();
        }
        else if (RepeatMode == 1) // Repeat Queue
        {
            CurrentQueueIndex = PlaybackQueue.Count - 1;
            await PlayQueueCurrentSongAsync();
        }
        else
        {
            await PlayQueueCurrentSongAsync();
        }
    }

    private void EnsureInfiniteQueue(string videoId)
    {
        if (PlaybackQueue.Count - CurrentQueueIndex <= 3)
        {
            _ = Task.Run(async () =>
            {
                var upNext = await _ytService.GetUpNextRadioAsync(videoId);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    foreach (var nextSong in upNext)
                    {
                        if (!_originalQueue.Any(s => s.VideoId == nextSong.VideoId))
                        {
                            PlaybackQueue.Add(nextSong);
                            _originalQueue.Add(nextSong);
                        }
                    }
                });
            });
        }
    }

    private void TriggerPrefetchNextSong()
    {
        _prefetchCts?.Cancel();
        _prefetchCts = new System.Threading.CancellationTokenSource();
        var ct = _prefetchCts.Token;

        int nextIndex = CurrentQueueIndex + 1;
        if (nextIndex < 0 || nextIndex >= PlaybackQueue.Count) return;

        var nextSong = PlaybackQueue[nextIndex];
        if (string.IsNullOrWhiteSpace(nextSong.VideoId)) return;

        if (_audioService.TryGetCachedAudio(nextSong.VideoId, out _))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, ct);
                if (ct.IsCancellationRequested) return;

                VmLog($"[Prefetch] Získávám URL pro {nextSong.Title} ({nextSong.VideoId})");
                var nextUrl = await _ytService.GetAudioStreamUrlAsync(nextSong.VideoId, $"{nextSong.Title} {nextSong.Artist}");
                if (string.IsNullOrEmpty(nextUrl) || ct.IsCancellationRequested) return;

                VmLog($"[Prefetch] Stahuji a ukládám do mezipaměti: {nextSong.Title}");
                await _audioService.PrefetchSongAsync(nextSong.VideoId, nextUrl, ct);
                VmLog($"[Prefetch] Skladba {nextSong.Title} je připravena v mezipaměti.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                VmLog($"[Prefetch] Chyba při přednačítání: {ex.Message}");
            }
        }, ct);
    }

    private async Task PlayQueueCurrentSongAsync()
    {
        if (CurrentQueueIndex < 0 || CurrentQueueIndex >= PlaybackQueue.Count) return;

        // Stop current playback
        _audioService.Stop();

        // Cancel previous download & prefetch
        _downloadCts?.Cancel();
        _downloadCts = new System.Threading.CancellationTokenSource();
        var token = _downloadCts.Token;

        var song = PlaybackQueue[CurrentQueueIndex];
        CurrentSong = song;
        SelectedQueueSong = song;
        IsCurrentSongLiked = song.IsLiked;
        IsBusy = true;
        StatusMessage = $"Příprava: {song.Title}...";

        // Reset position display
        PositionSeconds = 0;
        DurationSeconds = 0;
        PositionText = "0:00";
        DurationText = "0:00";

        // Reset or reload lyrics
        LyricsLines.Clear();
        CurrentLyricLine = null;
        HasLyrics = false;
        if (IsKaraokeMode)
        {
            _ = Task.Run(LoadLyricsForCurrentSongAsync);
        }

        try
        {
            // 1. FAST PATH: Okamžité přehrání z lokální diskové mezipaměti (0ms čekání na síť)
            if (_audioService.TryGetCachedAudio(song.VideoId, out var cachedPath))
            {
                VmLog($"[FAST PATH] Přehrávám přímo z diskové cache: {cachedPath}");
                StatusMessage = $"▶ {song.Title}";

                EnsureInfiniteQueue(song.VideoId);

                await _audioService.PlayFileAsync(cachedPath, msg =>
                {
                    MainThread.BeginInvokeOnMainThread(() => StatusMessage = $"{song.Title} — {msg}");
                });

                TriggerPrefetchNextSong();
                return;
            }

            // 2. NETWORK PATH: Získat stream URL a stáhnout/demuxovat do trvalé mezipaměti
            StatusMessage = $"[1/3] Získávám audio stream: {song.Title}...";
            VmLog($"Getting stream URL for videoId={song.VideoId}");

            await _ytService.EnsureInitializedAsync();
            var streamUrl = await _ytService.GetAudioStreamUrlAsync(song.VideoId, $"{song.Title} {song.Artist}");
            token.ThrowIfCancellationRequested();

            EnsureInfiniteQueue(song.VideoId);

            VmLog($"Stream URL result: {(streamUrl == null ? "NULL" : $"length={streamUrl.Length}")}");
            if (!string.IsNullOrEmpty(streamUrl))
            {
                StatusMessage = $"[2/3] Načítám: {song.Title}...";

                await _audioService.PlayFromUrlAsync(streamUrl, token, msg =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StatusMessage = $"{song.Title} — {msg}";
                    });
                }, videoId: song.VideoId);

                VmLog("PlayFromUrlAsync returned (přehrávání spuštěno).");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusMessage = $"▶ {song.Title}";
                });

                // Spustit prefetch pro další skladbu ve frontě na pozadí
                TriggerPrefetchNextSong();
            }
            else
            {
                StatusMessage = "❌ Nepodařilo se získat audio stream.";
                VmLog("ERROR: streamUrl is null");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Přehrávání přeskočeno.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Výjimka: {ex.GetType().Name}: {ex.Message}";
            VmLog($"EXCEPTION: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }


    private static void VmLog(string message)
    {
        try
        {
            var msg = $"[{DateTime.Now:HH:mm:ss.fff}] [ViewModel] {message}\n";
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_naudio_log.txt");
            System.IO.File.AppendAllText(logPath, msg);
            System.Diagnostics.Debug.WriteLine(msg);
        }
        catch { }
    }

    public void SeekTo(TimeSpan position)
    {
        _audioService.SeekTo(position);
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        IsShuffle = !IsShuffle;
        if (IsShuffle)
        {
            ApplyShuffle();
        }
        else
        {
            RemoveShuffle();
        }
    }

    [RelayCommand]
    private void ToggleRepeat()
    {
        RepeatMode = (RepeatMode + 1) % 3;
    }

    [RelayCommand]
    public async Task PlayFromQueueAsync(SongModel? song)
    {
        if (song == null) return;
        int index = PlaybackQueue.IndexOf(song);
        if (index >= 0)
        {
            CurrentQueueIndex = index;
            await PlayQueueCurrentSongAsync();
        }
    }

    [RelayCommand]
    private void ClearQueue()
    {
        PlaybackQueue.Clear();
        _originalQueue.Clear();
        CurrentQueueIndex = -1;
        CurrentSong = null;
        SelectedQueueSong = null;
        _audioService.Stop();
        IsPlaying = false;
        StatusMessage = "Fronta byla vymazána.";
    }

    [RelayCommand]
    private void RemoveFromQueue(SongModel? song)
    {
        if (song == null) return;
        
        int index = PlaybackQueue.IndexOf(song);
        if (index >= 0)
        {
            PlaybackQueue.RemoveAt(index);
            
            var origIndex = _originalQueue.IndexOf(song);
            if (origIndex >= 0)
                _originalQueue.RemoveAt(origIndex);

            if (index < CurrentQueueIndex)
            {
                CurrentQueueIndex--;
            }
            else if (index == CurrentQueueIndex)
            {
                if (PlaybackQueue.Count > 0)
                {
                    if (CurrentQueueIndex >= PlaybackQueue.Count)
                    {
                        CurrentQueueIndex = 0;
                    }
                    _ = PlayQueueCurrentSongAsync();
                }
                else
                {
                    ClearQueue();
                }
            }
        }
    }

    private void ApplyShuffle()
    {
        if (PlaybackQueue.Count <= 1) return;

        var current = CurrentSong;
        var otherSongs = PlaybackQueue.Where(s => s != current).ToList();

        var rng = new Random();
        int n = otherSongs.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var value = otherSongs[k];
            otherSongs[k] = otherSongs[n];
            otherSongs[n] = value;
        }

        PlaybackQueue.Clear();
        if (current != null)
        {
            PlaybackQueue.Add(current);
        }
        foreach (var s in otherSongs)
        {
            PlaybackQueue.Add(s);
        }
        CurrentQueueIndex = current != null ? 0 : -1;
    }

    private void RemoveShuffle()
    {
        if (_originalQueue.Count == 0) return;

        var current = CurrentSong;
        PlaybackQueue.Clear();
        foreach (var s in _originalQueue)
        {
            PlaybackQueue.Add(s);
        }

        if (current != null)
        {
            CurrentQueueIndex = PlaybackQueue.IndexOf(current);
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _audioService.Stop();

        SecureStorage.Default.Remove("ytm_cookies");
        _ytService.InitializeClient(null);
        IsLoggedIn = false;
        UserProfileName = "Nepřihlášen";
        LibrarySongs.Clear();
        LibraryPlaylists.Clear();
        EditablePlaylists.Clear();
        CurrentPlaylist = null;
        CurrentPlaylistSongs.Clear();
        LibraryAlbums.Clear();
        LibraryArtists.Clear();
        PlaybackQueue.Clear();
        CurrentSong = null;
        IsPlaying = false;

        StatusMessage = "Uživatel byl odhlášen.";
        CurrentView = "Home";
    }

    public async Task LoadSavedSessionAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync("ytm_cookies");
            if (!string.IsNullOrEmpty(json))
            {
                var dtos = System.Text.Json.JsonSerializer.Deserialize<List<CookieDto>>(json);
                if (dtos != null && dtos.Count > 0)
                {
                    var cookies = dtos.Select(d => new Cookie(d.Name, d.Value, d.Path, d.Domain)).ToList();
                    _ytService.InitializeClient(cookies);
                    IsLoggedIn = true;
                    UserProfileName = "Můj účet";
                    StatusMessage = "Relace obnovena.";

                    // Spustit domovskou stránku i knihovnu paralelně pro bleskový start
                    _ = Task.Run(LoadHomeRecommendationsAsync);
                    _ = Task.Run(LoadLibraryAsync);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nepodařilo se načíst relaci: {ex.Message}";
        }

        // Pokud není přihlášen, načteme alespoň domovskou obrazovku
        _ = Task.Run(LoadHomeRecommendationsAsync);
    }

    public async Task SaveSessionAsync(List<Cookie> cookies)
    {
        try
        {
            var dtos = cookies.Select(c => new CookieDto { Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path }).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(dtos);
            await SecureStorage.Default.SetAsync("ytm_cookies", json);
            
            _ytService.InitializeClient(cookies);
            IsLoggedIn = true;
            UserProfileName = "Můj účet";
            StatusMessage = "Přihlášení uloženo!";
            
            _ = Task.Run(LoadHomeRecommendationsAsync);
            _ = Task.Run(LoadLibraryAsync);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při ukládání přihlášení: {ex.Message}";
        }
    }

    public async Task LoadHomeRecommendationsAsync()
    {
        try
        {
            await _ytService.EnsureInitializedAsync();
            var sections = await _ytService.GetHomeSectionsAsync();

            // Optimalizace: Extrahovat skladby přímo ze získaných sekcí bez zbytečného duplicitního volání sítě
            List<SongModel>? homeSongs = null;
            if (sections != null && sections.Count > 0)
            {
                homeSongs = sections.SelectMany(s => s.Items)
                                    .Where(i => i.Song != null)
                                    .Select(i => i.Song!)
                                    .GroupBy(s => s.VideoId)
                                    .Select(g => g.First())
                                    .Take(40)
                                    .ToList();
            }

            if (homeSongs == null || homeSongs.Count == 0)
            {
                homeSongs = await _ytService.GetHomeRecommendationsAsync();
            }
            
            // Pokud Youtube vrátí prázdný seznam (např. u nepřihlášených účtů), uděláme fallback search
            if ((homeSongs == null || homeSongs.Count == 0) && (sections == null || sections.Count == 0))
            {
                homeSongs = await _ytService.SearchSongsAsync("Top 100 hitů");
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Synchronizovat parametry z YouTube Music chips
                if (_ytService.HomeMoodChips.Count > 0)
                {
                    foreach (var chip in _ytService.HomeMoodChips)
                    {
                        var existing = MoodFilters.FirstOrDefault(m => string.Equals(m.Title, chip.Key, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.Params = chip.Value;
                        }
                    }
                }

                HomeSections.Clear();
                if (sections != null && sections.Count > 0)
                {
                    foreach (var s in sections) HomeSections.Add(s);
                    HasHomeSections = true;
                }
                else
                {
                    HasHomeSections = false;
                }

                HomeRecommendations.Clear();
                if (homeSongs != null)
                {
                    foreach (var h in homeSongs) HomeRecommendations.Add(h);
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při stahování doporučení: {ex.Message}";
        }
    }

    public async Task LoadLibraryAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsBusy = true;
            StatusMessage = "Načítám knihovnu...";
        });
        try
        {
            await _ytService.EnsureInitializedAsync();

            // Uživatelský profil na pozadí bez blokování knihovny
            _ = Task.Run(async () =>
            {
                try
                {
                    var profile = await _ytService.GetAccountProfileAsync();
                    if (!string.IsNullOrEmpty(profile.Name))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            UserProfileName = profile.Name;
                        });
                    }
                }
                catch { }
            });

            // Fáze 1 (prioritní): Playlisty a oblíbené skladby (zobrazují se v postranním panelu)
            IsLikedSongsLoading = true;
            var playlistsTask = _ytService.GetLibraryPlaylistsAsync();
            var likedTask = _ytService.GetLikedSongsAsync();

            await Task.WhenAll(playlistsTask, likedTask);
            var playlists = await playlistsTask;
            var likedDetails = await likedTask;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                LikedSongs.Clear();
                if (likedDetails?.Songs != null)
                {
                    foreach (var ls in likedDetails.Songs) LikedSongs.Add(ls);
                    LikedSongsCount = likedDetails.Playlist.SongCount > 0 ? likedDetails.Playlist.SongCount : LikedSongs.Count;
                }
                IsLikedSongsLoading = false;

                LibraryPlaylists.Clear();
                foreach (var p in playlists) LibraryPlaylists.Add(p);
                UpdateEditablePlaylists();
            });

            // Fáze 2: Skladby, alba a interpreti
            var songsTask = _ytService.GetLibrarySongsAsync();
            var albumsTask = _ytService.GetLibraryAlbumsAsync();
            var artistsTask = _ytService.GetLibraryArtistsAsync();

            await Task.WhenAll(songsTask, albumsTask, artistsTask);
            var songs = await songsTask;
            var albums = await albumsTask;
            var artists = await artistsTask;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                LibrarySongs.Clear();
                foreach (var s in songs) LibrarySongs.Add(s);
                if (LibrarySongs.Count == 0 && LikedSongs.Count > 0)
                {
                    foreach (var ls in LikedSongs) LibrarySongs.Add(ls);
                }

                LibraryAlbums.Clear();
                foreach (var a in albums) LibraryAlbums.Add(a);

                LibraryArtists.Clear();
                foreach (var art in artists) LibraryArtists.Add(art);

                StatusMessage = TextStatusLibraryLoaded;
            });

            _ = Task.Run(() => GenerateLibraryRecommendationsAsync(LibrarySongs.ToList(), artists.ToList(), albums.ToList(), playlists.ToList()));
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusMessage = $"Chyba při stahování knihovny: {ex.Message}";
            });
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsBusy = false;
            });
        }
    }

    public async Task GenerateLibraryRecommendationsAsync(List<SongModel> userSongs, List<ArtistModel> userArtists, List<AlbumModel> userAlbums, List<PlaylistModel> userPlaylists)
    {
        // Původní kontrola byla: if (userSongs == null || userSongs.Count == 0) return;
        // Ale uživatel může mít knihovnu interpretů a alb místo jednotlivých skladeb.
        if ((userSongs == null || userSongs.Count == 0) && 
            (userArtists == null || userArtists.Count == 0) &&
            (userAlbums == null || userAlbums.Count == 0) &&
            (userPlaylists == null || userPlaylists.Count == 0)) 
            return;

        try
        {
            // 1. Najít nejoblíbenější interprety nebo přímo odebírané interprety
            var seedArtists = new HashSet<string>();
            
            if (userSongs != null && userSongs.Count > 0)
            {
                var topFromSongs = userSongs
                    .GroupBy(s => s.Artist)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key);
                foreach (var a in topFromSongs) seedArtists.Add(a);
            }

            if (userArtists != null && userArtists.Count > 0)
            {
                var topFromArtists = userArtists.Take(5).Select(a => a.Name);
                foreach (var a in topFromArtists) seedArtists.Add(a);
            }

            if (userAlbums != null && userAlbums.Count > 0)
            {
                var topFromAlbums = userAlbums.Take(5).Select(a => a.ArtistName);
                foreach (var a in topFromAlbums) { if (!string.IsNullOrEmpty(a) && a != "Neznámý interpret") seedArtists.Add(a); }
            }

            if (seedArtists.Count == 0) return;

            // 2. Získat náhodné songy od těchto interpretů a z nich stáhnout UpNext
            var rng = new Random();
            var allRecommendedSongs = new List<SongModel>();
            
            var artistsList = seedArtists.OrderBy(x => rng.Next()).Take(3).ToList();

            foreach (var artist in artistsList)
            {
                // Najít seed písničku (buď tu co uživatel má, nebo prohledat daného interpreta)
                string seedVideoId = null;
                var artistSongs = userSongs?.Where(s => s.Artist == artist).ToList();
                
                if (artistSongs != null && artistSongs.Count > 0)
                {
                    seedVideoId = artistSongs[rng.Next(artistSongs.Count)].VideoId;
                }
                else
                {
                    // Pokud uživatel má jen interpreta v knihovně, najdeme jeho hit
                    var searchRes = await _ytService.SearchSongsAsync(artist);
                    if (searchRes.Count > 0)
                    {
                        seedVideoId = searchRes[0].VideoId;
                    }
                }

                if (!string.IsNullOrEmpty(seedVideoId))
                {
                    var radioSongs = await _ytService.GetUpNextRadioAsync(seedVideoId);
                    allRecommendedSongs.AddRange(radioSongs);
                }
            }

            // 3. Odstranit duplicity a skladby, které už uživatel má v knihovně
            var libraryVideoIds = new HashSet<string>(userSongs?.Select(s => s.VideoId) ?? Enumerable.Empty<string>());
            
            var finalSongs = allRecommendedSongs
                .Where(s => !libraryVideoIds.Contains(s.VideoId))
                .GroupBy(s => s.VideoId)
                .Select(g => g.First())
                .OrderBy(x => rng.Next()) // Zamíchat
                .Take(20)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                LibraryBasedRecommendations.Clear();
                foreach (var s in finalSongs) LibraryBasedRecommendations.Add(s);

                HasPersonalizedRecommendations = LibraryBasedRecommendations.Count > 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chyba při generování doporučení: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StartMixAsync(SongModel? song)
    {
        if (song == null) return;
        
        // Zastavit aktuální přehrávání
        _audioService.Stop();
        
        // Vyčistit frontu
        PlaybackQueue.Clear();
        _originalQueue.Clear();
        CurrentQueueIndex = 0;
        
        // Přidat na první místo
        PlaybackQueue.Add(song);
        _originalQueue.Add(song);
        
        // Nyní rovnou spustíme přehrávání aktuální skladby, nekonečná fronta
        // se postará o zbytek jakmile se přehraje (PlaybackQueue.Count - 0 <= 3)
        await PlayQueueCurrentSongAsync();
        
        // Přepneme view na Frontu
        IsFullScreenPlayerVisible = true;
    }

    [RelayCommand]
    private void PlaySongNext(SongModel? song)
    {
        if (song == null) return;
        if (PlaybackQueue.Any(s => s.VideoId == song.VideoId))
        {
            StatusMessage = $"Skladba '{song.Title}' už ve frontě je.";
            return;
        }
        
        if (PlaybackQueue.Count == 0)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
            CurrentQueueIndex = 0;
            _ = PlayQueueCurrentSongAsync();
        }
        else
        {
            // Vložíme ihned za právě hrající skladbu
            PlaybackQueue.Insert(CurrentQueueIndex + 1, song);
            _originalQueue.Add(song);
            StatusMessage = $"Skladba '{song.Title}' zařazena jako další.";
        }
    }

    [RelayCommand]
    private void AddToQueue(SongModel? song)
    {
        if (song == null) return;
        if (PlaybackQueue.Any(s => s.VideoId == song.VideoId))
        {
            StatusMessage = $"Skladba '{song.Title}' už ve frontě je.";
            return;
        }
        
        if (PlaybackQueue.Count == 0)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
            CurrentQueueIndex = 0;
            _ = PlayQueueCurrentSongAsync();
        }
        else
        {
            // Vložíme na konec
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
            StatusMessage = $"Skladba '{song.Title}' přidána na konec fronty.";
        }
    }

    private SongModel? _draggedSong;

    [RelayCommand]
    private void DragStarting(SongModel? song)
    {
        _draggedSong = song;
    }

    [RelayCommand]
    private void Drop(SongModel? targetSong)
    {
        if (_draggedSong == null || targetSong == null || _draggedSong == targetSong)
        {
            _draggedSong = null;
            return;
        }

        int oldIndex = PlaybackQueue.IndexOf(_draggedSong);
        int newIndex = PlaybackQueue.IndexOf(targetSong);
        
        if (oldIndex >= 0 && newIndex >= 0)
        {
            PlaybackQueue.Move(oldIndex, newIndex);
            
            // Sync _originalQueue
            var origOldIndex = _originalQueue.IndexOf(_draggedSong);
            if (origOldIndex >= 0)
            {
                _originalQueue.RemoveAt(origOldIndex);
                var targetOrigIndex = _originalQueue.IndexOf(targetSong);
                if (targetOrigIndex >= 0)
                {
                    if (oldIndex > newIndex)
                        _originalQueue.Insert(targetOrigIndex, _draggedSong);
                    else
                        _originalQueue.Insert(targetOrigIndex + 1, _draggedSong);
                }
                else
                {
                    _originalQueue.Add(_draggedSong);
                }
            }

            // Sync CurrentQueueIndex if we moved the currently playing song, 
            // or if we moved a song before/after the currently playing song.
            if (CurrentSong != null)
            {
                CurrentQueueIndex = PlaybackQueue.IndexOf(CurrentSong);
            }
        }
        
        _draggedSong = null;
    }

    // --- Nové YT Music Web funkce: Like/Dislike, Mute, Explore & Nálady 🎶 ---

    [RelayCommand]
    public async Task ToggleLikeCurrentSongAsync()
    {
        if (CurrentSong == null) return;
        var song = CurrentSong;
        song.IsLiked = !song.IsLiked;
        IsCurrentSongLiked = song.IsLiked;

        if (song.IsLiked)
        {
            StatusMessage = $"Přidáno do Oblíbených ❤️: {song.Title}";
            await _ytService.LikeSongAsync(song.VideoId);
        }
        else
        {
            StatusMessage = $"Odebráno z Oblíbených: {song.Title}";
            await _ytService.RemoveLikeAsync(song.VideoId);
        }
    }

    [RelayCommand]
    public async Task DislikeCurrentSongAsync()
    {
        if (CurrentSong == null) return;
        var song = CurrentSong;
        StatusMessage = $"Skladba '{song.Title}' označena jako Nelíbí se 👎";
        await _ytService.DislikeSongAsync(song.VideoId);
        await PlayNextSongAsync();
    }

    private double _previousVolume = 0.5;

    [RelayCommand]
    public void ToggleMute()
    {
        if (Volume > 0.001)
        {
            _previousVolume = Volume;
            Volume = 0;
            StatusMessage = "Zvuk ztlumen (Mute).";
        }
        else
        {
            Volume = _previousVolume > 0.01 ? _previousVolume : 0.5;
            StatusMessage = $"Hlasitost obnovena: {(int)(Volume * 100)}%";
        }
    }

    [RelayCommand]
    public async Task SetSearchFilterAsync(string filter)
    {
        SelectedSearchFilter = filter;
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            await PerformSearchAsync();
        }
    }

    [RelayCommand]
    public async Task LoadExploreDataAsync()
    {
        if (ExploreCharts.Count > 0 && MoodCategories.Count > 0) return;

        IsExploreLoading = true;
        StatusMessage = "Načítám Objevovat & Žebříčky 🎶...";

        try
        {
            if (MoodCategories.Count == 0)
            {
                MoodCategories.Add(new() { Title = "Relax & Chill", Icon = "🧘", Description = "Klidné melodie, lo-fi a ambient pro odpočinek", Query = "Relax", GradientStart = "#20B2AA", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Energie", Icon = "⚡", Description = "Povzbuzující rytmy, dance a vysoká energie", Query = "Energie", GradientStart = "#FF8C00", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Cvičení & Workout", Icon = "🏋️", Description = "Motivace do posilovny a rychlé tempo", Query = "Cvičení", GradientStart = "#E63946", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Soustředění & Práce", Icon = "🧠", Description = "Lo-Fi a instrumentální hudba na studium", Query = "Soustředění", GradientStart = "#457B9D", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Párty & Klub", Icon = "🎉", Description = "Největší taneční hity na večírek", Query = "Párty", GradientStart = "#9D4EDD", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Rock & Metal", Icon = "🎸", Description = "Kytary, klasický rock i moderní metal", Query = "Rock", GradientStart = "#D62828", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Pop & Hity", Icon = "🎤", Description = "Aktuální globální a domácí popové žebříčky", Query = "Pop", GradientStart = "#FF006E", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Hip-Hop & Rap", Icon = "🎧", Description = "Trap, boom bap a moderní rapové beaty", Query = "Hip-Hop", GradientStart = "#FB5607", GradientEnd = "#1E202C" });
                MoodCategories.Add(new() { Title = "Elektronika & EDM", Icon = "🎹", Description = "Synthwave, house, techno a electro", Query = "Elektronika", GradientStart = "#28C6D0", GradientEnd = "#1E202C" });
            }

            var charts = await _ytService.GetExploreChartsAsync();
            ExploreCharts.Clear();
            foreach (var s in charts)
            {
                ExploreCharts.Add(s);
            }
            StatusMessage = $"Objevovat: Načteno {charts.Count} skladeb v žebříčcích 🎶.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při načítání Explore: {ex.Message}";
        }
        finally
        {
            IsExploreLoading = false;
        }
    }

    [RelayCommand]
    public async Task PlayAllExploreChartsAsync()
    {
        if (ExploreCharts.Count == 0) return;
        PlaybackQueue.Clear();
        _originalQueue.Clear();
        foreach (var song in ExploreCharts)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }
        CurrentQueueIndex = 0;
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    public async Task ShuffleExploreChartsAsync()
    {
        if (ExploreCharts.Count == 0) return;
        PlaybackQueue.Clear();
        _originalQueue.Clear();
        foreach (var song in ExploreCharts)
        {
            PlaybackQueue.Add(song);
            _originalQueue.Add(song);
        }
        CurrentQueueIndex = 0;
        IsShuffle = true;
        ApplyShuffle();
        await PlayQueueCurrentSongAsync();
    }

    [RelayCommand]
    public async Task PlayMoodAsync(MoodModel? mood)
    {
        if (mood == null) return;
        IsBusy = true;
        StatusMessage = $"Načítám náladu: {mood.Title} 🎶...";
        try
        {
            var songs = await _ytService.GetMoodSongsAsync(mood.Query);
            if (songs.Count > 0)
            {
                PlaybackQueue.Clear();
                _originalQueue.Clear();
                foreach (var s in songs)
                {
                    PlaybackQueue.Add(s);
                    _originalQueue.Add(s);
                }
                CurrentQueueIndex = 0;
                await PlayQueueCurrentSongAsync();
            }
            else
            {
                StatusMessage = $"Pro náladu {mood.Title} nebyly nalezeny žádné skladby.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při přehrávání nálady: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SelectMoodAsync(string mood)
    {
        if (string.IsNullOrWhiteSpace(mood)) return;

        // Toggle: pokud uživatel klikne na již aktivní náladu (která není "Vše"), vrátíme se zpět na "Vše"
        if (string.Equals(SelectedMood, mood, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mood, "Vše", StringComparison.OrdinalIgnoreCase))
        {
            mood = "Vše";
        }

        SelectedMood = mood;
        foreach (var f in MoodFilters)
        {
            f.IsSelected = string.Equals(f.Title, mood, StringComparison.OrdinalIgnoreCase);
        }

        IsBusy = true;
        StatusMessage = mood == "Vše" ? "Načítám domovskou stránku 🎶..." : $"Načítám hudbu pro: {mood} 🎶...";
        try
        {
            if (string.Equals(mood, "Vše", StringComparison.OrdinalIgnoreCase))
            {
                TextRecommendedMusic = "Doporučená hudba >";
                await LoadHomeRecommendationsAsync();
            }
            else
            {
                TextRecommendedMusic = $"Doporučená hudba pro: {mood}";
                var sections = await _ytService.GetMoodHomeSectionsAsync(mood);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    HomeSections.Clear();
                    if (sections != null && sections.Count > 0)
                    {
                        foreach (var s in sections) HomeSections.Add(s);
                        HasHomeSections = true;
                    }
                    else
                    {
                        HasHomeSections = false;
                    }

                    HomeRecommendations.Clear();
                    var songSection = sections?.FirstOrDefault(s => s.Items.Any(i => i.IsSong));
                    if (songSection != null)
                    {
                        foreach (var item in songSection.Items.Where(i => i.Song != null))
                        {
                            HomeRecommendations.Add(item.Song!);
                        }
                    }
                });
                StatusMessage = $"Nálada '{mood}' načtena 🎶.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při načítání nálady: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
