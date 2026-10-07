using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Melodium.Models;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models.Search;
using YouTubeMusicAPI.Models.Library;
using YouTubeMusicAPI.Models.Info;
using YouTubeSessionGenerator;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
namespace Melodium.Services
{
    public class PlaylistDetailsResult
    {
        public PlaylistModel Playlist { get; set; } = new();
        public List<SongModel> Songs { get; set; } = new();
    }

    public class MelodiumService
    {
        private YouTubeMusicClient _client;
        private string _visitorData = "";
        private string _poToken = "";
        private bool _isInitialized = false;
        private IEnumerable<Cookie>? _currentCookies;
        private System.Net.Http.HttpClient _httpClient;
        private YoutubeClient _ytExplodeClient;
        private readonly ConcurrentDictionary<string, (string Url, DateTime ExpiresAt)> _streamUrlCache = new();
        private string? _sapisid;

        private string? _userChannelName;
        private string? _userChannelHandle;

        public MelodiumService()
        {
            _ytExplodeClient = new YoutubeClient();
            // Výchozí inicializace bez cookies
            InitializeClient(null);
        }

        public void SetUserChannelName(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _userChannelName = name;
            }
        }

        public string? GetUserChannelName() => _userChannelName;

        public void SetUserChannelHandle(string? handle)
        {
            if (!string.IsNullOrWhiteSpace(handle))
            {
                _userChannelHandle = handle;
            }
        }

        public string? GetUserChannelHandle() => _userChannelHandle;

        public object CreateInnertubeContext()
        {
            if (!string.IsNullOrWhiteSpace(_visitorData))
            {
                return new
                {
                    client = new
                    {
                        clientName = "WEB_REMIX",
                        clientVersion = "1.20250101.01.00",
                        hl = "cs",
                        gl = "CZ",
                        visitorData = _visitorData
                    }
                };
            }

            return new
            {
                client = new
                {
                    clientName = "WEB_REMIX",
                    clientVersion = "1.20250101.01.00",
                    hl = "cs",
                    gl = "CZ"
                }
            };
        }

        public async Task<System.Text.Json.Nodes.JsonNode?> PostInnertubeAsync(string endpoint, object body, bool anonymous = false)
        {
            await EnsureInitializedAsync();
            try
            {
                string url = endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase) 
                    ? endpoint 
                    : $"https://music.youtube.com/youtubei/v1/{endpoint}";

                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");
                request.Headers.TryAddWithoutValidation("Referer", "https://music.youtube.com/");
                request.Headers.TryAddWithoutValidation("X-YouTube-Client-Name", "67");
                request.Headers.TryAddWithoutValidation("X-YouTube-Client-Version", "1.20250101.01.00");
                request.Headers.TryAddWithoutValidation("X-Goog-AuthUser", "0");

                if (!anonymous)
                {
                    if (_currentCookies != null && _currentCookies.Any())
                    {
                        var cookieHeader = string.Join("; ", _currentCookies.Select(c => $"{c.Name}={c.Value}"));
                        request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                    }

                    string? sapisidToUse = _sapisid;
                    if (string.IsNullOrEmpty(sapisidToUse) && _currentCookies != null)
                    {
                        sapisidToUse = _currentCookies.FirstOrDefault(c => c.Name == "SAPISID")?.Value
                                    ?? _currentCookies.FirstOrDefault(c => c.Name == "__Secure-3PAPISID")?.Value
                                    ?? _currentCookies.FirstOrDefault(c => c.Name == "__Secure-1PAPISID")?.Value;
                    }

                    if (!string.IsNullOrEmpty(sapisidToUse))
                    {
                        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        string input = $"{timestamp} {sapisidToUse} https://music.youtube.com";
                        using var sha1 = System.Security.Cryptography.SHA1.Create();
                        byte[] hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                        string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                        request.Headers.TryAddWithoutValidation("Authorization", $"SAPISIDHASH {timestamp}_{hash}");
                    }
                }

                string json = System.Text.Json.JsonSerializer.Serialize(body);
                request.Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                string responseText = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    return System.Text.Json.Nodes.JsonNode.Parse(responseText);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Innertube Error] HTTP {(int)response.StatusCode} on '{endpoint}': {responseText}");
                    Console.WriteLine($"[Innertube Error] HTTP {(int)response.StatusCode} on '{endpoint}': {responseText}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při Innertube volání '{endpoint}': {ex}");
                Console.WriteLine($"Chyba při Innertube volání '{endpoint}': {ex.Message}");
            }
            return null;
        }

        public async Task<(string? Name, string? AvatarUrl, string? ChannelId)> GetAccountProfileAsync()
        {
            try
            {
                var body = new { context = CreateInnertubeContext() };
                var root = await PostInnertubeAsync("account/account_menu", body);
                if (root != null)
                {
                    var activeHeader = root?["actions"]?[0]?["openPopupAction"]?["popup"]?["multiPageMenuRenderer"]?["header"]?["activeAccountHeaderRenderer"];
                    if (activeHeader != null)
                    {
                        var nameRuns = activeHeader?["accountName"]?["runs"]?.AsArray();
                        string? name = nameRuns != null
                            ? string.Join("", nameRuns.Select(r => r?["text"]?.ToString()))
                            : activeHeader?["accountName"]?["simpleText"]?.ToString();

                        var thumbs = activeHeader?["accountPhoto"]?["thumbnails"]?.AsArray();
                        string? avatarUrl = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                        string? channelId = activeHeader?["channelHandle"]?["runs"]?[0]?["text"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(channelId))
                        {
                            _userChannelHandle = channelId;
                        }

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            _userChannelName = name;
                            return (name, avatarUrl, channelId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování profilu: {ex.Message}");
            }
            return (null, null, null);
        }

        public async Task<List<SongModel>> GetAccountHistoryAsync()
        {
            await EnsureInitializedAsync();
            var songs = new List<SongModel>();

            try
            {
                var body = new { context = CreateInnertubeContext(), browseId = "FEmusic_history" };
                var root = await PostInnertubeAsync("browse", body);
                if (root == null) return songs;

                var sectionNodes = root?["contents"]?["singleColumnBrowseResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"]?.AsArray();
                if (sectionNodes != null)
                {
                    foreach (var sec in sectionNodes)
                    {
                        var shelf = sec?["musicShelfRenderer"];
                        var items = shelf?["contents"]?.AsArray();
                        if (items == null)
                        {
                            var carousel = sec?["musicCarouselShelfRenderer"];
                            items = carousel?["contents"]?.AsArray();
                        }

                        if (items == null) continue;

                        foreach (var itm in items)
                        {
                            var r = itm?["musicResponsiveListItemRenderer"];
                            if (r == null) continue;

                            string? videoId = r?["playlistItemData"]?["videoId"]?.ToString()
                                           ?? r?["navigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString()
                                           ?? r?["overlay"]?["musicItemThumbnailOverlayRenderer"]?["content"]?["musicPlayButtonRenderer"]?["playNavigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString();

                            if (string.IsNullOrEmpty(videoId)) continue;

                            var thumbs = r?["thumbnail"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                            string? thumbUrl = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                            var flex = r?["flexColumns"]?.AsArray();
                            string title = "Neznámá skladba";
                            string artist = "Neznámý interpret";
                            string? artistId = null;

                            if (flex != null && flex.Count > 0)
                            {
                                var titleRuns = flex[0]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                                if (titleRuns != null && titleRuns.Count > 0)
                                {
                                    title = string.Join("", titleRuns.Select(x => x?["text"]?.ToString()));
                                }

                                if (flex.Count > 1)
                                {
                                    var col1Runs = flex[1]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                                    if (col1Runs != null && col1Runs.Count > 0)
                                    {
                                        var artistList = new List<string>();
                                        foreach (var run in col1Runs)
                                        {
                                            var text = run?["text"]?.ToString();
                                            if (string.IsNullOrWhiteSpace(text) || text == "•" || text == " • " || text == ", " || text.Trim() == "a") continue;

                                            var nav = run?["navigationEndpoint"]?["browseEndpoint"];
                                            var pageType = nav?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();
                                            var browseId = nav?["browseId"]?.ToString();

                                            if (pageType == "MUSIC_PAGE_TYPE_ARTIST" || (browseId != null && browseId.StartsWith("UC")))
                                            {
                                                artistList.Add(text);
                                                if (artistId == null) artistId = browseId;
                                            }
                                        }

                                        if (artistList.Count > 0)
                                        {
                                            artist = string.Join(", ", artistList);
                                        }
                                        else
                                        {
                                            var first = col1Runs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x?["text"]?.ToString()) && x?["text"]?.ToString() != "•");
                                            if (first != null) artist = first["text"]!.ToString();
                                        }
                                    }
                                }
                            }

                            if (!songs.Any(s => s.VideoId == videoId))
                            {
                                songs.Add(new SongModel
                                {
                                    VideoId = videoId,
                                    Title = title,
                                    Artist = artist,
                                    ArtistId = artistId,
                                    ThumbnailUrl = thumbUrl
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při stahování historie účtu: {ex.Message}");
            }

            return songs;
        }

        private bool _isGeneratingPoToken = false;

        public Task EnsureInitializedAsync()
        {
            if (_isInitialized) return Task.CompletedTask;
            _isInitialized = true;
            InitializeClient(_currentCookies);

#if WINDOWS
            if (!_isGeneratingPoToken)
            {
                _isGeneratingPoToken = true;
                _ = Task.Run(async () =>
                {
                    try 
                    {
                        var jsEnv = new YouTubeSessionGenerator.Js.Environments.NodeEnvironment();
                        var config = new YouTubeSessionConfig { JsEnvironment = jsEnv };
                        var creator = new YouTubeSessionCreator(config);
                        _visitorData = await creator.VisitorDataAsync();
                        _poToken = await creator.ProofOfOriginTokenAsync(_visitorData);
                        _client = new YouTubeMusicClient(
                            visitorData: _visitorData,
                            poToken: _poToken,
                            cookies: _currentCookies
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Chyba při generování PoTokenu: {ex.Message}");
                    }
                    finally
                    {
                        _isGeneratingPoToken = false;
                    }
                });
            }
#endif
            return Task.CompletedTask;
        }

        public void InitializeClient(IEnumerable<Cookie>? cookies)
        {
            _currentCookies = cookies;
            _client = new YouTubeMusicClient(
                visitorData: _visitorData,
                poToken: _poToken,
                cookies: cookies
            );

            // YoutubeExplode stream extraction should always use a clean anonymous client
            // because passing user account cookies to YouTube player endpoint returns 400 Bad Request.
            _ytExplodeClient = new YoutubeClient();
            
            // Set up cookies for raw HttpClient if available
            var handler = new System.Net.Http.SocketsHttpHandler
            {
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 20,
                EnableMultipleHttp2Connections = true
            };

            _sapisid = null;
            if (cookies != null)
            {
                foreach (var c in cookies)
                {
                    if (c.Name == "SAPISID")
                    {
                        _sapisid = c.Value;
                    }
                    else if (c.Name == "__Secure-3PAPISID" && string.IsNullOrEmpty(_sapisid))
                    {
                        _sapisid = c.Value;
                    }
                    else if (c.Name == "__Secure-1PAPISID" && string.IsNullOrEmpty(_sapisid))
                    {
                        _sapisid = c.Value;
                    }
                }
            }
            _httpClient = new System.Net.Http.HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        }

        public async Task<List<SongModel>> SearchSongsAsync(string query)
        {
            var results = new List<SongModel>();

            if (string.IsNullOrWhiteSpace(query))
                return results;

            // 1. Zkusit primárně přímé Innertube vyhledávání s filtrem skladeb
            try
            {
                var body = new
                {
                    context = CreateInnertubeContext(),
                    query = query,
                    @params = "EgWKAQIIAWoKEAkQBRAKEAMQBBAK" // Songs filter
                };

                var root = await PostInnertubeAsync("search", body, anonymous: false);
                if (root == null)
                {
                    root = await PostInnertubeAsync("search", body, anonymous: true);
                }

                if (root != null)
                {
                    var sections = root?["contents"]?["tabbedSearchResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"]?.AsArray()
                                ?? root?["contents"]?["sectionListRenderer"]?["contents"]?.AsArray()
                                ?? root?["contents"]?["twoColumnSearchResultsRenderer"]?["primaryContents"]?["sectionListRenderer"]?["contents"]?.AsArray();
                    if (sections != null)
                    {
                        foreach (var sec in sections)
                        {
                            var shelf = sec?["musicShelfRenderer"];
                            if (shelf == null) continue;

                            var items = shelf?["contents"]?.AsArray();
                            if (items == null) continue;

                            foreach (var itm in items)
                            {
                                var r = itm?["musicResponsiveListItemRenderer"];
                                if (r == null) continue;

                                string? videoId = r?["playlistItemData"]?["videoId"]?.ToString() 
                                               ?? r?["navigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString()
                                               ?? r?["overlay"]?["musicItemThumbnailOverlayRenderer"]?["content"]?["musicPlayButtonRenderer"]?["playNavigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString();

                                if (string.IsNullOrEmpty(videoId)) continue;

                                var thumbs = r?["thumbnail"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                                string? thumbUrl = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                                var flex = r?["flexColumns"]?.AsArray();
                                string title = "Neznámá skladba";
                                string artist = "Neznámý interpret";
                                string? artistId = null;

                                if (flex != null && flex.Count > 0)
                                {
                                    var titleRuns = flex[0]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                                    if (titleRuns != null && titleRuns.Count > 0)
                                    {
                                        title = string.Join("", titleRuns.Select(x => x?["text"]?.ToString()));
                                    }

                                    if (flex.Count > 1)
                                    {
                                        var col1Runs = flex[1]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                                        if (col1Runs != null && col1Runs.Count > 0)
                                        {
                                            var artistList = new List<string>();
                                            foreach (var run in col1Runs)
                                            {
                                                var text = run?["text"]?.ToString();
                                                if (string.IsNullOrWhiteSpace(text) || text == "•" || text == " • " || text == ", " || text.Trim() == "a") continue;

                                                var nav = run?["navigationEndpoint"]?["browseEndpoint"];
                                                var pageType = nav?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();
                                                var browseId = nav?["browseId"]?.ToString();

                                                if (pageType == "MUSIC_PAGE_TYPE_ARTIST" || (browseId != null && browseId.StartsWith("UC")))
                                                {
                                                    artistList.Add(text);
                                                    if (artistId == null) artistId = browseId;
                                                }
                                            }

                                            if (artistList.Count > 0)
                                            {
                                                artist = string.Join(", ", artistList);
                                            }
                                            else
                                            {
                                                var runsBeforeBullet = new List<string>();
                                                foreach (var run in col1Runs)
                                                {
                                                    var text = run?["text"]?.ToString();
                                                    if (text != null && text.Contains("•")) break;
                                                    if (!string.IsNullOrWhiteSpace(text) && text != ", " && text.Trim() != "a")
                                                    {
                                                        runsBeforeBullet.Add(text);
                                                    }
                                                }
                                                if (runsBeforeBullet.Count > 0)
                                                {
                                                    artist = string.Join(", ", runsBeforeBullet);
                                                }
                                            }
                                        }
                                    }
                                }

                                results.Add(new SongModel
                                {
                                    VideoId = videoId,
                                    Title = title,
                                    Artist = artist,
                                    ArtistId = artistId,
                                    ThumbnailUrl = thumbUrl
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při přímém vyhledávání skladeb: {ex.Message}");
            }

            // 2. Fallback: Pokud Innertube nevrátil žádné výsledky, zkusit YoutubeExplode vyhledávání
            if (results.Count == 0)
            {
                try
                {
                    await foreach (var v in _ytExplodeClient.Search.GetVideosAsync(query))
                    {
                        results.Add(new SongModel
                        {
                            VideoId = v.Id.Value,
                            Title = v.Title,
                            Artist = v.Author.ChannelTitle,
                            ThumbnailUrl = v.Thumbnails.LastOrDefault()?.Url ?? v.Thumbnails.FirstOrDefault()?.Url
                        });
                        if (results.Count >= 20) break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"YoutubeExplode search fallback error: {ex.Message}");
                }
            }

            // 3. Fallback: Pokus o vyhledání přes YouTubeMusicClient
            if (results.Count == 0)
            {
                try
                {
                    var searchResults = _client.SearchAsync(query, SearchCategory.Songs);
                    var bufferedSearchResults = await searchResults.FetchItemsAsync(0, 20);

                    foreach (var song in bufferedSearchResults.OfType<SongSearchResult>())
                    {
                        string artistName = song.Artists != null && song.Artists.Any()
                            ? string.Join(", ", song.Artists.Select(a => a.Name))
                            : "Neznámý interpret";
                        string? artistId = song.Artists?.FirstOrDefault(a => !string.IsNullOrEmpty(a.Id))?.Id;

                        results.Add(new SongModel
                        {
                            VideoId = song.Id,
                            Title = song.Name,
                            Artist = artistName,
                            ArtistId = artistId,
                            ThumbnailUrl = song.Thumbnails?.LastOrDefault()?.Url ?? song.Thumbnails?.FirstOrDefault()?.Url
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Chyba při hledání přes YouTubeMusicClient: {ex.Message}");
                }
            }

            return results;
        }

        public async Task<List<AlbumModel>> SearchAlbumsAsync(string query)
        {
            var results = new List<AlbumModel>();
            try
            {
                var searchResults = _client.SearchAsync(query, SearchCategory.Albums);
                var buffered = await searchResults.FetchItemsAsync(0, 20);
                foreach (var album in buffered.OfType<AlbumSearchResult>())
                {
                    string artistName = album.Artists != null && album.Artists.Any()
                        ? string.Join(", ", album.Artists.Select(a => a.Name))
                        : "Neznámý interpret";

                    results.Add(new AlbumModel
                    {
                        Id = album.Id,
                        Title = album.Name,
                        ArtistName = artistName,
                        ReleaseYear = album.ReleaseYear,
                        ThumbnailUrl = album.Thumbnails?.LastOrDefault()?.Url ?? album.Thumbnails?.FirstOrDefault()?.Url
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při hledání alb: {ex.Message}");
            }
            return results;
        }

        public async Task<List<ArtistModel>> SearchArtistsAsync(string query)
        {
            var results = new List<ArtistModel>();
            try
            {
                var searchResults = _client.SearchAsync(query, SearchCategory.Artists);
                var buffered = await searchResults.FetchItemsAsync(0, 20);
                foreach (var artist in buffered.OfType<ArtistSearchResult>())
                {
                    results.Add(new ArtistModel
                    {
                        Id = artist.Id,
                        Name = artist.Name,
                        Subscribers = artist.PopularityInfo,
                        ThumbnailUrl = artist.Thumbnails?.LastOrDefault()?.Url ?? artist.Thumbnails?.FirstOrDefault()?.Url
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při hledání interpretů: {ex.Message}");
            }
            return results;
        }

        public async Task<List<PlaylistModel>> SearchPlaylistsAsync(string query)
        {
            var results = new List<PlaylistModel>();
            try
            {
                var searchResults = _client.SearchAsync(query, SearchCategory.CommunityPlaylists);
                var buffered = await searchResults.FetchItemsAsync(0, 20);
                foreach (var pl in buffered.OfType<CommunityPlaylistSearchResult>())
                {
                    results.Add(new PlaylistModel
                    {
                        Id = pl.Id,
                        Title = pl.Name,
                        Creator = pl.Creator?.Name ?? "YouTube Music",
                        ThumbnailUrl = pl.Thumbnails?.LastOrDefault()?.Url ?? pl.Thumbnails?.FirstOrDefault()?.Url,
                        CanEdit = false
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při hledání playlistů: {ex.Message}");
            }
            return results;
        }

        public async Task<bool> LikeSongAsync(string videoId)
        {
            try
            {
                var body = new {
                    context = CreateInnertubeContext(),
                    target = new { videoId = videoId }
                };
                var node = await PostInnertubeAsync("like/like", body);
                return node != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při lajkování skladby: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DislikeSongAsync(string videoId)
        {
            try
            {
                var body = new {
                    context = CreateInnertubeContext(),
                    target = new { videoId = videoId }
                };
                var node = await PostInnertubeAsync("like/dislike", body);
                return node != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při dislajkování skladby: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveLikeAsync(string videoId)
        {
            try
            {
                var body = new {
                    context = CreateInnertubeContext(),
                    target = new { videoId = videoId }
                };
                var node = await PostInnertubeAsync("like/removelike", body);
                return node != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při odebrání hodnocení skladby: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SongModel>> GetMoodSongsAsync(string mood)
        {
            var results = new List<SongModel>();
            try
            {
                string query = mood switch
                {
                    "Relax" or "Chill" => "chill relaxing lofi music",
                    "Cvičení" or "Workout" => "workout motivation gym music",
                    "Energie" => "high energy dance party music",
                    "Soustředění" or "Focus" => "lofi deep focus study instrumental music",
                    "Párty" => "party club dance hits",
                    "Rock" => "rock alternative greatest hits",
                    "Pop" => "top pop hits 2026",
                    "Hip-Hop" => "hip hop rap top tracks",
                    "Jazz" => "smooth jazz cafe relaxing",
                    "Elektronika" => "electronic edm synthwave tracks",
                    _ => $"{mood} music"
                };

                return await SearchSongsAsync(query);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování nálady {mood}: {ex.Message}");
            }
            return results;
        }

        public async Task<List<SongModel>> GetExploreChartsAsync()
        {
            var results = new List<SongModel>();
            try
            {
                var body = new {
                    context = CreateInnertubeContext(),
                    browseId = "FEmusic_charts"
                };

                var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://music.youtube.com/youtubei/v1/browse", content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync();
                    var root = System.Text.Json.Nodes.JsonNode.Parse(jsonStr);
                    ExtractSongsFromNode(root, results);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování Explore žebříčků: {ex.Message}");
            }

            if (results.Count < 5)
            {
                var fallback = await SearchSongsAsync("Top Hits Global & Czech 2026");
                results.AddRange(fallback);
            }

            return results.GroupBy(r => r.VideoId).Select(g => g.First()).Take(30).ToList();
        }

        public async Task<string?> GetAudioStreamUrlAsync(string videoId, string? fallbackQuery = null)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;

            if (_streamUrlCache.TryGetValue(videoId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                LogAudioService($"Stream URL načtena z mezipaměti pro {videoId}");
                return cached.Url;
            }

            try
            {
                var streamManifest = await _ytExplodeClient.Videos.Streams.GetManifestAsync(videoId);
                var audioStreams = streamManifest.GetAudioOnlyStreams().ToList();

                // Prefer MP4/M4A (AAC) streams for universal native Windows Media Foundation playback
                var audioStreamInfo = audioStreams.Where(s => s.Container.Name == "mp4" || s.Container.Name == "m4a").GetWithHighestBitrate()
                                   ?? audioStreams.GetWithHighestBitrate()
                                   ?? streamManifest.GetMuxedStreams().GetWithHighestVideoQuality() as IStreamInfo;

                if (audioStreamInfo != null)
                {
                    LogAudioService($"Stream nalezen pro {videoId}: {audioStreamInfo.Container.Name} ({audioStreamInfo.Bitrate})");
                    _streamUrlCache[videoId] = (audioStreamInfo.Url, DateTime.UtcNow.AddHours(4));
                    return audioStreamInfo.Url;
                }
            }
            catch (Exception ex)
            {
                LogAudioService($"Chyba při získávání URL streamu pro {videoId}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Chyba při získávání URL streamu přes YoutubeExplode pro video {videoId}: {ex.Message}");
            }

            // Fallback: If original videoId was restricted/unavailable, search for the song title + artist
            if (!string.IsNullOrWhiteSpace(fallbackQuery))
            {
                try
                {
                    LogAudioService($"Pokus o vyhledání záložního streamu pro '{fallbackQuery}'...");
                    int attempts = 0;
                    await foreach (var match in _ytExplodeClient.Search.GetVideosAsync(fallbackQuery))
                    {
                        if (match.Id.Value == videoId) continue;
                        attempts++;
                        if (attempts > 3) break;

                        try
                        {
                            var manifest = await _ytExplodeClient.Videos.Streams.GetManifestAsync(match.Id);
                            var streams = manifest.GetAudioOnlyStreams().ToList();
                            var info = streams.Where(s => s.Container.Name == "mp4" || s.Container.Name == "m4a").GetWithHighestBitrate()
                                    ?? streams.GetWithHighestBitrate()
                                    ?? manifest.GetMuxedStreams().GetWithHighestVideoQuality() as IStreamInfo;
                            if (info != null)
                            {
                                LogAudioService($"Záložní stream nalezen: {match.Id} ({match.Title})");
                                _streamUrlCache[videoId] = (info.Url, DateTime.UtcNow.AddHours(4));
                                return info.Url;
                            }
                        }
                        catch (Exception fallbackItemEx)
                        {
                            LogAudioService($"Záložní položka {match.Id} selhala: {fallbackItemEx.Message}");
                        }
                    }
                }
                catch (Exception searchEx)
                {
                    LogAudioService($"Záložní vyhledávání selhalo: {searchEx.Message}");
                }
            }

            LogAudioService($"Nepodařilo se najít žádný stream pro {videoId}");
            return null;
        }

        private static void LogAudioService(string message)
        {
            try
            {
                var msg = $"[{DateTime.Now:HH:mm:ss.fff}] [MelodiumService] {message}\n";
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_naudio_log.txt");
                System.IO.File.AppendAllText(logPath, msg);
            }
            catch { }
        }

        // --- Nové knihovní metody ---

        public async Task<List<SongModel>> GetLibrarySongsAsync()
        {
            var results = new List<SongModel>();
            try
            {
                var songs = await _client.GetLibrarySongsAsync();
                if (songs != null)
                {
                    foreach (var song in songs)
                    {
                        string artistName = song.Artists != null && song.Artists.Any()
                            ? string.Join(", ", song.Artists.Select(a => a.Name))
                            : "Neznámý interpret";
                        string? artistId = song.Artists?.FirstOrDefault(a => !string.IsNullOrEmpty(a.Id))?.Id;
                        results.Add(new SongModel
                        {
                            VideoId = song.Id,
                            Title = song.Name,
                            Artist = artistName,
                            ArtistId = artistId,
                            ThumbnailUrl = song.Thumbnails?.LastOrDefault()?.Url ?? song.Thumbnails?.FirstOrDefault()?.Url
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování skladeb z knihovny: {ex.Message}");
            }
            return results;
        }

        public async Task<List<PlaylistModel>> GetLibraryPlaylistsAsync()
        {
            await EnsureInitializedAsync();
            var results = new List<PlaylistModel>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(_userChannelName))
            {
                await GetAccountProfileAsync();
            }

            try
            {
                // 1. Primární: Přímé Innertube volání na FEmusic_liked_playlists (Knihovna -> Playlisty na YouTube Music)
                var body = new
                {
                    context = CreateInnertubeContext(),
                    browseId = "FEmusic_liked_playlists"
                };
                var root = await PostInnertubeAsync("browse", body);
                if (root != null)
                {
                    ExtractPlaylistsFromNode(root, results, seenIds);
                }

                // 2. Pokud nic nenačteno, zkusit přistávací stránku knihovny
                if (results.Count == 0)
                {
                    var landingBody = new
                    {
                        context = CreateInnertubeContext(),
                        browseId = "FEmusic_library_landing"
                    };
                    var landingRoot = await PostInnertubeAsync("browse", landingBody);
                    if (landingRoot != null)
                    {
                        ExtractPlaylistsFromNode(landingRoot, results, seenIds);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při přímém stahování playlistů z YouTube Music: {ex.Message}");
            }

            // 3. Fallback přes YouTubeMusicAPI klienta, pokud přímé Innertube volání nic nevrátilo
            if (results.Count == 0)
            {
                try
                {
                    var playlists = await _client.GetLibraryCommunityPlaylistsAsync();
                    if (playlists != null)
                    {
                        foreach (var playlist in playlists)
                        {
                            var creatorName = playlist.Creator?.Name;
                            bool isOwned = string.IsNullOrWhiteSpace(creatorName) || IsUserCreator(creatorName);
                            if (string.IsNullOrWhiteSpace(creatorName))
                            {
                                creatorName = !string.IsNullOrEmpty(_userChannelName) ? _userChannelName : "Vy";
                            }

                            results.Add(new PlaylistModel
                            {
                                Id = playlist.Id,
                                Title = playlist.Name,
                                ThumbnailUrl = playlist.Thumbnails?.FirstOrDefault()?.Url,
                                SongCount = playlist.SongCount,
                                Creator = creatorName,
                                CanEdit = isOwned
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Chyba při záložním stahování playlistů z knihovny: {ex.Message}");
                }
            }

            // Vždy vložit "Oblíbené skladby" (Liked Music / VLLM) jako první položku knihovny pro přihlášeného uživatele
            bool isLoggedIn = (_currentCookies != null && _currentCookies.Any()) || !string.IsNullOrEmpty(_sapisid);
            if (isLoggedIn && !seenIds.Contains("VLLM") && !seenIds.Contains("LM"))
            {
                results.Insert(0, new PlaylistModel
                {
                    Id = "VLLM",
                    Title = "Oblíbené skladby",
                    Creator = "Automatický playlist",
                    Description = "Skladby, které jste označili jako oblíbené v YouTube Music",
                    ThumbnailUrl = "https://www.gstatic.com/youtube/media/ytm/images/pbg/liked-songs-delhi-1200.png",
                    CanEdit = true
                });
                seenIds.Add("VLLM");
                seenIds.Add("LM");
            }

            return results;
        }

        public async Task<List<AlbumModel>> GetLibraryAlbumsAsync()
        {
            var results = new List<AlbumModel>();
            try
            {
                var albums = await _client.GetLibraryAlbumsAsync();
                if (albums != null)
                {
                    foreach (var album in albums)
                    {
                        string artistName = album.Artists?.FirstOrDefault()?.Name ?? "Neznámý interpret";
                        results.Add(new AlbumModel
                        {
                            Id = album.Id,
                            Title = album.Name,
                            ThumbnailUrl = album.Thumbnails?.FirstOrDefault()?.Url,
                            ArtistName = artistName,
                            ReleaseYear = album.ReleaseYear
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování alb z knihovny: {ex.Message}");
            }
            return results;
        }

        public async Task<List<ArtistModel>> GetLibraryArtistsAsync()
        {
            var results = new List<ArtistModel>();
            try
            {
                var artists = await _client.GetLibraryArtistsAsync();
                if (artists != null)
                {
                    foreach (var artist in artists)
                    {
                        results.Add(new ArtistModel
                        {
                            Id = artist.Id,
                            Name = artist.Name,
                            ThumbnailUrl = artist.Thumbnails?.FirstOrDefault()?.Url,
                            SongCount = artist.SongCount
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování interpretů z knihovny: {ex.Message}");
            }
            return results;
        }

        public async Task<PlaylistDetailsResult> GetPlaylistDetailsAsync(string playlistId)
        {
            await EnsureInitializedAsync();
            var result = new PlaylistDetailsResult();
            result.Playlist.Id = playlistId;

            if (string.IsNullOrEmpty(_userChannelName))
            {
                await GetAccountProfileAsync();
            }

            try
            {
                string browseId = playlistId.StartsWith("VL") ? playlistId : "VL" + playlistId;
                var body = new
                {
                    context = CreateInnertubeContext(),
                    browseId = browseId
                };

                var root = await PostInnertubeAsync("browse", body);
                if (root != null)
                {
                    var headerNode = root?["header"];
                    var editableHeader = headerNode?["musicEditablePlaylistDetailHeaderRenderer"];
                    bool canEdit = editableHeader != null;

                    var responsiveHeader = editableHeader?["header"]?["musicResponsiveHeaderRenderer"]
                                        ?? headerNode?["musicResponsiveHeaderRenderer"]
                                        ?? headerNode?["musicDetailHeaderRenderer"];

                    if (responsiveHeader == null)
                    {
                        responsiveHeader = root?["contents"]?["twoColumnBrowseResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"]?[0]?["musicResponsiveHeaderRenderer"];
                    }

                    if (responsiveHeader != null)
                    {
                        var titleRuns = responsiveHeader?["title"]?["runs"]?.AsArray();
                        string? title = titleRuns != null 
                            ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString())) 
                            : responsiveHeader?["title"]?["simpleText"]?.ToString() ?? responsiveHeader?["title"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            result.Playlist.Title = title;
                        }

                        var subtitleRuns = responsiveHeader?["subtitle"]?["runs"]?.AsArray()
                                        ?? responsiveHeader?["straplineTextOne"]?["runs"]?.AsArray();
                        if (subtitleRuns != null)
                        {
                            var (creator, _) = ParseArtistRuns(subtitleRuns);
                            result.Playlist.Creator = creator;
                        }

                        var descRuns = responsiveHeader?["description"]?["musicDescriptionShelfRenderer"]?["description"]?["runs"]?.AsArray()
                                    ?? responsiveHeader?["description"]?["runs"]?.AsArray();
                        if (descRuns != null)
                        {
                            result.Playlist.Description = string.Join("", descRuns.Select(r => r?["text"]?.ToString()));
                        }

                        var thumbs = responsiveHeader?["thumbnail"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                        result.Playlist.ThumbnailUrl = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();
                    }

                    bool isLikedMusic = playlistId.Equals("VLLM", StringComparison.OrdinalIgnoreCase) ||
                                       playlistId.Equals("LM", StringComparison.OrdinalIgnoreCase) ||
                                       browseId.Equals("VLLM", StringComparison.OrdinalIgnoreCase);

                    if (isLikedMusic)
                    {
                        canEdit = true;
                        if (string.IsNullOrWhiteSpace(result.Playlist.Title))
                            result.Playlist.Title = "Oblíbené skladby";
                        if (string.IsNullOrWhiteSpace(result.Playlist.ThumbnailUrl))
                            result.Playlist.ThumbnailUrl = "https://www.gstatic.com/youtube/media/ytm/images/pbg/liked-songs-delhi-1200.png";
                        if (string.IsNullOrWhiteSpace(result.Playlist.Creator))
                            result.Playlist.Creator = "Automatický playlist";
                    }

                    if (!canEdit)
                    {
                        if (responsiveHeader?["editHeader"] != null || 
                            editableHeader?["editHeader"] != null ||
                            IsUserCreator(result.Playlist.Creator))
                        {
                            canEdit = true;
                        }
                    }

                    var songs = new List<SongModel>();
                    ExtractSongsFromNode(root?["contents"], songs);

                    if (!canEdit && songs.Any(s => !string.IsNullOrEmpty(s.SetVideoId)))
                    {
                        canEdit = true;
                    }

                    result.Playlist.CanEdit = canEdit;

                    foreach (var s in songs)
                    {
                        s.CanEdit = result.Playlist.CanEdit;
                    }

                    result.Songs = songs;
                    result.Playlist.SongCount = songs.Count;
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování detailů playlistu: {ex.Message}");
            }

            // Fallback přes YouTubeMusicClient
            try
            {
                string cleanId = playlistId.StartsWith("VL") ? playlistId.Substring(2) : playlistId;
                var playlistSongs = _client.GetCommunityPlaylistSongsAsync(cleanId);
                var items = await playlistSongs.FetchItemsAsync(0, 100);
                foreach (var song in items)
                {
                    string artistName = song.Artists != null && song.Artists.Any()
                        ? string.Join(", ", song.Artists.Select(a => a.Name))
                        : "Neznámý interpret";
                    string? artistId = song.Artists?.FirstOrDefault(a => !string.IsNullOrEmpty(a.Id))?.Id;
                    result.Songs.Add(new SongModel
                    {
                        VideoId = song.Id,
                        Title = song.Name,
                        Artist = artistName,
                        ArtistId = artistId,
                        ThumbnailUrl = song.Thumbnails?.LastOrDefault()?.Url ?? song.Thumbnails?.FirstOrDefault()?.Url
                    });
                }
                result.Playlist.SongCount = result.Songs.Count;
            }
            catch { }

            return result;
        }

        public async Task<List<SongModel>> GetPlaylistSongsAsync(string playlistId)
        {
            var details = await GetPlaylistDetailsAsync(playlistId);
            return details.Songs;
        }

        public async Task<PlaylistDetailsResult> GetLikedSongsAsync()
        {
            return await GetPlaylistDetailsAsync("VLLM");
        }

        public async Task<string?> CreatePlaylistAsync(string title, string description = "", string privacyStatus = "PRIVATE")
        {
            await EnsureInitializedAsync();
            try
            {
                var body = new
                {
                    context = CreateInnertubeContext(),
                    title = title,
                    description = description ?? "",
                    privacyStatus = privacyStatus
                };

                var root = await PostInnertubeAsync("playlist/create", body);
                if (root != null)
                {
                    var playlistId = root?["playlistId"]?.ToString();
                    if (!string.IsNullOrEmpty(playlistId))
                    {
                        return playlistId;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při vytváření playlistu na YouTube Music: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> AddSongToPlaylistAsync(string playlistId, string videoId)
        {
            await EnsureInitializedAsync();
            try
            {
                string cleanId = playlistId.StartsWith("VL", StringComparison.OrdinalIgnoreCase) 
                    ? playlistId.Substring(2) 
                    : playlistId;

                // Oblíbené skladby na YouTube Music používají koncový bod like/like
                if (cleanId.Equals("LM", StringComparison.OrdinalIgnoreCase) || 
                    cleanId.Equals("VLLM", StringComparison.OrdinalIgnoreCase))
                {
                    return await LikeSongAsync(videoId);
                }

                var body = new
                {
                    context = CreateInnertubeContext(),
                    playlistId = cleanId,
                    actions = new object[]
                    {
                        new
                        {
                            action = "ACTION_ADD_VIDEO",
                            addedVideoId = videoId
                        }
                    }
                };

                var root = await PostInnertubeAsync("browse/edit_playlist", body);
                if (root != null)
                {
                    if (root["error"] != null)
                    {
                        string errMsg = root["error"]?["message"]?.ToString() ?? "Chyba API";
                        System.Diagnostics.Debug.WriteLine($"[MelodiumService] Chyba z YouTube Music při přidávání skladby: {errMsg}");
                        Console.WriteLine($"[MelodiumService] Chyba z YouTube Music při přidávání skladby: {errMsg}");
                        return false;
                    }

                    string? status = root?["status"]?.ToString();
                    if (!string.IsNullOrEmpty(status))
                    {
                        return status == "STATUS_SUCCEEDED" || status == "OK";
                    }

                    if (root["actions"] != null || root["playlistEditResults"] != null)
                    {
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při přidávání skladby do playlistu: {ex.Message}");
            }
            return false;
        }

        public async Task<List<PlaylistModel>> GetAddToPlaylistOptionsAsync(string videoId)
        {
            await EnsureInitializedAsync();
            var results = new List<PlaylistModel>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var body = new
                {
                    context = CreateInnertubeContext(),
                    videoIds = new[] { videoId }
                };

                var root = await PostInnertubeAsync("playlist/get_add_to_playlist", body);
                if (root != null)
                {
                    ExtractAddToPlaylistOptions(root, results, seenIds);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při volání playlist/get_add_to_playlist: {ex.Message}");
            }

            return results;
        }

        private void ExtractAddToPlaylistOptions(System.Text.Json.Nodes.JsonNode? node, List<PlaylistModel> results, HashSet<string> seenIds)
        {
            if (node == null) return;

            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                if (obj.ContainsKey("playlistAddToOptionRenderer"))
                {
                    var renderer = obj["playlistAddToOptionRenderer"];
                    var playlistId = renderer?["playlistId"]?.ToString();
                    if (!string.IsNullOrEmpty(playlistId) && !seenIds.Contains(playlistId))
                    {
                        seenIds.Add(playlistId);
                        var titleRuns = renderer?["title"]?["runs"]?.AsArray();
                        string title = titleRuns != null
                            ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString()))
                            : renderer?["title"]?["simpleText"]?.ToString() ?? "Playlist";

                        results.Add(new PlaylistModel
                        {
                            Id = playlistId,
                            Title = title,
                            Creator = !string.IsNullOrEmpty(_userChannelName) ? _userChannelName : "Vy",
                            CanEdit = true
                        });
                    }
                    return;
                }

                foreach (var prop in obj)
                {
                    ExtractAddToPlaylistOptions(prop.Value, results, seenIds);
                }
            }
            else if (node is System.Text.Json.Nodes.JsonArray arr)
            {
                foreach (var item in arr)
                {
                    ExtractAddToPlaylistOptions(item, results, seenIds);
                }
            }
        }

        public async Task<bool> RemoveSongFromPlaylistAsync(string playlistId, string? setVideoId, string videoId)
        {
            await EnsureInitializedAsync();
            try
            {
                string cleanId = playlistId.StartsWith("VL", StringComparison.OrdinalIgnoreCase) 
                    ? playlistId.Substring(2) 
                    : playlistId;

                if (cleanId.Equals("LM", StringComparison.OrdinalIgnoreCase) || 
                    cleanId.Equals("VLLM", StringComparison.OrdinalIgnoreCase))
                {
                    return await DislikeSongAsync(videoId);
                }

                object actionObj;
                if (!string.IsNullOrEmpty(setVideoId))
                {
                    actionObj = new
                    {
                        action = "ACTION_REMOVE_VIDEO",
                        setVideoId = setVideoId
                    };
                }
                else
                {
                    actionObj = new
                    {
                        action = "ACTION_REMOVE_VIDEO",
                        removedVideoId = videoId
                    };
                }

                var body = new
                {
                    context = CreateInnertubeContext(),
                    playlistId = cleanId,
                    actions = new object[] { actionObj }
                };

                var root = await PostInnertubeAsync("browse/edit_playlist", body);
                if (root != null)
                {
                    if (root["error"] != null) return false;
                    string? status = root?["status"]?.ToString();
                    if (!string.IsNullOrEmpty(status))
                    {
                        return status == "STATUS_SUCCEEDED" || status == "OK";
                    }
                    return root["actions"] != null || root["playlistEditResults"] != null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při odebírání skladby z playlistu: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> MoveSongInPlaylistAsync(string playlistId, string setVideoId, string? successorSetVideoId, string? predecessorSetVideoId)
        {
            await EnsureInitializedAsync();
            try
            {
                string cleanId = playlistId.StartsWith("VL") ? playlistId.Substring(2) : playlistId;
                object actionObj;
                if (!string.IsNullOrEmpty(successorSetVideoId))
                {
                    actionObj = new
                    {
                        action = "ACTION_MOVE_VIDEO_BEFORE",
                        setVideoId = setVideoId,
                        movedSetVideoIdSuccessor = successorSetVideoId
                    };
                }
                else if (!string.IsNullOrEmpty(predecessorSetVideoId))
                {
                    actionObj = new
                    {
                        action = "ACTION_MOVE_VIDEO_AFTER",
                        setVideoId = setVideoId,
                        movedSetVideoIdPredecessor = predecessorSetVideoId
                    };
                }
                else
                {
                    return false;
                }

                var body = new
                {
                    context = CreateInnertubeContext(),
                    playlistId = cleanId,
                    actions = new object[] { actionObj }
                };

                var root = await PostInnertubeAsync("browse/edit_playlist", body);
                if (root != null)
                {
                    string? status = root?["status"]?.ToString();
                    return status != "STATUS_FAILED";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při přesunu skladby v playlistu: {ex.Message}");
            }
            return false;
        }

        public async Task<List<SongModel>> GetAlbumSongsAsync(string albumId)
        {
            var results = new List<SongModel>();
            try
            {
                var albumInfo = await _client.GetAlbumInfoAsync(albumId);
                if (albumInfo?.Songs != null)
                {
                    string artistName = albumInfo.Artists != null && albumInfo.Artists.Any()
                        ? string.Join(", ", albumInfo.Artists.Select(a => a.Name))
                        : "Neznámý interpret";
                    string? artistId = albumInfo.Artists?.FirstOrDefault(a => !string.IsNullOrEmpty(a.Id))?.Id;
                    string? thumbnailUrl = albumInfo.Thumbnails?.LastOrDefault()?.Url ?? albumInfo.Thumbnails?.FirstOrDefault()?.Url;

                    foreach (var song in albumInfo.Songs)
                    {
                        results.Add(new SongModel
                        {
                            VideoId = song.Id,
                            Title = song.Name,
                            Artist = artistName,
                            ArtistId = artistId,
                            ThumbnailUrl = thumbnailUrl
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování skladeb alba: {ex.Message}");
            }
            return results;
        }
        // --- Nové funkce pro Domů a Rádio ---

        private readonly Dictionary<string, string> _homeMoodChips = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string> HomeMoodChips => _homeMoodChips;

        public async Task<List<HomeSectionModel>> GetHomeSectionsAsync(string? @params = null)
        {
            await EnsureInitializedAsync();
            var sections = new List<HomeSectionModel>();

            try
            {
                var body = @params != null
                    ? (object)new { context = CreateInnertubeContext(), browseId = "FEmusic_home", @params = @params }
                    : (object)new { context = CreateInnertubeContext(), browseId = "FEmusic_home" };

                var root = await PostInnertubeAsync("browse", body);
                if (root != null)
                {
                    if (@params == null)
                    {
                        var chipNodes = root?["contents"]?["singleColumnBrowseResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["header"]?["chipCloudRenderer"]?["chips"]?.AsArray();
                        if (chipNodes != null)
                        {
                            ParseHomeChips(chipNodes);
                        }
                    }

                    var sectionNodes = root?["contents"]?["singleColumnBrowseResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"]?.AsArray();
                    if (sectionNodes != null)
                    {
                        ParseHomeSections(sectionNodes, sections);
                    }

                    // Načíst 1 stránku pokračování pro další sekce, pokud je to potřeba (šetří čas a síť)
                    var continuations = root?["contents"]?["singleColumnBrowseResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["continuations"]?.AsArray();
                    string? ctoken = continuations?[0]?["nextContinuationData"]?["continuation"]?.ToString();

                    if (!string.IsNullOrEmpty(ctoken) && sections.Count < 6)
                    {
                        var contBody = new
                        {
                            context = CreateInnertubeContext()
                        };
                        var contRoot = await PostInnertubeAsync($"browse?continuation={ctoken}", contBody);
                        if (contRoot != null)
                        {
                            var contItems = contRoot?["continuationContents"]?["sectionListContinuation"]?["contents"]?.AsArray();
                            if (contItems != null)
                            {
                                ParseHomeSections(contItems, sections);
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při stahování Home sekcí: {ex.Message}");
                Console.WriteLine($"Chyba při stahování Home sekcí: {ex.Message}");
            }

            return sections;
        }

        private void ParseHomeChips(System.Text.Json.Nodes.JsonArray chipNodes)
        {
            try
            {
                _homeMoodChips.Clear();
                foreach (var cNode in chipNodes)
                {
                    var chip = cNode?["chipCloudChipRenderer"];
                    if (chip == null) continue;
                    var textRuns = chip?["text"]?["runs"]?.AsArray();
                    string? text = textRuns?.FirstOrDefault()?["text"]?.ToString();
                    string? chipParam = chip?["navigationEndpoint"]?["browseEndpoint"]?["params"]?.ToString();
                    if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(chipParam))
                    {
                        _homeMoodChips[text] = chipParam;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při parsování chips: {ex.Message}");
            }
        }

        public string? FindMoodParam(string mood)
        {
            if (string.IsNullOrWhiteSpace(mood)) return null;

            if (_homeMoodChips.TryGetValue(mood, out var exactParam))
                return exactParam;

            string lower = mood.ToLowerInvariant().Trim();
            foreach (var kvp in _homeMoodChips)
            {
                string key = kvp.Key.ToLowerInvariant();
                if (key == lower) return kvp.Value;

                if (lower.Contains("relax") && key.Contains("relax")) return kvp.Value;
                if ((lower.Contains("energ") || lower.Contains("party") || lower.Contains("párty")) &&
                    (key.Contains("energ") || key.Contains("party") || key.Contains("párty"))) return kvp.Value;
                if ((lower.Contains("cvič") || lower.Contains("workout")) &&
                    (key.Contains("workout") || key.Contains("cvič"))) return kvp.Value;
                if ((lower.Contains("soustřed") || lower.Contains("focus")) &&
                    (key.Contains("focus") || key.Contains("soustřed"))) return kvp.Value;
                if ((lower.Contains("dojížd") || lower.Contains("commute")) &&
                    (key.Contains("commute") || key.Contains("dojížd"))) return kvp.Value;
                if ((lower.Contains("spán") || lower.Contains("sleep")) &&
                    (key.Contains("sleep") || key.Contains("spán"))) return kvp.Value;
                if ((lower.Contains("romant") || lower.Contains("romance")) &&
                    (key.Contains("romance") || key.Contains("romant"))) return kvp.Value;
            }

            return null;
        }

        public async Task<List<HomeSectionModel>> GetMoodHomeSectionsAsync(string mood)
        {
            var sections = new List<HomeSectionModel>();

            // 1. Zkusit nativní YouTube Music browse s parametrem chipu
            var chipParam = FindMoodParam(mood);
            if (!string.IsNullOrEmpty(chipParam))
            {
                try
                {
                    var nativeSections = await GetHomeSectionsAsync(chipParam);
                    if (nativeSections != null && nativeSections.Count > 0)
                    {
                        return nativeSections;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Chyba při stahování nativních sekcí nálady {mood}: {ex.Message}");
                }
            }

            // 2. Fallback syntéza: skladby, playlisty a alba pro danou náladu/žánr
            try
            {
                var songsTask = GetMoodSongsAsync(mood);
                var playlistsTask = SearchPlaylistsAsync($"{mood}");
                var albumsTask = SearchAlbumsAsync($"{mood}");

                await Task.WhenAll(songsTask, playlistsTask, albumsTask);

                var songs = await songsTask;
                var playlists = await playlistsTask;
                var albums = await albumsTask;

                if (songs != null && songs.Count > 0)
                {
                    var songSection = new HomeSectionModel
                    {
                        Title = $"Doporučené skladby pro náladu {mood} 🎶",
                        Strapline = "SKLADBY"
                    };
                    foreach (var s in songs.Take(25))
                    {
                        songSection.Items.Add(new HomeItemModel
                        {
                            Type = HomeItemType.Song,
                            Id = s.VideoId,
                            Title = s.Title,
                            Subtitle = s.Artist,
                            ThumbnailUrl = s.ThumbnailUrl,
                            Song = s
                        });
                    }
                    sections.Add(songSection);
                }

                if (playlists != null && playlists.Count > 0)
                {
                    var plSection = new HomeSectionModel
                    {
                        Title = $"Oblíbené {mood} playlisty 📀",
                        Strapline = "PLAYLISTY"
                    };
                    foreach (var pl in playlists.Take(15))
                    {
                        plSection.Items.Add(new HomeItemModel
                        {
                            Type = HomeItemType.Playlist,
                            Id = pl.Id,
                            Title = pl.Title,
                            Subtitle = pl.Creator,
                            ThumbnailUrl = pl.ThumbnailUrl,
                            Playlist = pl
                        });
                    }
                    sections.Add(plSection);
                }

                if (albums != null && albums.Count > 0)
                {
                    var albumSection = new HomeSectionModel
                    {
                        Title = $"Populární alba ({mood}) 💿",
                        Strapline = "ALBA"
                    };
                    foreach (var a in albums.Take(15))
                    {
                        albumSection.Items.Add(new HomeItemModel
                        {
                            Type = HomeItemType.Album,
                            Id = a.Id,
                            Title = a.Title,
                            Subtitle = a.Artist,
                            ThumbnailUrl = a.ThumbnailUrl,
                            Album = a
                        });
                    }
                    sections.Add(albumSection);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při syntéze sekcí pro {mood}: {ex.Message}");
            }

            return sections;
        }

        private void ParseHomeSections(System.Text.Json.Nodes.JsonArray sectionNodes, List<HomeSectionModel> sections)
        {
            foreach (var sNode in sectionNodes)
            {
                var carousel = sNode?["musicCarouselShelfRenderer"];
                if (carousel == null) continue;

                var header = carousel?["header"]?["musicCarouselShelfBasicHeaderRenderer"];
                var titleRuns = header?["title"]?["runs"]?.AsArray();
                string? title = titleRuns != null 
                    ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString()))
                    : header?["title"]?["simpleText"]?.ToString();

                if (string.IsNullOrWhiteSpace(title)) continue;

                var strapRuns = header?["strapline"]?["runs"]?.AsArray();
                string? strapline = strapRuns != null
                    ? string.Join("", strapRuns.Select(r => r?["text"]?.ToString()))
                    : header?["strapline"]?["simpleText"]?.ToString();

                var section = new HomeSectionModel
                {
                    Title = title,
                    Strapline = strapline
                };

                var contents = carousel?["contents"]?.AsArray();
                if (contents != null)
                {
                    foreach (var c in contents)
                    {
                        var twoRow = c?["musicTwoRowItemRenderer"];
                        if (twoRow != null)
                        {
                            var itemTitleRuns = twoRow?["title"]?["runs"]?.AsArray();
                            string itemTitle = itemTitleRuns != null 
                                ? string.Join("", itemTitleRuns.Select(r => r?["text"]?.ToString()))
                                : twoRow?["title"]?["simpleText"]?.ToString() ?? "";

                            var subRuns = twoRow?["subtitle"]?["runs"]?.AsArray();
                            string itemSub = subRuns != null
                                ? string.Join("", subRuns.Select(r => r?["text"]?.ToString()))
                                : twoRow?["subtitle"]?["simpleText"]?.ToString() ?? "";

                            var thumbs = twoRow?["thumbnailRenderer"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                            string? thumb = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                            var browseEndpoint = twoRow?["navigationEndpoint"]?["browseEndpoint"];
                            string? browseId = browseEndpoint?["browseId"]?.ToString();
                            string? pageType = browseEndpoint?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();
                            string? watchVid = twoRow?["navigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString();

                            if (!string.IsNullOrEmpty(browseId))
                            {
                                if (browseId.StartsWith("VL") || browseId.StartsWith("PL") || browseId.StartsWith("RD") || pageType == "MUSIC_PAGE_TYPE_PLAYLIST")
                                {
                                    string cleanPlaylistId = browseId.StartsWith("VL") ? browseId.Substring(2) : browseId;
                                    var playlist = new PlaylistModel
                                    {
                                        Id = cleanPlaylistId,
                                        Title = itemTitle,
                                        ThumbnailUrl = thumb,
                                        Creator = itemSub
                                    };
                                    section.Items.Add(new HomeItemModel
                                    {
                                        Type = HomeItemType.Playlist,
                                        Id = cleanPlaylistId,
                                        Title = itemTitle,
                                        Subtitle = itemSub,
                                        ThumbnailUrl = thumb,
                                        Playlist = playlist
                                    });
                                    continue;
                                }
                                else if (pageType == "MUSIC_PAGE_TYPE_ALBUM" || browseId.StartsWith("MPREb_"))
                                {
                                    var album = new AlbumModel
                                    {
                                        Id = browseId,
                                        Title = itemTitle,
                                        ArtistName = itemSub,
                                        ThumbnailUrl = thumb
                                    };
                                    section.Items.Add(new HomeItemModel
                                    {
                                        Type = HomeItemType.Album,
                                        Id = browseId,
                                        Title = itemTitle,
                                        Subtitle = itemSub,
                                        ThumbnailUrl = thumb,
                                        Album = album
                                    });
                                    continue;
                                }
                                else if (pageType == "MUSIC_PAGE_TYPE_ARTIST" || browseId.StartsWith("UC"))
                                {
                                    var artist = new ArtistModel
                                    {
                                        Id = browseId,
                                        Name = itemTitle,
                                        Subscribers = itemSub,
                                        ThumbnailUrl = thumb
                                    };
                                    section.Items.Add(new HomeItemModel
                                    {
                                        Type = HomeItemType.Artist,
                                        Id = browseId,
                                        Title = itemTitle,
                                        Subtitle = itemSub,
                                        ThumbnailUrl = thumb,
                                        Artist = artist
                                    });
                                    continue;
                                }
                            }

                            if (!string.IsNullOrEmpty(watchVid))
                            {
                                var song = new SongModel
                                {
                                    VideoId = watchVid,
                                    Title = itemTitle,
                                    Artist = itemSub,
                                    ThumbnailUrl = thumb
                                };
                                section.Items.Add(new HomeItemModel
                                {
                                    Type = HomeItemType.Song,
                                    Id = watchVid,
                                    Title = itemTitle,
                                    Subtitle = itemSub,
                                    ThumbnailUrl = thumb,
                                    Song = song
                                });
                                continue;
                            }
                        }

                        var responsive = c?["musicResponsiveListItemRenderer"];
                        if (responsive != null)
                        {
                            var tRuns = responsive?["flexColumns"]?[0]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                            string t = tRuns != null ? string.Join("", tRuns.Select(r => r?["text"]?.ToString())) : "";

                            var sRuns = responsive?["flexColumns"]?[1]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                            string subText = sRuns != null ? string.Join("", sRuns.Select(r => r?["text"]?.ToString())) : "";

                            var thumbs = responsive?["thumbnail"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                            string? thumb = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                            string? vid = responsive?["playlistItemData"]?["videoId"]?.ToString()
                                       ?? responsive?["navigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString();

                            if (!string.IsNullOrEmpty(vid))
                            {
                                var song = new SongModel
                                {
                                    VideoId = vid,
                                    Title = t,
                                    Artist = subText,
                                    ThumbnailUrl = thumb
                                };
                                section.Items.Add(new HomeItemModel
                                {
                                    Type = HomeItemType.Song,
                                    Id = vid,
                                    Title = t,
                                    Subtitle = subText,
                                    ThumbnailUrl = thumb,
                                    Song = song
                                });
                            }
                        }
                    }
                }

                if (section.Items.Count > 0)
                {
                    sections.Add(section);
                }
            }
        }

        public async Task<List<SongModel>> GetHomeRecommendationsAsync()
        {
            var results = new List<SongModel>();
            try
            {
                var body = new {
                    context = CreateInnertubeContext(),
                    browseId = "FEmusic_home"
                };

                var root = await PostInnertubeAsync("browse", body);
                if (root != null)
                {
                    ExtractSongsFromNode(root, results);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování Home: {ex.Message}");
            }
            return results.GroupBy(r => r.VideoId).Select(g => g.First()).Take(40).ToList();
        }

        public async Task<List<SongModel>> GetUpNextRadioAsync(string videoId)
        {
            var results = new List<SongModel>();
            try
            {
                var body = new {
                    context = new {
                        client = new {
                            clientName = "WEB_REMIX",
                            clientVersion = "1.20230508.01.00",
                            hl = "cs",
                            gl = "CZ",
                            visitorData = _visitorData
                        }
                    },
                    videoId = videoId,
                    playlistId = "RDAMVM" + videoId
                };

                var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://music.youtube.com/youtubei/v1/next", content);
                response.EnsureSuccessStatusCode();

                var jsonStr = await response.Content.ReadAsStringAsync();
                var root = System.Text.Json.Nodes.JsonNode.Parse(jsonStr);
                
                ExtractSongsFromNode(root, results);
                
                // Odstranit aktuálně přehrávaný song a vzít limit
                results.RemoveAll(r => r.VideoId == videoId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování Up Next Rádia: {ex.Message}");
            }
            return results.GroupBy(r => r.VideoId).Select(g => g.First()).Take(30).ToList();
        }

        private static (string ArtistName, string? ArtistId) ParseArtistRuns(System.Text.Json.Nodes.JsonArray? runs)
        {
            if (runs == null || runs.Count == 0)
                return ("Neznámý interpret", null);

            var artistParts = new List<string>();
            string? artistId = null;

            int startIndex = 0;
            var firstText = runs[0]?["text"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(firstText) && 
                (firstText.Equals("Skladba", StringComparison.OrdinalIgnoreCase) ||
                 firstText.Equals("Píseň", StringComparison.OrdinalIgnoreCase) ||
                 firstText.Equals("Song", StringComparison.OrdinalIgnoreCase) ||
                 firstText.Equals("Video", StringComparison.OrdinalIgnoreCase)))
            {
                for (int i = 0; i < runs.Count; i++)
                {
                    if (runs[i]?["text"]?.ToString()?.Trim() == "•")
                    {
                        startIndex = i + 1;
                        break;
                    }
                }
            }

            for (int i = startIndex; i < runs.Count; i++)
            {
                var run = runs[i];
                var text = run?["text"]?.ToString();
                if (string.IsNullOrEmpty(text)) continue;

                if (text.Trim() == "•")
                    break;

                var browseId = run?["navigationEndpoint"]?["browseEndpoint"]?["browseId"]?.ToString();
                var pageType = run?["navigationEndpoint"]?["browseEndpoint"]?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();

                if (string.IsNullOrEmpty(artistId) && !string.IsNullOrEmpty(browseId))
                {
                    if (browseId.StartsWith("UC") || pageType == "MUSIC_PAGE_TYPE_ARTIST")
                    {
                        artistId = browseId;
                    }
                }

                artistParts.Add(text);
            }

            var fullArtist = string.Join("", artistParts).Trim();
            if (string.IsNullOrWhiteSpace(fullArtist))
            {
                foreach (var run in runs)
                {
                    var t = run?["text"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(t) && t != "•" &&
                        !t.Equals("Skladba", StringComparison.OrdinalIgnoreCase) &&
                        !t.Equals("Píseň", StringComparison.OrdinalIgnoreCase))
                    {
                        fullArtist = t;
                        break;
                    }
                }
                if (string.IsNullOrWhiteSpace(fullArtist))
                {
                    fullArtist = runs[0]?["text"]?.ToString() ?? "Neznámý interpret";
                }
            }

            if (string.IsNullOrEmpty(artistId))
            {
                foreach (var run in runs)
                {
                    var bId = run?["navigationEndpoint"]?["browseEndpoint"]?["browseId"]?.ToString();
                    var pType = run?["navigationEndpoint"]?["browseEndpoint"]?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();
                    if (!string.IsNullOrEmpty(bId) && (bId.StartsWith("UC") || pType == "MUSIC_PAGE_TYPE_ARTIST"))
                    {
                        artistId = bId;
                        break;
                    }
                }
            }

            return (fullArtist, artistId);
        }

        public bool IsUserCreator(string? creator)
        {
            if (string.IsNullOrWhiteSpace(creator)) return false;
            creator = creator.Trim();

            if (creator.Equals("Vy", StringComparison.OrdinalIgnoreCase) ||
                creator.Equals("You", StringComparison.OrdinalIgnoreCase) ||
                creator.Equals("Já", StringComparison.OrdinalIgnoreCase) ||
                creator.Equals("Me", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(_userChannelName) &&
                (creator.Equals(_userChannelName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 creator.Equals(_userChannelName.Trim().TrimStart('@'), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(_userChannelHandle) &&
                (creator.Equals(_userChannelHandle.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 creator.Equals(_userChannelHandle.Trim().TrimStart('@'), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private void ExtractPlaylistsFromNode(System.Text.Json.Nodes.JsonNode? node, List<PlaylistModel> results, HashSet<string> seenIds)
        {
            if (node == null) return;

            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                if (obj.ContainsKey("musicTwoRowItemRenderer"))
                {
                    var renderer = obj["musicTwoRowItemRenderer"];
                    var browseEndpoint = renderer?["navigationEndpoint"]?["browseEndpoint"];
                    var browseId = browseEndpoint?["browseId"]?.ToString();
                    var pageType = browseEndpoint?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();

                    bool isPlaylist = !string.IsNullOrEmpty(browseId) &&
                        (browseId.StartsWith("VLPL") || browseId.StartsWith("PL") || browseId.StartsWith("VLLM") || (browseId.StartsWith("VL") && !browseId.Contains("MPREb_")) || pageType == "MUSIC_PAGE_TYPE_PLAYLIST") &&
                        pageType != "MUSIC_PAGE_TYPE_ALBUM" &&
                        pageType != "MUSIC_PAGE_TYPE_ARTIST" &&
                        !browseId.StartsWith("UC");

                    if (isPlaylist && !string.IsNullOrEmpty(browseId))
                    {
                        string id = browseId.StartsWith("VL") ? browseId.Substring(2) : browseId;
                        if (!seenIds.Contains(id))
                        {
                            seenIds.Add(id);
                            var titleRuns = renderer?["title"]?["runs"]?.AsArray();
                            string title = titleRuns != null 
                                ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString())) 
                                : renderer?["title"]?["runs"]?[0]?["text"]?.ToString() ?? renderer?["title"]?["simpleText"]?.ToString() ?? "Playlist";

                            var subtitleRuns = renderer?["subtitle"]?["runs"]?.AsArray();
                            string creator = "YouTube Music";
                            int songCount = 0;

                            if (subtitleRuns != null)
                            {
                                var parts = new List<string>();
                                foreach (var run in subtitleRuns)
                                {
                                    var t = run?["text"]?.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(t) && t != "•")
                                    {
                                        parts.Add(t);
                                    }
                                }

                                foreach (var part in parts)
                                {
                                    if (part.Contains("sklad", StringComparison.OrdinalIgnoreCase) || 
                                        part.Contains("song", StringComparison.OrdinalIgnoreCase) || 
                                        part.Contains("track", StringComparison.OrdinalIgnoreCase) ||
                                        part.Contains("video", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var numStr = new string(part.TakeWhile(char.IsDigit).ToArray());
                                        if (int.TryParse(numStr, out int count))
                                            songCount = count;
                                    }
                                    else if (!part.Equals("Playlist", StringComparison.OrdinalIgnoreCase) &&
                                             !part.Equals("Seznam skladeb", StringComparison.OrdinalIgnoreCase))
                                    {
                                        creator = part;
                                    }
                                }
                            }

                            string jsonStr = renderer?.ToJsonString() ?? "";
                            bool canEdit = jsonStr.Contains("playlistEditorEndpoint") ||
                                           jsonStr.Contains("deletePlaylistEndpoint") ||
                                           jsonStr.Contains("editPlaylistEndpoint") ||
                                           jsonStr.Contains("\"iconType\":\"EDIT\"") ||
                                           jsonStr.Contains("\"iconType\":\"DELETE\"") ||
                                           jsonStr.Contains("USER_IMAGE_TYPE_OWNER") ||
                                           jsonStr.Contains("musicPlaylistEditHeaderRenderer") ||
                                           IsUserCreator(creator);

                            if (canEdit && (creator == "YouTube Music" || creator == "Playlist" || creator == "Seznam skladeb" || string.IsNullOrWhiteSpace(creator)))
                            {
                                creator = !string.IsNullOrEmpty(_userChannelName) ? _userChannelName : "Vy";
                            }

                            var thumbs = renderer?["thumbnailRenderer"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                            string? thumb = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                            results.Add(new PlaylistModel
                            {
                                Id = id,
                                Title = title,
                                ThumbnailUrl = thumb,
                                SongCount = songCount,
                                Creator = creator,
                                CanEdit = canEdit
                            });
                        }
                    }
                    return;
                }
                else if (obj.ContainsKey("musicResponsiveListItemRenderer"))
                {
                    var renderer = obj["musicResponsiveListItemRenderer"];
                    var browseEndpoint = renderer?["navigationEndpoint"]?["browseEndpoint"];
                    var browseId = browseEndpoint?["browseId"]?.ToString();
                    var pageType = browseEndpoint?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();

                    bool isPlaylist = !string.IsNullOrEmpty(browseId) &&
                        (browseId.StartsWith("VLPL") || browseId.StartsWith("PL") || (browseId.StartsWith("VL") && !browseId.Contains("MPREb_")) || pageType == "MUSIC_PAGE_TYPE_PLAYLIST") &&
                        pageType != "MUSIC_PAGE_TYPE_ALBUM" &&
                        pageType != "MUSIC_PAGE_TYPE_ARTIST" &&
                        !browseId.StartsWith("UC");

                    if (isPlaylist && !string.IsNullOrEmpty(browseId))
                    {
                        string id = browseId.StartsWith("VL") ? browseId.Substring(2) : browseId;
                        if (!seenIds.Contains(id))
                        {
                            seenIds.Add(id);
                            var titleRuns = renderer?["flexColumns"]?[0]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                            string title = titleRuns != null 
                                ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString())) 
                                : "Playlist";

                            var subtitleRuns = renderer?["flexColumns"]?.AsArray()?.ElementAtOrDefault(1)?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                            string creator = "YouTube Music";
                            if (subtitleRuns != null)
                            {
                                var (artist, _) = ParseArtistRuns(subtitleRuns);
                                if (!string.IsNullOrEmpty(artist)) creator = artist;
                            }

                            string jsonStr = renderer?.ToJsonString() ?? "";
                            bool canEdit = jsonStr.Contains("playlistEditorEndpoint") ||
                                           jsonStr.Contains("deletePlaylistEndpoint") ||
                                           jsonStr.Contains("editPlaylistEndpoint") ||
                                           jsonStr.Contains("\"iconType\":\"EDIT\"") ||
                                           jsonStr.Contains("\"iconType\":\"DELETE\"") ||
                                           jsonStr.Contains("USER_IMAGE_TYPE_OWNER") ||
                                           jsonStr.Contains("musicPlaylistEditHeaderRenderer") ||
                                           IsUserCreator(creator);

                            if (canEdit && (creator == "YouTube Music" || creator == "Playlist" || creator == "Seznam skladeb" || string.IsNullOrWhiteSpace(creator)))
                            {
                                creator = !string.IsNullOrEmpty(_userChannelName) ? _userChannelName : "Vy";
                            }

                            var thumbs = renderer?["thumbnail"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                            string? thumb = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                            results.Add(new PlaylistModel
                            {
                                Id = id,
                                Title = title,
                                ThumbnailUrl = thumb,
                                Creator = creator,
                                CanEdit = canEdit
                            });
                        }
                    }
                    return;
                }

                foreach (var prop in obj)
                {
                    ExtractPlaylistsFromNode(prop.Value, results, seenIds);
                }
            }
            else if (node is System.Text.Json.Nodes.JsonArray arr)
            {
                foreach (var item in arr)
                {
                    ExtractPlaylistsFromNode(item, results, seenIds);
                }
            }
        }

        private void ExtractSongsFromNode(System.Text.Json.Nodes.JsonNode? node, List<SongModel> results)
        {
            if (node == null) return;

            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                if (obj.ContainsKey("musicTwoRowItemRenderer"))
                {
                    var renderer = obj["musicTwoRowItemRenderer"];
                    var videoId = renderer?["navigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString();
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        var titleRuns = renderer?["title"]?["runs"]?.AsArray();
                        var title = titleRuns != null ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString())) : renderer?["title"]?["runs"]?[0]?["text"]?.ToString();
                        
                        var subtitleRuns = renderer?["subtitle"]?["runs"]?.AsArray();
                        var (artist, artistId) = ParseArtistRuns(subtitleRuns);
                        
                        var thumbs = renderer?["thumbnailRenderer"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                        var thumb = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();
                        
                        results.Add(new SongModel 
                        { 
                            VideoId = videoId, 
                            Title = !string.IsNullOrWhiteSpace(title) ? title : "Neznámé", 
                            Artist = artist, 
                            ArtistId = artistId, 
                            ThumbnailUrl = thumb 
                        });
                    }
                    return;
                }
                else if (obj.ContainsKey("musicResponsiveListItemRenderer"))
                {
                    var renderer = obj["musicResponsiveListItemRenderer"];
                    var videoId = renderer?["playlistItemData"]?["videoId"]?.ToString() 
                               ?? renderer?["navigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString()
                               ?? renderer?["overlay"]?["musicItemThumbnailOverlayRenderer"]?["content"]?["musicPlayButtonRenderer"]?["playNavigationEndpoint"]?["watchEndpoint"]?["videoId"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        var titleRuns = renderer?["flexColumns"]?[0]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                        var title = titleRuns != null ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString())) : renderer?["flexColumns"]?[0]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?[0]?["text"]?.ToString();

                        var artistRuns = renderer?["flexColumns"]?[1]?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?.AsArray();
                        var (artist, artistId) = ParseArtistRuns(artistRuns);

                        var thumbs = renderer?["thumbnail"]?["musicThumbnailRenderer"]?["thumbnail"]?["thumbnails"]?.AsArray();
                        var thumb = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                        var setVideoId = renderer?["playlistItemData"]?["playlistSetVideoId"]?.ToString();
                        var durationRuns = renderer?["fixedColumns"]?[0]?["musicResponsiveListItemFixedColumnRenderer"]?["text"]?["runs"]?.AsArray();
                        var duration = durationRuns != null ? string.Join("", durationRuns.Select(r => r?["text"]?.ToString())) : null;
                        
                        results.Add(new SongModel 
                        { 
                            VideoId = videoId, 
                            Title = !string.IsNullOrWhiteSpace(title) ? title : "Neznámé", 
                            Artist = artist, 
                            ArtistId = artistId, 
                            ThumbnailUrl = thumb,
                            SetVideoId = setVideoId,
                            Duration = duration
                        });
                    }
                    return;
                }
                else if (obj.ContainsKey("playlistPanelVideoRenderer"))
                {
                    var renderer = obj["playlistPanelVideoRenderer"];
                    var videoId = renderer?["videoId"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        var titleRuns = renderer?["title"]?["runs"]?.AsArray();
                        var title = titleRuns != null ? string.Join("", titleRuns.Select(r => r?["text"]?.ToString())) : renderer?["title"]?["runs"]?[0]?["text"]?.ToString();

                        var artistRuns = renderer?["longBylineText"]?["runs"]?.AsArray()
                                      ?? renderer?["shortBylineText"]?["runs"]?.AsArray();
                        var (artist, artistId) = ParseArtistRuns(artistRuns);

                        var thumbs = renderer?["thumbnail"]?["thumbnails"]?.AsArray();
                        var thumb = thumbs?.LastOrDefault()?["url"]?.ToString() ?? thumbs?.FirstOrDefault()?["url"]?.ToString();

                        var setVideoId = renderer?["playlistSetVideoId"]?.ToString();
                        
                        results.Add(new SongModel 
                        { 
                            VideoId = videoId, 
                            Title = !string.IsNullOrWhiteSpace(title) ? title : "Neznámé", 
                            Artist = artist, 
                            ArtistId = artistId, 
                            ThumbnailUrl = thumb,
                            SetVideoId = setVideoId
                        });
                    }
                    return;
                }
                
                foreach (var prop in obj)
                {
                    ExtractSongsFromNode(prop.Value, results);
                }
            }
            else if (node is System.Text.Json.Nodes.JsonArray arr)
            {
                foreach (var item in arr)
                {
                    ExtractSongsFromNode(item, results);
                }
            }
        }

        public async Task<ArtistDetailsModel?> GetArtistDetailsAsync(string? artistId, string? artistName)
        {
            await EnsureInitializedAsync();

            try
            {
                string resolvedId = artistId ?? string.Empty;

                // Pokud artistId není kanál (nezačíná UC), vyhledáme interpreta pro získání ID
                if (string.IsNullOrEmpty(resolvedId) || !resolvedId.StartsWith("UC"))
                {
                    if (!string.IsNullOrWhiteSpace(artistName))
                    {
                        var candidateNames = new List<string> { artistName.Trim() };

                        var primary = artistName.Split(new[] { ',', '&', ';', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?
                            .Split(new[] { " feat.", " feat ", " ft.", " ft ", " a " }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?
                            .Trim();

                        if (!string.IsNullOrEmpty(primary) && !candidateNames.Contains(primary, StringComparer.OrdinalIgnoreCase))
                        {
                            candidateNames.Add(primary);
                        }

                        foreach (var candidate in candidateNames)
                        {
                            try
                            {
                                var artistSearch = _client.SearchAsync(candidate, SearchCategory.Artists);
                                var foundArtists = await artistSearch.FetchItemsAsync(0, 3);
                                var topArtist = foundArtists.Cast<ArtistSearchResult>().FirstOrDefault();
                                if (topArtist != null && !string.IsNullOrEmpty(topArtist.Id))
                                {
                                    resolvedId = topArtist.Id;
                                    if (string.IsNullOrEmpty(artistName) || candidate != artistName)
                                    {
                                        artistName = topArtist.Name;
                                    }
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (string.IsNullOrEmpty(resolvedId) && string.IsNullOrWhiteSpace(artistName))
                {
                    return null;
                }

                YouTubeMusicAPI.Models.Info.ArtistInfo? artistInfo = null;
                if (!string.IsNullOrEmpty(resolvedId) && resolvedId.StartsWith("UC"))
                {
                    try
                    {
                        artistInfo = await _client.GetArtistInfoAsync(resolvedId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetArtistInfoAsync pro {resolvedId} vyhodilo: {ex.Message}");
                    }
                }

                var details = new ArtistDetailsModel
                {
                    Id = !string.IsNullOrEmpty(resolvedId) ? resolvedId : (artistInfo?.Id ?? string.Empty),
                    Name = artistInfo?.Name ?? artistName ?? "Neznámý interpret",
                    Description = artistInfo?.Description,
                    Subscribers = artistInfo?.SubscribersInfo,
                    ThumbnailUrl = artistInfo?.Thumbnails?.LastOrDefault()?.Url ?? artistInfo?.Thumbnails?.FirstOrDefault()?.Url
                };

                // Alba a singly
                if (artistInfo?.Albums != null)
                {
                    foreach (var a in artistInfo.Albums)
                    {
                        var albumModel = new AlbumModel
                        {
                            Id = a.Id,
                            Title = a.Name,
                            ArtistName = details.Name,
                            ReleaseYear = a.ReleaseYear,
                            ThumbnailUrl = a.Thumbnails?.LastOrDefault()?.Url ?? a.Thumbnails?.FirstOrDefault()?.Url
                        };

                        if (a.IsSingle)
                        {
                            details.Singles.Add(albumModel);
                        }
                        else
                        {
                            details.Albums.Add(albumModel);
                        }
                    }
                }

                // Populární skladby: nejprve zkusit přímo ze stránky interpreta
                if (artistInfo?.Songs != null && artistInfo.Songs.Length > 0)
                {
                    foreach (var s in artistInfo.Songs)
                    {
                        details.TopSongs.Add(new SongModel
                        {
                            VideoId = s.Id,
                            Title = s.Name,
                            Artist = details.Name,
                            ArtistId = details.Id,
                            ThumbnailUrl = s.Thumbnails?.LastOrDefault()?.Url ?? s.Thumbnails?.FirstOrDefault()?.Url
                        });
                    }
                }

                // Pokud artistInfo.Songs bylo prázdné, dohledat populární skladby interpreta přes vyhledávání
                if (details.TopSongs.Count == 0 && !string.IsNullOrWhiteSpace(details.Name))
                {
                    try
                    {
                        var songs = await SearchSongsAsync(details.Name);
                        foreach (var s in songs.Take(15))
                        {
                            if (string.IsNullOrEmpty(s.ArtistId))
                            {
                                s.ArtistId = details.Id;
                            }
                            details.TopSongs.Add(s);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Chyba při vyhledávání skladeb pro {details.Name}: {ex.Message}");
                    }
                }

                // Pokud interpret neměl thumbnail ze stránky, použít z první skladby
                if (string.IsNullOrEmpty(details.ThumbnailUrl) && details.TopSongs.Count > 0)
                {
                    details.ThumbnailUrl = details.TopSongs.FirstOrDefault(s => !string.IsNullOrEmpty(s.ThumbnailUrl))?.ThumbnailUrl;
                }

                return details;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při stahování detailů interpreta: {ex.Message}");
                return null;
            }
        }

        public async Task<LyricsResult?> GetLyricsForVideoAsync(string videoId)
        {
            try
            {
                await EnsureInitializedAsync();

                var body = new {
                    context = new {
                        client = new {
                            clientName = "WEB_REMIX",
                            clientVersion = "1.20230508.01.00",
                            hl = "cs",
                            gl = "CZ",
                            visitorData = _visitorData
                        }
                    },
                    videoId = videoId
                };

                var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://music.youtube.com/youtubei/v1/next", content);
                if (!response.IsSuccessStatusCode) return null;

                var jsonStr = await response.Content.ReadAsStringAsync();
                var root = System.Text.Json.Nodes.JsonNode.Parse(jsonStr);

                string? lyricsBrowseId = null;
                var tabs = root?["contents"]?["singleColumnMusicWatchNextResultsRenderer"]?["tabbedRenderer"]?["watchNextTabbedResultsRenderer"]?["tabs"]?.AsArray();
                if (tabs != null)
                {
                    foreach (var tab in tabs)
                    {
                        var renderer = tab?["tabRenderer"];
                        var pageType = renderer?["endpoint"]?["browseEndpoint"]?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString();
                        if (pageType == "MUSIC_PAGE_TYPE_TRACK_LYRICS")
                        {
                            lyricsBrowseId = renderer?["endpoint"]?["browseEndpoint"]?["browseId"]?.ToString();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(lyricsBrowseId)) return null;

                var browseBody = new {
                    context = new {
                        client = new {
                            clientName = "WEB_REMIX",
                            clientVersion = "1.20230508.01.00",
                            hl = "cs",
                            gl = "CZ",
                            visitorData = _visitorData
                        }
                    },
                    browseId = lyricsBrowseId
                };

                var browseContent = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(browseBody), System.Text.Encoding.UTF8, "application/json");
                var browseResp = await _httpClient.PostAsync("https://music.youtube.com/youtubei/v1/browse", browseContent);
                if (!browseResp.IsSuccessStatusCode) return null;

                var browseJson = await browseResp.Content.ReadAsStringAsync();
                var browseRoot = System.Text.Json.Nodes.JsonNode.Parse(browseJson);

                var shelf = browseRoot?["contents"]?["sectionListRenderer"]?["contents"]?[0]?["musicDescriptionShelfRenderer"];
                var runs = shelf?["description"]?["runs"]?.AsArray();
                var plainText = runs != null ? string.Join("", runs.Select(r => r?["text"]?.ToString())) : shelf?["description"]?["runs"]?[0]?["text"]?.ToString();

                if (!string.IsNullOrWhiteSpace(plainText))
                {
                    var lines = plainText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                         .Select(l => new LyricLineModel { Text = l.Trim() })
                                         .ToList();
                    return new LyricsResult
                    {
                        Lines = lines,
                        IsSynced = false,
                        Source = "YouTube Music"
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLyricsForVideoAsync chyba: {ex.Message}");
            }
            return null;
        }
    }
}