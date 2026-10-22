using System;
using System.Threading;
using System.Threading.Tasks;
using Melodium.Models;

namespace Melodium.Services
{
    public class DiscordRpcService : IDisposable
    {
        private Discord.Discord? _discord;
        private Discord.ActivityManager? _activityManager;
        private readonly long _clientId = 1006633898123468851;
        private CancellationTokenSource? _updateLoopCts;
        private CancellationTokenSource? _reconnectCts;
        private bool _isInitialized;
        private SongModel? _currentSong;
        private bool _isPlaying;
        private double _positionSeconds;
        private double _durationSeconds;

        private static void RpcLog(string message)
        {
            try
            {
                var msg = $"[{DateTime.Now:HH:mm:ss.fff}] [DiscordRPC] {message}\n";
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ytm_discord_log.txt");
                System.IO.File.AppendAllText(logPath, msg);
                System.Diagnostics.Debug.WriteLine(msg);
            }
            catch { }
        }

        public void Initialize()
        {
            RpcLog("Initialize called");
            if (_isInitialized) return;

            bool isEnabled = Preferences.Default.Get("IsDiscordRpcEnabled", false);
            RpcLog($"IsDiscordRpcEnabled={isEnabled}");
            if (!isEnabled) return;

            try
            {
                RpcLog("Creating Discord instance...");
                _discord = new Discord.Discord(_clientId, (ulong)Discord.CreateFlags.NoRequireDiscord);
                _activityManager = _discord.GetActivityManager();
                _discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
                {
                    RpcLog($"{level}: {message}");
                });

                _isInitialized = true;
                RpcLog("Initialization successful, starting update loop.");

                _reconnectCts?.Cancel();
                _reconnectCts?.Dispose();
                _reconnectCts = null;

                _updateLoopCts = new CancellationTokenSource();
                _ = RunUpdateLoopAsync(_updateLoopCts.Token);
                
                UpdatePresence();
            }
            catch (Exception ex)
            {
                RpcLog($"Failed to initialize: {ex.GetType().Name} - {ex.Message}");
                Deinitialize();
                ScheduleReconnect();
            }
        }

        private void ScheduleReconnect()
        {
            bool isEnabled = Preferences.Default.Get("IsDiscordRpcEnabled", false);
            if (!isEnabled || _isInitialized) return;

            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(5000, token);
                    if (!token.IsCancellationRequested && !_isInitialized)
                    {
                        Initialize();
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, token);
        }

        public void Deinitialize()
        {
            RpcLog("Deinitialize called");
            _isInitialized = false;
            
            try
            {
                if (_updateLoopCts != null)
                {
                    _updateLoopCts.Cancel();
                    _updateLoopCts.Dispose();
                    _updateLoopCts = null;
                }
            }
            catch { }

            try
            {
                if (_activityManager != null)
                {
                    _activityManager.ClearActivity((res) => { });
                }
            }
            catch { }

            try
            {
                if (_discord != null)
                {
                    _discord.Dispose();
                }
            }
            catch { }
            finally
            {
                _discord = null;
                _activityManager = null;
            }
        }

        private async Task RunUpdateLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _isInitialized)
                {
                    try
                    {
                        _discord?.RunCallbacks();
                    }
                    catch (Discord.ResultException ex)
                    {
                        RpcLog($"ResultException in RunCallbacks: {ex.Message}");
                        Deinitialize();
                        ScheduleReconnect();
                        break;
                    }
                    catch (Exception)
                    {
                        // Ignorovat
                    }

                    await Task.Delay(1000 / 60, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normální stav při ukončení / deinitializaci
            }
            catch (Exception ex)
            {
                RpcLog($"RunUpdateLoopAsync error: {ex.Message}");
            }
        }

        public void UpdatePlaybackState(SongModel? song, bool isPlaying, double positionSeconds = 0, double durationSeconds = 0)
        {
            bool songChanged = _currentSong != song;
            _currentSong = song;
            _isPlaying = isPlaying;
            _positionSeconds = Math.Max(0, positionSeconds);

            if (durationSeconds > 0)
            {
                _durationSeconds = durationSeconds;
            }
            else if (song != null && !string.IsNullOrEmpty(song.Duration))
            {
                _durationSeconds = ParseDurationToSeconds(song.Duration);
            }
            else if (songChanged)
            {
                _durationSeconds = 0;
            }

            if (_isInitialized)
            {
                UpdatePresence();
            }
        }

        private void UpdatePresence()
        {
            if (!_isInitialized || _activityManager == null) return;

            try
            {
                var activity = new Discord.Activity
                {
                    Type = Discord.ActivityType.Playing
                };

                if (_currentSong != null)
                {
                    string artist = CleanArtist(_currentSong.Artist);

                    // 1. Aktivita je samotná písnička (podobně jako u Spotify), nikoli aplikace
                    activity.Name = Truncate(_currentSong.Title, 127);
                    activity.Details = Truncate(_currentSong.Title, 127);

                    // 2. Interpret a stav přehrávání
                    if (_isPlaying)
                    {
                        activity.State = !string.IsNullOrWhiteSpace(artist)
                            ? (_durationSeconds > 0 ? Truncate($"od {artist} ({FormatTime(_durationSeconds)})", 127) : Truncate($"od {artist}", 127))
                            : (_durationSeconds > 0 ? Truncate($"Melodium ({FormatTime(_durationSeconds)})", 127) : "Melodium");
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(artist))
                        {
                            activity.State = _durationSeconds > 0
                                ? Truncate($"Pozastaveno - {artist} ({FormatTime(_durationSeconds)})", 127)
                                : Truncate($"Pozastaveno - {artist}", 127);
                        }
                        else
                        {
                            activity.State = _durationSeconds > 0
                                ? Truncate($"Pozastaveno ({FormatTime(_durationSeconds)})", 127)
                                : "Pozastaveno";
                        }
                    }

                    // 3. Obrázek alba (album cover art)
                    string imageUrl = ResolveThumbnailUrl(_currentSong);
                    activity.Assets = new Discord.ActivityAssets
                    {
                        LargeImage = imageUrl,
                        LargeText = Truncate(_currentSong.Title, 127),
                        SmallImage = "logo",
                        SmallText = _isPlaying
                            ? (_durationSeconds > 0 ? Truncate($"Přehrává se ({FormatTime(_durationSeconds)})", 127) : "Melodium")
                            : (_durationSeconds > 0 ? Truncate($"Pozastaveno ({FormatTime(_durationSeconds)})", 127) : "Pozastaveno")
                    };

                    // 4. Čas skladby v zeleném časovači Discordu (Timestamps.Start = now - position, End = 0)
                    // Tím Discord zobrazí zelený časovač přímo s aktuálním časem skladby (🎮 MM:SS) místo času od spuštění aplikace
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    long start = now - (long)Math.Max(0, _positionSeconds);

                    activity.Timestamps = new Discord.ActivityTimestamps
                    {
                        Start = start,
                        End = 0
                    };
                }
                else
                {
                    activity.Name = "Melodium";
                    activity.Details = "Vybírá si hudbu...";
                    activity.State = string.Empty;
                    activity.Assets = new Discord.ActivityAssets
                    {
                        LargeImage = "logo",
                        LargeText = "Melodium"
                    };
                    activity.Timestamps = new Discord.ActivityTimestamps();
                }

                _activityManager.UpdateActivity(activity, (res) =>
                {
                    if (res != Discord.Result.Ok)
                    {
                        RpcLog($"UpdateActivity failed: {res}");
                    }
                    else
                    {
                        RpcLog($"UpdateActivity OK: '{activity.Details}' - '{activity.State}' ({_positionSeconds:F0}s / {_durationSeconds:F0}s)");
                    }
                });
            }
            catch (Exception ex)
            {
                RpcLog($"UpdatePresence Exception: {ex.Message}");
            }
        }

        private static string CleanArtist(string? artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return string.Empty;
            string clean = artist.Trim();
            if (clean.StartsWith("Skladba • ", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(10).Trim();
            }
            else if (clean.StartsWith("Song • ", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(7).Trim();
            }
            return clean;
        }

        public static string FormatTime(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return "0:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        public static string ResolveThumbnailUrl(SongModel song)
        {
            if (!string.IsNullOrWhiteSpace(song.ThumbnailUrl))
            {
                string url = song.ThumbnailUrl.Trim();
                if (url.StartsWith("//"))
                {
                    url = "https:" + url;
                }
                else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url.Substring(7);
                }

                // Discord Game SDK Asset řetězce mají omezenou velikost bufferu (SizeConst = 128, max 127 znaků)
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && url.Length <= 127)
                {
                    return url;
                }
            }

            // Fallback na spolehlivou YouTube CDN miniaturu, pokud máme VideoId
            if (!string.IsNullOrWhiteSpace(song.VideoId))
            {
                return $"https://i.ytimg.com/vi/{song.VideoId}/hqdefault.jpg";
            }

            return "logo";
        }

        public static double ParseDurationToSeconds(string? durationStr)
        {
            if (string.IsNullOrWhiteSpace(durationStr)) return 0;
            string str = durationStr.Trim();

            // Pokud řetězec obsahuje další text (např. " • 3:45"), extrahujeme token s dvojtečkou
            if (str.Contains('•') || str.Contains(' '))
            {
                var tokens = str.Split(new[] { ' ', '•', '-' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    if (token.Contains(':'))
                    {
                        str = token;
                        break;
                    }
                }
            }

            var parts = str.Split(':');
            if (parts.Length == 2
                && int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var m)
                && int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var s))
            {
                return m * 60 + s;
            }
            if (parts.Length == 3
                && int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var h)
                && int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var m2)
                && int.TryParse(parts[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var s2))
            {
                return h * 3600 + m2 * 60 + s2;
            }
            return 0;
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public void HandleSettingsChanged()
        {
            RpcLog("HandleSettingsChanged called");
            bool isEnabled = Preferences.Default.Get("IsDiscordRpcEnabled", false);
            if (isEnabled)
            {
                Initialize();
            }
            else
            {
                _reconnectCts?.Cancel();
                _reconnectCts?.Dispose();
                _reconnectCts = null;
                Deinitialize();
            }
        }

        public void Dispose()
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;
            Deinitialize();
        }
    }
}
