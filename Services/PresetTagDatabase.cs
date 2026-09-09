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

        private readonly object _dbLock = new();
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

        /// <summary>
        /// True if the curated database contains no subjects (user needs to download curated presets from GitHub).
        /// </summary>
        public bool IsEmpty => _inMemoryTree == null || _inMemoryTree.Count == 0;

        public void InitializeDatabase()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    _inMemoryTree = _generator.Parse(json);
                    return;
                }
            }
            catch (Exception)
            {
                // Fallback to empty tree
            }

            // In v0.0.7.3: All mock starter presets are removed.
            // Database initializes empty until curated tags are downloaded from GitHub.
            _inMemoryTree = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase);

            try
            {
                File.WriteAllText(_storagePath, "{}");
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
            lock (_dbLock)
            {
                _inMemoryTree = updatedTree ?? new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase);
                string json = _generator.GenerateJson(_inMemoryTree);

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        File.WriteAllText(_storagePath, json);
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }

                PresetsChanged?.Invoke();
            }
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

        public string StoragePath => _storagePath;
        public int SubjectCount => _inMemoryTree.Count;
        public int ChapterCount => _inMemoryTree.Values.Sum(s => s.Chapters?.Count ?? 0);
        public int TopicCount => _inMemoryTree.Values.Sum(s => s.Chapters?.Values.Sum(c => c.Topics?.Count ?? 0) ?? 0);

        /// <summary>
        /// Merges an external preset tree into the active database.
        /// </summary>
        public void MergeTree(Dictionary<string, PresetSubject> otherTree)
        {
            if (otherTree == null) return;

            foreach (var (subjKey, otherSubj) in otherTree)
            {
                if (!_inMemoryTree.TryGetValue(subjKey, out var existingSubj))
                {
                    _inMemoryTree[subjKey] = otherSubj;
                }
                else
                {
                    // Merge subject attributes if updated
                    if (!string.IsNullOrWhiteSpace(otherSubj.short_name)) existingSubj.short_name = otherSubj.short_name;
                    if (!string.IsNullOrWhiteSpace(otherSubj.full_name)) existingSubj.full_name = otherSubj.full_name;
                    if (!string.IsNullOrWhiteSpace(otherSubj.edition)) existingSubj.edition = otherSubj.edition;

                    if (otherSubj.Chapters != null)
                    {
                        existingSubj.Chapters ??= new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (chKey, otherCh) in otherSubj.Chapters)
                        {
                            if (!existingSubj.Chapters.TryGetValue(chKey, out var existingCh))
                            {
                                existingSubj.Chapters[chKey] = otherCh;
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(otherCh.name)) existingCh.name = otherCh.name;
                                if (otherCh.Topics != null)
                                {
                                    existingCh.Topics ??= new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var (topKey, otherTop) in otherCh.Topics)
                                    {
                                        existingCh.Topics[topKey] = otherTop;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            SaveTree(_inMemoryTree);
        }

        /// <summary>
        /// Imports multiple JSON files into the database.
        /// </summary>
        public (int success, int failed) ImportJsonFiles(IEnumerable<string> filePaths)
        {
            int success = 0;
            int failed = 0;

            if (filePaths == null) return (success, failed);

            foreach (var file in filePaths)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        string json = File.ReadAllText(file);
                        var tree = _generator.Parse(json);
                        if (tree.Count > 0)
                        {
                            MergeTree(tree);
                            success++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            return (success, failed);
        }

        /// <summary>
        /// Recursively scans a directory for all .json files and feeds them into the database.
        /// </summary>
        public (int totalFound, int success, int failed) ImportDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return (0, 0, 0);
            }

            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
            var (success, failed) = ImportJsonFiles(files);
            return (files.Length, success, failed);
        }

        /// <summary>
        /// Formats and resets the database to an empty state.
        /// </summary>
        public void FormatDatabase()
        {
            _inMemoryTree.Clear();
            SaveTree(_inMemoryTree);
        }

        /// <summary>
        /// Clears the curated presets database. (Starter presets removed in v0.0.7.3; only GitHub tags used).
        /// </summary>
        public void ResetToStarterPresets()
        {
            FormatDatabase();
        }
    }
}
