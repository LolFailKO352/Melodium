using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Melodium.Services;

public class TranslationService
{
    private static readonly string TranslationsCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Melodium", "Translations");

    private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10,
        EnableMultipleHttp2Connections = true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly ConcurrentDictionary<string, string> _cache = new();

    public static readonly Dictionary<string, string> English = new()
    {
        { "TextHome", "Home" },
        { "TextExplore", "Explore 🎶" },
        { "TextSearch", "Search" },
        { "TextLibrary", "Library" },
        { "TextQueue", "Queue" },
        { "TextAccount", "Account" },
        { "TextLogout", "Log out" },
        { "TextLogin", "Log in" },
        { "TextSettings", "Settings" },
        { "TextLanguageSelection", "Language Selection" },
        { "TextLanguageDescription", "Select your preferred language. All world languages are supported." },
        { "TextSearchPlaceholder", "Search songs, artists, albums..." },
        { "TextHeroSubtitle", "Listen to music without limits and without ads" },
        { "TextStartListening", "Start listening" },
        { "TextRecommendedMusic", "Recommended music >" },
        { "TextLoadingRecommendations", "Loading recommendations..." },
        { "TextPersonalizedSongs", "Recommended tracks just for you" },
        { "TextLockedLibrary", "Locked Library" },
        { "TextLockedLibraryDesc", "To view your songs, playlists, and albums from Melodium, please log in." },
        { "TextGoToLogin", "Go to login" },
        { "TextMusic", "Music" },
        { "TextLikedSongs", "Liked Songs" },
        { "TextSongs", "Songs" },
        { "TextPlaylists", "Playlists" },
        { "TextAlbums", "Albums" },
        { "TextArtists", "Artists" },
        { "TextAddFolder", "Add folder" },
        { "TextShuffleAndPlay", "Shuffle & Play" },
        { "TextSortBy", "Sort by: Title" },
        { "TextUnknownGenre", "Unknown genre" },
        { "TextPlay", "Play" },
        { "TextPlayAll", "Play all" },
        { "TextPlayNext", "Play next" },
        { "TextAddToQueue", "Add to queue" },
        { "TextSaveToPlaylist", "Add to playlist" },
        { "TextGoToAlbum", "Go to album" },
        { "TextGoToArtist", "Go to artist" },
        { "TextShare", "Share" },
        { "TextStartMix", "Start radio" },
        { "TextShuffle", "Shuffle" },
        { "TextPlaybackQueue", "Playback Queue" },
        { "TextClearQueue", "Clear queue" },
        { "TextEmptyQueue", "Queue is empty" },
        { "TextEmptyQueueDesc", "Find some songs and start listening." },
        { "TextRemoveFromQueue", "Remove from queue" },
        { "TextSongsCountLabel", "Songs:" },
        { "TextReleaseYearLabel", "Year:" },
        { "TextSubscribersLabel", "Subscribers:" },
        { "TextSongsInQueueLabel", "Songs in queue:" },
        { "TextStatusReady", "Ready. Select music and enjoy." },
        { "TextStatusLibraryEmpty", "Library is empty, playing recommendations..." },
        { "TextStatusShuffleFailed", "Cannot shuffle - no songs available." },
        { "TextStatusLibraryLoaded", "Your library and recommendations were loaded." },
        { "TextEnableDiscordRpc", "Display activity on Discord" },
        { "TextCloseToTray", "Minimize to system tray" },
        { "TextCloseToTrayDesc", "When closing the window, keep Melodium running in the notification area." },
        { "TextExitApp", "Exit Application" },
        { "TextExitAppDesc", "Completely exits Melodium and frees all background processes." },
        { "TextExitButton", "Exit application" },
        { "TextUpdateSettings", "Application Updates" },
        { "TextCheckForUpdates", "Check for updates" },
        { "TextCheckForUpdatesDesc", "Checks for the latest Melodium release on GitHub." },
        { "TextCheckingForUpdates", "Checking for updates..." },
        { "TextAppUpToDate", "Melodium is up to date." },
        { "TextUpdateAvailable", "A new update is available!" },
        { "TextDownloadAndInstall", "Download and update" },
        { "TextDownloadAndInstallDesc", "Downloads and launches the update installer." },
        { "TextDownloadingUpdate", "Downloading update..." },
        { "TextCurrentVersionLabel", "Installed version:" },
        { "TextLatestVersionLabel", "Latest version on GitHub:" },
        { "TextChangelogLabel", "Changelog:" },
        { "TextOpenOnGitHub", "View on GitHub" },
        { "TextLoginInstructions", "Login Instructions" },
        { "TextLoginInstruction1", "1. Sign in to your Google / YouTube account in the window below." },
        { "TextLoginInstruction2", "2. Once signed in, Melodium will automatically connect and load your personal library." },
        { "TextDiscoverNewMusic", "Discover new music" },
        { "TextDiscoverSubtitle", "Latest hits, global charts, and playlists for every mood." },
        { "TextPlayCharts", "Play charts 🎶" },
        { "TextShuffleCharts", "Shuffle charts 🔀" },
        { "TextMoodsAndGenres", "Moods & Genres" },
        { "TextChartsAndTrends", "Charts & Trends 🎶" },
        { "TextRefresh", "Refresh" },
        { "TextSearchResults", "Search results" },
        { "TextBackToHome", "Back to home" },
        { "TextFilterAll", "All" },
        { "TextFilterRelax", "Relax" },
        { "TextFilterEnergy", "Energy" },
        { "TextFilterWorkout", "Workout" },
        { "TextFilterFocus", "Focus" },
        { "TextAddSongToPlaylist", "Add song to playlist" },
        { "TextAddSong", "Add song" },
        { "TextNoResults", "No results found" },
        { "TextNoResultsDesc", "Try searching for a different artist, song title, or album." },
        { "TextOpenFullPage", "Open full page" },
        { "TextOpenPlaylist", "Open playlist" },
        { "TextEditable", "• Editable" },
        { "TextPopularSongs", "Popular songs" },
        { "TextShowAll", "Show all" },
        { "TextSinglesAndEps", "Singles & EPs" },
        { "TextBack", "Back" },
        { "TextEditablePlaylist", "Editable playlist" },
        { "TextReadOnlyPlaylist", "Shared playlist • Read-only" },
        { "TextSearchAndAddSong", "Search and add song" },
        { "TextSearchSongInYtMusic", "Search songs on YouTube Music..." },
        { "TextSongsInPlaylist", "Songs in playlist" },
        { "TextMoveUp", "Move up" },
        { "TextMoveDown", "Move down" },
        { "TextRemoveFromPlaylist", "Remove from playlist" },
        { "TextNoSongPlaying", "No song playing" },
        { "TextPreviousSong", "Previous track" },
        { "TextNextSong", "Next track" },
        { "TextPlayPause", "Play / Pause" },
        { "TextRepeatMode", "Repeat mode" },
        { "TextMute", "Mute / Unmute" },
        { "TextFullScreen", "Full screen" },
        { "TextCloseFullScreen", "Close full screen" },
        { "TextLyrics", "Lyrics" },
        { "TextLikeSong", "Like" },
        { "TextDislikeSong", "Dislike" },
        { "TextAutoplayTooltip", "Infinite Radio (Autoplay) 🎶 - Automatically plays similar songs when queue ends" },
        { "TextReloadLyrics", "Reload lyrics" },
        { "TextNoLyricsFound", "No lyrics found for this song." },
        { "TextTryAgainOrSelectOther", "Try again or select another song." },
        { "TextTryAgain", "Try again" },
        { "TextLoadingLyrics", "Loading lyrics..." },
        { "TextShowHideWindow", "Show / Hide window" }
    };

    public static readonly Dictionary<string, string> Czech = new()
    {
        { "TextHome", "Domů" },
        { "TextExplore", "Objevovat 🎶" },
        { "TextSearch", "Hledat" },
        { "TextLibrary", "Knihovna" },
        { "TextQueue", "Fronta" },
        { "TextAccount", "Účet" },
        { "TextLogout", "Odhlásit" },
        { "TextLogin", "Přihlásit se" },
        { "TextSettings", "Nastavení" },
        { "TextLanguageSelection", "Výběr jazyka" },
        { "TextLanguageDescription", "Vyberte preferovaný jazyk aplikace. Seznam obsahuje všechny dostupné světové jazyky." },
        { "TextSearchPlaceholder", "Hledat skladby, interprety, alba..." },
        { "TextHeroSubtitle", "Poslouchej hudbu bez omezení a bez reklam" },
        { "TextStartListening", "Začít poslouchat" },
        { "TextRecommendedMusic", "Doporučená hudba >" },
        { "TextLoadingRecommendations", "Načítám doporučení..." },
        { "TextPersonalizedSongs", "Doporučené skladby přímo pro vás" },
        { "TextLockedLibrary", "Uzamčená Knihovna" },
        { "TextLockedLibraryDesc", "Chcete-li zobrazit své skladby, playlisty a alba z Melodium, musíte se přihlásit." },
        { "TextGoToLogin", "Přejít k přihlášení" },
        { "TextMusic", "Hudba" },
        { "TextLikedSongs", "Oblíbené" },
        { "TextSongs", "Skladby" },
        { "TextPlaylists", "Playlisty" },
        { "TextAlbums", "Alba" },
        { "TextArtists", "Interpreti" },
        { "TextAddFolder", "Přidat složku" },
        { "TextShuffleAndPlay", "Zamíchat a přehrát" },
        { "TextSortBy", "Řadit dle: Názvu" },
        { "TextUnknownGenre", "Neznámý žánr" },
        { "TextPlay", "Přehrát" },
        { "TextPlayAll", "Přehrát vše" },
        { "TextPlayNext", "Přehrát jako další" },
        { "TextAddToQueue", "Přidat do fronty" },
        { "TextSaveToPlaylist", "Přidat do playlistu" },
        { "TextGoToAlbum", "Přejít do alba" },
        { "TextGoToArtist", "Přejít na interpreta" },
        { "TextShare", "Sdílet" },
        { "TextStartMix", "Spustit mix" },
        { "TextShuffle", "Zamíchat" },
        { "TextPlaybackQueue", "Fronta přehrávání" },
        { "TextClearQueue", "Vyčistit frontu" },
        { "TextEmptyQueue", "Fronta je prázdná" },
        { "TextEmptyQueueDesc", "Najděte nějaké skladby a spusťte přehrávání." },
        { "TextRemoveFromQueue", "Odebrat z fronty" },
        { "TextSongsCountLabel", "Skladeb:" },
        { "TextReleaseYearLabel", "Rok:" },
        { "TextSubscribersLabel", "Odběratelé:" },
        { "TextSongsInQueueLabel", "Skladeb ve frontě:" },
        { "TextStatusReady", "Připraveno. Zvol sekci a hraj." },
        { "TextStatusLibraryEmpty", "Knihovna skladeb je prázdná, míchám z doporučené hudby..." },
        { "TextStatusShuffleFailed", "Nelze zahájit míchání - žádné dostupné skladby." },
        { "TextStatusLibraryLoaded", "Vaše knihovna a doporučení byly úspěšně načteny." },
        { "TextEnableDiscordRpc", "Zobrazovat aktivitu na Discordu" },
        { "TextCloseToTray", "Zavřít do oznamovací oblasti" },
        { "TextCloseToTrayDesc", "Při kliknutí na křížek (zavření okna) zůstane aplikace spuštěná v oznamovací oblasti na hlavním panelu." },
        { "TextExitApp", "Ukončení aplikace" },
        { "TextExitAppDesc", "Zcela ukončí aplikaci Melodium a uvolní všechny procesy a prostředky na pozadí." },
        { "TextExitButton", "Ukončit aplikaci" },
        { "TextUpdateSettings", "Aktualizace aplikace" },
        { "TextCheckForUpdates", "Zkontrolovat aktualizace" },
        { "TextCheckForUpdatesDesc", "Zkontroluje dostupnost nejnovější verze aplikace Melodium na GitHubu." },
        { "TextCheckingForUpdates", "Ověřuji dostupnost nové verze..." },
        { "TextAppUpToDate", "Melodium je aktuální. Máte nejnovější verzi." },
        { "TextUpdateAvailable", "Je k dispozici nová verze!" },
        { "TextDownloadAndInstall", "Stáhnout a aktualizovat" },
        { "TextDownloadAndInstallDesc", "Aplikace stáhne instalační balíček a spustí instalátor pro provedení aktualizace." },
        { "TextDownloadingUpdate", "Stahování aktualizace..." },
        { "TextCurrentVersionLabel", "Nainstalovaná verze:" },
        { "TextLatestVersionLabel", "Nejnovější verze na GitHubu:" },
        { "TextChangelogLabel", "Přehled změn:" },
        { "TextOpenOnGitHub", "Zobrazit na GitHubu" },
        { "TextLoginInstructions", "Instrukce pro přihlášení" },
        { "TextLoginInstruction1", "1. Přihlaste se ke svému Google / YouTube účtu přímo v okně níže." },
        { "TextLoginInstruction2", "2. Po úspěšném přihlášení a načtení hlavní stránky Melodium vás aplikace automaticky připojí a stáhne vaši osobní knihovnu." },
        { "TextDiscoverNewMusic", "Objevujte novou hudbu" },
        { "TextDiscoverSubtitle", "Nejnovější hity, globální žebříčky a výběr skladeb pro každou náladu." },
        { "TextPlayCharts", "Přehrát žebříčky 🎶" },
        { "TextShuffleCharts", "Zamíchat žebříček 🔀" },
        { "TextMoodsAndGenres", "Nálady a žánry" },
        { "TextChartsAndTrends", "Žebříčky & Trendy 🎶" },
        { "TextRefresh", "Obnovit" },
        { "TextSearchResults", "Výsledky vyhledávání" },
        { "TextBackToHome", "Zpět domů" },
        { "TextFilterAll", "Vše" },
        { "TextFilterRelax", "Relax" },
        { "TextFilterEnergy", "Energie" },
        { "TextFilterWorkout", "Cvičení" },
        { "TextFilterFocus", "Soustředění" },
        { "TextAddSongToPlaylist", "Přidat skladbu do playlistu" },
        { "TextAddSong", "Přidat skladbu" },
        { "TextNoResults", "Nebyly nalezeny žádné výsledky" },
        { "TextNoResultsDesc", "Zkuste vyhledat jiného interpreta, název skladby nebo album." },
        { "TextOpenFullPage", "Otevřít celou stránku" },
        { "TextOpenPlaylist", "Otevřít playlist" },
        { "TextEditable", "• Upravitelný" },
        { "TextPopularSongs", "Populární skladby" },
        { "TextShowAll", "Zobrazit všechny" },
        { "TextSinglesAndEps", "Singly a EP" },
        { "TextBack", "Zpět" },
        { "TextEditablePlaylist", "Upravitelný playlist" },
        { "TextReadOnlyPlaylist", "Sdílený playlist • Pouze ke čtení" },
        { "TextSearchAndAddSong", "Hledat a přidat skladbu" },
        { "TextSearchSongInYtMusic", "Hledat skladbu na YouTube Music..." },
        { "TextSongsInPlaylist", "Skladby v playlistu" },
        { "TextMoveUp", "Posunout nahoru" },
        { "TextMoveDown", "Posunout dolů" },
        { "TextRemoveFromPlaylist", "Odebrat z playlistu" },
        { "TextNoSongPlaying", "Žádná skladba nehraje" },
        { "TextPreviousSong", "Předchozí skladba" },
        { "TextNextSong", "Další skladba" },
        { "TextPlayPause", "Přehrát / Pozastavit" },
        { "TextRepeatMode", "Režim opakování" },
        { "TextMute", "Ztlumit / Zapnout zvuk" },
        { "TextFullScreen", "Celá obrazovka" },
        { "TextCloseFullScreen", "Zavřít celou obrazovku" },
        { "TextLyrics", "Text skladby" },
        { "TextLikeSong", "Líbí se mi" },
        { "TextDislikeSong", "Nelíbí se mi" },
        { "TextAutoplayTooltip", "Nekonečné rádio (Autoplay) 🎶 - Po dohrání fronty automaticky načte a pustí podobné skladby" },
        { "TextReloadLyrics", "Znovu načíst text" },
        { "TextNoLyricsFound", "Pro tuto skladbu nebyl nalezen žádný text." },
        { "TextTryAgainOrSelectOther", "Zkuste to znovu nebo vyberte jinou skladbu." },
        { "TextTryAgain", "Zkusit znovu" },
        { "TextLoadingLyrics", "Načítám text skladby..." },
        { "TextShowHideWindow", "Zobrazit / Skrýt okno" }
    };

    public static readonly Dictionary<string, string> Slovak = new()
    {
        { "TextHome", "Domov" },
        { "TextExplore", "Objavovať 🎶" },
        { "TextSearch", "Hľadať" },
        { "TextLibrary", "Knižnica" },
        { "TextQueue", "Fronta" },
        { "TextAccount", "Účet" },
        { "TextLogout", "Odhlásiť" },
        { "TextLogin", "Prihlásiť sa" },
        { "TextSettings", "Nastavenia" },
        { "TextLanguageSelection", "Výber jazyka" },
        { "TextLanguageDescription", "Vyberte preferovaný jazyk aplikácie." },
        { "TextSearchPlaceholder", "Hľadať skladby, interpretov, albumy..." },
        { "TextHeroSubtitle", "Počúvaj hudbu bez obmedzení a bez reklám" },
        { "TextStartListening", "Začať počúvať" },
        { "TextRecommendedMusic", "Odporúčaná hudba >" },
        { "TextLoadingRecommendations", "Načítavam odporúčania..." },
        { "TextPersonalizedSongs", "Odporúčané skladby priamo pre vás" },
        { "TextLockedLibrary", "Uzamknutá Knižnica" },
        { "TextLockedLibraryDesc", "Pre zobrazenie vašich skladieb sa musíte prihlásiť." },
        { "TextGoToLogin", "Prejsť k prihláseniu" },
        { "TextMusic", "Hudba" },
        { "TextLikedSongs", "Obľúbené" },
        { "TextSongs", "Skladby" },
        { "TextPlaylists", "Playlisty" },
        { "TextAlbums", "Albumy" },
        { "TextArtists", "Interpreti" },
        { "TextAddFolder", "Pridať priečinok" },
        { "TextShuffleAndPlay", "Zamiešať a prehrať" },
        { "TextSortBy", "Radiť podľa: Názvu" },
        { "TextUnknownGenre", "Neznámy žáner" },
        { "TextPlay", "Prehrať" },
        { "TextPlayAll", "Prehrať všetko" },
        { "TextPlayNext", "Prehrať ako ďalšie" },
        { "TextAddToQueue", "Pridať do fronty" },
        { "TextSaveToPlaylist", "Pridať do playlistu" },
        { "TextGoToAlbum", "Prejsť do albumu" },
        { "TextGoToArtist", "Prejsť na interpreta" },
        { "TextShare", "Zdieľať" },
        { "TextStartMix", "Spustiť mix" },
        { "TextShuffle", "Zamiešať" },
        { "TextPlaybackQueue", "Fronta prehrávania" },
        { "TextClearQueue", "Vyčistiť frontu" },
        { "TextEmptyQueue", "Fronta je prázdna" },
        { "TextEmptyQueueDesc", "Nájdite nejaké skladby a spustite prehrávanie." },
        { "TextRemoveFromQueue", "Odobrať z fronty" },
        { "TextSongsCountLabel", "Skladieb:" },
        { "TextReleaseYearLabel", "Rok:" },
        { "TextSubscribersLabel", "Odberatelia:" },
        { "TextSongsInQueueLabel", "Skladieb vo fronte:" },
        { "TextStatusReady", "Pripravené. Zvoľ sekciu a hraj." },
        { "TextStatusLibraryEmpty", "Knižnica je prázdna, miešam z odporúčaní..." },
        { "TextStatusShuffleFailed", "Nemožno zamiešať - žiadne skladby." },
        { "TextStatusLibraryLoaded", "Vaša knižnica a odporúčania boli načítané." },
        { "TextEnableDiscordRpc", "Zobrazovať aktivitu na Discorde" },
        { "TextCloseToTray", "Minimalizovať do lišty" },
        { "TextCloseToTrayDesc", "Pri zatvorení okna ponechať aplikáciu bežať v oznamovacej oblasti." },
        { "TextExitApp", "Ukončenie aplikácie" },
        { "TextExitAppDesc", "Úplne ukončí aplikáciu Melodium." },
        { "TextExitButton", "Ukončiť aplikáciu" },
        { "TextUpdateSettings", "Aktualizácia aplikácie" },
        { "TextCheckForUpdates", "Skontrolovať aktualizácie" },
        { "TextCheckForUpdatesDesc", "Skontroluje najnovšiu verziu na GitHube." },
        { "TextCheckingForUpdates", "Overujem aktualizácie..." },
        { "TextAppUpToDate", "Melodium je aktuálny." },
        { "TextUpdateAvailable", "K dispozícii je nová verzia!" },
        { "TextDownloadAndInstall", "Stiahnuť a aktualizovať" },
        { "TextDownloadAndInstallDesc", "Stiahne a spustí inštalátor." },
        { "TextDownloadingUpdate", "Sťahovanie aktualizácie..." },
        { "TextCurrentVersionLabel", "Nainštalovaná verzia:" },
        { "TextLatestVersionLabel", "Najnovšia verzia na GitHube:" },
        { "TextChangelogLabel", "Prehľad zmien:" },
        { "TextOpenOnGitHub", "Zobraziť na GitHube" },
        { "TextLoginInstructions", "Inštrukcie pre prihlásenie" },
        { "TextLoginInstruction1", "1. Prihláste sa k svojmu účtu nižšie." },
        { "TextLoginInstruction2", "2. Po prihlásení Melodium automaticky načíta vašu knižnicu." },
        { "TextDiscoverNewMusic", "Objavujte novú hudbu" },
        { "TextDiscoverSubtitle", "Najnovšie hity, rebríčky a výbery." },
        { "TextPlayCharts", "Prehrať rebríčky 🎶" },
        { "TextShuffleCharts", "Zamiešať rebríček 🔀" },
        { "TextMoodsAndGenres", "Nálady a žánre" },
        { "TextChartsAndTrends", "Rebríčky & Trendy 🎶" },
        { "TextRefresh", "Obnoviť" },
        { "TextSearchResults", "Výsledky vyhľadávania" },
        { "TextBackToHome", "Späť domov" },
        { "TextFilterAll", "Všetko" },
        { "TextFilterRelax", "Relax" },
        { "TextFilterEnergy", "Energia" },
        { "TextFilterWorkout", "Cvičenie" },
        { "TextFilterFocus", "Sústredenie" },
        { "TextAddSongToPlaylist", "Pridať skladbu do playlistu" },
        { "TextAddSong", "Pridať skladbu" },
        { "TextNoResults", "Neboli nájdené žiadne výsledky" },
        { "TextNoResultsDesc", "Skúste vyhľadať iného interpreta, názov skladby alebo album." },
        { "TextOpenFullPage", "Otvoriť celú stránku" },
        { "TextOpenPlaylist", "Otvoriť playlist" },
        { "TextEditable", "• Upraviteľný" },
        { "TextPopularSongs", "Populárne skladby" },
        { "TextShowAll", "Zobraziť všetky" },
        { "TextSinglesAndEps", "Single a EP" },
        { "TextBack", "Späť" },
        { "TextEditablePlaylist", "Upraviteľný playlist" },
        { "TextReadOnlyPlaylist", "Zdieľaný playlist • Len na čítanie" },
        { "TextSearchAndAddSong", "Hľadať a pridať skladbu" },
        { "TextSearchSongInYtMusic", "Hľadať skladbu na YouTube Music..." },
        { "TextSongsInPlaylist", "Skladby v playliste" },
        { "TextMoveUp", "Posunúť nahor" },
        { "TextMoveDown", "Posunúť nadol" },
        { "TextRemoveFromPlaylist", "Odstrániť z playlistu" },
        { "TextNoSongPlaying", "Žiadna skladba nehrá" },
        { "TextPreviousSong", "Predchádzajúca skladba" },
        { "TextNextSong", "Ďalšia skladba" },
        { "TextPlayPause", "Prehrať / Pozastaviť" },
        { "TextRepeatMode", "Režim opakovania" },
        { "TextMute", "Stlmiť / Zapnúť zvuk" },
        { "TextFullScreen", "Celá obrazovka" },
        { "TextCloseFullScreen", "Zavrieť celú obrazovku" },
        { "TextLyrics", "Text skladby" },
        { "TextLikeSong", "Páči sa mi" },
        { "TextDislikeSong", "Nepáči sa mi" },
        { "TextAutoplayTooltip", "Nekonečné rádio (Autoplay) 🎶 - Po dohraní fronty automaticky načíta a pustí podobné skladby" },
        { "TextReloadLyrics", "Znova načítať text" },
        { "TextNoLyricsFound", "Pre túto skladbu sa nenašiel žiaden text." },
        { "TextTryAgainOrSelectOther", "Skúste to znova alebo vyberte inú skladbu." },
        { "TextTryAgain", "Skúsiť znova" },
        { "TextLoadingLyrics", "Načítavam text skladby..." },
        { "TextShowHideWindow", "Zobraziť / Skryť okno" }
    };

    public TranslationService()
    {
        try
        {
            Directory.CreateDirectory(TranslationsCacheDir);
        }
        catch { }
    }

    public async Task<Dictionary<string, string>> GetDictionaryAsync(string targetLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(targetLanguageCode))
            return new Dictionary<string, string>(English);

        string lang = targetLanguageCode.ToLowerInvariant();
        if (lang.Length > 2 && lang.Contains('-'))
        {
            lang = lang.Split('-')[0];
        }

        // 1. Okamžitý návrat vestavěných slovníků (0ms, 100% spolehlivost)
        if (lang == "en") return new Dictionary<string, string>(English);
        if (lang == "cs") return new Dictionary<string, string>(Czech);
        if (lang == "sk") return new Dictionary<string, string>(Slovak);

        // 2. Kontrola trvalé diskové mezipaměti (rychlé načtení < 5ms)
        string cacheFile = Path.Combine(TranslationsCacheDir, $"{lang}.json");
        try
        {
            if (File.Exists(cacheFile))
            {
                var json = await File.ReadAllTextAsync(cacheFile);
                var cached = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (cached != null && cached.Count >= English.Count - 5)
                {
                    return cached;
                }
            }
        }
        catch { }

        // 3. Rychlý paralelní dávkový překlad (v 2-3 požadavcích místo 60)
        var result = new Dictionary<string, string>(English);
        try
        {
            var keys = English.Keys.ToList();
            const int batchSize = 25;
            var batches = keys.Chunk(batchSize).ToList();

            var tasks = batches.Select(async batch =>
            {
                var batchKeys = batch.ToList();
                var textToTranslate = string.Join("\n===\n", batchKeys.Select(k => English[k]));
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl={lang}&dt=t&q={Uri.EscapeDataString(textToTranslate)}";

                using var response = await SharedHttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var sentences = root[0];
                    if (sentences.ValueKind == JsonValueKind.Array)
                    {
                        var sb = new StringBuilder();
                        foreach (var s in sentences.EnumerateArray())
                        {
                            if (s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0)
                            {
                                sb.Append(s[0].GetString());
                            }
                        }

                        var translatedParts = sb.ToString()
                            .Split(new[] { "===", "=== ", " ===" }, StringSplitOptions.None)
                            .Select(p => p.Trim())
                            .ToList();

                        for (int i = 0; i < batchKeys.Count && i < translatedParts.Count; i++)
                        {
                            var translatedText = translatedParts[i];
                            if (!string.IsNullOrWhiteSpace(translatedText))
                            {
                                lock (result)
                                {
                                    result[batchKeys[i]] = translatedText;
                                }
                            }
                        }
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Uložit do trvalé diskové mezipaměti
            _ = Task.Run(async () =>
            {
                try
                {
                    var serialized = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(cacheFile, serialized);
                }
                catch { }
            });
        }
        catch { }

        return result;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (string.IsNullOrWhiteSpace(targetLanguageCode)) return text;

        string lang = targetLanguageCode.ToLowerInvariant();
        if (lang.Length > 2 && lang.Contains('-'))
        {
            lang = lang.Split('-')[0];
        }

        // Pokud cílový jazyk odpovídá angličtině, zkontrolujeme známé překlady
        if (lang == "en")
        {
            var matchCs = Czech.FirstOrDefault(kvp => kvp.Value.Equals(text, StringComparison.OrdinalIgnoreCase));
            if (matchCs.Key != null && English.TryGetValue(matchCs.Key, out var enText))
            {
                return enText;
            }
            return text;
        }

        if (lang == "cs")
        {
            var matchEn = English.FirstOrDefault(kvp => kvp.Value.Equals(text, StringComparison.OrdinalIgnoreCase));
            if (matchEn.Key != null && Czech.TryGetValue(matchEn.Key, out var csText))
            {
                return csText;
            }
        }

        string cacheKey = $"{lang}:{text}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        try
        {
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={lang}&dt=t&q={Uri.EscapeDataString(text)}";
            var response = await SharedHttpClient.GetStringAsync(url);

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var sentences = root[0];
                if (sentences.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var s in sentences.EnumerateArray())
                    {
                        if (s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0)
                        {
                            sb.Append(s[0].GetString());
                        }
                    }
                    string result = sb.ToString();
                    _cache[cacheKey] = result;
                    return result;
                }
            }
        }
        catch { }

        return text;
    }
}
