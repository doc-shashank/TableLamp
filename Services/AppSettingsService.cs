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
    }

    public class AppSettingsService
    {
        private static AppSettingsService? _instance;
        public static AppSettingsService Instance => _instance ??= new AppSettingsService();

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
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storagePath, json);
            }
            catch
            {
                // Silently handle IO errors
            }
        }
    }
}
