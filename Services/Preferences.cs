using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace Melodium.Services;

public class Preferences
{
    private static readonly Lazy<Preferences> _default = new(() => new Preferences());
    public static Preferences Default => _default.Value;

    private readonly string _settingsFilePath;
    private readonly ConcurrentDictionary<string, object> _settings = new();
    private readonly object _lock = new();

    public Preferences()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Melodium");
        Directory.CreateDirectory(dir);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        _settings[kv.Key] = kv.Value;
                    }
                }
            }
        }
        catch { }
    }

    private void Save()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }
    }

    public T Get<T>(string key, T defaultValue)
    {
        if (_settings.TryGetValue(key, out var val))
        {
            if (val is JsonElement elem)
            {
                try
                {
                    if (typeof(T) == typeof(string)) return (T)(object)elem.GetString()!;
                    if (typeof(T) == typeof(bool)) return (T)(object)elem.GetBoolean();
                    if (typeof(T) == typeof(int)) return (T)(object)elem.GetInt32();
                    if (typeof(T) == typeof(double)) return (T)(object)elem.GetDouble();
                    if (typeof(T) == typeof(float)) return (T)(object)(float)elem.GetDouble();
                    return JsonSerializer.Deserialize<T>(elem.GetRawText()) ?? defaultValue;
                }
                catch
                {
                    return defaultValue;
                }
            }
            if (val is T typed) return typed;
        }
        return defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        if (value == null)
        {
            _settings.TryRemove(key, out _);
        }
        else
        {
            _settings[key] = value;
        }
        Save();
    }
}

public static class SecureStorage
{
    public static class Default
    {
        public static Task<string?> GetAsync(string key)
        {
            var val = Preferences.Default.Get<string>(key, string.Empty);
            return Task.FromResult<string?>(string.IsNullOrEmpty(val) ? null : val);
        }

        public static Task SetAsync(string key, string value)
        {
            Preferences.Default.Set(key, value);
            return Task.CompletedTask;
        }

        public static bool Remove(string key)
        {
            Preferences.Default.Set<string?>(key, null);
            return true;
        }

        public static void RemoveAll()
        {
        }
    }
}
