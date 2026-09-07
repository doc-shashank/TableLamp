using System;

namespace TableLamp.Models
{
    /// <summary>
    /// Tag class holding categorization metadata for questions.
    /// In v0.0.3, extended with book_name and page_range.
    /// </summary>
    public class Tag
    {
        public const int SelfSession = 0;
        public const int ClassSession = 1;

        private int _sessionType = 0;

        public string? subject_name { get; set; }
        public int chapter_number { get; set; }
        public string? chapter_name { get; set; }
        public string? topic_name { get; set; }
        public int id { get; set; }
        public string? book_name { get; set; }
        public string? page_range { get; set; }

        /// <summary>
        /// 0 = self-session, 1 = class-session. Restricted to only these two values.
        /// </summary>
        public int session_type
        {
            get => _sessionType;
            set => _sessionType = (value == 1 ? 1 : 0);
        }

        // Idiomatic C# aliases
        public string? SubjectName { get => subject_name; set => subject_name = value; }
        public int ChapterNumber { get => chapter_number; set => chapter_number = value; }
        public string? ChapterName { get => chapter_name; set => chapter_name = value; }
        public string? TopicName { get => topic_name; set => topic_name = value; }
        public int Id { get => id; set => id = value; }
        public string? BookName { get => book_name; set => book_name = value; }
        public string? PageRange { get => page_range; set => page_range = value; }
        public int SessionType { get => session_type; set => session_type = value; }

        public Tag()
        {
        }

        public Tag(int id, string? subjectName = null, int chapterNumber = 0, string? chapterName = null, string? topicName = null, string? bookName = null, string? pageRange = null, int sessionType = 0)
        {
            this.id = id;
            this.subject_name = subjectName;
            this.chapter_number = chapterNumber;
            this.chapter_name = chapterName;
            this.topic_name = topicName;
            this.book_name = bookName;
            this.page_range = pageRange;
            this.session_type = sessionType;
        }

        public override string ToString()
        {
            string typeLabel = session_type == 1 ? "Class" : "Self";
            string bookInfo = !string.IsNullOrEmpty(book_name) ? $" | Book: {book_name}" : "";
            string pageInfo = !string.IsNullOrEmpty(page_range) ? $" (pp. {page_range})" : "";
            return $"Tag #{id} [{typeLabel}]: {subject_name} | Ch.{chapter_number} ({chapter_name}) | Topic: {topic_name}{bookInfo}{pageInfo}";
        }
    }
}
