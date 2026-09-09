using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TableLamp.Services
{
    public class CuratedUpdateCheckResult
    {
        public bool Success { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseTitle { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string? PackageDownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class CuratedUpdateApplyResult
    {
        public bool Success { get; set; }
        public string VersionApplied { get; set; } = string.Empty;
        public int TotalFound { get; set; }
        public int FilesImported { get; set; }
        public int FailedCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service responsible for checking, downloading, decompressing, and building the Curated Tags database
    /// from GitHub Releases.
    /// NOTE: Loads strictly into PresetTagDatabase (curated database) and never modifies CustomPresetTagDatabase.
    /// </summary>
    public static class CuratedContentUpdateService
    {
        // =========================================================================
        // GITHUB REPOSITORY CONFIGURATION FOR CURATED CONTENT
        // (Edit the owner and repository name below to target your GitHub repo)
        // =========================================================================
        public static string RepoOwner { get; set; } = "doc-shashank";
        public static string RepoName { get; set; } = "TableLamp-CuratedContent";

        private static readonly HttpClient DefaultHttpClient = new();

        static CuratedContentUpdateService()
        {
            if (!DefaultHttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                DefaultHttpClient.DefaultRequestHeaders.Add("User-Agent", "TableLamp-App");
            }
        }

        /// <summary>
        /// Checks GitHub for the latest release of Curated Content.
        /// </summary>
        public static async Task<CuratedUpdateCheckResult> CheckForUpdatesAsync(HttpClient? httpClient = null, CancellationToken ct = default)
        {
            var client = httpClient ?? DefaultHttpClient;
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            string currentVersion = AppSettingsService.Instance.CuratedContentVersion;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!request.Headers.Contains("User-Agent"))
                {
                    request.Headers.Add("User-Agent", "TableLamp-App");
                }

                using var response = await client.SendAsync(request, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new CuratedUpdateCheckResult
                    {
                        Success = false,
                        CurrentVersion = currentVersion,
                        ErrorMessage = $"Curated content repository not found: '{RepoOwner}/{RepoName}'."
                    };
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return new CuratedUpdateCheckResult
                    {
                        Success = false,
                        CurrentVersion = currentVersion,
                        ErrorMessage = "GitHub API rate limit exceeded. Please try again later."
                    };
                }

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(ct);
                return ParseCuratedReleaseJson(json, currentVersion);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                return new CuratedUpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    ErrorMessage = $"Network error checking curated content: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new CuratedUpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    ErrorMessage = $"Failed checking curated content: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Parses the GitHub release JSON to identify release tag, download asset, and check for update.
        /// </summary>
        public static CuratedUpdateCheckResult ParseCuratedReleaseJson(string json, string currentVersion)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            string title = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
            string? downloadUrl = null;

            // Check assets for a .zip file
            if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElem.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        if (asset.TryGetProperty("browser_download_url", out var dl))
                        {
                            downloadUrl = dl.GetString();
                            break;
                        }
                    }
                }
            }

            // Fallback to zipball_url if no dedicated asset zip was attached
            if (string.IsNullOrWhiteSpace(downloadUrl) && root.TryGetProperty("zipball_url", out var zipballElem))
            {
                downloadUrl = zipballElem.GetString();
            }

            bool isUpdateAvailable = !string.IsNullOrWhiteSpace(tagName) &&
                                     !string.Equals(tagName.Trim(), currentVersion.Trim(), StringComparison.OrdinalIgnoreCase);

            return new CuratedUpdateCheckResult
            {
                Success = true,
                IsUpdateAvailable = isUpdateAvailable,
                CurrentVersion = currentVersion,
                LatestVersion = tagName,
                ReleaseTitle = string.IsNullOrWhiteSpace(title) ? tagName : title,
                ReleaseNotes = body,
                PackageDownloadUrl = downloadUrl
            };
        }

        /// <summary>
        /// Checks for latest release, downloads zip package, decompresses it,
        /// loads into PresetTagDatabase (curated database only), rebuilds the database,
        /// deletes temporary files, and updates recorded version.
        /// </summary>
        public static async Task<CuratedUpdateApplyResult> DownloadAndApplyUpdateAsync(
            string? downloadUrl = null,
            string? releaseTag = null,
            HttpClient? httpClient = null,
            CancellationToken ct = default)
        {
            var client = httpClient ?? DefaultHttpClient;

            // If URL or tag was not provided, check GitHub first
            if (string.IsNullOrWhiteSpace(downloadUrl) || string.IsNullOrWhiteSpace(releaseTag))
            {
                var check = await CheckForUpdatesAsync(client, ct);
                if (!check.Success)
                {
                    return new CuratedUpdateApplyResult
                    {
                        Success = false,
                        ErrorMessage = check.ErrorMessage ?? "Could not check curated updates."
                    };
                }

                if (string.IsNullOrWhiteSpace(check.PackageDownloadUrl))
                {
                    return new CuratedUpdateApplyResult
                    {
                        Success = false,
                        ErrorMessage = "No downloadable .zip package found in latest release."
                    };
                }

                downloadUrl = check.PackageDownloadUrl;
                releaseTag = check.LatestVersion;
            }

            string tempZipPath = Path.Combine(Path.GetTempPath(), $"TableLamp_Curated_{Guid.NewGuid():N}.zip");
            string tempExtractDir = Path.Combine(Path.GetTempPath(), $"TableLamp_Curated_{Guid.NewGuid():N}");

            try
            {
                // 1. Download zip file
                using (var req = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                {
                    if (!req.Headers.Contains("User-Agent"))
                    {
                        req.Headers.Add("User-Agent", "TableLamp-App");
                    }

                    using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                    res.EnsureSuccessStatusCode();

                    using var stream = await res.Content.ReadAsStreamAsync(ct);
                    using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fileStream, ct);
                }

                // 2. Extract package and import into separate Curated database
                return ApplyExtractedPackage(tempZipPath, tempExtractDir, releaseTag);
            }
            catch (Exception ex)
            {
                return new CuratedUpdateApplyResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to download or apply curated update: {ex.Message}"
                };
            }
            finally
            {
                // 3. Delete temporary package and extracted files
                CleanTempFiles(tempZipPath, tempExtractDir);
            }
        }

        /// <summary>
        /// Decompresses the package, imports .json files into the Curated Database (PresetTagDatabase),
        /// rebuilds/saves the database, and updates version metadata.
        /// </summary>
        public static CuratedUpdateApplyResult ApplyExtractedPackage(string zipFilePath, string extractDirectory, string releaseTag)
        {
            try
            {
                if (!File.Exists(zipFilePath))
                {
                    return new CuratedUpdateApplyResult
                    {
                        Success = false,
                        ErrorMessage = "Downloaded zip package not found."
                    };
                }

                if (Directory.Exists(extractDirectory))
                {
                    Directory.Delete(extractDirectory, recursive: true);
                }
                Directory.CreateDirectory(extractDirectory);

                ZipFile.ExtractToDirectory(zipFilePath, extractDirectory, overwriteFiles: true);

                // Import into Curated Database (PresetTagDatabase.Instance) ONLY
                // Custom tags in CustomPresetTagDatabase remain completely separate and untouched.
                var (totalFound, success, failed) = PresetTagDatabase.Instance.ImportDirectory(extractDirectory);

                // Update settings version and timestamp
                AppSettingsService.Instance.CuratedContentVersion = releaseTag;
                AppSettingsService.Instance.CuratedContentLastUpdated = DateTime.UtcNow;
                AppSettingsService.Instance.Save();

                return new CuratedUpdateApplyResult
                {
                    Success = true,
                    VersionApplied = releaseTag,
                    TotalFound = totalFound,
                    FilesImported = success,
                    FailedCount = failed
                };
            }
            catch (Exception ex)
            {
                return new CuratedUpdateApplyResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to extract or import curated presets: {ex.Message}"
                };
            }
        }

        private static void CleanTempFiles(string zipPath, string extractDir)
        {
            try
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
            catch { /* best effort cleanup */ }

            try
            {
                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(extractDir, recursive: true);
                }
            }
            catch { /* best effort cleanup */ }
        }
    }
}
