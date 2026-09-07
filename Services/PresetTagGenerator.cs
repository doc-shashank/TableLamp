using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TableLamp.Models;

namespace TableLamp.Services
{
    /// <summary>
    /// Tree-generator class that reads formatted text/JSON to automatically generate
    /// hierarchical preset tag collections, performs page searches, and outputs presets JSON files.
    /// </summary>
    public class PresetTagGenerator
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Parses presets JSON string into a dictionary tree structure.
        /// </summary>
        public Dictionary<string, PresetSubject> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase);

            var dict = JsonSerializer.Deserialize<Dictionary<string, PresetSubject>>(json, JsonOptions);
            if (dict == null)
                return new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase);

            // Populate Key metadata across hierarchy
            foreach (var (subjKey, subj) in dict)
            {
                subj.Key = subjKey;
                if (subj.Chapters != null)
                {
                    foreach (var (chKey, ch) in subj.Chapters)
                    {
                        ch.Key = chKey;
                        if (ch.Topics != null)
                        {
                            foreach (var (topKey, top) in ch.Topics)
                            {
                                top.Key = topKey;
                            }
                        }
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// Reads presets from a file path and parses into tree structure.
        /// </summary>
        public Dictionary<string, PresetSubject> LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase);

            string json = File.ReadAllText(filePath);
            return Parse(json);
        }

        /// <summary>
        /// Serializes preset tree structure back into formatted presets JSON.
        /// </summary>
        public string GenerateJson(Dictionary<string, PresetSubject> subjects)
        {
            if (subjects == null) return "{}";
            return JsonSerializer.Serialize(subjects, JsonOptions);
        }

        /// <summary>
        /// Searches all subjects, chapters, and topics for page range overlap/matching.
        /// </summary>
        public List<PresetSearchResult> SearchByPageRange(Dictionary<string, PresetSubject> subjects, int startPage, int endPage)
        {
            var results = new List<PresetSearchResult>();
            if (subjects == null) return results;

            int queryStart = Math.Min(startPage, endPage);
            int queryEnd = Math.Max(startPage, endPage);

            foreach (var (subjKey, subj) in subjects)
            {
                if (subj.Chapters == null) continue;

                foreach (var (chKey, ch) in subj.Chapters)
                {
                    if (ch.Topics == null) continue;

                    foreach (var (topKey, top) in ch.Topics)
                    {
                        int tStart = top.StartPageNumber;
                        int tEnd = top.EndPageNumber;

                        if (tStart <= 0 && tEnd <= 0) continue;

                        // Check if requested range falls within or overlaps topic range
                        bool isMatch = (queryStart >= tStart && queryEnd <= tEnd) ||
                                       (Math.Max(queryStart, tStart) <= Math.Min(queryEnd, tEnd));

                        if (isMatch)
                        {
                            results.Add(new PresetSearchResult
                            {
                                SubjectKey = subjKey,
                                SubjectShortName = string.IsNullOrWhiteSpace(subj.short_name) ? subjKey : subj.short_name,
                                BookFullName = string.IsNullOrWhiteSpace(subj.full_name) ? subj.short_name : subj.full_name,
                                Edition = subj.edition,
                                ChapterNumber = ch.ChapterNumber,
                                ChapterName = ch.name,
                                TopicName = top.name,
                                StartPage = tStart,
                                EndPage = tEnd
                            });
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Generates the canonical starter preset JSON specified in Agent Instructions v0-0-3.
        /// </summary>
        public string CreateStarterPresetJson()
        {
            var starter = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
            {
                ["Subject1"] = new PresetSubject
                {
                    short_name = "Robins Physiology",
                    full_name = "Robins Pathologic Basis of Disease",
                    edition = "7th",
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Chapter 1"] = new PresetChapter
                        {
                            name = "Cell Injury",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["topic_1"] = new PresetTopic
                                {
                                    name = "Causes of Cell Injury",
                                    start_page = "23",
                                    end_page = "27"
                                },
                                ["topic_2"] = new PresetTopic
                                {
                                    name = "Mechanisms of Cell Injury",
                                    start_page = "26",
                                    end_page = "32"
                                }
                            }
                        },
                        ["Chapter 2"] = new PresetChapter
                        {
                            name = "Inflammation and Repair",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["topic_1"] = new PresetTopic
                                {
                                    name = "Acute Inflammation",
                                    start_page = "45",
                                    end_page = "55"
                                }
                            }
                        }
                    }
                },
                ["Subject2"] = new PresetSubject
                {
                    short_name = "Guyton Medical Physiology",
                    full_name = "Guyton and Hall Textbook of Medical Physiology",
                    edition = "14th",
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Chapter 9"] = new PresetChapter
                        {
                            name = "Cardiac Muscle; The Heart as a Pump",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["topic_1"] = new PresetTopic
                                {
                                    name = "Physiology of Cardiac Muscle",
                                    start_page = "105",
                                    end_page = "115"
                                }
                            }
                        }
                    }
                }
            };

            return GenerateJson(starter);
        }
    }
}
