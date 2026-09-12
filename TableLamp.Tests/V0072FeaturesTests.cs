using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0072FeaturesTests
    {
        [Fact]
        public void VersionConstants_AreUpdatedToCurrentVersion()
        {
            Assert.True(AppVersionService.CompareVersions(AppVersionService.CurrentVersionString, "0.0.7.2") >= 0);
            Assert.True(new AppSettings().CuratedContentVersion.StartsWith("v0.0."));
            AppSettingsService.Instance.CuratedContentVersion = "v0.0.7.5";
            Assert.Equal("v0.0.7.5", AppSettingsService.Instance.CuratedContentVersion);
        }

        [Theory]
        [InlineData("https://github.com/doc-shashank/table-lamp-curated-tags/releases/tag/v0.0.1", "v0.0.1")]
        [InlineData("https://github.com/doc-shashank/table-lamp/releases/tag/v0.0.7.2/", "v0.0.7.2")]
        [InlineData("https://github.com/owner/repo/releases/tag/v1.2.3?param=val#sec", "v1.2.3")]
        [InlineData("/doc-shashank/table-lamp/releases/tag/v2.0.0", "v2.0.0")]
        [InlineData("https://github.com/doc-shashank/table-lamp/releases", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ExtractTagFromReleaseUrl_ExtractsCorrectly(string? url, string? expectedTag)
        {
            string? result = CuratedContentUpdateService.ExtractTagFromReleaseUrl(url);
            Assert.Equal(expectedTag, result);
        }

        [Theory]
        [InlineData("v0.0.7.2", true)]
        [InlineData("0.0.1", true)]
        [InlineData("v1.0.0-beta.1", true)]
        [InlineData("v1.0.0+build.42", true)]
        [InlineData("../../malicious", false)]
        [InlineData("v1.0/payload", false)]
        [InlineData("v1.0\\payload", false)]
        [InlineData("..", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidTag_PreventsPathTraversalAndInjection(string? tag, bool expectedValid)
        {
            bool valid = CuratedContentUpdateService.IsValidTag(tag);
            Assert.Equal(expectedValid, valid);
        }

        [Fact]
        public void IsTrustedGitHubUrl_EnforcesHttpsAndTrustedHosts()
        {
            Assert.True(CuratedContentUpdateService.IsTrustedGitHubUrl(new Uri("https://github.com/doc-shashank/table-lamp")));
            Assert.True(CuratedContentUpdateService.IsTrustedGitHubUrl(new Uri("https://codeload.github.com/doc-shashank/table-lamp/zip/refs/tags/v0.0.1")));
            Assert.True(CuratedContentUpdateService.IsTrustedGitHubUrl(new Uri("https://objects.githubusercontent.com/github-production-release-asset")));
            Assert.True(CuratedContentUpdateService.IsTrustedGitHubUrl(new Uri("https://github-production-release-asset-2e65be.s3.amazonaws.com/test.zip")));

            // Insecure HTTP rejected
            Assert.False(CuratedContentUpdateService.IsTrustedGitHubUrl(new Uri("http://github.com/insecure")));
            // Untrusted domain rejected
            Assert.False(CuratedContentUpdateService.IsTrustedGitHubUrl(new Uri("https://evil-phishing.com/download.zip")));
            Assert.False(CuratedContentUpdateService.IsTrustedGitHubUrl(new Uri("https://malicious-github.com")));
        }

        [Fact]
        public void DownloadUrlBlueprints_AreConstructedWithoutApi()
        {
            string assetUrl = CuratedContentUpdateService.BuildAssetDownloadUrl("doc-shashank", "table-lamp", "v0.0.7.2", "TableLamp-Setup-v0.0.7.2.exe");
            Assert.Equal("https://github.com/doc-shashank/table-lamp/releases/download/v0.0.7.2/TableLamp-Setup-v0.0.7.2.exe", assetUrl);

            string archiveUrl = CuratedContentUpdateService.BuildArchiveDownloadUrl("doc-shashank", "table-lamp-curated-tags", "v0.0.1");
            Assert.Equal("https://github.com/doc-shashank/table-lamp-curated-tags/archive/refs/tags/v0.0.1.zip", archiveUrl);
        }

        [Fact]
        public void SafeExtractZipArchive_RejectsZipSlipAttack()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"TableLamp_Test_ZipSlip_{Guid.NewGuid():N}");
            string zipPath = Path.Combine(Path.GetTempPath(), $"TableLamp_Malicious_{Guid.NewGuid():N}.zip");

            try
            {
                Directory.CreateDirectory(tempDir);

                // Create a zip with a malicious entry attempting directory traversal escape
                using (var zipStream = new FileStream(zipPath, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var evilEntry = archive.CreateEntry("../escaped_evil.json");
                    using var writer = new StreamWriter(evilEntry.Open());
                    writer.WriteLine("{\"malicious\": true}");
                }

                // Verify extraction throws InvalidOperationException because of Zip Slip attempt
                var ex = Assert.Throws<InvalidOperationException>(() =>
                {
                    CuratedContentUpdateService.SafeExtractZipArchive(zipPath, tempDir);
                });

                Assert.Contains("Zip Slip traversal attempt detected", ex.Message);
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
                string escapedFile = Path.Combine(Path.GetTempPath(), "escaped_evil.json");
                if (File.Exists(escapedFile)) File.Delete(escapedFile);
            }
        }

        [Fact]
        public void SafeExtractZipArchive_FiltersNonJsonFiles()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"TableLamp_Test_Filter_{Guid.NewGuid():N}");
            string zipPath = Path.Combine(Path.GetTempPath(), $"TableLamp_Mixed_{Guid.NewGuid():N}.zip");

            try
            {
                Directory.CreateDirectory(tempDir);

                using (var zipStream = new FileStream(zipPath, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var validEntry = archive.CreateEntry("valid_preset.json");
                    using (var writer = new StreamWriter(validEntry.Open()))
                    {
                        writer.WriteLine("{\"SubjectName\": \"Physics\"}");
                    }

                    var maliciousExe = archive.CreateEntry("payload.exe");
                    using (var writer = new StreamWriter(maliciousExe.Open()))
                    {
                        writer.WriteLine("MZ executable binary bytes");
                    }

                    var maliciousScript = archive.CreateEntry("script.bat");
                    using (var writer = new StreamWriter(maliciousScript.Open()))
                    {
                        writer.WriteLine("@echo off");
                    }
                }

                CuratedContentUpdateService.SafeExtractZipArchive(zipPath, tempDir);

                // Valid .json must be extracted
                Assert.True(File.Exists(Path.Combine(tempDir, "valid_preset.json")));

                // Disallowed executables/scripts must be discarded
                Assert.False(File.Exists(Path.Combine(tempDir, "payload.exe")));
                Assert.False(File.Exists(Path.Combine(tempDir, "script.bat")));
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
        }


        [Fact]
        public async Task CuratedContentUpdateService_ZeroApiRedirectCheck_ReturnsDirectUrls()
        {
            // Simulate 302 Found redirect for curated content repo
            var handler = new MockRedirectHttpMessageHandler(
                HttpStatusCode.Found,
                new Uri("https://github.com/doc-shashank/table-lamp-curated-tags/releases/tag/v0.0.1"));

            using var httpClient = new HttpClient(handler);
            var result = await CuratedContentUpdateService.CheckForUpdatesAsync(httpClient);

            Assert.True(result.Success);
            Assert.Equal("v0.0.1", result.LatestVersion);
            Assert.Equal("https://github.com/doc-shashank/table-lamp-curated-tags/releases/download/v0.0.1/table-lamp-curated-tags.zip", result.PackageDownloadUrl);
            Assert.Equal("https://github.com/doc-shashank/table-lamp-curated-tags/archive/refs/tags/v0.0.1.zip", result.FallbackDownloadUrl);
        }

        [Fact]
        public void ProjectAndInstallerConfigurations_MatchCurrentVersion()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
            string issPath = Path.Combine(solutionRoot, "installer", "TableLampSetup.iss");
            string csprojPath = Path.Combine(solutionRoot, "TableLamp.csproj");

            Assert.True(File.Exists(issPath));
            string issContent = File.ReadAllText(issPath);
            Assert.True(issContent.Contains("MyAppVersion \"0.0.7.5\"") || issContent.Contains("MyAppVersion \"0.0.8.0\"") || issContent.Contains("MyAppVersion \"0.0.8.1\"") || issContent.Contains("MyAppVersion \"0.0.8.2\""));
            Assert.DoesNotContain("SignTool=", issContent);

            Assert.True(File.Exists(csprojPath));
            string csprojContent = File.ReadAllText(csprojPath);
            Assert.True(csprojContent.Contains("<Version>0.0.7.5</Version>") || csprojContent.Contains("<Version>0.0.8.0</Version>") || csprojContent.Contains("<Version>0.0.8.1</Version>") || csprojContent.Contains("<Version>0.0.8.2</Version>"));
            Assert.True(csprojContent.Contains("<AssemblyVersion>0.0.7.5</AssemblyVersion>") || csprojContent.Contains("<AssemblyVersion>0.0.8.0</AssemblyVersion>") || csprojContent.Contains("<AssemblyVersion>0.0.8.1</AssemblyVersion>") || csprojContent.Contains("<AssemblyVersion>0.0.8.2</AssemblyVersion>"));
            Assert.True(csprojContent.Contains("<FileVersion>0.0.7.5</FileVersion>") || csprojContent.Contains("<FileVersion>0.0.8.0</FileVersion>") || csprojContent.Contains("<FileVersion>0.0.8.1</FileVersion>") || csprojContent.Contains("<FileVersion>0.0.8.2</FileVersion>"));
        }

        [Fact]
        public void XamlFiles_DoNotContainRetiredRateLimitLabel()
        {
            string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
            string launcherSettingsXaml = Path.Combine(solutionRoot, "Views", "LauncherSettingsPage.xaml");
            string settingsXaml = Path.Combine(solutionRoot, "Views", "SettingsPage.xaml");

            string lsContent = File.ReadAllText(launcherSettingsXaml);
            Assert.DoesNotContain("GitHubRateLimitInfoText", lsContent);

            string sContent = File.ReadAllText(settingsXaml);
            Assert.DoesNotContain("GitHubRateLimitInfoText", sContent);
        }
    }

    /// <summary>
    /// Mock HTTP handler that simulates GitHub 302 Found redirects without actual network traffic.
    /// </summary>
    public class MockRedirectHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly Uri _redirectUri;

        public MockRedirectHttpMessageHandler(HttpStatusCode statusCode, Uri redirectUri)
        {
            _statusCode = statusCode;
            _redirectUri = redirectUri;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            response.Headers.Location = _redirectUri;
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
