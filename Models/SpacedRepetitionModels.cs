using System;

namespace TableLamp.Models
{
    /// <summary>
    /// User recall rating for a question during review.
    /// Scalable beyond the 3 standard ratings to accommodate 4-point (Anki) or 6-point (SuperMemo) scales.
    /// </summary>
    public enum RecallRating
    {
        // Core 3-point scale matching technical specification
        Hard = 0,
        Medium = 1,
        Easy = 2,

        // Scalable extensions for richer grading & custom algorithms
        Again = 10,     // Total blackout / failure (resets interval)
        Good = 11,      // Solid recall between Medium and Easy
        Mastered = 12   // Effortless, immediate recall
    }

    /// <summary>
    /// Card-level spaced repetition state tracking memory trace strength, retrievability,
    /// and scheduled interval. Spaced repetition metrics belong strictly to individual questions.
    /// </summary>
    public sealed class QuestionReviewState
    {
        public Guid QuestionId { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public double IntervalDays { get; set; } = 1.0;
        public double EaseFactor { get; set; } = 2.5;
        public DateTimeOffset LastReviewedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset NextDueDate { get; set; } = DateTimeOffset.UtcNow.AddDays(1.0);
        public bool IsGraduated { get; set; } = false;
        public int TotalRepetitions { get; set; } = 0;
        public int LapsesCount { get; set; } = 0;

        /// <summary>
        /// Calculates whether this card is currently due as of the specified time (defaults to now).
        /// </summary>
        public bool IsDue(DateTimeOffset? asOf = null)
        {
            if (IsGraduated) return false;
            DateTimeOffset now = asOf ?? DateTimeOffset.UtcNow;
            return NextDueDate <= now;
        }

        public override string ToString()
        {
            return $"QuestionReviewState [Q:{QuestionId:N}, S:{SessionId:N}, Interval:{IntervalDays:F1}d, Ease:{EaseFactor:F2}, Reps:{TotalRepetitions}, Due:{NextDueDate:yyyy-MM-dd HH:mm}, Graduated:{IsGraduated}]";
        }
    }

    /// <summary>
    /// Dynamic aggregation of spaced repetition state across all cards in a study session.
    /// </summary>
    public sealed class SessionReviewAggregate
    {
        public Guid SessionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTimeOffset? NextSessionDue { get; set; }
        public int TotalQuestions { get; set; }
        public int DueQuestionsCount { get; set; }
        public int GraduatedQuestionsCount { get; set; }
        public bool IsSessionCompleted => GraduatedQuestionsCount == TotalQuestions && TotalQuestions > 0;

        /// <summary>
        /// Human-friendly description of review due status.
        /// </summary>
        public string DueStatusDescription
        {
            get
            {
                if (IsSessionCompleted) return "Completed (All Graduated)";
                if (DueQuestionsCount > 0) return $"{DueQuestionsCount} card{(DueQuestionsCount == 1 ? "" : "s")} due now";
                if (!NextSessionDue.HasValue) return "No scheduled review";

                var localDue = NextSessionDue.Value.ToLocalTime().Date;
                var localToday = DateTime.Today;

                if (localDue < localToday) return "Overdue";
                if (localDue == localToday) return "Due today";
                if (localDue == localToday.AddDays(1)) return "Due tomorrow";
                int days = (localDue - localToday).Days;
                return $"Due in {days} days";
            }
        }
    }
}
