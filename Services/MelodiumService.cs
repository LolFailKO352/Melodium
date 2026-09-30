using System;
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
    public class MelodiumService
    {
        private YouTubeMusicClient _client;
        private string _visitorData = "";
        private string _poToken = "";
        private bool _isInitialized = false;
        private IEnumerable<Cookie>? _currentCookies;
        private System.Net.Http.HttpClient _httpClient;
        private YoutubeClient _ytExplodeClient;

        public MelodiumService()
        {
            _ytExplodeClient = new YoutubeClient();
            // Výchozí inicializace bez cookies
            InitializeClient(null);
        }

        public async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

#if WINDOWS
            try 
            {
                var jsEnv = new YouTubeSessionGenerator.Js.Environments.NodeEnvironment();
                var config = new YouTubeSessionConfig { JsEnvironment = jsEnv };
                var creator = new YouTubeSessionCreator(config);
                _visitorData = await creator.VisitorDataAsync();
                _poToken = await creator.ProofOfOriginTokenAsync(_visitorData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při generování PoTokenu: {ex.Message}");
            }
#endif
            _isInitialized = true;
            InitializeClient(_currentCookies);
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
            var handler = new System.Net.Http.HttpClientHandler();
            string sapisid = null;

            if (cookies != null)
            {
                handler.CookieContainer = new System.Net.CookieContainer();
                foreach (var c in cookies)
                {
                    if (c.Name == "SAPISID" || c.Name == "__Secure-3PAPISID") sapisid = c.Value;
                    handler.CookieContainer.Add(new Uri("https://music.youtube.com"), c);
                }
            }
            _httpClient = new System.Net.Http.HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("X-Origin", "https://music.youtube.com");
            
            if (!string.IsNullOrEmpty(sapisid))
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string input = $"{timestamp} {sapisid} https://music.youtube.com";
                using var sha1 = System.Security.Cryptography.SHA1.Create();
                byte[] hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"SAPISIDHASH {timestamp}_{hash}");
            }
        }

        public async Task<List<SongModel>> SearchSongsAsync(string query)
        {
            var results = new List<SongModel>();

            try
            {
                var searchResults = _client.SearchAsync(query, SearchCategory.Songs);
                var bufferedSearchResults = await searchResults.FetchItemsAsync(0, 20);

                foreach (var song in bufferedSearchResults.Cast<SongSearchResult>())
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
                Console.WriteLine($"Chyba při hledání: {ex.Message}");
            }

            return results;
        }

        public async Task<string?> GetAudioStreamUrlAsync(string videoId, string? fallbackQuery = null)
        {
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
            var results = new List<PlaylistModel>();
            try
            {
                var playlists = await _client.GetLibraryCommunityPlaylistsAsync();
                if (playlists != null)
                {
                    foreach (var playlist in playlists)
                    {
                        results.Add(new PlaylistModel
                        {
                            Id = playlist.Id,
                            Title = playlist.Name,
                            ThumbnailUrl = playlist.Thumbnails?.FirstOrDefault()?.Url,
                            SongCount = playlist.SongCount,
                            Creator = playlist.Creator?.Name ?? "Komunita"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při stahování playlistů z knihovny: {ex.Message}");
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

        public async Task<List<SongModel>> GetPlaylistSongsAsync(string playlistId)
        {
            var results = new List<SongModel>();
            try
            {
                var playlistSongs = _client.GetCommunityPlaylistSongsAsync(playlistId);
                var items = await playlistSongs.FetchItemsAsync(0, 100);
                foreach (var song in items)
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
                Console.WriteLine($"Chyba při stahování skladeb playlistu: {ex.Message}");
            }
            return results;
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

        public async Task<List<SongModel>> GetHomeRecommendationsAsync()
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
                    browseId = "FEmusic_home"
                };

                var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://music.youtube.com/youtubei/v1/browse", content);
                response.EnsureSuccessStatusCode();

                var jsonStr = await response.Content.ReadAsStringAsync();
                var root = System.Text.Json.Nodes.JsonNode.Parse(jsonStr);
                
                ExtractSongsFromNode(root, results);
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
                        
                        results.Add(new SongModel 
                        { 
                            VideoId = videoId, 
                            Title = !string.IsNullOrWhiteSpace(title) ? title : "Neznámé", 
                            Artist = artist, 
                            ArtistId = artistId, 
                            ThumbnailUrl = thumb 
                        });
                    }
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
                        
                        results.Add(new SongModel 
                        { 
                            VideoId = videoId, 
                            Title = !string.IsNullOrWhiteSpace(title) ? title : "Neznámé", 
                            Artist = artist, 
                            ArtistId = artistId, 
                            ThumbnailUrl = thumb 
                        });
                    }
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
                        var songSearch = _client.SearchAsync(details.Name, SearchCategory.Songs);
                        var songs = await songSearch.FetchItemsAsync(0, 15);
                        foreach (var s in songs.Cast<SongSearchResult>())
                        {
                            details.TopSongs.Add(new SongModel
                            {
                                VideoId = s.Id,
                                Title = s.Name,
                                Artist = s.Artists != null && s.Artists.Any() ? string.Join(", ", s.Artists.Select(a => a.Name)) : details.Name,
                                ArtistId = s.Artists?.FirstOrDefault(a => !string.IsNullOrEmpty(a.Id))?.Id ?? details.Id,
                                ThumbnailUrl = s.Thumbnails?.LastOrDefault()?.Url ?? s.Thumbnails?.FirstOrDefault()?.Url
                            });
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