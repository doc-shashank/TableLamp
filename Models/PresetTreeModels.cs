using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TableLamp.Models
{
    public class PresetSubject
    {
        [JsonIgnore]
        public string Key { get; set; } = "";

        [JsonPropertyName("short_name")]
        public string short_name { get; set; } = "";

        [JsonPropertyName("full_name")]
        public string full_name { get; set; } = "";

        [JsonPropertyName("edition")]
        public string edition { get; set; } = "";

        [JsonPropertyName("Chapters")]
        public Dictionary<string, PresetChapter> Chapters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class PresetChapter
    {
        [JsonIgnore]
        public string Key { get; set; } = "";

        [JsonPropertyName("name")]
        public string name { get; set; } = "";

        [JsonPropertyName("Topics")]
        public Dictionary<string, PresetTopic> Topics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonIgnore]
        public int ChapterNumber
        {
            get
            {
                var match = Regex.Match(Key, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int num))
                {
                    return num;
                }
                var nameMatch = Regex.Match(name, @"\d+");
                if (nameMatch.Success && int.TryParse(nameMatch.Value, out int nameNum))
                {
                    return nameNum;
                }
                return 1;
            }
        }
    }

    public class PresetTopic
    {
        [JsonIgnore]
        public string Key { get; set; } = "";

        [JsonPropertyName("name")]
        public string name { get; set; } = "";

        [JsonPropertyName("start_page")]
        public string start_page { get; set; } = "";

        [JsonPropertyName("end_page")]
        public string end_page { get; set; } = "";

        [JsonIgnore]
        public int StartPageNumber => int.TryParse(start_page, out int s) ? s : 0;

        [JsonIgnore]
        public int EndPageNumber => int.TryParse(end_page, out int e) ? e : 0;
    }

    /// <summary>
    /// Represents a resolved match when querying presets by page numbers.
    /// </summary>
    public class PresetSearchResult
    {
        public string SubjectKey { get; set; } = "";
        public string SubjectShortName { get; set; } = "";
        public string BookFullName { get; set; } = "";
        public string Edition { get; set; } = "";
        public int ChapterNumber { get; set; } = 1;
        public string ChapterName { get; set; } = "";
        public string TopicName { get; set; } = "";
        public int StartPage { get; set; }
        public int EndPage { get; set; }

        public string PageRangeString => $"{StartPage}-{EndPage}";
        public string DisplayText => $"{SubjectShortName} • Ch.{ChapterNumber} {ChapterName} • {TopicName} (pp. {StartPage}-{EndPage})";

        public override string ToString() => DisplayText;
    }
}
