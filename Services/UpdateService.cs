using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Melodium.Services;

public class UpdateCheckResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public Version CurrentVersion { get; set; } = new(1, 0, 0, 0);
    public Version? LatestVersion { get; set; }
    public string? LatestVersionTag { get; set; }
    public string? ReleaseTitle { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? DownloadFileName { get; set; }
    public long DownloadFileSize { get; set; }
}

public class UpdateDownloadProgressInfo
{
    public double ProgressPercentage { get; set; }
    public long BytesReceived { get; set; }
    public long TotalBytes { get; set; }

    public UpdateDownloadProgressInfo(double progressPercentage, long bytesReceived, long totalBytes)
    {
        ProgressPercentage = progressPercentage;
        BytesReceived = bytesReceived;
        TotalBytes = totalBytes;
    }
}

public class UpdateService
{
    private readonly HttpClient _httpClient;
    private static readonly string[] Repositories = new[]
    {
        "LolFailKO352/Melodium",
        "LolFailKO352/YoutubeMusicMAUI"
    };

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Melodium-App", GetCurrentVersion().ToString()));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static Version GetCurrentVersion()
    {
        var asm = typeof(UpdateService).Assembly;
        var ver = asm.GetName().Version;
        if (ver != null && ver != new Version(0, 0, 0, 0))
        {
            return ver;
        }

        var infoVerAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrWhiteSpace(infoVerAttr?.InformationalVersion))
        {
            var parsed = ParseVersionString(infoVerAttr.InformationalVersion);
            if (parsed != null) return parsed;
        }

        return new Version(1, 6, 0, 0);
    }

    public static string GetCurrentVersionDisplay()
    {
        var ver = GetCurrentVersion();
        return $"{ver.Major}.{ver.Minor}" + (ver.Build > 0 ? $".{ver.Build}" : "");
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();
        Exception? lastException = null;

        foreach (var repo in Repositories)
        {
            try
            {
                var result = await QueryReleaseForRepoAsync(repo, currentVersion, cancellationToken);
                if (result != null && result.IsSuccess)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        return new UpdateCheckResult
        {
            IsSuccess = false,
            CurrentVersion = currentVersion,
            ErrorMessage = lastException != null ? lastException.Message : "Nepodařilo se načíst informace o aktualizacích z GitHubu."
        };
    }

    private async Task<UpdateCheckResult?> QueryReleaseForRepoAsync(string repo, Version currentVersion, CancellationToken cancellationToken)
    {
        string url = $"https://api.github.com/repos/{repo}/releases/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Fallback to all releases if latest gives 404
            string allUrl = $"https://api.github.com/repos/{repo}/releases";
            using var allResponse = await _httpClient.GetAsync(allUrl, cancellationToken);
            if (!allResponse.IsSuccessStatusCode) return null;

            var allJson = await allResponse.Content.ReadAsStringAsync(cancellationToken);
            using var allDoc = JsonDocument.Parse(allJson);
            if (allDoc.RootElement.ValueKind != JsonValueKind.Array || allDoc.RootElement.GetArrayLength() == 0)
                return null;

            var firstRelease = allDoc.RootElement[0];
            return ParseReleaseJson(firstRelease, currentVersion);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return ParseReleaseJson(doc.RootElement, currentVersion);
    }

    private UpdateCheckResult ParseReleaseJson(JsonElement releaseEl, Version currentVersion)
    {
        string tagName = releaseEl.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
        string releaseTitle = releaseEl.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
        string body = releaseEl.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
        string htmlUrl = releaseEl.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? "" : "";

        var parsedLatestVersion = ParseVersionString(tagName) ?? ParseVersionString(releaseTitle) ?? new Version(0, 0, 0, 0);

        string? downloadUrl = null;
        string? downloadFileName = null;
        long downloadFileSize = 0;

        if (releaseEl.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
        {
            // First search for .msi installer
            foreach (var asset in assetsEl.EnumerateArray())
            {
                string assetName = asset.TryGetProperty("name", out var aNameProp) ? aNameProp.GetString() ?? "" : "";
                if (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    downloadFileName = assetName;
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() : null;
                    downloadFileSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                    break;
                }
            }

            // Fallback to .exe if no .msi was found
            if (string.IsNullOrEmpty(downloadUrl))
            {
                foreach (var asset in assetsEl.EnumerateArray())
                {
                    string assetName = asset.TryGetProperty("name", out var aNameProp) ? aNameProp.GetString() ?? "" : "";
                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadFileName = assetName;
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() : null;
                        downloadFileSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                        break;
                    }
                }
            }
        }

        bool isUpdateAvailable = CompareVersions(parsedLatestVersion, currentVersion) > 0;

        return new UpdateCheckResult
        {
            IsSuccess = true,
            IsUpdateAvailable = isUpdateAvailable,
            CurrentVersion = currentVersion,
            LatestVersion = parsedLatestVersion,
            LatestVersionTag = tagName,
            ReleaseTitle = releaseTitle,
            ReleaseNotes = body,
            ReleaseUrl = htmlUrl,
            DownloadUrl = downloadUrl,
            DownloadFileName = downloadFileName ?? "Melodium-Setup.msi",
            DownloadFileSize = downloadFileSize
        };
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl, string fileName, IProgress<UpdateDownloadProgressInfo>? progress, CancellationToken cancellationToken = default)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "MelodiumUpdates");
        Directory.CreateDirectory(tempDir);
        string destinationFilePath = Path.Combine(tempDir, fileName);

        if (File.Exists(destinationFilePath))
        {
            try { File.Delete(destinationFilePath); } catch { }
        }

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var destStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                double percentage = Math.Round((double)totalRead / totalBytes * 100.0, 1);
                progress?.Report(new UpdateDownloadProgressInfo(percentage, totalRead, totalBytes));
            }
        }

        return destinationFilePath;
    }

    public void LaunchInstallerAndExit(string installerFilePath)
    {
        if (!File.Exists(installerFilePath))
        {
            throw new FileNotFoundException("Instalační soubor nebyl nalezen.", installerFilePath);
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "MelodiumUpdates");
        Directory.CreateDirectory(tempDir);
        string scriptPath = Path.Combine(tempDir, "apply_update.cmd");
        string currentExe = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Melodium.exe");

        // Pokud je balíček MSI, použijeme msiexec /passive /norestart pro autonomní instalaci s pruhem průběhu bez nutnosti klikání
        string installCmd = installerFilePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
            ? $"start /wait msiexec.exe /i \"{installerFilePath}\" /passive /norestart"
            : $"start /wait \"\" \"{installerFilePath}\"";

        // Skript počká na ukončení běžící instance, provede instalaci a automaticky znovu spustí novou verzi Melodium
        string scriptContent = $@"@echo off
chcp 65001 >nul
:: Cekani na ukonceni puvodni instance aplikace Melodium
timeout /t 2 /nobreak >nul

:: Spusteni instalatoru
{installCmd}

:: Cekani na uvolneni souboru
timeout /t 1 /nobreak >nul

:: Automaticke znovuspusteni aktualizovane aplikace Melodium
if exist ""{currentExe}"" (
    start """" ""{currentExe}""
) else if exist ""%ProgramFiles%\Melodium\Melodium.exe"" (
    start """" ""%ProgramFiles%\Melodium\Melodium.exe""
)
";

        File.WriteAllText(scriptPath, scriptContent);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(startInfo);

        // Ukončit aplikaci Melodium pro umožnění přepisu souborů instalátorem
        Task.Run(async () =>
        {
            await Task.Delay(400);
            App.ExitApplication();
        });
    }

    public static Version? ParseVersionString(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Regex.Match(text, @"(?:v|version|release)?\s*([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(text, @"([0-9]+\.[0-9]+(?:\.[0-9]+)?)");
        }

        if (match.Success)
        {
            var parts = match.Groups[1].Value.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int ma) ? ma : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int mi) ? mi : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out int bu) ? bu : 0;
            int revision = parts.Length > 3 && int.TryParse(parts[3], out int re) ? re : 0;
            return new Version(major, minor, build, revision);
        }

        return null;
    }

    public static int CompareVersions(Version v1, Version v2)
    {
        int c = v1.Major.CompareTo(v2.Major);
        if (c != 0) return c;
        c = v1.Minor.CompareTo(v2.Minor);
        if (c != 0) return c;
        c = Math.Max(0, v1.Build).CompareTo(Math.Max(0, v2.Build));
        if (c != 0) return c;
        return Math.Max(0, v1.Revision).CompareTo(Math.Max(0, v2.Revision));
    }
}
