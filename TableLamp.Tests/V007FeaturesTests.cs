using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V007FeaturesTests
    {
        [Fact]
        public void AppUpdateService_ParseVersion_HandlesVariousFormats()
        {
            var v1 = AppUpdateService.ParseVersion("0.0.7.0");
            Assert.Equal(new Version(0, 0, 7, 0), v1);

            var v2 = AppUpdateService.ParseVersion("v0.0.8.0");
            Assert.Equal(new Version(0, 0, 8, 0), v2);

            var v3 = AppUpdateService.ParseVersion("v1.2");
            Assert.Equal(new Version(1, 2, 0, 0), v3);

            var v4 = AppUpdateService.ParseVersion("v2.1.0-beta.1");
            Assert.Equal(new Version(2, 1, 0, 0), v4);

            var vEmpty = AppUpdateService.ParseVersion("");
            Assert.Equal(new Version(0, 0, 0, 0), vEmpty);
        }

        [Fact]
        public void AppUpdateService_CompareVersions_ComparesAccurately()
        {
            Assert.True(AppUpdateService.CompareVersions("0.0.8.0", "0.0.7.0") > 0);
            Assert.True(AppUpdateService.CompareVersions("v0.0.6.5", "0.0.7.0") < 0);
            Assert.Equal(0, AppUpdateService.CompareVersions("v0.0.7.0", "0.0.7.0"));
            Assert.True(AppUpdateService.CompareVersions("v1.0.0.0", "0.0.7.0") > 0);
        }

        [Fact]
        public void AppUpdateService_ParseReleaseJson_DetectsUpdateAvailability()
        {
            string jsonWithNewRelease = @"
            {
                ""tag_name"": ""v0.0.8.0"",
                ""name"": ""Table Lamp v0.0.8.0 Release"",
                ""body"": ""- Added new study modes\n- Performance enhancements"",
                ""html_url"": ""https://github.com/doc-shashank/table-lamp/releases/tag/v0.0.8.0"",
                ""published_at"": ""2026-09-09T12:00:00Z""
            }";

            var result = AppUpdateService.ParseReleaseJson(jsonWithNewRelease);
            Assert.True(result.Success);
            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("v0.0.8.0", result.LatestVersion);
            Assert.Equal("Table Lamp v0.0.8.0 Release", result.ReleaseTitle);
            Assert.Contains("Added new study modes", result.ReleaseNotes);
            Assert.Equal("https://github.com/doc-shashank/table-lamp/releases/tag/v0.0.8.0", result.ReleaseUrl);
            Assert.NotNull(result.PublishedAt);

            string jsonWithCurrentRelease = @"
            {
                ""tag_name"": ""v0.0.7.1"",
                ""name"": ""Table Lamp v0.0.7.1 Release"",
                ""body"": ""Current release notes"",
                ""html_url"": ""https://github.com/doc-shashank/table-lamp/releases/tag/v0.0.7.1"",
                ""published_at"": ""2026-09-09T10:00:00Z""
            }";

            var resultCurrent = AppUpdateService.ParseReleaseJson(jsonWithCurrentRelease);
            Assert.True(resultCurrent.Success);
            Assert.False(resultCurrent.IsUpdateAvailable);
        }

        [Fact]
        public void CuratedContentUpdateService_ParseCuratedReleaseJson_ExtractsZipAsset()
        {
            string releaseJson = @"
            {
                ""tag_name"": ""v1.2.0"",
                ""name"": ""Curated Content Pack v1.2.0"",
                ""body"": ""Includes new Pathology and Pharmacology modules."",
                ""assets"": [
                    {
                        ""name"": ""readme.txt"",
                        ""browser_download_url"": ""https://github.com/.../readme.txt""
                    },
                    {
                        ""name"": ""curated-presets-v1.2.0.zip"",
                        ""browser_download_url"": ""https://github.com/doc-shashank/TableLamp-CuratedContent/releases/download/v1.2.0/curated-presets-v1.2.0.zip""
                    }
                ],
                ""zipball_url"": ""https://api.github.com/repos/doc-shashank/TableLamp-CuratedContent/zipball/v1.2.0""
            }";

            var check = CuratedContentUpdateService.ParseCuratedReleaseJson(releaseJson, "v0.0.7.0");
            Assert.True(check.Success);
            Assert.True(check.IsUpdateAvailable);
            Assert.Equal("v1.2.0", check.LatestVersion);
            Assert.Equal("https://github.com/doc-shashank/TableLamp-CuratedContent/releases/download/v1.2.0/curated-presets-v1.2.0.zip", check.PackageDownloadUrl);
        }

        [Fact]
        public void CuratedContentUpdateService_ApplyExtractedPackage_LoadsIntoCuratedDb_AndNeverModifiesCustomDb()
        {
            // Verify strict database separation: Curated Presets vs Custom Presets
            string tempDir = Path.Combine(Path.GetTempPath(), $"V007Test_{Guid.NewGuid():N}");
            string zipPath = Path.Combine(tempDir, "curated_test.zip");
            string extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create a temporary valid preset JSON file
                string testPresetContent = @"{
                  ""CuratedSubjectV007"": {
                    ""short_name"": ""V007 Curated Subject"",
                    ""full_name"": ""V007 Curated Subject Full"",
                    ""edition"": ""1st"",
                    ""chapters"": {
                      ""1"": {
                        ""name"": ""Curated Chapter 1"",
                        ""topics"": {
                          ""1"": {
                            ""name"": ""Curated Topic 1"",
                            ""pages"": [1, 10]
                          }
                        }
                      }
                    }
                  }
                }";

                // Create a zip archive containing the preset file
                using (var zipStream = new FileStream(zipPath, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("presets/curated_test.json");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(testPresetContent);
                }

                string originalVersion = AppSettingsService.Instance.CuratedContentVersion;

                // Record initial state of custom database
                int initialCustomSubjectCount = CustomPresetTagDatabase.Instance.SubjectCount;

                // Execute ApplyExtractedPackage
                var applyResult = CuratedContentUpdateService.ApplyExtractedPackage(zipPath, extractDir, "v0.0.7.0-test");

                Assert.True(applyResult.Success);
                Assert.Equal("v0.0.7.0-test", applyResult.VersionApplied);
                Assert.True(applyResult.FilesImported >= 1);

                // Verify loaded into Curated Database (PresetTagDatabase)
                var curatedTree = PresetTagDatabase.Instance.GetTree();
                Assert.True(curatedTree.ContainsKey("CuratedSubjectV007"));
                Assert.Equal("V007 Curated Subject", curatedTree["CuratedSubjectV007"].short_name);

                // Verify Custom Database was untouched
                int finalCustomSubjectCount = CustomPresetTagDatabase.Instance.SubjectCount;
                Assert.Equal(initialCustomSubjectCount, finalCustomSubjectCount);
                Assert.False(CustomPresetTagDatabase.Instance.GetTree().ContainsKey("CuratedSubjectV007"));

                // Verify AppSettings updated version and last updated date
                Assert.Equal("v0.0.7.0-test", AppSettingsService.Instance.CuratedContentVersion);
                Assert.NotNull(AppSettingsService.Instance.CuratedContentLastUpdated);

                AppSettingsService.Instance.CuratedContentVersion = originalVersion;
                AppSettingsService.Instance.Save();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void InnoSetup_ScriptFile_ExistsAndContainsRequiredConfiguration()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
            string issPath = Path.Combine(solutionRoot, "installer", "TableLampSetup.iss");
            string ps1Path = Path.Combine(solutionRoot, "installer", "build_installer.ps1");

            Assert.True(File.Exists(issPath), $"Expected Inno Setup script to exist at {issPath}");
            Assert.True(File.Exists(ps1Path), $"Expected build automation script to exist at {ps1Path}");

            string issContent = File.ReadAllText(issPath);
            Assert.Contains("MyAppName \"Table Lamp\"", issContent);
            Assert.Contains("MyAppVersion", issContent);
            Assert.Contains("[Setup]", issContent);
            Assert.Contains("[Files]", issContent);
            Assert.Contains("[Icons]", issContent);
            Assert.Contains("[Run]", issContent);
            Assert.Contains("TableLamp.exe", issContent);
        }

        [Fact]
        public void AppSettingsService_CuratedContentVersion_PersistsCorrectly()
        {
            var service = AppSettingsService.Instance;
            string original = service.CuratedContentVersion;

            try
            {
                service.CuratedContentVersion = "v0.0.7.1-custom-test";
                Assert.Equal("v0.0.7.1-custom-test", service.CuratedContentVersion);

                // Reload from disk
                service.Load();
                Assert.Equal("v0.0.7.1-custom-test", service.CuratedContentVersion);
            }
            finally
            {
                service.CuratedContentVersion = original;
                service.Save();
            }
        }
    }
}
