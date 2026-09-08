using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableLamp.Models;

namespace TableLamp.Services
{
    public class WorkspaceFileEntry
    {
        public string FilePath { get; set; } = "";
        public string FileName => Path.GetFileName(FilePath);
        public Dictionary<string, PresetSubject> Tree { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class DevToolsWorkspaceDatabase
    {
        private readonly PresetTagGenerator _generator = new();
        private readonly Dictionary<string, WorkspaceFileEntry> _files = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, WorkspaceFileEntry> Files => _files;

        public void Clear()
        {
            _files.Clear();
        }

        public void ScanWorkspace(string workspaceDir)
        {
            _files.Clear();
            if (string.IsNullOrWhiteSpace(workspaceDir) || !Directory.Exists(workspaceDir))
                return;

            string[] jsonFiles;
            try
            {
                jsonFiles = Directory.GetFiles(workspaceDir, "*.json", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            foreach (var file in jsonFiles)
            {
                IndexFile(file);
            }
        }

        public void IndexFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);
                var tree = _generator.Parse(json);
                _files[filePath] = new WorkspaceFileEntry
                {
                    FilePath = filePath,
                    Tree = tree
                };
            }
            catch
            {
                // Silently skip malformed json files
            }
        }

        public void UpdateFileBuffer(string filePath, Dictionary<string, PresetSubject> tree)
        {
            _files[filePath] = new WorkspaceFileEntry
            {
                FilePath = filePath,
                Tree = tree
            };
        }

        public void RemoveFile(string filePath)
        {
            _files.Remove(filePath);
        }

        /// <summary>
        /// Checks if a chapter with the given name or chapterNumber already exists under this subject in any OTHER file in the workspace.
        /// </summary>
        public bool IsDuplicateChapter(string? currentFilePath, string subjectIdentifier, string chapterName, int? chapterNumber, out string? conflictFile)
        {
            conflictFile = null;
            if (string.IsNullOrWhiteSpace(subjectIdentifier) || string.IsNullOrWhiteSpace(chapterName))
                return false;

            foreach (var entry in _files.Values)
            {
                if (!string.IsNullOrEmpty(currentFilePath) && string.Equals(entry.FilePath, currentFilePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var subj in entry.Tree.Values)
                {
                    bool sameSubject = string.Equals(subj.Key, subjectIdentifier, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(subj.short_name, subjectIdentifier, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(subj.full_name, subjectIdentifier, StringComparison.OrdinalIgnoreCase);

                    if (!sameSubject || subj.Chapters == null) continue;

                    foreach (var ch in subj.Chapters.Values)
                    {
                        bool sameName = string.Equals(ch.name, chapterName, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(ch.Key, chapterName, StringComparison.OrdinalIgnoreCase);

                        if (sameName)
                        {
                            conflictFile = entry.FilePath;
                            return true;
                        }
                        if (chapterNumber.HasValue && chapterNumber.Value > 0 && ch.ChapterNumber == chapterNumber.Value)
                        {
                            conflictFile = entry.FilePath;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a topic with the given name already exists under this subject and chapter in any OTHER file in the workspace.
        /// </summary>
        public bool IsDuplicateTopic(string? currentFilePath, string subjectIdentifier, string chapterIdentifier, string topicName, out string? conflictFile)
        {
            conflictFile = null;
            if (string.IsNullOrWhiteSpace(subjectIdentifier) || string.IsNullOrWhiteSpace(chapterIdentifier) || string.IsNullOrWhiteSpace(topicName))
                return false;

            foreach (var entry in _files.Values)
            {
                if (!string.IsNullOrEmpty(currentFilePath) && string.Equals(entry.FilePath, currentFilePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var subj in entry.Tree.Values)
                {
                    bool sameSubject = string.Equals(subj.Key, subjectIdentifier, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(subj.short_name, subjectIdentifier, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(subj.full_name, subjectIdentifier, StringComparison.OrdinalIgnoreCase);

                    if (!sameSubject || subj.Chapters == null) continue;

                    foreach (var ch in subj.Chapters.Values)
                    {
                        bool sameChapter = string.Equals(ch.name, chapterIdentifier, StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(ch.Key, chapterIdentifier, StringComparison.OrdinalIgnoreCase);

                        if (!sameChapter || ch.Topics == null)
                            continue;

                        foreach (var top in ch.Topics.Values)
                        {
                            if (string.Equals(top.name, topicName, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(top.Key, topicName, StringComparison.OrdinalIgnoreCase))
                            {
                                conflictFile = entry.FilePath;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
