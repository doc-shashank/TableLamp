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
        public Tag? Tag { get; set; }
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

        public BasicSessionBundle() : base()
        {
        }

        public BasicSessionBundle(IEnumerable<BaseQuestion>? initialQuestions, bool isCurated = false, DateTime? nextReviewDate = null, string? sessionName = null, Tag? tag = null)
            : base(initialQuestions, isCurated, nextReviewDate)
        {
            SessionName = sessionName;
            Tag = tag;
        }

        public override string ToString()
        {
            return $"BasicSessionBundle: '{DisplayTitle}', {questions.Count} questions, Curated: {isCurated}";
        }
    }
}
