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
        private bool _isInitialized;
        private SongModel? _currentSong;
        private bool _isPlaying;
        private long _startTimestamp;

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

                _updateLoopCts = new CancellationTokenSource();
                _ = RunUpdateLoopAsync(_updateLoopCts.Token);
                
                UpdatePresence();
            }
            catch (Exception ex)
            {
                RpcLog($"Failed to initialize: {ex.GetType().Name} - {ex.Message}");
                Deinitialize();
            }
        }

        public void Deinitialize()
        {
            RpcLog("Deinitialize called");
            _isInitialized = false;
            
            if (_updateLoopCts != null)
            {
                _updateLoopCts.Cancel();
                _updateLoopCts.Dispose();
                _updateLoopCts = null;
            }

            if (_activityManager != null)
            {
                try
                {
                    _activityManager.ClearActivity((res) => { });
                }
                catch { }
            }

            if (_discord != null)
            {
                try
                {
                    _discord.Dispose();
                }
                catch { }
                _discord = null;
                _activityManager = null;
            }
        }

        private async Task RunUpdateLoopAsync(CancellationToken token)
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
                    break;
                }
                catch (Exception)
                {
                    // Ignorovat
                }
                
                await Task.Delay(1000 / 60, token);
            }
        }

        public void UpdatePlaybackState(SongModel? song, bool isPlaying)
        {
            if (_currentSong != song)
            {
                _currentSong = song;
                _startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            
            _isPlaying = isPlaying;

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
                    // Assets = new Discord.ActivityAssets
                    // {
                    //    LargeImage = "logo",
                    //    LargeText = "Melodium"
                    // }
                };

                if (_currentSong != null)
                {
                    activity.Details = _currentSong.Title;
                    
                    if (!string.IsNullOrEmpty(_currentSong.Artist))
                    {
                        activity.State = "Od " + _currentSong.Artist;
                    }
                    else
                    {
                        activity.State = "Neznámý interpret";
                    }

                    if (_isPlaying)
                    {
                        activity.Timestamps = new Discord.ActivityTimestamps
                        {
                            Start = _startTimestamp
                        };
                    }
                    else
                    {
                        activity.State = "Pozastaveno";
                        activity.Details = _currentSong.Title;
                    }
                }
                else
                {
                    activity.Details = "Vybírá si hudbu...";
                    activity.State = string.Empty;
                }

                _activityManager.UpdateActivity(activity, (res) =>
                {
                    if (res != Discord.Result.Ok)
                    {
                        RpcLog($"UpdateActivity failed: {res}");
                    }
                });
            }
            catch (Exception ex)
            {
                RpcLog($"UpdatePresence Exception: {ex.Message}");
            }
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
                Deinitialize();
            }
        }

        public void Dispose()
        {
            Deinitialize();
        }
    }
}
