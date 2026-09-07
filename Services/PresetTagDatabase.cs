using System;
using System.Collections.Generic;
using System.IO;
using TableLamp.Models;

namespace TableLamp.Services
{
    /// <summary>
    /// Singleton database service managing the in-memory tree database and persistent storage
    /// for the Preset Tag System. Dynamically loaded at launch.
    /// </summary>
    public class PresetTagDatabase
    {
        private static PresetTagDatabase? _instance;
        public static PresetTagDatabase Instance => _instance ??= new PresetTagDatabase();

        private readonly PresetTagGenerator _generator = new();
        private Dictionary<string, PresetSubject> _inMemoryTree = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _storagePath;

        public event Action? PresetsChanged;

        public PresetTagDatabase()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "TableLamp");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "preset_tags.json");

            InitializeDatabase();
        }

        public void InitializeDatabase()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    _inMemoryTree = _generator.Parse(json);
                    if (_inMemoryTree.Count > 0)
                    {
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to starter presets
            }

            // Seed starter preset JSON
            string starterJson = _generator.CreateStarterPresetJson();
            _inMemoryTree = _generator.Parse(starterJson);

            try
            {
                File.WriteAllText(_storagePath, starterJson);
            }
            catch (Exception)
            {
                // Non-fatal if filesystem write fails in restricted environments
            }
        }

        public List<PresetSearchResult> Search(int startPage, int endPage)
        {
            return _generator.SearchByPageRange(_inMemoryTree, startPage, endPage);
        }

        public Dictionary<string, PresetSubject> GetTree()
        {
            return _inMemoryTree;
        }

        public void SaveTree(Dictionary<string, PresetSubject> updatedTree)
        {
            _inMemoryTree = updatedTree ?? new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase);
            string json = _generator.GenerateJson(_inMemoryTree);

            try
            {
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception)
            {
                // In-memory cache continues to serve requests
            }

            PresetsChanged?.Invoke();
        }

        public void ReloadFromJson(string json)
        {
            _inMemoryTree = _generator.Parse(json);
            try
            {
                File.WriteAllText(_storagePath, _generator.GenerateJson(_inMemoryTree));
            }
            catch (Exception)
            {
            }

            PresetsChanged?.Invoke();
        }

        public string ExportJson()
        {
            return _generator.GenerateJson(_inMemoryTree);
        }
    }
}
