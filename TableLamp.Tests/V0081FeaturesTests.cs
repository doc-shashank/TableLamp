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
    public class V0081FeaturesTests
    {
        [Fact]
        public void Version_Is_AtLeast_0_0_8_1_AcrossAllCoreConfigurations()
        {
            Assert.True(AppVersionService.CompareVersions(AppVersionService.CurrentVersionString, "0.0.8.1") >= 0);
            Assert.True(AppVersionService.CurrentVersion >= new Version(0, 0, 8, 1));

            // AppSettings fallback
            Assert.True(AppVersionService.CompareVersions(new AppSettings().CuratedContentVersion, "0.0.8.1") >= 0);

            // Project root paths
            var testDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));

            var issPath = Path.Combine(projectDir, "installer", "TableLampSetup.iss");
            var csprojPath = Path.Combine(projectDir, "TableLamp.csproj");
            var ps1Path = Path.Combine(projectDir, "installer", "build_installer.ps1");

            if (File.Exists(issPath))
            {
                string issContent = File.ReadAllText(issPath);
                Assert.Contains("MyAppVersion", issContent);
            }

            if (File.Exists(csprojPath))
            {
                string csprojContent = File.ReadAllText(csprojPath);
                Assert.Contains("<Version>", csprojContent);
            }

            if (File.Exists(ps1Path))
            {
                string ps1Content = File.ReadAllText(ps1Path);
                Assert.Contains("$AppVersion", ps1Content);
            }
        }

        [Fact]
        public void SessionService_PremadeSampleSessionDetector_IdentifiesAllLegacySampleSessions()
        {
            var s1 = new BasicSessionBundle(null, false, null, "Kinematics Warmup", null) { Id = "sample-1" };
            var s2 = new BasicSessionBundle(null, false, null, "Cell Bio Lecture Notes", null) { Id = "sample-4" };
            var s3 = new BasicSessionBundle(null, false, null, "Chemical Bonding Lecture", null) { Id = "sample-2" };
            var s4 = new BasicSessionBundle(null, false, null, "Calculus Mastery", null) { Id = "sample-3" };
            var userSession = new BasicSessionBundle(null, false, null, "My Anatomy Notes", null) { Id = Guid.NewGuid().ToString() };

            Assert.True(SessionService.IsPremadeSampleSession(s1));
            Assert.True(SessionService.IsPremadeSampleSession(s2));
            Assert.True(SessionService.IsPremadeSampleSession(s3));
            Assert.True(SessionService.IsPremadeSampleSession(s4));
            Assert.False(SessionService.IsPremadeSampleSession(userSession));
            Assert.False(SessionService.IsPremadeSampleSession(null));
        }

        [Fact]
        public void SessionService_LoadSessions_FiltersSampleSessions_LeavingOnlyUserDefined()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "TableLamp_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string storagePath = Path.Combine(tempDir, "sessions.json");

            try
            {
                // Create a JSON file with 2 sample sessions and 1 user session
                string json = @"[
                    {
                        ""Id"": ""sample-1"",
                        ""SessionName"": ""Kinematics Warmup"",
                        ""CreationDate"": ""2026-09-01T10:00:00Z"",
                        ""IsCurated"": false,
                        ""Questions"": []
                    },
                    {
                        ""Id"": ""user-custom-session-123"",
                        ""SessionName"": ""User Created Session"",
                        ""CreationDate"": ""2026-09-10T12:00:00Z"",
                        ""IsCurated"": false,
                        ""Questions"": []
                    },
                    {
                        ""Id"": ""sample-2"",
                        ""SessionName"": ""Chemical Bonding Lecture"",
                        ""CreationDate"": ""2026-09-02T10:00:00Z"",
                        ""IsCurated"": false,
                        ""Questions"": []
                    }
                ]";
                File.WriteAllText(storagePath, json);

                var service = new SessionService(storagePath);
                var sessions = service.GetAllSessions();

                // Only the user created session should remain
                Assert.Single(sessions);
                Assert.Equal("user-custom-session-123", sessions[0].Id);
                Assert.Equal("User Created Session", sessions[0].SessionName);

                // Disk file should also have been cleansed
                string updatedJson = File.ReadAllText(storagePath);
                Assert.DoesNotContain("sample-1", updatedJson);
                Assert.DoesNotContain("sample-2", updatedJson);
                Assert.Contains("user-custom-session-123", updatedJson);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void SessionService_FreshInstall_InitializesWithZeroSessions()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "TableLamp_Fresh_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string storagePath = Path.Combine(tempDir, "sessions.json");

            try
            {
                // No file exists
                var service = new SessionService(storagePath);
                var sessions = service.GetAllSessions();

                // Must be completely empty - no premade sessions!
                Assert.Empty(sessions);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void DueStatusDescription_DoesNotReport_DueToday_WhenReviewIsTomorrow()
        {
            var today = DateTime.Today;

            // 1. Due tomorrow
            var tomorrow = new DateTimeOffset(today.AddDays(1).AddHours(14), DateTimeOffset.Now.Offset);
            var aggTomorrow = new SessionReviewAggregate
            {
                TotalQuestions = 5,
                DueQuestionsCount = 0,
                GraduatedQuestionsCount = 0,
                NextSessionDue = tomorrow
            };

            Assert.Equal("Due tomorrow", aggTomorrow.DueStatusDescription);

            // 2. Due in 3 days
            var in3Days = new DateTimeOffset(today.AddDays(3).AddHours(10), DateTimeOffset.Now.Offset);
            var agg3Days = new SessionReviewAggregate
            {
                TotalQuestions = 5,
                DueQuestionsCount = 0,
                GraduatedQuestionsCount = 0,
                NextSessionDue = in3Days
            };

            Assert.Equal("Due in 3 days", agg3Days.DueStatusDescription);

            // 3. Due today (later today, but 0 questions due now)
            var laterToday = new DateTimeOffset(today.AddHours(23), DateTimeOffset.Now.Offset);
            var aggToday = new SessionReviewAggregate
            {
                TotalQuestions = 5,
                DueQuestionsCount = 0,
                GraduatedQuestionsCount = 0,
                NextSessionDue = laterToday
            };

            Assert.Equal("Due today", aggToday.DueStatusDescription);

            // 4. Overdue
            var yesterday = new DateTimeOffset(today.AddDays(-1), DateTimeOffset.Now.Offset);
            var aggOverdue = new SessionReviewAggregate
            {
                TotalQuestions = 5,
                DueQuestionsCount = 0,
                GraduatedQuestionsCount = 0,
                NextSessionDue = yesterday
            };

            Assert.Equal("Overdue", aggOverdue.DueStatusDescription);

            // 5. Due now (has due questions)
            var aggDueNow = new SessionReviewAggregate
            {
                TotalQuestions = 5,
                DueQuestionsCount = 3,
                GraduatedQuestionsCount = 0,
                NextSessionDue = laterToday
            };

            Assert.Equal("3 cards due now", aggDueNow.DueStatusDescription);

            // 6. Completed
            var aggCompleted = new SessionReviewAggregate
            {
                TotalQuestions = 5,
                DueQuestionsCount = 0,
                GraduatedQuestionsCount = 5
            };

            Assert.Equal("Completed (All Graduated)", aggCompleted.DueStatusDescription);
        }

        [Fact]
        public void IdeRunConfiguration_FilesExist_WithExpectedOptions()
        {
            var testDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));

            var tasksJson = Path.Combine(projectDir, ".vscode", "tasks.json");

            Assert.True(File.Exists(tasksJson), "tasks.json must exist in .vscode");

            string tasksText = File.ReadAllText(tasksJson);
            Assert.Contains("Run Table Lamp (After Build)", tasksText);
            Assert.Contains("Run Table Lamp (Without Building)", tasksText);
        }

        [Fact]
        public async Task SpacedRepetitionManager_GetPendingReviews_ExcludesReviewsScheduledForTomorrow()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), "tablelamp_sr_" + Guid.NewGuid().ToString("N") + ".db");
            var db = new SpacedRepetitionDatabase(tempDb);
            var scheduler = new ContinuousAdaptiveScheduler();
            var manager = new SpacedRepetitionManager(db);

            try
            {
                var qid = Guid.NewGuid();
                var sid = Guid.NewGuid();

                // Create a card whose review was just completed today, so next due is tomorrow
                var now = DateTimeOffset.UtcNow;
                var completedState = new QuestionReviewState
                {
                    QuestionId = qid,
                    SessionId = sid,
                    IntervalDays = 2.0,
                    EaseFactor = 2.5,
                    LastReviewedAt = now,
                    NextDueDate = now.AddDays(2.0),
                    TotalRepetitions = 1,
                    IsGraduated = false
                };
                await db.SaveCardStateAsync(completedState);

                // Evaluation as of today:
                var aggregate = scheduler.EvaluateSession(sid, "Test Session", new[] { completedState }, now);
                Assert.Equal(0, aggregate.DueQuestionsCount);
                Assert.NotNull(aggregate.NextSessionDue);
                Assert.True(aggregate.NextSessionDue.Value.ToLocalTime().Date > DateTime.Today);

                // Due status description must NOT be "Due today"
                Assert.Equal("Due in 2 days", aggregate.DueStatusDescription);
            }
            finally
            {
                db.Dispose();
                if (File.Exists(tempDb))
                {
                    try { File.Delete(tempDb); } catch { }
                }
            }
        }

        [Fact]
        public void LauncherMode_BasicVsAdvancedWorkflow_ScopingRules()
        {
            Assert.Equal("Basic", LauncherMode.Basic);
            Assert.Equal("Advanced", LauncherMode.Advanced);
            Assert.Equal("Generator", LauncherMode.Generator);

            Assert.Equal("Basic", LauncherMode.Normalize(null));
            Assert.Equal("Basic", LauncherMode.Normalize(""));
            Assert.Equal("Basic", LauncherMode.Normalize("basic"));
            Assert.Equal("Advanced", LauncherMode.Normalize("advanced"));
            Assert.Equal("Generator", LauncherMode.Normalize("generator"));
        }
    }
}
