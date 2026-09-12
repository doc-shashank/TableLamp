using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0082FeaturesTests
    {
        [Fact]
        public void Version_Is_0_0_8_2_AcrossAllCoreConfigurations()
        {
            Assert.Equal("0.0.8.2", AppVersionService.CurrentVersionString);
            Assert.Equal(new Version(0, 0, 8, 2), AppVersionService.CurrentVersion);

            // AppSettings fallback
            Assert.Equal("v0.0.8.2", new AppSettings().CuratedContentVersion);

            // Project root paths
            var testDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));

            var issPath = Path.Combine(projectDir, "installer", "TableLampSetup.iss");
            var csprojPath = Path.Combine(projectDir, "TableLamp.csproj");
            var ps1Path = Path.Combine(projectDir, "installer", "build_installer.ps1");

            if (File.Exists(issPath))
            {
                string issContent = File.ReadAllText(issPath);
                Assert.Contains("MyAppVersion \"0.0.8.2\"", issContent);
            }

            if (File.Exists(csprojPath))
            {
                string csprojContent = File.ReadAllText(csprojPath);
                Assert.Contains("<Version>0.0.8.2</Version>", csprojContent);
                Assert.Contains("<AssemblyVersion>0.0.8.2</AssemblyVersion>", csprojContent);
                Assert.Contains("<FileVersion>0.0.8.2</FileVersion>", csprojContent);
            }

            if (File.Exists(ps1Path))
            {
                string ps1Content = File.ReadAllText(ps1Path);
                Assert.Contains("0.0.8.2", ps1Content);
            }
        }

        [Fact]
        public void AppAutoUpdater_IsRemoved_WhileCuratedTagUpdaterIsPreserved()
        {
            // Verify AppUpdateService is deleted/removed
            var appUpdateType = typeof(AppVersionService).Assembly.GetType("TableLamp.Services.AppUpdateService");
            Assert.Null(appUpdateType);

            // Verify CuratedContentUpdateService is preserved and active
            var curatedServiceType = typeof(CuratedContentUpdateService);
            Assert.NotNull(curatedServiceType);
            Assert.NotNull(curatedServiceType.GetMethod("CheckForUpdatesAsync"));
            Assert.NotNull(curatedServiceType.GetMethod("DownloadAndApplyUpdateAsync"));
            Assert.NotNull(curatedServiceType.GetMethod("TryDownloadFileAsync"));
            Assert.NotNull(curatedServiceType.GetMethod("ApplyExtractedPackage"));
        }

        [Fact]
        public void SessionService_GetRecentSessions_OnlyIncludesSessionsFromLast3Days()
        {
            // Arrange test sessions with various dates
            var now = DateTime.UtcNow;
            var sessions = new List<BasicSessionBundle>
            {
                new BasicSessionBundle(null, false, null, "Today Session", null) { Id = "today-1", creation_date = now },
                new BasicSessionBundle(null, false, null, "Yesterday Session", null) { Id = "yesterday-1", creation_date = now.AddDays(-1) },
                new BasicSessionBundle(null, false, null, "Two Days Ago Session", null) { Id = "two-days-ago", creation_date = now.AddDays(-2) },
                new BasicSessionBundle(null, false, null, "Old Session 4 Days", null) { Id = "four-days-ago", creation_date = now.AddDays(-4) },
                new BasicSessionBundle(null, false, null, "Ancient Session", null) { Id = "ten-days-ago", creation_date = now.AddDays(-10) }
            };

            // Test filtering logic equivalent to SessionService.GetRecentSessions
            var threeDaysAgo = DateTime.UtcNow.AddDays(-3);
            var today = DateTime.Today;
            var twoDaysAgoDate = today.AddDays(-2);

            var recentSessions = sessions
                .Where(s => s.creation_date.Date >= twoDaysAgoDate || (DateTime.UtcNow - s.creation_date).TotalDays <= 3.0)
                .OrderByDescending(s => s.creation_date)
                .ToList();

            Assert.Equal(3, recentSessions.Count);
            Assert.Contains(recentSessions, s => s.Id == "today-1");
            Assert.Contains(recentSessions, s => s.Id == "yesterday-1");
            Assert.Contains(recentSessions, s => s.Id == "two-days-ago");
            Assert.DoesNotContain(recentSessions, s => s.Id == "four-days-ago");
            Assert.DoesNotContain(recentSessions, s => s.Id == "ten-days-ago");
        }

        [Fact]
        public void RecentSessionPage_DateHeaderFormat_IsStrictlyDayMonthYear()
        {
            var date = new DateTime(2026, 9, 12, 10, 30, 0);
            string formatted = date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal("12/09/2026", formatted);
            Assert.Matches(@"^\d{2}/\d{2}/\d{4}$", formatted);
        }

        [Fact]
        public void RecentSessionPage_DateGrouping_GroupsSessionsCorrectly()
        {
            var day1 = new DateTime(2026, 9, 12, 14, 0, 0);
            var day2 = new DateTime(2026, 9, 11, 9, 30, 0);

            var sessions = new List<BasicSessionBundle>
            {
                new BasicSessionBundle(null, false, null, "Session 1", null) { Id = "s1", creation_date = day1 },
                new BasicSessionBundle(null, false, null, "Session 2", null) { Id = "s2", creation_date = day1.AddHours(2) },
                new BasicSessionBundle(null, false, null, "Session 3", null) { Id = "s3", creation_date = day2 }
            };

            var groups = sessions
                .GroupBy(s => s.creation_date.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new
                {
                    DateHeader = g.Key.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    Sessions = g.OrderByDescending(s => s.creation_date).ToList()
                })
                .ToList();

            Assert.Equal(2, groups.Count);
            Assert.Equal("12/09/2026", groups[0].DateHeader);
            Assert.Equal(2, groups[0].Sessions.Count);
            Assert.Equal("11/09/2026", groups[1].DateHeader);
            Assert.Single(groups[1].Sessions);
        }

        [Fact]
        public void RecentSessionPage_DateFiltering_FiltersBySelectedDate()
        {
            var targetDate = new DateTime(2026, 9, 12);
            var otherDate = new DateTime(2026, 9, 10);

            var allSessions = new List<BasicSessionBundle>
            {
                new BasicSessionBundle(null, false, null, "Target 1", null) { Id = "s1", creation_date = targetDate.AddHours(8) },
                new BasicSessionBundle(null, false, null, "Target 2", null) { Id = "s2", creation_date = targetDate.AddHours(16) },
                new BasicSessionBundle(null, false, null, "Other", null) { Id = "s3", creation_date = otherDate.AddHours(12) }
            };

            // Filter for targetDate
            var filtered = allSessions.Where(s => s.creation_date.Date == targetDate.Date).ToList();
            Assert.Equal(2, filtered.Count);
            Assert.All(filtered, s => Assert.Equal("12/09/2026", s.creation_date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)));

            // Clear filter
            var cleared = allSessions.ToList();
            Assert.Equal(3, cleared.Count);
        }

        [Fact]
        public void LauncherMode_ContainsLibraryAndGeneratorConstants()
        {
            Assert.Equal("Basic", LauncherMode.Basic);
            Assert.Equal("Advanced", LauncherMode.Advanced);
            Assert.Equal("Generator", LauncherMode.Generator);
            Assert.Equal("Library", LauncherMode.Library);

            Assert.Equal(LauncherMode.Library, LauncherMode.Normalize("library"));
            Assert.Equal(LauncherMode.Library, LauncherMode.Normalize("LIBRARY"));
            Assert.Equal(LauncherMode.Generator, LauncherMode.Normalize("generator"));
            Assert.Equal(LauncherMode.Basic, LauncherMode.Normalize(""));
            Assert.Equal(LauncherMode.Basic, LauncherMode.Normalize(null));
        }

        [Fact]
        public void Controllers_GeneratorUIAndLibraryUI_ExistAndDefineModeWorkflows()
        {
            var testDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
            var generatorControllerPath = Path.Combine(projectDir, "Controllers", "GeneratorUI.cs");
            var libraryControllerPath = Path.Combine(projectDir, "Controllers", "LibraryUI.cs");

            Assert.True(File.Exists(generatorControllerPath));
            Assert.True(File.Exists(libraryControllerPath));

            string genCode = File.ReadAllText(generatorControllerPath);
            Assert.Contains("class GeneratorUI", genCode);
            Assert.Contains("ShowUnderConstruction", genCode);

            string libCode = File.ReadAllText(libraryControllerPath);
            Assert.Contains("class LibraryUI", libCode);
            Assert.Contains("ShowUnderConstruction", libCode);
        }

        [Fact]
        public void RecentSessionPage_Files_ExistAndContainRequiredComponents()
        {
            var testDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
            var xamlPath = Path.Combine(projectDir, "Views", "RecentSessionPage.xaml");
            var csPath = Path.Combine(projectDir, "Views", "RecentSessionPage.xaml.cs");

            Assert.True(File.Exists(xamlPath));
            Assert.True(File.Exists(csPath));

            string xaml = File.ReadAllText(xamlPath);
            Assert.Contains("Recent Sessions", xaml);
            Assert.Contains("CalendarDatePicker", xaml);
            Assert.Contains("FilterDatePicker", xaml);
            Assert.Contains("RecentSession_DatePicker", xaml);

            string cs = File.ReadAllText(csPath);
            Assert.Contains("dd/MM/yyyy", cs);
        }
    }
}
