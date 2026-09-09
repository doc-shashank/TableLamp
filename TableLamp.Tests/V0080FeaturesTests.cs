using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0080FeaturesTests
    {
        private readonly ContinuousAdaptiveScheduler _scheduler = new();

        [Fact]
        public void SameDayPracticeGuard_PreservesIntervalAndEase_WhenUnderThreshold()
        {
            var t0 = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
            var dummyState = new QuestionReviewState
            {
                QuestionId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                IntervalDays = 4.0,
                EaseFactor = 2.5,
                LastReviewedAt = t0,
                NextDueDate = t0.AddDays(4.0),
                TotalRepetitions = 2,
                LapsesCount = 0
            };

            // Review after only 2 hours (0.083 days < 0.2 days threshold)
            var tNow = t0.AddHours(2.0);

            var easyResult = _scheduler.ProcessReview(dummyState, RecallRating.Easy, tNow);
            var mediumResult = _scheduler.ProcessReview(dummyState, RecallRating.Medium, tNow);
            var hardResult = _scheduler.ProcessReview(dummyState, RecallRating.Hard, tNow);

            foreach (var res in new[] { easyResult, mediumResult, hardResult })
            {
                Assert.Equal(4.0, res.IntervalDays);
                Assert.Equal(2.5, res.EaseFactor);
                Assert.Equal(tNow, res.LastReviewedAt);
                Assert.Equal(tNow.AddDays(4.0), res.NextDueDate);
                Assert.Equal(2, res.TotalRepetitions); // Not incremented
                Assert.Equal(0, res.LapsesCount);      // Not incremented
            }
        }

        [Fact]
        public void OverdueReview_CalculatesContinuousMath_ForEasyMediumHard()
        {
            var t0 = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
            var dummyState = new QuestionReviewState
            {
                QuestionId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                IntervalDays = 2.0,
                EaseFactor = 2.5,
                LastReviewedAt = t0,
                NextDueDate = t0.AddDays(2.0),
                TotalRepetitions = 1,
                LapsesCount = 0
            };

            // 5 days elapsed (overdue, currentInterval is 2.0)
            // effectiveElapsed = Min(5.0, 2.0 * 2.0) = 4.0
            var tNow = t0.AddDays(5.0);

            // 1. Easy: I_next = effectiveElapsed * E_prev = 4.0 * 2.5 = 10.0; E_next = 2.5 + 0.15 = 2.65
            var easy = _scheduler.ProcessReview(dummyState, RecallRating.Easy, tNow);
            Assert.Equal(10.0, easy.IntervalDays, 2);
            Assert.Equal(2.65, easy.EaseFactor, 2);
            Assert.Equal(2, easy.TotalRepetitions);
            Assert.Equal(0, easy.LapsesCount);
            Assert.False(easy.IsGraduated);

            // 2. Medium: I_next = I_prev * 1.10 = 2.0 * 1.10 = 2.20; E_next = 2.5 - 0.15 = 2.35
            var medium = _scheduler.ProcessReview(dummyState, RecallRating.Medium, tNow);
            Assert.Equal(2.20, medium.IntervalDays, 2);
            Assert.Equal(2.35, medium.EaseFactor, 2);
            Assert.Equal(2, medium.TotalRepetitions);
            Assert.Equal(0, medium.LapsesCount);

            // 3. Hard: I_next = Max(1.0, 2.0 * 0.20) = 1.0; E_next = 2.5 - 0.30 = 2.20; lapses++
            var hard = _scheduler.ProcessReview(dummyState, RecallRating.Hard, tNow);
            Assert.Equal(1.0, hard.IntervalDays, 2);
            Assert.Equal(2.20, hard.EaseFactor, 2);
            Assert.Equal(2, hard.TotalRepetitions);
            Assert.Equal(1, hard.LapsesCount);
        }

        [Fact]
        public void EarlyReview_CalculatesContinuousMath_ForEasyMediumHard()
        {
            var t0 = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
            var dummyState = new QuestionReviewState
            {
                QuestionId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                IntervalDays = 10.0,
                EaseFactor = 2.5,
                LastReviewedAt = t0,
                NextDueDate = t0.AddDays(10.0),
                TotalRepetitions = 2,
                LapsesCount = 0
            };

            // Review after 2 days (early: elapsedDays = 2.0 < currentInterval = 10.0)
            var tNow = t0.AddDays(2.0);

            // 1. Easy: I_next = I_prev + (elapsedDays * 0.50) = 10.0 + 1.0 = 11.0; E_next = 2.5 + 0.05 = 2.55
            var easy = _scheduler.ProcessReview(dummyState, RecallRating.Easy, tNow);
            Assert.Equal(11.0, easy.IntervalDays, 2);
            Assert.Equal(2.55, easy.EaseFactor, 2);

            // 2. Medium: I_next = Max(1.0, elapsedDays * 0.90) = Max(1.0, 1.8) = 1.80; E_next = 2.5 - 0.10 = 2.40
            var medium = _scheduler.ProcessReview(dummyState, RecallRating.Medium, tNow);
            Assert.Equal(1.80, medium.IntervalDays, 2);
            Assert.Equal(2.40, medium.EaseFactor, 2);

            // 3. Hard: I_next = MinIntervalDays = 1.0; E_next = 2.5 - 0.30 = 2.20; lapses++
            var hard = _scheduler.ProcessReview(dummyState, RecallRating.Hard, tNow);
            Assert.Equal(1.0, hard.IntervalDays, 2);
            Assert.Equal(2.20, hard.EaseFactor, 2);
            Assert.Equal(1, hard.LapsesCount);
        }

        [Fact]
        public void ClampingBounds_EnforceEaseAndIntervalLimits()
        {
            var t0 = DateTimeOffset.UtcNow.AddDays(-10);
            var lowEaseState = new QuestionReviewState
            {
                IntervalDays = 1.0,
                EaseFactor = 1.35,
                LastReviewedAt = t0,
                TotalRepetitions = 1
            };

            // Hard rating subtracts 0.30 -> 1.05, should be clamped to MinEase = 1.3
            var clampedLow = _scheduler.ProcessReview(lowEaseState, RecallRating.Hard);
            Assert.Equal(1.3, clampedLow.EaseFactor, 2);
            Assert.Equal(1.0, clampedLow.IntervalDays, 2); // Cannot go below 1.0

            var highEaseState = new QuestionReviewState
            {
                IntervalDays = 200.0,
                EaseFactor = 3.45,
                LastReviewedAt = t0.AddDays(-200),
                TotalRepetitions = 5
            };

            // Easy rating adds 0.15 -> 3.60, should be clamped to MaxEase = 3.5
            var clampedHigh = _scheduler.ProcessReview(highEaseState, RecallRating.Easy);
            Assert.Equal(3.5, clampedHigh.EaseFactor, 2);
            Assert.True(clampedHigh.IntervalDays <= 365.0); // MaxIntervalDays
        }

        [Fact]
        public void MaturityCeiling_TriggersGraduation_AtSixtyDays()
        {
            var state = new QuestionReviewState
            {
                IntervalDays = 25.0,
                EaseFactor = 2.5,
                LastReviewedAt = DateTimeOffset.UtcNow.AddDays(-25),
                TotalRepetitions = 3
            };

            // 25.0 * 2.5 = 62.5 days (>= 60.0 days maturity ceiling)
            var reviewed = _scheduler.ProcessReview(state, RecallRating.Easy);
            Assert.True(reviewed.IntervalDays >= 60.0);
            Assert.True(reviewed.IsGraduated);
        }

        [Fact]
        public void ScalableRatings_SupportExtensionRatingsAndScores()
        {
            var t0 = DateTimeOffset.UtcNow.AddDays(-5);
            var dummyState = new QuestionReviewState
            {
                IntervalDays = 2.0,
                EaseFactor = 2.5,
                LastReviewedAt = t0,
                TotalRepetitions = 1
            };

            // Test RecallRating.Again (blackout / full reset)
            var again = _scheduler.ProcessReview(dummyState, RecallRating.Again);
            Assert.Equal(1.0, again.IntervalDays, 2);
            Assert.Equal(1, again.LapsesCount);

            // Test RecallRating.Good
            var good = _scheduler.ProcessReview(dummyState, RecallRating.Good);
            Assert.True(good.IntervalDays > dummyState.IntervalDays);

            // Test RecallRating.Mastered
            var mastered = _scheduler.ProcessReview(dummyState, RecallRating.Mastered);
            Assert.True(mastered.IntervalDays > good.IntervalDays);

            // Test numeric score overload: score 0.95 -> maps to Easy/Mastered
            var highNumeric = _scheduler.ProcessReview(dummyState, 0.95);
            Assert.True(highNumeric.IntervalDays > dummyState.IntervalDays);

            // Test numeric score overload: score 0.10 -> maps to Hard
            var lowNumeric = _scheduler.ProcessReview(dummyState, 0.10);
            Assert.Equal(1.0, lowNumeric.IntervalDays, 2);
        }

        [Fact]
        public void CustomRatingHandler_CanBeRegisteredAndExecuted()
        {
            ContinuousAdaptiveScheduler.RegisterRatingHandler(999, (state, elapsed, isOverdue, eff) =>
            {
                return (NewInterval: 42.0, NewEase: 3.14, LapsesDelta: 0);
            });

            var dummy = new QuestionReviewState
            {
                LastReviewedAt = DateTimeOffset.UtcNow.AddDays(-1)
            };

            var customResult = _scheduler.ProcessReview(dummy, (RecallRating)999);
            Assert.Equal(42.0, customResult.IntervalDays);
            Assert.Equal(3.14, customResult.EaseFactor);
        }

        [Fact]
        public void EvaluateSession_CalculatesDynamicAggregate_FromActiveCards()
        {
            var sid = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            var q1 = new QuestionReviewState
            {
                QuestionId = Guid.NewGuid(),
                SessionId = sid,
                IsGraduated = true,
                NextDueDate = now.AddDays(-10) // Graduated card ignored for next session due
            };

            var q2 = new QuestionReviewState
            {
                QuestionId = Guid.NewGuid(),
                SessionId = sid,
                IsGraduated = false,
                NextDueDate = now.AddDays(1.5) // Active card due in 1.5 days
            };

            var q3 = new QuestionReviewState
            {
                QuestionId = Guid.NewGuid(),
                SessionId = sid,
                IsGraduated = false,
                NextDueDate = now.AddDays(4.0) // Active card due in 4 days
            };

            var aggregate = _scheduler.EvaluateSession(sid, "Biology Session", new[] { q1, q2, q3 }, now);

            Assert.Equal(sid, aggregate.SessionId);
            Assert.Equal("Biology Session", aggregate.Title);
            Assert.Equal(3, aggregate.TotalQuestions);
            Assert.Equal(1, aggregate.GraduatedQuestionsCount);
            Assert.Equal(0, aggregate.DueQuestionsCount); // Neither q2 nor q3 are overdue relative to now
            Assert.Equal(q2.NextDueDate, aggregate.NextSessionDue); // Minimum NextDueDate among active cards
            Assert.False(aggregate.IsSessionCompleted);
        }

        [Fact]
        public async Task SpacedRepetitionDatabase_SeparateDatabase_StoresAndRetrievesState()
        {
            // Use in-memory SQLite database
            using var db = new SpacedRepetitionDatabase("Data Source=:memory:");

            var qid = Guid.NewGuid();
            var sid = Guid.NewGuid();

            var created = await db.GetOrCreateCardStateAsync(qid, sid);
            Assert.Equal(qid, created.QuestionId);
            Assert.Equal(sid, created.SessionId);
            Assert.Equal(1.0, created.IntervalDays);
            Assert.Equal(2.5, created.EaseFactor);

            // Modify state and save
            created.IntervalDays = 7.5;
            created.EaseFactor = 2.8;
            created.TotalRepetitions = 3;
            created.LapsesCount = 1;
            created.IsGraduated = false;

            await db.SaveCardStateAsync(created);

            var retrieved = await db.GetCardStateAsync(qid);
            Assert.NotNull(retrieved);
            Assert.Equal(7.5, retrieved!.IntervalDays);
            Assert.Equal(2.8, retrieved.EaseFactor);
            Assert.Equal(3, retrieved.TotalRepetitions);
            Assert.Equal(1, retrieved.LapsesCount);

            // Test session states retrieval
            var sessionStates = await db.GetStatesForSessionAsync(sid);
            Assert.Single(sessionStates);
            Assert.Equal(qid, sessionStates[0].QuestionId);

            // Test review history recording
            await db.RecordReviewHistoryAsync(created, RecallRating.Easy, 2.0, 2.5);
        }

        [Fact]
        public void VersionConstants_AreDynamicallyResolvedToV0080()
        {
            Assert.Equal("0.0.8.0", AppUpdateService.CurrentVersionString);
            Assert.Equal(new Version(0, 0, 8, 0), AppUpdateService.CurrentVersion);
            Assert.Equal("v0.0.8.0", new AppSettings().CuratedContentVersion);
        }

        [Fact]
        public void ProjectAndInstallerConfigurations_MatchV0080()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
            string issPath = Path.Combine(solutionRoot, "installer", "TableLampSetup.iss");
            string csprojPath = Path.Combine(solutionRoot, "TableLamp.csproj");
            string buildPs1Path = Path.Combine(solutionRoot, "installer", "build_installer.ps1");

            Assert.True(File.Exists(issPath));
            string issContent = File.ReadAllText(issPath);
            Assert.Contains("MyAppVersion \"0.0.8.0\"", issContent);

            Assert.True(File.Exists(csprojPath));
            string csprojContent = File.ReadAllText(csprojPath);
            Assert.Contains("<Version>0.0.8.0</Version>", csprojContent);
            Assert.Contains("<AssemblyVersion>0.0.8.0</AssemblyVersion>", csprojContent);
            Assert.Contains("<FileVersion>0.0.8.0</FileVersion>", csprojContent);

            Assert.True(File.Exists(buildPs1Path));
            string ps1Content = File.ReadAllText(buildPs1Path);
            Assert.Contains("0.0.8.0", ps1Content);
        }
    }
}
