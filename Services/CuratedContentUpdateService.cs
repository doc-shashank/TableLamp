using System;
using System.IO;
using System.IO.Compression;
using System.Net;
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
        public string? FallbackDownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class CuratedUpdateApplyResult
    {
        public bool Success { get; set; }
        public bool AlreadyUpToDate { get; set; }
        public string VersionApplied { get; set; } = string.Empty;
        public int TotalFound { get; set; }
        public int FilesImported { get; set; }
        public int FailedCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service responsible for checking, downloading, decompressing, and building the Curated Tags database
    /// from GitHub Releases using zero-API web redirects and direct CDN download URLs.
    /// NOTE: Loads strictly into PresetTagDatabase (curated database) and never touches CustomPresetTagDatabase.
    /// </summary>
    public static class CuratedContentUpdateService
    {
        // =========================================================================
        // GITHUB REPOSITORY CONFIGURATION FOR CURATED CONTENT
        // =========================================================================
        public static string RepoOwner { get; set; } = "doc-shashank";
        public static string RepoName { get; set; } = "table-lamp-curated-tags";

        // Security limits to guard against Zip Bomb attacks
        public const long MaxUncompressedBytes = 100 * 1024 * 1024; // 100 MB
        public const int MaxArchiveEntries = 1000;                  // 1,000 files

        private static readonly HttpClient DefaultRedirectInterceptorClient;
        private static readonly HttpClient DefaultDownloadClient;
        private static readonly SemaphoreSlim SyncLock = new(1, 1);

        static CuratedContentUpdateService()
        {
            var interceptorHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };
            DefaultRedirectInterceptorClient = new HttpClient(interceptorHandler);
            DefaultRedirectInterceptorClient.DefaultRequestHeaders.Add("User-Agent", "TableLamp-App");

            // Download client allows redirects (GitHub redirects asset downloads to AWS S3 or codeload)
            var downloadHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            DefaultDownloadClient = new HttpClient(downloadHandler);
            DefaultDownloadClient.DefaultRequestHeaders.Add("User-Agent", "TableLamp-App");
        }

        /// <summary>
        /// Mathematically constructs the direct tag source archive URL without querying any API.
        /// Blueprint: https://github.com/{owner}/{repo}/archive/refs/tags/{version}.zip
        /// </summary>
        public static string BuildArchiveDownloadUrl(string owner, string repo, string tag)
        {
            return $"https://github.com/{owner}/{repo}/archive/refs/tags/{tag}.zip";
        }

        /// <summary>
        /// Mathematically constructs the direct release asset download URL without querying any API.
        /// Blueprint: https://github.com/{owner}/{repo}/releases/download/{version}/{filename}
        /// </summary>
        public static string BuildAssetDownloadUrl(string owner, string repo, string tag, string filename)
        {
            return $"https://github.com/{owner}/{repo}/releases/download/{tag}/{filename}";
        }

        /// <summary>
        /// Checks GitHub for the latest release of Curated Content using the zero-API 302 redirect trick.
        /// Thread-safe and rate-limit-free.
        /// </summary>
        public static async Task<CuratedUpdateCheckResult> CheckForUpdatesAsync(HttpClient? httpClient = null, CancellationToken ct = default)
        {
            var client = httpClient ?? DefaultRedirectInterceptorClient;
            string pingUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";
            string currentVersion = AppSettingsService.Instance.CuratedContentVersion;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, pingUrl);
                if (!request.Headers.Contains("User-Agent"))
                {
                    request.Headers.Add("User-Agent", "TableLamp-App");
                }

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, cts.Token);
                }
                catch (HttpRequestException)
                {
                    // Fallback to GET if network proxy restricts HEAD
                    using var getReq = new HttpRequestMessage(HttpMethod.Get, pingUrl);
                    if (!getReq.Headers.Contains("User-Agent"))
                    {
                        getReq.Headers.Add("User-Agent", "TableLamp-App");
                    }
                    response = await client.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                }

                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return new CuratedUpdateCheckResult
                        {
                            Success = false,
                            CurrentVersion = currentVersion,
                            ErrorMessage = $"Curated content repository not found: '{RepoOwner}/{RepoName}'."
                        };
                    }

                    string? latestTag = AppUpdateService.ExtractTagFromResponse(response);

                    if (string.IsNullOrWhiteSpace(latestTag))
                    {
                        return new CuratedUpdateCheckResult
                        {
                            Success = true,
                            IsUpdateAvailable = false,
                            CurrentVersion = currentVersion,
                            LatestVersion = currentVersion,
                            ReleaseTitle = "No curated releases published",
                            ErrorMessage = null
                        };
                    }

                    bool isDbEmpty = PresetTagDatabase.Instance.IsEmpty;
                    bool isNewerVersion = AppUpdateService.CompareVersions(latestTag, currentVersion) > 0;
                    bool isUpdateAvailable = isDbEmpty || isNewerVersion;
                    string assetUrl = BuildAssetDownloadUrl(RepoOwner, RepoName, latestTag, $"{RepoName}.zip");
                    string archiveUrl = BuildArchiveDownloadUrl(RepoOwner, RepoName, latestTag);

                    return new CuratedUpdateCheckResult
                    {
                        Success = true,
                        IsUpdateAvailable = isUpdateAvailable,
                        CurrentVersion = currentVersion,
                        LatestVersion = latestTag,
                        ReleaseTitle = $"Curated Tags {latestTag}",
                        PackageDownloadUrl = assetUrl,
                        FallbackDownloadUrl = archiveUrl
                    };
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new CuratedUpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    ErrorMessage = "Curated content check timed out after 15 seconds."
                };
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
        /// Checks for latest release, downloads zip package directly via CDN (zero API calls),
        /// safely decompresses with Zip Slip & Zip Bomb protection, imports into PresetTagDatabase,
        /// rebuilds the database, deletes temporary files, and updates recorded version.
        /// Thread-safe with SemaphoreSlim concurrency lock.
        /// </summary>
        public static async Task<CuratedUpdateApplyResult> DownloadAndApplyUpdateAsync(
            string? downloadUrl = null,
            string? releaseTag = null,
            HttpClient? httpClient = null,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            // Concurrency guard: ignore duplicate calls while a sync is in progress
            if (!await SyncLock.WaitAsync(0, ct))
            {
                return new CuratedUpdateApplyResult
                {
                    Success = false,
                    ErrorMessage = "A curated content synchronization is already in progress. Please wait."
                };
            }

            var downloadClient = httpClient ?? DefaultDownloadClient;

            try
            {
                string? fallbackUrl = null;

                // If URL or tag was not provided, perform zero-API version check first
                if (string.IsNullOrWhiteSpace(downloadUrl) || string.IsNullOrWhiteSpace(releaseTag))
                {
                    var check = await CheckForUpdatesAsync(downloadClient, ct);
                    if (!check.Success)
                    {
                        return new CuratedUpdateApplyResult
                        {
                            Success = false,
                            ErrorMessage = check.ErrorMessage ?? "Could not check curated updates."
                        };
                    }

                    downloadUrl = check.PackageDownloadUrl;
                    fallbackUrl = check.FallbackDownloadUrl;
                    releaseTag = check.LatestVersion;

                    if (!check.IsUpdateAvailable)
                    {
                        return new CuratedUpdateApplyResult
                        {
                            Success = true,
                            AlreadyUpToDate = true,
                            VersionApplied = check.CurrentVersion,
                            ErrorMessage = null
                        };
                    }
                }

                string currentVer = AppSettingsService.Instance.CuratedContentVersion;
                if (!PresetTagDatabase.Instance.IsEmpty && !string.IsNullOrWhiteSpace(releaseTag) && AppUpdateService.CompareVersions(releaseTag, currentVer) <= 0)
                {
                    return new CuratedUpdateApplyResult
                    {
                        Success = true,
                        AlreadyUpToDate = true,
                        VersionApplied = currentVer,
                        ErrorMessage = null
                    };
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return new CuratedUpdateApplyResult
                    {
                        Success = false,
                        ErrorMessage = "No download URL available for curated content."
                    };
                }

                string tempZipPath = Path.Combine(Path.GetTempPath(), $"TableLamp_Curated_{Guid.NewGuid():N}.zip");
                string tempExtractDir = Path.Combine(Path.GetTempPath(), $"TableLamp_Curated_{Guid.NewGuid():N}");

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30s timeout for package download

                    bool downloaded = false;

                    // 1. Try downloading primary asset URL
                    try
                    {
                        downloaded = await TryDownloadFileAsync(downloadClient, downloadUrl, tempZipPath, progress, cts.Token);
                    }
                    catch { /* Fallback below */ }

                    // 2. If primary asset not found, try fallback archive URL (archive/refs/tags/{tag}.zip)
                    if (!downloaded && !string.IsNullOrWhiteSpace(fallbackUrl))
                    {
                        downloaded = await TryDownloadFileAsync(downloadClient, fallbackUrl, tempZipPath, progress, cts.Token);
                    }

                    if (!downloaded || !File.Exists(tempZipPath))
                    {
                        return new CuratedUpdateApplyResult
                        {
                            Success = false,
                            ErrorMessage = "Failed to download curated package from GitHub CDN."
                        };
                    }

                    // 3. Extract safely with Zip Slip and Zip Bomb guards, importing strictly into Curated database
                    return ApplyExtractedPackage(tempZipPath, tempExtractDir, releaseTag);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    return new CuratedUpdateApplyResult
                    {
                        Success = false,
                        ErrorMessage = "Package download timed out after 30 seconds."
                    };
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
                    // 4. Clean up temporary files
                    CleanTempFiles(tempZipPath, tempExtractDir);
                }
            }
            finally
            {
                SyncLock.Release();
            }
        }

        private static async Task<bool> TryDownloadFileAsync(HttpClient client, string url, string destinationPath, IProgress<double>? progress, CancellationToken ct)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !AppUpdateService.IsTrustedGitHubUrl(uri))
            {
                return false;
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!req.Headers.Contains("User-Agent"))
            {
                req.Headers.Add("User-Agent", "TableLamp-App");
            }

            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
            {
                return false;
            }

            long totalBytes = res.Content.Headers.ContentLength ?? -1;
            using (var stream = await res.Content.ReadAsStreamAsync(ct))
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    if (totalBytes > 0)
                    {
                        double pct = (double)totalRead / totalBytes * 100.0;
                        progress?.Report(Math.Min(100.0, pct));
                    }
                }
            }

            var fileInfo = new FileInfo(destinationPath);
            return fileInfo.Length > 0;
        }

        /// <summary>
        /// Decompresses the package safely with Zip Slip and Zip Bomb mitigations,
        /// imports .json files into the Curated Database (PresetTagDatabase),
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

                // Safe Zip Extraction (Zip Slip, Zip Bomb, and file extension filtering)
                SafeExtractZipArchive(zipFilePath, extractDirectory);

                // Import into Curated Database (PresetTagDatabase.Instance) ONLY.
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

        /// <summary>
        /// Safely extracts a zip archive with:
        /// 1. Zip Slip mitigation: ensures all target paths normalize strictly within the destination directory.
        /// 2. Zip Bomb mitigation: enforces entry count and uncompressed byte limits.
        /// 3. File Whitelisting: only extracts .json files; strictly ignores executables, scripts, or binary files.
        /// </summary>
        public static void SafeExtractZipArchive(string zipFilePath, string destinationDirectory)
        {
            string fullDestDir = Path.GetFullPath(destinationDirectory);
            if (!fullDestDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullDestDir += Path.DirectorySeparatorChar;
            }

            using var archive = ZipFile.OpenRead(zipFilePath);
            if (archive.Entries.Count > MaxArchiveEntries)
            {
                throw new InvalidOperationException($"Zip archive contains {archive.Entries.Count} entries, exceeding maximum limit of {MaxArchiveEntries}.");
            }

            long totalUncompressedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                // Zip Bomb check
                totalUncompressedBytes += entry.Length;
                if (totalUncompressedBytes > MaxUncompressedBytes)
                {
                    throw new InvalidOperationException($"Total uncompressed content exceeds maximum allowable size of {MaxUncompressedBytes} bytes.");
                }

                // Zip Slip Path Traversal validation
                string entryDestination = Path.GetFullPath(Path.Combine(fullDestDir, entry.FullName));
                if (!entryDestination.StartsWith(fullDestDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Zip Slip traversal attempt detected in entry: '{entry.FullName}'.");
                }

                // Directory entry
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(entryDestination);
                    continue;
                }

                // File Whitelist: strictly allow .json preset files.
                // Discard any executable, script, or unknown payload (.exe, .dll, .bat, .cmd, .ps1, .vbs, .js, .lnk, etc.)
                string extension = Path.GetExtension(entry.Name);
                if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? dir = Path.GetDirectoryName(entryDestination);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                entry.ExtractToFile(entryDestination, overwrite: true);
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

        /// <summary>
        /// Backward-compatible legacy parser for tests.
        /// </summary>
        [Obsolete("Use zero-API redirect checking instead.")]
        public static CuratedUpdateCheckResult ParseCuratedReleaseJson(string json, string currentVersion)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                string title = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
                string? downloadUrl = null;

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
                    PackageDownloadUrl = downloadUrl,
                    FallbackDownloadUrl = BuildArchiveDownloadUrl(RepoOwner, RepoName, tagName)
                };
            }
            catch (Exception ex)
            {
                return new CuratedUpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = currentVersion,
                    ErrorMessage = $"Invalid release format: {ex.Message}"
                };
            }
        }
    }
}
