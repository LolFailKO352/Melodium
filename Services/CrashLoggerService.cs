using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Melodium.Services
{
    public static class CrashLoggerService
    {
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // 1. Zkontrolovat složku s logy a vymazat logy starší než 1 týden (7 dní)
            try
            {
                CleanupOldLogs(TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba při promazávání starých logů: {ex.Message}");
            }

            // 2. Globální zachytávače chyb pro neošetřené výjimky
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogCrash(ex, "AppDomain.UnhandledException", isTerminating: args.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogCrash(args.Exception, "TaskScheduler.UnobservedTaskException", isTerminating: false);
            };
        }

        public static void CleanupOldLogs(TimeSpan maxAge)
        {
            var cutoff = DateTime.Now - maxAge;
            var directoriesToCheck = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Melodium", "logs")
            };

            foreach (var dir in directoriesToCheck)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        foreach (var file in dirInfo.GetFiles("*.log"))
                        {
                            try
                            {
                                if (file.LastWriteTime < cutoff)
                                {
                                    file.Delete();
                                }
                            }
                            catch { }
                        }

                        foreach (var file in dirInfo.GetFiles("*.txt"))
                        {
                            try
                            {
                                if (file.LastWriteTime < cutoff)
                                {
                                    file.Delete();
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        public static void LogCrash(Exception? ex, string source = "UnhandledException", bool isTerminating = false)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"MELODIUM CRASH REPORT - {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine($"Is Terminating: {isTerminating}");
                sb.AppendLine($"OS Version: {Environment.OSVersion}");
                sb.AppendLine($"64-Bit OS: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"64-Bit Process: {Environment.Is64BitProcess}");
                sb.AppendLine($".NET Runtime: {Environment.Version}");
                sb.AppendLine($"App Base Directory: {AppContext.BaseDirectory}");
                sb.AppendLine("--------------------------------------------------------------------------------");

                if (ex != null)
                {
                    sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                    sb.AppendLine($"Message: {ex.Message}");
                    sb.AppendLine($"HResult: 0x{ex.HResult:X8}");
                    sb.AppendLine("StackTrace:");
                    sb.AppendLine(ex.StackTrace ?? "No stack trace available.");

                    var inner = ex.InnerException;
                    int level = 1;
                    while (inner != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"--- Inner Exception (Level {level}) ---");
                        sb.AppendLine($"Type: {inner.GetType().FullName}");
                        sb.AppendLine($"Message: {inner.Message}");
                        sb.AppendLine($"HResult: 0x{inner.HResult:X8}");
                        sb.AppendLine("StackTrace:");
                        sb.AppendLine(inner.StackTrace ?? "No stack trace available.");
                        inner = inner.InnerException;
                        level++;
                    }
                }
                else
                {
                    sb.AppendLine("No exception object provided.");
                }

                sb.AppendLine("================================================================================");

                string logContent = sb.ToString();
                string fileName = $"crash_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.log";

                // Pokusíme se zapsat do složky instalace aplikace (AppContext.BaseDirectory/logs)
                bool written = false;
                try
                {
                    string primaryDir = Path.Combine(AppContext.BaseDirectory, "logs");
                    if (!Directory.Exists(primaryDir))
                    {
                        Directory.CreateDirectory(primaryDir);
                    }
                    string primaryPath = Path.Combine(primaryDir, fileName);
                    File.WriteAllText(primaryPath, logContent, Encoding.UTF8);
                    written = true;
                }
                catch
                {
                    // V případě omezených práv k zápisu v instalační složce (např. Program Files)
                }

                // Pokud se nepodařilo zapsat do instalační složky, uložíme do LocalApplicationData
                if (!written)
                {
                    try
                    {
                        string fallbackDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Melodium", "logs");
                        if (!Directory.Exists(fallbackDir))
                        {
                            Directory.CreateDirectory(fallbackDir);
                        }
                        string fallbackPath = Path.Combine(fallbackDir, fileName);
                        File.WriteAllText(fallbackPath, logContent, Encoding.UTF8);
                    }
                    catch { }
                }

                System.Diagnostics.Debug.WriteLine(logContent);
            }
            catch { }
        }
    }
}
