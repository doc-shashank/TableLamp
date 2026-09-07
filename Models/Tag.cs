using System;

namespace TableLamp.Models
{
    /// <summary>
    /// Tag class holding categorization metadata for questions.
    /// </summary>
    public class Tag
    {
        public string? subject_name { get; set; }
        public int chapter_number { get; set; }
        public string? chapter_name { get; set; }
        public string? topic_name { get; set; }
        public int id { get; set; }

        // Idiomatic C# aliases
        public string? SubjectName { get => subject_name; set => subject_name = value; }
        public int ChapterNumber { get => chapter_number; set => chapter_number = value; }
        public string? ChapterName { get => chapter_name; set => chapter_name = value; }
        public string? TopicName { get => topic_name; set => topic_name = value; }
        public int Id { get => id; set => id = value; }

        public Tag()
        {
        }

        public Tag(int id, string? subjectName = null, int chapterNumber = 0, string? chapterName = null, string? topicName = null)
        {
            this.id = id;
            this.subject_name = subjectName;
            this.chapter_number = chapterNumber;
            this.chapter_name = chapterName;
            this.topic_name = topicName;
        }

        public override string ToString()
        {
            return $"Tag #{id}: {subject_name} | Ch.{chapter_number} ({chapter_name}) | Topic: {topic_name}";
        }
    }
}
