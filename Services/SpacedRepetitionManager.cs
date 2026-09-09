using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TableLamp.Models;

namespace TableLamp.Services
{
    /// <summary>
    /// Central manager coordinating continuous adaptive scheduling, card persistence
    /// in the separate SQLite database, and session-level aggregation.
    /// </summary>
    public class SpacedRepetitionManager
    {
        private static SpacedRepetitionManager? _instance;
        public static SpacedRepetitionManager Instance => _instance ??= new SpacedRepetitionManager();

        private readonly ContinuousAdaptiveScheduler _scheduler = new();
        private readonly SpacedRepetitionDatabase _database;

        public ContinuousAdaptiveScheduler Scheduler => _scheduler;
        public SpacedRepetitionDatabase Database => _database;

        public event Action? ReviewStatesChanged;

        public SpacedRepetitionManager() : this(SpacedRepetitionDatabase.Instance)
        {
        }

        public SpacedRepetitionManager(SpacedRepetitionDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Gets or creates the QuestionReviewState for a given question in a session.
        /// </summary>
        public async Task<QuestionReviewState> GetOrCreateCardStateAsync(string questionIdStr, string sessionIdStr)
        {
            Guid qid = SpacedRepetitionDatabase.DeterministicGuid(questionIdStr);
            Guid sid = SpacedRepetitionDatabase.DeterministicGuid(sessionIdStr);
            return await _database.GetOrCreateCardStateAsync(qid, sid);
        }

        /// <summary>
        /// Synchronously processes recall rating (pure math) then asynchronously commits to separate SQLite database.
        /// </summary>
        public async Task<QuestionReviewState> ProcessRecallRatingAsync(
            string questionIdStr,
            string sessionIdStr,
            RecallRating rating,
            DateTimeOffset? reviewTime = null)
        {
            Guid qid = SpacedRepetitionDatabase.DeterministicGuid(questionIdStr);
            Guid sid = SpacedRepetitionDatabase.DeterministicGuid(sessionIdStr);

            var currentState = await _database.GetOrCreateCardStateAsync(qid, sid);
            double prevInterval = currentState.IntervalDays;
            double prevEase = currentState.EaseFactor;

            // Pure math evaluation
            var updatedState = _scheduler.ProcessReview(currentState, rating, reviewTime);

            // Asynchronously save updated state and record history in SQLite database
            await _database.SaveCardStateAsync(updatedState);
            await _database.RecordReviewHistoryAsync(updatedState, rating, prevInterval, prevEase);

            ReviewStatesChanged?.Invoke();
            return updatedState;
        }

        /// <summary>
        /// Evaluates the session review aggregate for a given BasicSessionBundle.
        /// </summary>
        public async Task<SessionReviewAggregate> EvaluateSessionAsync(BasicSessionBundle session, DateTimeOffset? asOf = null)
        {
            ArgumentNullException.ThrowIfNull(session);

            Guid sid = SpacedRepetitionDatabase.DeterministicGuid(session.Id);
            var cardStates = new List<QuestionReviewState>();

            foreach (var q in session.questions)
            {
                string qidStr = (q as SimpleQuestion)?.Id ?? q.ToString() ?? Guid.NewGuid().ToString();
                Guid qid = SpacedRepetitionDatabase.DeterministicGuid(qidStr);
                var state = await _database.GetOrCreateCardStateAsync(qid, sid);
                cardStates.Add(state);
            }

            return _scheduler.EvaluateSession(sid, session.DisplayTitle, cardStates, asOf);
        }

        /// <summary>
        /// Fetches all sessions that have cards due or upcoming reviews, sorted by next due date.
        /// </summary>
        public async Task<IReadOnlyList<SessionReviewAggregate>> GetPendingReviewsAsync(DateTimeOffset? asOf = null)
        {
            DateTimeOffset now = asOf ?? DateTimeOffset.UtcNow;
            var allSessions = SessionService.Instance.GetAllSessions();
            var aggregates = new List<SessionReviewAggregate>();

            foreach (var s in allSessions)
            {
                if (s.questions == null || s.questions.Count == 0) continue;
                var agg = await EvaluateSessionAsync(s, now);
                if (!agg.IsSessionCompleted && (agg.DueQuestionsCount > 0 || agg.NextSessionDue.HasValue))
                {
                    aggregates.Add(agg);
                }
            }

            return aggregates
                .OrderBy(a => a.DueQuestionsCount > 0 ? 0 : 1) // Due now first
                .ThenBy(a => a.NextSessionDue ?? DateTimeOffset.MaxValue)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Gets sessions with reviews due on a specific calendar date (or overdue as of that date).
        /// </summary>
        public async Task<IReadOnlyList<SessionReviewAggregate>> GetReviewsForDateAsync(DateTime targetDate)
        {
            var pending = await GetPendingReviewsAsync();
            return pending.Where(p =>
                (p.DueQuestionsCount > 0 && targetDate.Date >= DateTime.UtcNow.Date) ||
                (p.NextSessionDue.HasValue && p.NextSessionDue.Value.Date == targetDate.Date)
            ).ToList().AsReadOnly();
        }
    }
}
