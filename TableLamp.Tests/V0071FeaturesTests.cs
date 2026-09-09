using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0071FeaturesTests
    {
        [Fact]
        public void CuratedContentUpdateService_RepositoryName_MatchesInstruction()
        {
            Assert.Equal("table-lamp-curated-tags", CuratedContentUpdateService.RepoName);
        }

        [Fact]
        public void AppUpdateService_CurrentVersion_IsAtLeastV0071()
        {
            Assert.True(AppUpdateService.CompareVersions(AppUpdateService.CurrentVersionString, "0.0.7.1") >= 0);
            Assert.True(AppUpdateService.CurrentVersion >= new Version(0, 0, 7, 1));
        }

        [Fact]
        public void GitHubRateLimitTracker_RecordsHeadersAndGeneratesStatusText()
        {
            var response = new HttpResponseMessage();
            response.Headers.Add("X-RateLimit-Used", "12");
            response.Headers.Add("X-RateLimit-Remaining", "48");
            response.Headers.Add("X-RateLimit-Limit", "60");
            response.Headers.Add("X-RateLimit-Reset", "1788945752");

            bool eventFired = false;
            Action handler = () => eventFired = true;
            GitHubRateLimitTracker.RateLimitUpdated += handler;

            try
            {
                GitHubRateLimitTracker.RecordResponseHeaders(response.Headers);

                Assert.True(eventFired);
                Assert.Equal(12, GitHubRateLimitTracker.RequestsUsedPastHour);
                Assert.Equal(48, GitHubRateLimitTracker.RequestsRemaining);
                Assert.Equal(60, GitHubRateLimitTracker.RateLimitMax);
                Assert.NotNull(GitHubRateLimitTracker.ResetTime);

                string statusText = GitHubRateLimitTracker.GetStatusText();
                Assert.Contains("12/60", statusText);
                Assert.Contains("48 remaining", statusText);
            }
            finally
            {
                GitHubRateLimitTracker.RateLimitUpdated -= handler;
            }
        }

        [Fact]
        public void InnoSetup_HasVersionInfoDirectives_ToReflectAppVersion()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
            string issPath = Path.Combine(solutionRoot, "installer", "TableLampSetup.iss");
            string ps1Path = Path.Combine(solutionRoot, "installer", "build_installer.ps1");

            Assert.True(File.Exists(issPath));
            string issContent = File.ReadAllText(issPath);

            Assert.Contains("VersionInfoVersion={#MyAppVersion}", issContent);
            Assert.Contains("VersionInfoProductVersion={#MyAppVersion}", issContent);
            Assert.Contains("VersionInfoCompany={#MyAppPublisher}", issContent);

            string ps1Content = File.ReadAllText(ps1Path);
            Assert.Contains("/DMyAppVersion=$AppVersion", ps1Content);
        }

        [Fact]
        public void PresetTagDatabase_SaveTree_ThreadSafeUnderConcurrentCalls()
        {
            // Rapid concurrent calls to SaveTree should not throw IOException sharing violations
            var db = PresetTagDatabase.Instance;
            var currentTree = db.GetTree();

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            Parallel.For(0, 10, i =>
            {
                try
                {
                    db.SaveTree(currentTree);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }
    }
}
