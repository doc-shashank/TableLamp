using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TableLamp.Models;

namespace TableLamp.Services
{
    /// <summary>
    /// Service managing custom tags that do not match curated preset tags.
    /// Enforces the rule that no custom tag can use a Subject name that matches any curated preset subject.
    /// Persists custom tags in %LOCALAPPDATA%\TableLamp\custom_tags.json.
    /// </summary>
    public class CustomTagService
    {
        private static CustomTagService? _instance;
        public static CustomTagService Instance => _instance ??= new CustomTagService();

        private readonly List<Tag> _customTags = new();
        private readonly string _storagePath;
        private readonly object _lock = new();

        public CustomTagService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "TableLamp");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "custom_tags.json");

            LoadCustomTags();
        }

        private void LoadCustomTags()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_storagePath))
                    {
                        string json = File.ReadAllText(_storagePath);
                        var loaded = JsonSerializer.Deserialize<List<Tag>>(json);
                        if (loaded != null)
                        {
                            _customTags.Clear();
                            _customTags.AddRange(loaded);
                        }
                    }
                }
                catch (Exception)
                {
                    // Fallback to empty list
                }
            }
        }

        private void PersistCustomTags()
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_customTags, options);
                    File.WriteAllText(_storagePath, json);
                }
                catch (Exception)
                {
                    // Non-fatal
                }
            }
        }

        /// <summary>
        /// Checks whether the given subject name matches any curated preset subject in the database.
        /// </summary>
        public bool IsCuratedSubject(string? subjectName)
        {
            if (string.IsNullOrWhiteSpace(subjectName)) return false;
            string clean = subjectName.Trim();

            var tree = PresetTagDatabase.Instance.GetTree();
            if (tree == null || tree.Count == 0)
            {
                tree = new PresetTagGenerator().Parse(new PresetTagGenerator().CreateStarterPresetJson());
            }

            foreach (var kvp in tree)
            {
                if (string.Equals(kvp.Key.Trim(), clean, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(kvp.Value.short_name) &&
                    string.Equals(kvp.Value.short_name.Trim(), clean, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(kvp.Value.full_name) &&
                    string.Equals(kvp.Value.full_name.Trim(), clean, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the given tag fully matches an existing entry in the curated preset tag database.
        /// </summary>
        public bool MatchesCuratedPreset(Tag tag)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.subject_name)) return false;

            var tree = PresetTagDatabase.Instance.GetTree();
            foreach (var kvp in tree)
            {
                bool subjectMatches = string.Equals(kvp.Key, tag.subject_name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kvp.Value.short_name, tag.subject_name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kvp.Value.full_name, tag.subject_name, StringComparison.OrdinalIgnoreCase);

                if (!subjectMatches) continue;

                if (kvp.Value.Chapters == null) continue;

                foreach (var chKvp in kvp.Value.Chapters)
                {
                    bool chapterMatches = string.Equals(chKvp.Value.name, tag.chapter_name, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(chKvp.Key, tag.chapter_name, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(chKvp.Key, $"Chapter {tag.chapter_number}", StringComparison.OrdinalIgnoreCase);

                    if (chapterMatches)
                    {
                        if (!string.IsNullOrWhiteSpace(tag.topic_name) && chKvp.Value.Topics != null)
                        {
                            foreach (var topKvp in chKvp.Value.Topics)
                            {
                                if (string.Equals(topKvp.Value.name, tag.topic_name, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }
                        }
                        else if (string.IsNullOrWhiteSpace(tag.topic_name))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Validates a tag. If it's a custom tag (not matching curated presets), enforces that
        /// the Subject name does not match any curated preset subject. If valid, stores the custom tag.
        /// </summary>
        public bool ValidateAndSaveCustomTag(Tag tag, out string? errorMessage)
        {
            if (tag == null)
            {
                errorMessage = "Tag cannot be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(tag.subject_name))
            {
                errorMessage = "Subject name cannot be empty.";
                return false;
            }

            // If it matches a curated preset tag, it's valid as a curated preset.
            if (MatchesCuratedPreset(tag))
            {
                errorMessage = null;
                return true;
            }

            // It's a custom tag: check if subject matches any curated subject.
            if (IsCuratedSubject(tag.subject_name))
            {
                errorMessage = $"Subject '{tag.subject_name}' is a curated preset subject. Custom tags cannot have a Subject matching any curated preset subject.";
                return false;
            }

            // Valid custom tag: save it.
            SaveCustomTag(tag);
            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Saves a custom tag to the repository and persists it.
        /// </summary>
        public void SaveCustomTag(Tag tag)
        {
            if (tag == null) return;

            lock (_lock)
            {
                // Avoid exact duplicate custom tags
                bool exists = _customTags.Any(t =>
                    string.Equals(t.subject_name, tag.subject_name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.chapter_name, tag.chapter_name, StringComparison.OrdinalIgnoreCase) &&
                    t.chapter_number == tag.chapter_number &&
                    string.Equals(t.topic_name, tag.topic_name, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    _customTags.Add(tag);
                    PersistCustomTags();
                }
            }
        }

        /// <summary>
        /// Returns all stored custom tags.
        /// </summary>
        public IReadOnlyList<Tag> GetCustomTags()
        {
            lock (_lock)
            {
                return _customTags.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Clears custom tags (useful for testing).
        /// </summary>
        public void ClearCustomTags()
        {
            lock (_lock)
            {
                _customTags.Clear();
                PersistCustomTags();
            }
        }
    }
}
