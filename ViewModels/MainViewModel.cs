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

namespace Melodium.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MelodiumService _ytService;
    private readonly IAudioService _audioService;
    private readonly TranslationService _translationService;
    private System.Threading.CancellationTokenSource? _downloadCts;
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

    [ObservableProperty] public partial string TextHome { get; set; } = "Domů";
    [ObservableProperty] public partial string TextSearch { get; set; } = "Hledat";
    [ObservableProperty] public partial string TextLibrary { get; set; } = "Knihovna";
    [ObservableProperty] public partial string TextQueue { get; set; } = "Fronta";
    [ObservableProperty] public partial string TextAccount { get; set; } = "Účet";
    [ObservableProperty] public partial string TextLogout { get; set; } = "Odhlásit";
    [ObservableProperty] public partial string TextSettings { get; set; } = "Nastavení";
    [ObservableProperty] public partial string TextLanguageSelection { get; set; } = "Výběr jazyka";
    
    [ObservableProperty] public partial bool IsDiscordRpcEnabled { get; set; }
    [ObservableProperty] public partial string TextEnableDiscordRpc { get; set; } = "Zobrazovat aktivitu na Discordu";

    partial void OnIsDiscordRpcEnabledChanged(bool value)
    {
        Preferences.Default.Set("IsDiscordRpcEnabled", value);
        _discordRpcService?.HandleSettingsChanged();
    }

    [ObservableProperty] public partial bool IsCloseToTrayEnabled { get; set; } = true;
    [ObservableProperty] public partial string TextCloseToTray { get; set; } = "Zavřít do oznamovací oblasti";
    [ObservableProperty] public partial string TextCloseToTrayDesc { get; set; } = "Při kliknutí na křížek (zavření okna) zůstane aplikace spuštěná v oznamovací oblasti na hlavním panelu.";

    partial void OnIsCloseToTrayEnabledChanged(bool value)
    {
        Preferences.Default.Set("IsCloseToTrayEnabled", value);
    }

    [ObservableProperty] public partial string TextExitApp { get; set; } = "Ukončení aplikace";
    [ObservableProperty] public partial string TextExitAppDesc { get; set; } = "Zcela ukončí aplikaci Melodium a uvolní všechny procesy a prostředky na pozadí.";
    [ObservableProperty] public partial string TextExitButton { get; set; } = "Ukončit aplikaci";
    
    [ObservableProperty] public partial string TextSearchPlaceholder { get; set; } = "Hledat skladby, interprety, alba...";
    [ObservableProperty] public partial string TextLanguageDescription { get; set; } = "Vyberte preferovaný jazyk aplikace. Seznam obsahuje všechny dostupné světové jazyky.";
    [ObservableProperty] public partial string TextHeroSubtitle { get; set; } = "Poslouchej hudbu bez omezení a bez reklam";
    [ObservableProperty] public partial string TextStartListening { get; set; } = "Začít poslouchat";
    [ObservableProperty] public partial string TextRecommendedMusic { get; set; } = "Doporučená hudba";
    [ObservableProperty] public partial string TextLoadingRecommendations { get; set; } = "Načítám doporučení...";
    [ObservableProperty] public partial string TextPersonalizedSongs { get; set; } = "Doporučené skladby přímo pro vás";
    [ObservableProperty] public partial string TextLockedLibrary { get; set; } = "Uzamčená Knihovna";
    [ObservableProperty] public partial string TextLockedLibraryDesc { get; set; } = "Chcete-li zobrazit své skladby, playlisty a alba z Melodium, musíte se přihlásit.";
    [ObservableProperty] public partial string TextGoToLogin { get; set; } = "Přejít k přihlášení";
    [ObservableProperty] public partial string TextMusic { get; set; } = "Hudba";
    [ObservableProperty] public partial string TextSongs { get; set; } = "Skladby";
    [ObservableProperty] public partial string TextPlaylists { get; set; } = "Playlisty";
    [ObservableProperty] public partial string TextAlbums { get; set; } = "Alba";
    [ObservableProperty] public partial string TextArtists { get; set; } = "Interpreti";
    [ObservableProperty] public partial string TextAddFolder { get; set; } = "Přidat složku";
    [ObservableProperty] public partial string TextShuffleAndPlay { get; set; } = "Zamíchat a přehrát";
    [ObservableProperty] public partial string TextSortBy { get; set; } = "Řadit dle: Názvu";
    [ObservableProperty] public partial string TextUnknownGenre { get; set; } = "Neznámý žánr";
    [ObservableProperty] public partial string TextPlay { get; set; } = "Přehrát";
    [ObservableProperty] public partial string TextLoginInstructions { get; set; } = "Instrukce pro přihlášení";
    [ObservableProperty] public partial string TextLoginInstruction1 { get; set; } = "1. Přihlaste se ke svému Google / YouTube účtu přímo v okně níže.";
    [ObservableProperty] public partial string TextLoginInstruction2 { get; set; } = "2. Po úspěšném přihlášení a načtení hlavní stránky Melodium vás aplikace automaticky připojí a stáhne vaši osobní knihovnu.";
    [ObservableProperty] public partial string TextPlaybackQueue { get; set; } = "Fronta přehrávání";
    [ObservableProperty] public partial string TextClearQueue { get; set; } = "Vyčistit frontu";
    [ObservableProperty] public partial string TextEmptyQueue { get; set; } = "Fronta je prázdná";
    [ObservableProperty] public partial string TextEmptyQueueDesc { get; set; } = "Najděte nějaké skladby a spusťte přehrávání.";
    [ObservableProperty] public partial string TextRemoveFromQueue { get; set; } = "Odebrat z fronty";
    [ObservableProperty] public partial string TextSongsCountLabel { get; set; } = "Skladeb:";
    [ObservableProperty] public partial string TextReleaseYearLabel { get; set; } = "Rok:";
    [ObservableProperty] public partial string TextSubscribersLabel { get; set; } = "Odběratelé:";
    [ObservableProperty] public partial string TextSongsInQueueLabel { get; set; } = "Skladeb ve frontě:";
    [ObservableProperty] public partial string TextStatusReady { get; set; } = "Připraveno. Zvol sekci a hraj.";
    [ObservableProperty] public partial string TextStatusLibraryEmpty { get; set; } = "Knihovna skladeb je prázdná, míchám z doporučené hudby...";
    [ObservableProperty] public partial string TextStatusShuffleFailed { get; set; } = "Nelze zahájit míchání - žádné dostupné skladby.";
    [ObservableProperty] public partial string TextStatusLibraryLoaded { get; set; } = "Vaše knihovna a doporučení byly úspěšně načteny.";

    [ObservableProperty] public partial string TextStartMix { get; set; } = "Spustit mix";
    [ObservableProperty] public partial string TextPlayNext { get; set; } = "Přehrát jako další";
    [ObservableProperty] public partial string TextAddToQueue { get; set; } = "Přidat do fronty";
    [ObservableProperty] public partial string TextSaveToPlaylist { get; set; } = "Uložit do playlistu";
    [ObservableProperty] public partial string TextGoToAlbum { get; set; } = "Přejít do alba";
    [ObservableProperty] public partial string TextGoToArtist { get; set; } = "Přejít na interpreta";
    [ObservableProperty] public partial string TextShare { get; set; } = "Sdílet";

    private readonly Dictionary<string, string> _baseTexts = new()
    {
        { nameof(TextHome), "Domů" },
        { nameof(TextSearch), "Hledat" },
        { nameof(TextLibrary), "Knihovna" },
        { nameof(TextQueue), "Fronta" },
        { nameof(TextAccount), "Účet" },
        { nameof(TextLogout), "Odhlásit" },
        { nameof(TextSettings), "Nastavení" },
        { nameof(TextEnableDiscordRpc), "Zobrazovat aktivitu na Discordu" },
        { nameof(TextCloseToTray), "Zavřít do oznamovací oblasti" },
        { nameof(TextCloseToTrayDesc), "Při kliknutí na křížek (zavření okna) zůstane aplikace spuštěná v oznamovací oblasti na hlavním panelu." },
        { nameof(TextExitApp), "Ukončení aplikace" },
        { nameof(TextExitAppDesc), "Zcela ukončí aplikaci Melodium a uvolní všechny procesy a prostředky na pozadí." },
        { nameof(TextExitButton), "Ukončit aplikaci" },
        { nameof(TextLanguageSelection), "Výběr jazyka" },
        { nameof(TextSearchPlaceholder), "Hledat skladby, interprety, alba..." },
        { nameof(TextLanguageDescription), "Vyberte preferovaný jazyk aplikace. Seznam obsahuje všechny dostupné světové jazyky." },
        { nameof(TextHeroSubtitle), "Poslouchej hudbu bez omezení a bez reklam" },
        { nameof(TextStartListening), "Začít poslouchat" },
        { nameof(TextRecommendedMusic), "Doporučená hudba >" },
        { nameof(TextLoadingRecommendations), "Načítám doporučení..." },
        { nameof(TextPersonalizedSongs), "Doporučené skladby přímo pro vás" },
        { nameof(TextLockedLibrary), "Uzamčená Knihovna" },
        { nameof(TextLockedLibraryDesc), "Chcete-li zobrazit své skladby, playlisty a alba z Melodium, musíte se přihlásit." },
        { nameof(TextGoToLogin), "Přejít k přihlášení" },
        { nameof(TextMusic), "Hudba" },
        { nameof(TextSongs), "Skladby" },
        { nameof(TextAlbums), "Alba" },
        { nameof(TextArtists), "Interpreti" },
        { nameof(TextAddFolder), "Přidat složku" },
        { nameof(TextShuffleAndPlay), "Zamíchat a přehrát" },
        { nameof(TextSortBy), "Řadit dle: Názvu" },
        { nameof(TextUnknownGenre), "Neznámý žánr" },
        { nameof(TextPlay), "Přehrát" },
        { nameof(TextLoginInstructions), "Instrukce pro přihlášení" },
        { nameof(TextLoginInstruction1), "1. Přihlaste se ke svému Google / YouTube účtu přímo v okně níže." },
        { nameof(TextLoginInstruction2), "2. Po úspěšném přihlášení a načtení hlavní stránky Melodium vás aplikace automaticky připojí a stáhne vaši osobní knihovnu." },
        { nameof(TextPlaybackQueue), "Fronta přehrávání" },
        { nameof(TextClearQueue), "Vyčistit frontu" },
        { nameof(TextEmptyQueue), "Fronta je prázdná" },
        { nameof(TextEmptyQueueDesc), "Najděte nějaké skladby a spusťte přehrávání." },
        { nameof(TextSongsCountLabel), "Skladeb:" },
        { nameof(TextReleaseYearLabel), "Rok:" },
        { nameof(TextSubscribersLabel), "Odběratelé:" },
        { nameof(TextSongsInQueueLabel), "Skladeb ve frontě:" },
        { nameof(TextStatusReady), "Připraveno. Zvol sekci a hraj." },
        { nameof(TextStatusLibraryEmpty), "Knihovna skladeb je prázdná, míchám z doporučené hudby..." },
        { nameof(TextStatusShuffleFailed), "Nelze zahájit míchání - žádné dostupné skladby." },
        { nameof(TextStatusLibraryLoaded), "Vaše knihovna a doporučení byly úspěšně načteny." },
        { nameof(TextStartMix), "Spustit mix" },
        { nameof(TextPlayNext), "Přehrát jako další" },
        { nameof(TextAddToQueue), "Přidat do fronty" },
        { nameof(TextSaveToPlaylist), "Uložit do playlistu" },
        { nameof(TextGoToAlbum), "Přejít do alba" },
        { nameof(TextGoToArtist), "Přejít na interpreta" },
        { nameof(TextShare), "Sdílet" }
    };

    private async Task UpdateLocalizedStringsAsync(CultureInfo culture)
    {
        string targetLang = culture.TwoLetterISOLanguageName;
        
        foreach (var kvp in _baseTexts)
        {
            string translated = await _translationService.TranslateAsync(kvp.Value, targetLang);
            var prop = this.GetType().GetProperty(kvp.Key);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(this, translated);
            }
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
    public partial string LibraryTab { get; set; } = "Songs"; // Songs, Playlists, Albums, Artists

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

    public bool IsSearchResultsEmpty => HasSearched && SearchResults.Count == 0 && !IsBusy;

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
    public ObservableCollection<SongModel> HomeRecommendations { get; } = new();
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

    private readonly DiscordRpcService _discordRpcService;
    private readonly LyricsService _lyricsService;

    public MainViewModel(MelodiumService ytService, IAudioService audioService, TranslationService translationService, DiscordRpcService discordRpcService, LyricsService lyricsService)
    {
        _ytService = ytService;
        _audioService = audioService;
        _translationService = translationService;
        _discordRpcService = discordRpcService;
        _lyricsService = lyricsService;

        IsDiscordRpcEnabled = Preferences.Default.Get("IsDiscordRpcEnabled", false);
        IsCloseToTrayEnabled = Preferences.Default.Get("IsCloseToTrayEnabled", true);
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

        var savedLang = Preferences.Default.Get("AppLanguage", "cs");
        SelectedLanguage = Languages.FirstOrDefault(c => c.Name == savedLang) ?? Languages.FirstOrDefault(c => c.TwoLetterISOLanguageName == "cs");

        // Načteme domovskou obrazovku nezávisle na přihlášení
        _ = Task.Run(LoadHomeRecommendationsAsync);

        // Načteme uložené přihlášení při startu
        _ = Task.Run(LoadSavedSessionAsync);
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

        CurrentView = "Search";
        IsBusy = true;
        SearchResults.Clear();
        StatusMessage = $"Vyhledávám: {SearchQuery}...";

        await _ytService.EnsureInitializedAsync();
        var songs = await _ytService.SearchSongsAsync(SearchQuery);

        HasSearched = true;
        foreach (var song in songs)
        {
            SearchResults.Add(song);
        }

        OnPropertyChanged(nameof(IsSearchResultsEmpty));
        StatusMessage = $"Hledání dokončeno. Nalezeno {songs.Count} skladeb.";
        IsBusy = false;
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
        foreach (var p in LibraryPlaylists.Where(p => p.CanEdit))
        {
            EditablePlaylists.Add(p);
        }
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

        if (CurrentView != "Playlist")
        {
            _previousView = CurrentView;
        }

        CurrentView = "Playlist";
        CurrentPlaylist = playlist;
        CurrentPlaylistSongs.Clear();
        IsPlaylistLoading = true;
        StatusMessage = $"Načítám playlist {playlist.Title}...";

        try
        {
            var details = await _ytService.GetPlaylistDetailsAsync(playlist.Id);
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při načítání playlistu: {ex.Message}";
        }
        finally
        {
            IsPlaylistLoading = false;
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

    public async Task AddSongToPlaylistAsync(PlaylistModel playlist, SongModel song)
    {
        if (!playlist.CanEdit)
        {
            StatusMessage = "Do tohoto playlistu nemáte oprávnění přidávat skladby.";
            return;
        }

        StatusMessage = $"Přidávám '{song.Title}' do playlistu '{playlist.Title}'...";
        bool success = await _ytService.AddSongToPlaylistAsync(playlist.Id, song.VideoId);
        if (success)
        {
            playlist.SongCount++;
            if (CurrentPlaylist?.Id == playlist.Id)
            {
                CurrentPlaylistSongs.Add(song);
            }
            StatusMessage = $"Skladba '{song.Title}' byla přidána do '{playlist.Title}'.";
        }
        else
        {
            StatusMessage = "Chyba: Nepodařilo se přidat skladbu do playlistu na YouTube Music.";
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

    private async Task PlayQueueCurrentSongAsync()
    {
        if (CurrentQueueIndex < 0 || CurrentQueueIndex >= PlaybackQueue.Count) return;

        // Stop current playback
        _audioService.Stop();

        // Cancel previous download
        _downloadCts?.Cancel();
        _downloadCts = new System.Threading.CancellationTokenSource();
        var token = _downloadCts.Token;

        var song = PlaybackQueue[CurrentQueueIndex];
        CurrentSong = song;
        SelectedQueueSong = song;
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
            StatusMessage = $"[1/4] Získávám stream URL pro: {song.VideoId}...";
            VmLog($"Getting stream URL for videoId={song.VideoId}");

            await _ytService.EnsureInitializedAsync();
            var streamUrl = await _ytService.GetAudioStreamUrlAsync(song.VideoId, $"{song.Title} {song.Artist}");
            token.ThrowIfCancellationRequested();

            // Nekonečná fronta (Auto-play / Radio)
            // Pokud se blížíme ke konci fronty (zbývají 3 a méně skladeb), načteme další doporučené k aktuální
            if (PlaybackQueue.Count - CurrentQueueIndex <= 3)
            {
                _ = Task.Run(async () =>
                {
                    var upNext = await _ytService.GetUpNextRadioAsync(song.VideoId);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        foreach (var nextSong in upNext)
                        {
                            // Přidat pouze skladby, které ještě ve frontě nebyly, abychom zamezili duplicitám
                            if (!_originalQueue.Any(s => s.VideoId == nextSong.VideoId))
                            {
                                PlaybackQueue.Add(nextSong);
                                _originalQueue.Add(nextSong);
                            }
                        }
                    });
                });
            }

            VmLog($"Stream URL result: {(streamUrl == null ? "NULL" : $"length={streamUrl.Length}, first100={streamUrl[..Math.Min(100, streamUrl.Length)]}")}");
            VmLog($"Stream URL has pot={streamUrl?.Contains("&pot=") == true}, has sig={streamUrl?.Contains("&sig=") == true}");
            VmLog($"FULL URL: {streamUrl}");
            if (!string.IsNullOrEmpty(streamUrl))
            {
                StatusMessage = $"[2/4] Stream URL získáno. Stahuji: {song.Title}...";

                await _audioService.PlayFromUrlAsync(streamUrl, token, msg =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StatusMessage = $"{song.Title} — {msg}";
                    });
                });

                VmLog("PlayFromUrlAsync returned (přehrávání je event-driven).");

                // Stav přehrávání teď řídí události MediaElementu (OnAudioPlaybackStateChanged /
                // OnAudioPlaybackError), tady jen nastavíme úvodní hlášku.
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusMessage = $"▶ {song.Title}";
                });
            }
            else
            {
                StatusMessage = "❌ GetAudioStreamUrlAsync vrátilo NULL — nepodařilo se získat stream.";
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
                    
                    _ = Task.Run(LoadLibraryAsync);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nepodařilo se načíst relaci: {ex.Message}";
        }
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
            var homeSongs = await _ytService.GetHomeRecommendationsAsync();
            
            // Pokud Youtube vrátí prázdný seznam (např. u nepřihlášených účtů), uděláme fallback search
            if (homeSongs == null || homeSongs.Count == 0)
            {
                homeSongs = await _ytService.SearchSongsAsync("Top 100 hitů");
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                HomeRecommendations.Clear();
                foreach (var h in homeSongs) HomeRecommendations.Add(h);
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při stahování doporučení: {ex.Message}";
        }
    }

    public async Task LoadLibraryAsync()
    {
        IsBusy = true;
        StatusMessage = "Načítám domovskou obrazovku a knihovnu z Melodium...";
        try
        {
            await _ytService.EnsureInitializedAsync();
            
            _ = Task.Run(LoadHomeRecommendationsAsync);
            
            var songs = await _ytService.GetLibrarySongsAsync();
            var playlists = await _ytService.GetLibraryPlaylistsAsync();
            var albums = await _ytService.GetLibraryAlbumsAsync();
            var artists = await _ytService.GetLibraryArtistsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {

                LibrarySongs.Clear();
                foreach (var s in songs) LibrarySongs.Add(s);

                LibraryPlaylists.Clear();
                foreach (var p in playlists) LibraryPlaylists.Add(p);
                UpdateEditablePlaylists();

                LibraryAlbums.Clear();
                foreach (var a in albums) LibraryAlbums.Add(a);

                LibraryArtists.Clear();
                foreach (var art in artists) LibraryArtists.Add(art);

                StatusMessage = TextStatusLibraryLoaded;
            });

            _ = Task.Run(() => GenerateLibraryRecommendationsAsync(songs.ToList(), artists.ToList(), albums.ToList(), playlists.ToList()));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba při stahování knihovny: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
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


}
