using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0073FeaturesTests
    {
        [Fact]
        public void VersionConstants_AreUpdatedToCurrentVersion()
        {
            Assert.Equal("0.0.7.5", AppUpdateService.CurrentVersionString);
            Assert.Equal("v0.0.7.5", new AppSettings().CuratedContentVersion);
            AppSettingsService.Instance.CuratedContentVersion = "v0.0.7.5";
            Assert.Equal("v0.0.7.5", AppSettingsService.Instance.CuratedContentVersion);
        }

        [Fact]
        public void AuthenticodeLogic_IsCompletelyRemovedFromAppUpdateService()
        {
            var appUpdateServiceType = typeof(AppUpdateService);

            // Verify VerifyBinarySignature does not exist
            var verifyMethod = appUpdateServiceType.GetMethod("VerifyBinarySignature", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.Null(verifyMethod);

            // Verify TrustedPublisherSubject does not exist
            var trustedPublisherField = appUpdateServiceType.GetField("TrustedPublisherSubject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            var trustedPublisherProp = appUpdateServiceType.GetProperty("TrustedPublisherSubject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.Null(trustedPublisherField);
            Assert.Null(trustedPublisherProp);
        }

        [Fact]
        public void PresetTagDatabase_StartsEmptyByDefault_AndIsEmptyReflectsState()
        {
            var db = PresetTagDatabase.Instance;
            db.FormatDatabase();

            Assert.True(db.IsEmpty);
            Assert.Equal(0, db.SubjectCount);
            Assert.Equal(0, db.ChapterCount);
            Assert.Equal(0, db.TopicCount);

            // ResetToStarterPresets now resets to empty state (no mock presets seeded)
            db.ResetToStarterPresets();
            Assert.True(db.IsEmpty);
            Assert.Equal(0, db.SubjectCount);
        }

        [Fact]
        public async Task CuratedContentUpdateService_NoRedundantDownload_WhenVersionAlreadyMatches()
        {
            // Simulate 302 redirect returning tag v0.0.7.3
            var handler = new MockRedirectHttpMessageHandler(
                HttpStatusCode.Found,
                new Uri("https://github.com/doc-shashank/table-lamp-curated-tags/releases/tag/v0.0.7.3"));

            using var httpClient = new HttpClient(handler);

            PresetTagDatabase.Instance.ReloadFromJson(@"{ ""Subj"": { ""short_name"": ""S"", ""Chapters"": {} } }");
            // Set app's current curated content version to v0.0.7.3
            AppSettingsService.Instance.CuratedContentVersion = "v0.0.7.3";

            var result = await CuratedContentUpdateService.DownloadAndApplyUpdateAsync(httpClient: httpClient);

            Assert.True(result.Success);
            Assert.True(result.AlreadyUpToDate);
            Assert.Equal("v0.0.7.3", result.VersionApplied);
            Assert.Equal(0, result.FilesImported);
        }

        [Fact]
        public void CuratedContentUpdateService_50MbZipLimitRemoved_SecurityLimitsRetained()
        {
            // Verify MaxZipSizeBytes no longer exists in CuratedContentUpdateService
            var curatedServiceType = typeof(CuratedContentUpdateService);
            var maxZipField = curatedServiceType.GetField("MaxZipSizeBytes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.Null(maxZipField);

            // Verify uncompressed bytes limit (100 MB) is still enforced
            Assert.Equal(100L * 1024 * 1024, CuratedContentUpdateService.MaxUncompressedBytes);

            // Verify entry count limit (1000) is still enforced
            Assert.Equal(1000, CuratedContentUpdateService.MaxArchiveEntries);
        }

        [Fact]
        public void ProjectAndInstallerConfigurations_MatchCurrentVersion()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
            string issPath = Path.Combine(solutionRoot, "installer", "TableLampSetup.iss");
            string csprojPath = Path.Combine(solutionRoot, "TableLamp.csproj");
            string buildPs1Path = Path.Combine(solutionRoot, "installer", "build_installer.ps1");

            Assert.True(File.Exists(issPath));
            string issContent = File.ReadAllText(issPath);
            Assert.Contains("MyAppVersion \"0.0.7.5\"", issContent);
            Assert.DoesNotContain("SignTool=", issContent);

            Assert.True(File.Exists(csprojPath));
            string csprojContent = File.ReadAllText(csprojPath);
            Assert.Contains("<Version>0.0.7.5</Version>", csprojContent);
            Assert.Contains("<AssemblyVersion>0.0.7.5</AssemblyVersion>", csprojContent);
            Assert.Contains("<FileVersion>0.0.7.5</FileVersion>", csprojContent);

            Assert.True(File.Exists(buildPs1Path));
            string ps1Content = File.ReadAllText(buildPs1Path);
            Assert.Contains("0.0.7.5", ps1Content);
        }

        [Fact]
        public void DevToolsWorkspace_BlankPresetJsonContainsOnlyDummySubject()
        {
            var generator = new PresetTagGenerator();
            string blankJson = generator.CreateBlankSubjectPresetJson();

            Assert.NotNull(blankJson);
            Assert.Contains("\"Subject1\"", blankJson);
            var tree = generator.Parse(blankJson);
            Assert.True(tree.ContainsKey("Subject1"));
            Assert.True(tree["Subject1"].Chapters == null || tree["Subject1"].Chapters.Count == 0);
        }
    }
}
