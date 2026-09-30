using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Melodium.Models;

namespace Melodium.Services
{
    public class LyricsResult
    {
        public List<LyricLineModel> Lines { get; set; } = new();
        public bool IsSynced { get; set; }
        public string? Source { get; set; }
    }

    public class LyricsService
    {
        private readonly HttpClient _httpClient;
        private readonly MelodiumService _melodiumService;

        public LyricsService(MelodiumService melodiumService)
        {
            _melodiumService = melodiumService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Melodium/1.0 (Windows; https://github.com/LolFailKO352/YoutubeMusicMAUI)");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<LyricsResult?> GetLyricsAsync(string title, string artist, double durationSeconds = 0, string? videoId = null)
        {
            // 1. Try LRCLIB for synced (or plain) lyrics
            try
            {
                var lrcResult = await FetchFromLrcLibAsync(title, artist, durationSeconds);
                if (lrcResult != null && lrcResult.Lines.Count > 0)
                {
                    return lrcResult;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při stahování textu z LRCLIB: {ex.Message}");
            }

            // 2. Fallback to YouTube Music lyrics
            if (!string.IsNullOrEmpty(videoId))
            {
                try
                {
                    var ytmResult = await _melodiumService.GetLyricsForVideoAsync(videoId);
                    if (ytmResult != null && ytmResult.Lines.Count > 0)
                    {
                        return ytmResult;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Chyba při stahování textu z YouTube Music: {ex.Message}");
                }
            }

            return null;
        }

        private async Task<LyricsResult?> FetchFromLrcLibAsync(string title, string artist, double durationSeconds)
        {
            var cleanTitle = CleanTitle(title);
            var cleanArtist = CleanArtist(artist);

            if (string.IsNullOrWhiteSpace(cleanTitle))
                cleanTitle = title;
            if (string.IsNullOrWhiteSpace(cleanArtist))
                cleanArtist = artist;

            // Strategy 1: Exact /api/get
            var query = $"track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(cleanArtist)}";
            if (durationSeconds > 0)
            {
                query += $"&duration={Math.Round(durationSeconds)}";
            }

            try
            {
                var response = await _httpClient.GetAsync($"https://lrclib.net/api/get?{query}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync();
                    var result = ParseLrcLibJson(jsonStr);
                    if (result != null) return result;
                }
            }
            catch { }

            // Strategy 2: /api/search?track_name=...&artist_name=...
            try
            {
                var searchUrl = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(cleanArtist)}";
                var searchResponse = await _httpClient.GetAsync(searchUrl);
                if (searchResponse.IsSuccessStatusCode)
                {
                    var jsonStr = await searchResponse.Content.ReadAsStringAsync();
                    var node = JsonNode.Parse(jsonStr);
                    if (node is JsonArray arr && arr.Count > 0)
                    {
                        var bestItem = arr.FirstOrDefault(item => !string.IsNullOrEmpty(item?["syncedLyrics"]?.ToString()))
                                    ?? arr.FirstOrDefault();
                        if (bestItem != null)
                        {
                            var result = ParseLrcLibJson(bestItem.ToJsonString());
                            if (result != null) return result;
                        }
                    }
                }
            }
            catch { }

            // Strategy 3: /api/search?q=cleanTitle cleanArtist
            try
            {
                var qUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(cleanTitle + " " + cleanArtist)}";
                var qResponse = await _httpClient.GetAsync(qUrl);
                if (qResponse.IsSuccessStatusCode)
                {
                    var jsonStr = await qResponse.Content.ReadAsStringAsync();
                    var node = JsonNode.Parse(jsonStr);
                    if (node is JsonArray arr && arr.Count > 0)
                    {
                        var bestItem = arr.FirstOrDefault(item => !string.IsNullOrEmpty(item?["syncedLyrics"]?.ToString()))
                                    ?? arr.FirstOrDefault();
                        if (bestItem != null)
                        {
                            var result = ParseLrcLibJson(bestItem.ToJsonString());
                            if (result != null) return result;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private LyricsResult? ParseLrcLibJson(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                if (node == null) return null;

                var synced = node["syncedLyrics"]?.ToString();
                if (!string.IsNullOrWhiteSpace(synced))
                {
                    var lines = ParseLrc(synced);
                    if (lines.Count > 0)
                    {
                        return new LyricsResult
                        {
                            Lines = lines,
                            IsSynced = true,
                            Source = "LRCLIB"
                        };
                    }
                }

                var plain = node["plainLyrics"]?.ToString();
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    var lines = plain.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                     .Select(l => new LyricLineModel { Text = l.Trim() })
                                     .ToList();
                    return new LyricsResult
                    {
                        Lines = lines,
                        IsSynced = false,
                        Source = "LRCLIB"
                    };
                }
            }
            catch { }
            return null;
        }

        public static List<LyricLineModel> ParseLrc(string lrc)
        {
            var results = new List<LyricLineModel>();
            var lineRegex = new Regex(@"\[(\d{1,2}):(\d{2})(?:\.(\d{1,3}))?\](.*)");

            var rawLines = lrc.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in rawLines)
            {
                var match = lineRegex.Match(rawLine);
                if (match.Success)
                {
                    int minutes = int.Parse(match.Groups[1].Value);
                    int seconds = int.Parse(match.Groups[2].Value);
                    int millis = 0;
                    if (match.Groups[3].Success)
                    {
                        var msStr = match.Groups[3].Value.PadRight(3, '0');
                        if (msStr.Length > 3) msStr = msStr.Substring(0, 3);
                        int.TryParse(msStr, out millis);
                    }

                    var text = match.Groups[4].Value.Trim();
                    // Ignore metadata tags like [length: ...], [ar: ...]
                    if (rawLine.StartsWith("[ti:", StringComparison.OrdinalIgnoreCase) ||
                        rawLine.StartsWith("[ar:", StringComparison.OrdinalIgnoreCase) ||
                        rawLine.StartsWith("[al:", StringComparison.OrdinalIgnoreCase) ||
                        rawLine.StartsWith("[by:", StringComparison.OrdinalIgnoreCase) ||
                        rawLine.StartsWith("[re:", StringComparison.OrdinalIgnoreCase) ||
                        rawLine.StartsWith("[ve:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    results.Add(new LyricLineModel
                    {
                        Timestamp = new TimeSpan(0, 0, minutes, seconds, millis),
                        Text = text
                    });
                }
            }

            return results.OrderBy(r => r.Timestamp).ToList();
        }

        public static string CleanTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            string cleaned = title;
            cleaned = Regex.Replace(cleaned, @"\s*[\(\[](?:official|music|video|audio|lyrics|hd|4k|remastered|lyric video|visualizer|clip|video clip)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*[\(\[](?:feat\.|ft\.)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*-\s*YouTube$", "", RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }

        public static string CleanArtist(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return string.Empty;
            var parts = artist.Split(new[] { ',', '&', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var first = parts[0];
                var subParts = Regex.Split(first, @"\s+(?:feat\.|ft\.|a)\s+", RegexOptions.IgnoreCase);
                return subParts[0].Trim();
            }
            return artist.Trim();
        }
    }
}
