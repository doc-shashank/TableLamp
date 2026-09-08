using System;
using System.Collections.Generic;
using System.Linq;

namespace TableLamp.Models
{
    /// <summary>
    /// BasicSessionBundle is an extension of Bundle. It retains the core methods and properties of superclass Bundle.
    /// In v0.0.2 it manages study sessions, associated Tag metadata, and question navigation.
    /// </summary>
    public class BasicSessionBundle : Bundle
    {
        public string? SessionName { get; set; }
        private Tag? _tag;
        public Tag? Tag
        {
            get => (Tags != null && Tags.Count > 0) ? Tags[0] : _tag;
            set
            {
                _tag = value;
                if (value != null)
                {
                    if (Tags.Count == 0)
                        Tags.Add(value);
                    else
                        Tags[0] = value;
                }
            }
        }

        public List<Tag> Tags { get; set; } = new();

        /// <summary>
        /// Summary of all unique chapter names assigned to this bundle.
        /// </summary>
        public string ChaptersSummary
        {
            get
            {
                var chapters = Tags.Select(t => t.chapter_number > 0 ? $"Ch.{t.chapter_number}: {t.chapter_name}" : t.chapter_name)
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .Distinct()
                                   .ToList();
                return chapters.Count > 0 ? string.Join(", ", chapters) : (Tag?.chapter_name ?? "General");
            }
        }

        /// <summary>
        /// Summary of all unique topic names assigned to this bundle.
        /// </summary>
        public string TopicsSummary
        {
            get
            {
                var topics = Tags.Select(t => t.topic_name)
                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                 .Distinct()
                                 .ToList();
                return topics.Count > 0 ? string.Join(", ", topics) : (Tag?.topic_name ?? "");
            }
        }

        public int CurrentQuestionIndex { get; set; }

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SessionName))
                    return SessionName;

                if (Tag != null)
                {
                    string sub = Tag.subject_name ?? "General";
                    string ch = Tag.chapter_number > 0 ? $"Ch.{Tag.chapter_number}" : "";
                    string chName = Tag.chapter_name ?? "";
                    return $"{sub} {ch} {chName}".Trim();
                }

                return $"Session {creation_date:yyyy-MM-dd HH:mm}";
            }
        }

        public string FormattedDate => creation_date.ToLocalTime().ToString("MMM dd, yyyy");
        public string FormattedTime => creation_date.ToLocalTime().ToString("h:mm tt");
        public int QuestionCount => questions.Count;

        public int SessionType => Tag?.session_type ?? 0;
        public string SessionTypeName => SessionType == 1 ? "Class Session" : "Self Session";
        public bool IsClassSession => SessionType == 1;
        public bool IsSelfSession => SessionType == 0;

        public string SessionTypeGlyph => IsClassSession ? "\uE7BE" : "\uE77B";
        public string SessionTypeIconBackground => IsClassSession ? "#8A4FFF" : "#0067C0";
        public string SessionTypeTooltip => SessionTypeName;

        public BasicSessionBundle() : base()
        {
        }

        public BasicSessionBundle(IEnumerable<BaseQuestion>? initialQuestions, bool isCurated = false, DateTime? nextReviewDate = null, string? sessionName = null, Tag? tag = null, IEnumerable<Tag>? tags = null)
            : base(initialQuestions, isCurated, nextReviewDate)
        {
            SessionName = sessionName;
            if (tags != null)
            {
                Tags.AddRange(tags);
                _tag = Tags.FirstOrDefault() ?? tag;
            }
            else if (tag != null)
            {
                Tag = tag;
            }
        }

        public override string ToString()
        {
            return $"BasicSessionBundle: '{DisplayTitle}', {questions.Count} questions, Curated: {isCurated}";
        }
    }
}
