using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Melodium.Services;

public class WindowsAudioService : IAudioService, IDisposable
{
    private static readonly string AudioCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Melodium", "AudioCache");

    private static readonly HttpClient SharedAudioHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10,
        EnableMultipleHttp2Connections = true,
        AutomaticDecompression = System.Net.DecompressionMethods.None
    })
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    private readonly MediaPlayer _player;
    private string? _currentTempFile;
    private string? _currentPlayFile;
    private bool _disposed;
    private float _volume = 0.5f;

    public event Action<TimeSpan, TimeSpan>? PositionChanged;
    public event Action? MediaEnded;
    public event Action<bool>? PlaybackStateChanged;
    public event Action<string>? PlaybackError;

    static WindowsAudioService()
    {
        try
        {
            Directory.CreateDirectory(AudioCacheDir);
        }
        catch { }
    }

    public WindowsAudioService()
    {
        _player = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media,
            Volume = _volume
        };

        _player.PlaybackSession.PositionChanged += OnPositionChanged;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
    }

    private void OnPositionChanged(MediaPlaybackSession sender, object args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PositionChanged?.Invoke(sender.Position, sender.NaturalDuration);
        });
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlaybackStateChanged?.Invoke(IsPlaying);
        });
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlaybackStateChanged?.Invoke(false);
            MediaEnded?.Invoke();
        });
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Log($"MediaPlayer Failed: {args.ErrorMessage} (Error: {args.Error})");
            PlaybackError?.Invoke($"Chyba přehrávače: {args.ErrorMessage}");
            PlaybackStateChanged?.Invoke(false);
        });
    }

    public bool IsPlaying => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            _player.Volume = _volume;
        }
    }

    private static void Log(string message)
    {
        try
        {
            var msg = $"[{DateTime.Now:HH:mm:ss.fff}] [WindowsAudioService] {message}\n";
            var logPath = Path.Combine(Path.GetTempPath(), "ytm_audio_log.txt");
            File.AppendAllText(logPath, msg);
            System.Diagnostics.Debug.WriteLine(msg);
        }
        catch { }
    }

    public bool TryGetCachedAudio(string videoId, out string filePath)
    {
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(videoId)) return false;

        try
        {
            var aac = Path.Combine(AudioCacheDir, $"{videoId}.aac");
            if (File.Exists(aac))
            {
                var fi = new FileInfo(aac);
                if (fi.Length > 20_000)
                {
                    filePath = aac;
                    return true;
                }
            }

            var webm = Path.Combine(AudioCacheDir, $"{videoId}.webm");
            if (File.Exists(webm))
            {
                var fi = new FileInfo(webm);
                if (fi.Length > 20_000)
                {
                    filePath = webm;
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    public Task PlayFileAsync(string filePath, Action<string>? statusCallback = null)
    {
        Log($"PlayFileAsync called: {filePath}");

        try
        {
            if (!File.Exists(filePath))
            {
                PlaybackError?.Invoke("Soubor s audiem nenalezen.");
                return Task.CompletedTask;
            }

            Stop();
            _currentPlayFile = filePath;

            try
            {
                File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow);
            }
            catch { }

            statusCallback?.Invoke("Přehrávám (okamžitě z mezipaměti)");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var source = MediaSource.CreateFromUri(new Uri(filePath));
                _player.Source = source;
                _player.Play();
            });
        }
        catch (Exception ex)
        {
            Log($"ERROR in PlayFileAsync: {ex}");
            PlaybackError?.Invoke($"Chyba přehrávání: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public async Task PlayFromUrlAsync(string streamUrl, CancellationToken cancellationToken, Action<string>? statusCallback = null, string? videoId = null)
    {
        Log($"PlayFromUrlAsync called. VideoId={videoId}, URL length: {streamUrl?.Length}");

        // 1. Zkontrolovat, zda už nemáme skladbu v lokální mezipaměti
        if (!string.IsNullOrEmpty(videoId) && TryGetCachedAudio(videoId, out var cachedPath))
        {
            Log($"PlayFromUrlAsync: Cache hit pro {videoId} -> okamžité spuštění");
            await PlayFileAsync(cachedPath, statusCallback);
            return;
        }

        Stop();
        CleanupTempFile();

        statusCallback?.Invoke("Stahování audio streamu...");

        var playPath = await DownloadAndProcessAsync(videoId, streamUrl, cancellationToken, statusCallback);
        if (string.IsNullOrEmpty(playPath))
        {
            Log("Download/Process returned null or empty path");
            PlaybackError?.Invoke("Nepodařilo se stáhnout nebo zpracovat audio stream.");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await PlayFileAsync(playPath, statusCallback);
    }

    public async Task PrefetchSongAsync(string videoId, string streamUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(streamUrl))
            return;

        if (TryGetCachedAudio(videoId, out _))
            return; // Již v mezipaměti

        try
        {
            Log($"Prefetch: zahajuji stahování pro {videoId}");
            await DownloadAndProcessAsync(videoId, streamUrl, cancellationToken, statusCallback: null);
            Log($"Prefetch: dokončeno pro {videoId}");
        }
        catch (OperationCanceledException)
        {
            Log($"Prefetch: zrušeno pro {videoId}");
        }
        catch (Exception ex)
        {
            Log($"Prefetch: chyba pro {videoId}: {ex.Message}");
        }
    }

    private async Task<string?> DownloadAndProcessAsync(string? videoId, string url, CancellationToken ct, Action<string>? statusCallback)
    {
        string? tempPath = null;
        string? demuxPath = null;

        try
        {
            string ext = ".m4a";
            if (url.Contains("webm", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("mime=audio%2Fwebm", StringComparison.OrdinalIgnoreCase))
            {
                ext = ".webm";
            }

            var cleanUrl = System.Text.RegularExpressions.Regex.Replace(url, @"(?:&|\?)range=\d+(?:-\d+)?", "");
            Directory.CreateDirectory(AudioCacheDir);

            tempPath = Path.Combine(AudioCacheDir, $"dl_{Guid.NewGuid():N}{ext}");
            _currentTempFile = tempPath;

            const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            const long chunkSize = 10 * 1024 * 1024;
            long totalRead = 0;
            long? contentLength = null;

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    long rangeStart = totalRead;
                    long rangeEnd = totalRead + chunkSize - 1;

                    using var request = new HttpRequestMessage(HttpMethod.Get, cleanUrl);
                    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    request.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");
                    request.Headers.TryAddWithoutValidation("Referer", "https://music.youtube.com/");
                    request.Headers.TryAddWithoutValidation("Accept", "*/*");
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(rangeStart, rangeEnd);

                    using var response = await SharedAudioHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                    if (response.StatusCode != System.Net.HttpStatusCode.OK &&
                        response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    if (!contentLength.HasValue && response.Content.Headers.ContentRange?.Length.HasValue == true)
                    {
                        contentLength = response.Content.Headers.ContentRange.Length;
                    }
                    if (!contentLength.HasValue && response.Content.Headers.ContentLength.HasValue && response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        contentLength = response.Content.Headers.ContentLength;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(ct);
                    var buffer = new byte[131072];
                    int bytesRead;
                    long chunkRead = 0;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead, ct);
                        totalRead += bytesRead;
                        chunkRead += bytesRead;

                        if (statusCallback != null && totalRead % (512 * 1024) < 131072)
                        {
                            var mb = totalRead / (1024.0 * 1024.0);
                            if (contentLength.HasValue)
                            {
                                var pct = (totalRead * 100.0) / contentLength.Value;
                                statusCallback.Invoke($"Staženo: {mb:F1} MB ({pct:F0}%)");
                            }
                            else
                            {
                                statusCallback.Invoke($"Staženo: {mb:F1} MB...");
                            }
                        }
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.OK) break;
                    if (chunkRead < chunkSize) break;
                    if (contentLength.HasValue && totalRead >= contentLength.Value) break;
                }
            }

            ct.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(tempPath);
            if (!fileInfo.Exists || fileInfo.Length < 1000)
            {
                Log($"File too small or doesn't exist: {fileInfo.Length} bytes");
                return null;
            }

            statusCallback?.Invoke("Zpracovávám audio...");
            string processedPath = tempPath;

            // Pokud jde o MP4/M4A, demuxujeme na surový ADTS AAC pro bezproblémové přehrávání ve Windows MF
            if (ext == ".m4a")
            {
                try
                {
                    processedPath = FragmentedMp4Demuxer.DemuxToAacFile(tempPath);
                    demuxPath = processedPath;
                }
                catch (Exception demuxEx)
                {
                    Log($"Demux warning (používám původní soubor): {demuxEx.Message}");
                    processedPath = tempPath;
                }
            }

            // Pokud známe videoId, uložíme finální soubor do trvalé mezipaměti
            if (!string.IsNullOrEmpty(videoId))
            {
                string targetExt = processedPath.EndsWith(".aac", StringComparison.OrdinalIgnoreCase) ? ".aac" : ext;
                string finalCachedPath = Path.Combine(AudioCacheDir, $"{videoId}{targetExt}");

                try
                {
                    if (File.Exists(finalCachedPath))
                    {
                        File.Delete(finalCachedPath);
                    }
                    File.Move(processedPath, finalCachedPath);
                    processedPath = finalCachedPath;
                    _currentTempFile = null;
                }
                catch (Exception moveEx)
                {
                    Log($"Chyba při přesunu do cache ({moveEx.Message}), použije se processedPath.");
                }

                // Spustit promazání staré mezipaměti na pozadí
                _ = Task.Run(PruneCacheIfNeeded);
            }

            return processedPath;
        }
        catch (OperationCanceledException)
        {
            Log("Download cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Log($"Download ERROR: {ex}");
            statusCallback?.Invoke($"Chyba stahování: {ex.Message}");
            return null;
        }
        finally
        {
            // Smazat mezikrok temporary downloadu, pokud byl demuxován nebo přesunut
            try
            {
                if (tempPath != null && File.Exists(tempPath) && tempPath != _currentPlayFile && !tempPath.EndsWith(".aac") && !tempPath.EndsWith(".webm"))
                {
                    File.Delete(tempPath);
                }
            }
            catch { }
        }
    }

    private static void PruneCacheIfNeeded()
    {
        try
        {
            var dir = new DirectoryInfo(AudioCacheDir);
            if (!dir.Exists) return;

            var files = dir.GetFiles();
            long totalBytes = files.Sum(f => f.Length);
            const long maxCacheBytes = 1536L * 1024 * 1024; // 1.5 GB
            const long targetCacheBytes = 1024L * 1024 * 1024; // 1.0 GB

            if (totalBytes > maxCacheBytes)
            {
                // Seřadit od nejstarších podle data posledního přístupu
                var sorted = files.OrderBy(f => f.LastAccessTimeUtc).ToList();
                foreach (var file in sorted)
                {
                    try
                    {
                        // Nesmazat aktivní stahování .tmp
                        if (file.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)) continue;

                        long sz = file.Length;
                        file.Delete();
                        totalBytes -= sz;
                        if (totalBytes <= targetCacheBytes) break;
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public void Play()
    {
        Log("Play() called");
        _player.Play();
    }

    public void Pause()
    {
        Log("Pause() called");
        _player.Pause();
    }

    public void Stop()
    {
        Log("Stop() called");
        _player.Pause();
        _player.PlaybackSession.Position = TimeSpan.Zero;
    }

    public void SeekTo(TimeSpan position)
    {
        var duration = _player.PlaybackSession.NaturalDuration;
        var maxSeconds = duration > TimeSpan.Zero ? duration.TotalSeconds : 10000;
        var clampedPosition = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, maxSeconds));

        _player.PlaybackSession.Position = clampedPosition;
        Log($"SeekTo: {clampedPosition}");
    }

    private void CleanupTempFile()
    {
        // Maže POUZE přechodné stahovací soubory (.tmp), NIKDY ne trvalou cache (.aac/.webm)
        if (!string.IsNullOrEmpty(_currentTempFile) && File.Exists(_currentTempFile) && _currentTempFile.Contains(".tmp"))
        {
            try { File.Delete(_currentTempFile); } catch { }
        }
        _currentTempFile = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _player.PlaybackSession.PositionChanged -= OnPositionChanged;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        _player.Dispose();
        CleanupTempFile();
    }
}
