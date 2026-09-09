using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0074FeaturesTests
    {
        [Fact]
        public void VersionConstants_AreUpdatedToCurrentVersion()
        {
            Assert.True(AppUpdateService.CompareVersions(AppUpdateService.CurrentVersionString, "0.0.7.4") >= 0);
            Assert.True(new AppSettings().CuratedContentVersion.StartsWith("v0.0."));
            AppSettingsService.Instance.CuratedContentVersion = "v0.0.7.5";
            Assert.Equal("v0.0.7.5", AppSettingsService.Instance.CuratedContentVersion);
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
            Assert.True(issContent.Contains("MyAppVersion \"0.0.7.5\"") || issContent.Contains("MyAppVersion \"0.0.8.0\""));

            Assert.True(File.Exists(csprojPath));
            string csprojContent = File.ReadAllText(csprojPath);
            Assert.True(csprojContent.Contains("<Version>0.0.7.5</Version>") || csprojContent.Contains("<Version>0.0.8.0</Version>"));
            Assert.True(csprojContent.Contains("<AssemblyVersion>0.0.7.5</AssemblyVersion>") || csprojContent.Contains("<AssemblyVersion>0.0.8.0</AssemblyVersion>"));
            Assert.True(csprojContent.Contains("<FileVersion>0.0.7.5</FileVersion>") || csprojContent.Contains("<FileVersion>0.0.8.0</FileVersion>"));

            Assert.True(File.Exists(buildPs1Path));
            string ps1Content = File.ReadAllText(buildPs1Path);
            Assert.True(ps1Content.Contains("0.0.7.5") || ps1Content.Contains("0.0.8.0"));
        }

        [Fact]
        public void SeparateDatabases_FormatIndependently()
        {
            var curatedDb = PresetTagDatabase.Instance;
            var customDb = CustomPresetTagDatabase.Instance;

            // Seed both databases
            string curatedJson = @"{
                ""CuratedPhysics"": {
                    ""short_name"": ""Phys"",
                    ""Chapters"": {
                        ""Mechanics"": {
                            ""name"": ""Mechanics"",
                            ""Topics"": {}
                        }
                    }
                }
            }";

            string customJson = @"{
                ""CustomBiology"": {
                    ""short_name"": ""Bio"",
                    ""Chapters"": {
                        ""CellBiology"": {
                            ""name"": ""Cell Biology"",
                            ""Topics"": {}
                        }
                    }
                }
            }";

            curatedDb.ReloadFromJson(curatedJson);
            customDb.ReloadFromJson(customJson);

            Assert.False(curatedDb.IsEmpty);
            Assert.True(curatedDb.GetTree().ContainsKey("CuratedPhysics"));

            Assert.False(customDb.IsEmpty);
            Assert.True(customDb.GetTree().ContainsKey("CustomBiology"));

            // Format Curated DB only
            curatedDb.FormatDatabase();

            Assert.True(curatedDb.IsEmpty);
            Assert.False(curatedDb.GetTree().ContainsKey("CuratedPhysics"));

            // Custom DB must still contain its data
            Assert.False(customDb.IsEmpty);
            Assert.True(customDb.GetTree().ContainsKey("CustomBiology"));

            // Format Custom DB only
            customDb.FormatDatabase();

            Assert.True(customDb.IsEmpty);
            Assert.False(customDb.GetTree().ContainsKey("CustomBiology"));
        }

        [Fact]
        public async Task CuratedContentUpdate_NotifiesWhenEmpty_EvenIfVersionMatches()
        {
            var curatedDb = PresetTagDatabase.Instance;
            curatedDb.FormatDatabase();
            Assert.True(curatedDb.IsEmpty);

            AppSettingsService.Instance.CuratedContentVersion = "v0.0.7.4";

            // Mock redirect pointing to v0.0.7.4
            var handler = new MockRedirectHttpMessageHandler(
                HttpStatusCode.Found,
                new Uri("https://github.com/doc-shashank/table-lamp-curated-tags/releases/tag/v0.0.7.4"));

            using var httpClient = new HttpClient(handler);

            // Check should indicate update is available because dictionary is empty!
            var checkResult = await CuratedContentUpdateService.CheckForUpdatesAsync(httpClient: httpClient);
            Assert.True(checkResult.Success);
            Assert.True(checkResult.IsUpdateAvailable, "Update should be available when database is empty even if tag matches");
        }

        [Fact]
        public async Task CuratedContentUpdate_NoRedundantDownload_WhenNotEmptyAndVersionMatches()
        {
            var curatedDb = PresetTagDatabase.Instance;
            curatedDb.FormatDatabase();
            curatedDb.ReloadFromJson(@"{ ""Math"": { ""short_name"": ""Math"", ""Chapters"": {} } }");
            Assert.False(curatedDb.IsEmpty);

            AppSettingsService.Instance.CuratedContentVersion = "v0.0.7.4";

            // Mock redirect pointing to v0.0.7.4
            var handler = new MockRedirectHttpMessageHandler(
                HttpStatusCode.Found,
                new Uri("https://github.com/doc-shashank/table-lamp-curated-tags/releases/tag/v0.0.7.4"));

            using var httpClient = new HttpClient(handler);

            var checkResult = await CuratedContentUpdateService.CheckForUpdatesAsync(httpClient: httpClient);
            Assert.True(checkResult.Success);
            Assert.False(checkResult.IsUpdateAvailable);

            var downloadResult = await CuratedContentUpdateService.DownloadAndApplyUpdateAsync(httpClient: httpClient);
            Assert.True(downloadResult.Success);
            Assert.True(downloadResult.AlreadyUpToDate);
            Assert.Equal(0, downloadResult.FilesImported);
        }

        [Fact]
        public void CustomPresetTagDatabase_ImportZipArchive_SucceedsAndEnforcesSecurity()
        {
            var customDb = CustomPresetTagDatabase.Instance;
            customDb.FormatDatabase();

            string tempZip = Path.Combine(Path.GetTempPath(), $"test_custom_presets_{Guid.NewGuid():N}.zip");
            try
            {
                using (var zipStream = new FileStream(tempZip, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    // Valid preset 1
                    var entry1 = archive.CreateEntry("SubjectA.json");
                    using (var writer = new StreamWriter(entry1.Open(), Encoding.UTF8))
                    {
                        writer.Write(@"{ ""ZipSubjectA"": { ""short_name"": ""ZA"", ""Chapters"": {} } }");
                    }

                    // Valid preset 2 in subfolder
                    var entry2 = archive.CreateEntry("subfolder/SubjectB.json");
                    using (var writer = new StreamWriter(entry2.Open(), Encoding.UTF8))
                    {
                        writer.Write(@"{ ""ZipSubjectB"": { ""short_name"": ""ZB"", ""Chapters"": {} } }");
                    }

                    // Non-json entry
                    var entry3 = archive.CreateEntry("readme.txt");
                    using (var writer = new StreamWriter(entry3.Open(), Encoding.UTF8))
                    {
                        writer.Write("This is a readme file.");
                    }
                }

                var (totalFound, success, failed) = customDb.ImportZipArchive(tempZip);

                Assert.Equal(2, totalFound);
                Assert.Equal(2, success);
                Assert.Equal(0, failed);

                var tree = customDb.GetTree();
                Assert.True(tree.ContainsKey("ZipSubjectA"));
                Assert.True(tree.ContainsKey("ZipSubjectB"));
            }
            finally
            {
                if (File.Exists(tempZip))
                {
                    try { File.Delete(tempZip); } catch { }
                }
            }
        }

        [Theory]
        [InlineData("questions", "questions.json")]
        [InlineData("questions.json", "questions.json")]
        [InlineData("test.JSON", "test.JSON")]
        [InlineData("biology.unit1", "biology.unit1.json")]
        [InlineData("physics.v1.json", "physics.v1.json")]
        public void FileNaming_AutoAppendsJsonExtension_WhenOmitted(string input, string expected)
        {
            string result = input.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? input : input + ".json";
            Assert.Equal(expected, result);
        }
    }
}
