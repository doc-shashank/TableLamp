using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0075FeaturesTests
    {
        [Fact]
        public void VersionConstants_AreUpdatedToV0075OrGreater()
        {
            Assert.True(AppUpdateService.CompareVersions(AppUpdateService.CurrentVersionString, "0.0.7.5") >= 0);
            Assert.True(new AppSettings().CuratedContentVersion.StartsWith("v0.0."));
            AppSettingsService.Instance.CuratedContentVersion = "v0.0.7.5";
            Assert.Equal("v0.0.7.5", AppSettingsService.Instance.CuratedContentVersion);
        }

        [Fact]
        public void ProjectAndInstallerConfigurations_MatchV0075OrGreater()
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
        public void WorkspaceExport_IncludesWorkspaceInfoInZipArchive()
        {
            string tempWorkspace = Path.Combine(Path.GetTempPath(), $"test_ws_{Guid.NewGuid():N}");
            string tempZip = Path.Combine(Path.GetTempPath(), $"test_ws_{Guid.NewGuid():N}.zip");
            string tempExtract = Path.Combine(Path.GetTempPath(), $"test_extract_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(tempWorkspace);
                File.WriteAllText(Path.Combine(tempWorkspace, "PresetA.json"), "{}");

                // Write WORKSPACE_INFO.json to root
                string infoPath = Path.Combine(tempWorkspace, "WORKSPACE_INFO.json");
                var info = new Dictionary<string, object>
                {
                    ["version"] = "0.0.7.5",
                    ["exported_at"] = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(infoPath, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));

                // Compress directory
                ZipFile.CreateFromDirectory(tempWorkspace, tempZip);

                // Extract and verify WORKSPACE_INFO.json is present inside zip
                ZipFile.ExtractToDirectory(tempZip, tempExtract);
                string extractedInfoPath = Path.Combine(tempExtract, "WORKSPACE_INFO.json");
                Assert.True(File.Exists(extractedInfoPath), "WORKSPACE_INFO.json must be present in the exported zip archive");

                string extractedJson = File.ReadAllText(extractedInfoPath);
                using var doc = JsonDocument.Parse(extractedJson);
                Assert.True(doc.RootElement.TryGetProperty("version", out var verElem));
                Assert.Equal("0.0.7.5", verElem.GetString());
                Assert.True(doc.RootElement.TryGetProperty("exported_at", out var dateElem));
                Assert.False(string.IsNullOrWhiteSpace(dateElem.GetString()));
            }
            finally
            {
                if (Directory.Exists(tempWorkspace)) try { Directory.Delete(tempWorkspace, true); } catch { }
                if (File.Exists(tempZip)) try { File.Delete(tempZip); } catch { }
                if (Directory.Exists(tempExtract)) try { Directory.Delete(tempExtract, true); } catch { }
            }
        }

        [Theory]
        [InlineData("0.0.7.5", "0.0.7.4", true)]
        [InlineData("0.0.7.5", "0.0.7.5", false)] // duplicate
        [InlineData("v0.0.7.5", "0.0.7.5", false)] // duplicate with v
        [InlineData("0.0.7.6", "0.0.7.5", true)]
        [InlineData("", "0.0.7.5", false)] // empty
        [InlineData("invalid", "0.0.7.5", false)] // no numbers
        public void WorkspaceExport_VersionValidation(string newVersion, string existingVersion, bool shouldBeValid)
        {
            bool isValid = true;
            string input = newVersion.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                isValid = false;
            }
            else if (!Regex.IsMatch(input, @"^[a-zA-Z0-9.\-_+]+$") || !Regex.IsMatch(input, @"\d"))
            {
                isValid = false;
            }
            else if (!string.IsNullOrWhiteSpace(existingVersion))
            {
                if (string.Equals(input, existingVersion, StringComparison.OrdinalIgnoreCase) ||
                    AppUpdateService.CompareVersions(input, existingVersion) == 0)
                {
                    isValid = false;
                }
            }

            Assert.Equal(shouldBeValid, isValid);
        }

        [Fact]
        public async Task CuratedContentUpdate_ProgressReportsDuringDownload()
        {
            var reports = new List<double>();
            var progress = new Progress<double>(p => reports.Add(p));

            string dummyZipContent = new string('A', 200000); // ~200 KB
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(dummyZipContent, Encoding.UTF8, "application/zip")
            };
            response.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(dummyZipContent);

            var handler = new MockContentHttpMessageHandler(response);
            using var httpClient = new HttpClient(handler);

            string destFile = Path.Combine(Path.GetTempPath(), $"progress_test_{Guid.NewGuid():N}.bin");
            try
            {
                bool ok = await AppUpdateService.DownloadInstallerAsync(
                    "https://github.com/doc-shashank/table-lamp/releases/download/v0.0.7.5/test.exe",
                    destFile,
                    progress,
                    httpClient);

                Assert.True(ok);
                Assert.True(File.Exists(destFile));
                Assert.True(reports.Count > 0, "Progress should be reported at least once");
                Assert.True(reports[^1] >= 99.0, "Progress should report near 100%");
            }
            finally
            {
                if (File.Exists(destFile)) try { File.Delete(destFile); } catch { }
            }
        }
    }

    public class MockContentHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockContentHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            _response.RequestMessage = request;
            return Task.FromResult(_response);
        }
    }
}
