using System;
using System.IO;
using System.Text.Json;

namespace TableLamp.Services
{
    public class AppSettings
    {
        public int NotificationDurationSeconds { get; set; } = 5;
        public string? LastWorkspacePath { get; set; }
        public string? LastOpenedJsonPath { get; set; }
        public string CuratedContentVersion { get; set; } = "v0.0.7.5";
        public DateTime? CuratedContentLastUpdated { get; set; }
    }

    public class AppSettingsService
    {
        private static AppSettingsService? _instance;
        public static AppSettingsService Instance => _instance ??= new AppSettingsService();

        private readonly object _saveLock = new();
        private readonly string _storagePath;
        private AppSettings _settings = new();

        public int NotificationDurationSeconds
        {
            get => _settings.NotificationDurationSeconds;
            set
            {
                int clamped = Math.Clamp(value, 1, 60);
                if (_settings.NotificationDurationSeconds != clamped)
                {
                    _settings.NotificationDurationSeconds = clamped;
                    Save();
                    SettingsChanged?.Invoke();
                }
            }
        }

        public string? LastWorkspacePath
        {
            get => _settings.LastWorkspacePath;
            set
            {
                if (_settings.LastWorkspacePath != value)
                {
                    _settings.LastWorkspacePath = value;
                    Save();
                    SettingsChanged?.Invoke();
                }
            }
        }

        public string? LastOpenedJsonPath
        {
            get => _settings.LastOpenedJsonPath;
            set
            {
                if (_settings.LastOpenedJsonPath != value)
                {
                    _settings.LastOpenedJsonPath = value;
                    Save();
                    SettingsChanged?.Invoke();
                }
            }
        }

        public string CuratedContentVersion
        {
            get => string.IsNullOrWhiteSpace(_settings.CuratedContentVersion) ? "v0.0.7.5" : _settings.CuratedContentVersion;
            set
            {
                if (_settings.CuratedContentVersion != value)
                {
                    _settings.CuratedContentVersion = value;
                    Save();
                    SettingsChanged?.Invoke();
                }
            }
        }

        public DateTime? CuratedContentLastUpdated
        {
            get => _settings.CuratedContentLastUpdated;
            set
            {
                if (_settings.CuratedContentLastUpdated != value)
                {
                    _settings.CuratedContentLastUpdated = value;
                    Save();
                    SettingsChanged?.Invoke();
                }
            }
        }

        public event Action? SettingsChanged;

        public AppSettingsService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "TableLamp");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "settings.json");

            Load();
        }

        public void Load()
        {
            lock (_saveLock)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        if (File.Exists(_storagePath))
                        {
                            string json = File.ReadAllText(_storagePath);
                            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                            if (loaded != null)
                            {
                                _settings = loaded;
                                if (_settings.NotificationDurationSeconds <= 0)
                                {
                                    _settings.NotificationDurationSeconds = 5;
                                }
                            }
                        }
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(30);
                    }
                    catch
                    {
                        _settings = new AppSettings();
                        break;
                    }
                }
            }
        }

        public void Save()
        {
            lock (_saveLock)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(_storagePath, json);
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(30);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
        }
    }
}
