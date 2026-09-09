using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TableLamp.Models;

namespace TableLamp.Services
{
    public interface ISpacedRepetitionService
    {
        QuestionReviewState ProcessReview(QuestionReviewState state, RecallRating rating, DateTimeOffset? reviewTime = null);
        QuestionReviewState ProcessReview(QuestionReviewState state, double score, DateTimeOffset? reviewTime = null);
        SessionReviewAggregate EvaluateSession(Guid sessionId, string title, IEnumerable<QuestionReviewState> questions, DateTimeOffset? asOfTime = null);
    }

    /// <summary>
    /// Continuous Adaptive Spaced Repetition Scheduler implementing the memory curve algorithm
    /// without fixed-step indexes, featuring asymmetric elapsed-time adaptation and maturity ceilings.
    /// </summary>
    public sealed class ContinuousAdaptiveScheduler : ISpacedRepetitionService
    {
        public const double MinIntervalDays = 1.0;
        public const double MaxIntervalDays = 365.0;
        public const double MaturityCeilingDays = 60.0;
        public const double MinEase = 1.3;
        public const double MaxEase = 3.5;
        public const double DefaultInitialEase = 2.5;
        public const double MinPracticeThresholdDays = 0.2; // ~4.8 hours (anti-spam guard)
        public const double OverdueBonusCapMultiplier = 2.0;

        // Scalable rating evaluation handler registry: allows developers to register custom ratings
        public delegate (double NewInterval, double NewEase, int LapsesDelta) CustomRatingHandler(
            QuestionReviewState state,
            double elapsedDays,
            bool isOverdue,
            double effectiveElapsed);

        private static readonly ConcurrentDictionary<int, CustomRatingHandler> CustomHandlers = new();

        /// <summary>
        /// Registers or overrides an evaluation handler for a custom integer/enum rating value.
        /// </summary>
        public static void RegisterRatingHandler(int ratingValue, CustomRatingHandler handler)
        {
            CustomHandlers[ratingValue] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public QuestionReviewState ProcessReview(
            QuestionReviewState state,
            RecallRating rating,
            DateTimeOffset? reviewTime = null)
        {
            ArgumentNullException.ThrowIfNull(state);

            DateTimeOffset now = reviewTime ?? DateTimeOffset.UtcNow;
            double elapsedDays = Math.Max(0.0, (now - state.LastReviewedAt).TotalDays);

            // 1. Guard against sub-threshold review spamming (< 0.2 days = ~4.8 hours)
            if (elapsedDays < MinPracticeThresholdDays && state.TotalRepetitions > 0)
            {
                // Unpenalized practice: preserve interval and ease, refresh due date relative to now
                return new QuestionReviewState
                {
                    QuestionId = state.QuestionId,
                    SessionId = state.SessionId,
                    IntervalDays = state.IntervalDays,
                    EaseFactor = state.EaseFactor,
                    LastReviewedAt = now,
                    NextDueDate = now.AddDays(state.IntervalDays),
                    IsGraduated = state.IsGraduated,
                    TotalRepetitions = state.TotalRepetitions,
                    LapsesCount = state.LapsesCount
                };
            }

            double currentInterval = Math.Max(MinIntervalDays, state.IntervalDays);
            double currentEase = Math.Clamp(state.EaseFactor, MinEase, MaxEase);
            double newInterval;
            double newEase;
            int lapses = state.LapsesCount;

            bool isOverdue = elapsedDays >= currentInterval;
            double effectiveElapsed = isOverdue
                ? Math.Min(elapsedDays, currentInterval * OverdueBonusCapMultiplier)
                : elapsedDays;

            // Check if a custom rating handler is registered for this rating value
            if (CustomHandlers.TryGetValue((int)rating, out var customHandler))
            {
                var result = customHandler(state, elapsedDays, isOverdue, effectiveElapsed);
                newInterval = result.NewInterval;
                newEase = result.NewEase;
                lapses += result.LapsesDelta;
            }
            else if (isOverdue)
            {
                switch (rating)
                {
                    case RecallRating.Mastered:
                        newInterval = effectiveElapsed * (currentEase + 0.20);
                        newEase = currentEase + 0.25;
                        break;

                    case RecallRating.Easy:
                        newInterval = effectiveElapsed * currentEase;
                        newEase = currentEase + 0.15;
                        break;

                    case RecallRating.Good:
                        newInterval = Math.Max(currentInterval * 1.25, effectiveElapsed * (currentEase * 0.85));
                        newEase = currentEase + 0.05;
                        break;

                    case RecallRating.Medium:
                        newInterval = currentInterval * 1.10;
                        newEase = currentEase - 0.15;
                        break;

                    case RecallRating.Again:
                    case RecallRating.Hard:
                    default:
                        newInterval = Math.Max(MinIntervalDays, currentInterval * 0.20);
                        newEase = currentEase - 0.30;
                        lapses++;
                        break;
                }
            }
            else // Early Review (elapsedDays < currentInterval)
            {
                switch (rating)
                {
                    case RecallRating.Mastered:
                        newInterval = currentInterval + (elapsedDays * 0.75);
                        newEase = currentEase + 0.10;
                        break;

                    case RecallRating.Easy:
                        newInterval = currentInterval + (elapsedDays * 0.50);
                        newEase = currentEase + 0.05;
                        break;

                    case RecallRating.Good:
                        newInterval = Math.Max(MinIntervalDays, (currentInterval * 0.50) + (elapsedDays * 0.40));
                        newEase = currentEase;
                        break;

                    case RecallRating.Medium:
                        newInterval = Math.Max(MinIntervalDays, elapsedDays * 0.90);
                        newEase = currentEase - 0.10;
                        break;

                    case RecallRating.Again:
                    case RecallRating.Hard:
                    default:
                        newInterval = MinIntervalDays;
                        newEase = currentEase - 0.30;
                        lapses++;
                        break;
                }
            }

            // Clamping & Normalization
            newEase = Math.Clamp(newEase, MinEase, MaxEase);
            newInterval = Math.Clamp(newInterval, MinIntervalDays, MaxIntervalDays);
            bool isGraduated = newInterval >= MaturityCeilingDays;

            return new QuestionReviewState
            {
                QuestionId = state.QuestionId,
                SessionId = state.SessionId,
                IntervalDays = newInterval,
                EaseFactor = newEase,
                LastReviewedAt = now,
                NextDueDate = now.AddDays(newInterval),
                IsGraduated = isGraduated,
                TotalRepetitions = state.TotalRepetitions + 1,
                LapsesCount = lapses
            };
        }

        /// <summary>
        /// Scalable continuous score-based review process (score in [0.0, 1.0]).
        /// Allows slider/percentage based rating systems to directly interface with the scheduler.
        /// </summary>
        public QuestionReviewState ProcessReview(
            QuestionReviewState state,
            double score,
            DateTimeOffset? reviewTime = null)
        {
            double clampedScore = Math.Clamp(score, 0.0, 1.0);
            RecallRating mappedRating = clampedScore switch
            {
                < 0.30 => RecallRating.Hard,
                < 0.60 => RecallRating.Medium,
                < 0.85 => RecallRating.Good,
                < 0.95 => RecallRating.Easy,
                _ => RecallRating.Mastered
            };

            return ProcessReview(state, mappedRating, reviewTime);
        }

        public SessionReviewAggregate EvaluateSession(
            Guid sessionId,
            string title,
            IEnumerable<QuestionReviewState> questions,
            DateTimeOffset? asOfTime = null)
        {
            DateTimeOffset now = asOfTime ?? DateTimeOffset.UtcNow;
            var qList = questions?.ToList() ?? new List<QuestionReviewState>();

            int total = qList.Count;
            int graduated = qList.Count(q => q.IsGraduated);
            int due = qList.Count(q => !q.IsGraduated && q.NextDueDate <= now);

            var activeQuestions = qList.Where(q => !q.IsGraduated).ToList();
            DateTimeOffset? nextDue = activeQuestions.Count != 0
                ? activeQuestions.Min(q => q.NextDueDate)
                : null;

            return new SessionReviewAggregate
            {
                SessionId = sessionId,
                Title = title ?? string.Empty,
                TotalQuestions = total,
                DueQuestionsCount = due,
                GraduatedQuestionsCount = graduated,
                NextSessionDue = nextDue
            };
        }
    }
}
