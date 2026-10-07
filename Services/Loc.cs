using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;

namespace Melodium.Services;

public partial class Loc : ObservableObject
{
    public static Loc Instance { get; } = new();

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
    [ObservableProperty] public partial string TextLanguageDescription { get; set; } = "Select your preferred language. All world languages are supported.";
    [ObservableProperty] public partial string TextResumeFromOtherDevice { get; set; } = "Resume from another device";
    [ObservableProperty] public partial string TextResumeAction { get; set; } = "Resume";
    [ObservableProperty] public partial string TextSearchPlaceholder { get; set; } = "Search songs, artists, albums...";
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
    [ObservableProperty] public partial string TextPlayNext { get; set; } = "Play next";
    [ObservableProperty] public partial string TextAddToQueue { get; set; } = "Add to queue";
    [ObservableProperty] public partial string TextSaveToPlaylist { get; set; } = "Add to playlist";
    [ObservableProperty] public partial string TextGoToAlbum { get; set; } = "Go to album";
    [ObservableProperty] public partial string TextGoToArtist { get; set; } = "Go to artist";
    [ObservableProperty] public partial string TextShare { get; set; } = "Share";
    [ObservableProperty] public partial string TextStartMix { get; set; } = "Start radio";
    [ObservableProperty] public partial string TextShuffle { get; set; } = "Shuffle";
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
    [ObservableProperty] public partial string TextEnableDiscordRpc { get; set; } = "Display activity on Discord";
    [ObservableProperty] public partial string TextCloseToTray { get; set; } = "Minimize to system tray";
    [ObservableProperty] public partial string TextCloseToTrayDesc { get; set; } = "When closing the window, keep Melodium running in the notification area.";
    [ObservableProperty] public partial string TextExitApp { get; set; } = "Exit Application";
    [ObservableProperty] public partial string TextExitAppDesc { get; set; } = "Completely exits Melodium and frees all background processes.";
    [ObservableProperty] public partial string TextExitButton { get; set; } = "Exit application";
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
    [ObservableProperty] public partial string TextLoginInstructions { get; set; } = "Login Instructions";
    [ObservableProperty] public partial string TextLoginInstruction1 { get; set; } = "1. Sign in to your Google / YouTube account in the window below.";
    [ObservableProperty] public partial string TextLoginInstruction2 { get; set; } = "2. Once signed in, Melodium will automatically connect and load your personal library.";
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

    public void ApplyDictionary(IDictionary<string, string> dict)
    {
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
        });
    }
}
