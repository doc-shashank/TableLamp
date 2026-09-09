using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TableLamp.Services
{
    public class AppUpdateResult
    {
        public bool Success { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseTitle { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service responsible for checking application updates against GitHub Releases
    /// using zero-API web redirects (HTTP 302) to bypass REST API rate limits completely.
    /// </summary>
    public static class AppUpdateService
    {
        // =========================================================================
        // GITHUB REPOSITORY CONFIGURATION FOR APP UPDATES
        // =========================================================================
        public static string RepoOwner { get; set; } = "doc-shashank";
        public static string RepoName { get; set; } = "table-lamp";

        private static readonly (string VersionStr, Version Ver) DetectedVersion = ResolveVersion();

        public static string CurrentVersionString => DetectedVersion.VersionStr;
        public static Version CurrentVersion => DetectedVersion.Ver;

        private static (string VersionStr, Version Ver) ResolveVersion()
        {
            // 1. Primary: pull version directly from TableLamp.csproj dynamically as requested
            try
            {
                string? current = AppContext.BaseDirectory;
                for (int i = 0; i < 7 && !string.IsNullOrEmpty(current); i++)
                {
                    string csproj = Path.Combine(current, "TableLamp.csproj");
                    if (File.Exists(csproj))
                    {
                        string content = File.ReadAllText(csproj);
                        var m = Regex.Match(content, @"<Version>([^<]+)</Version>");
                        if (m.Success)
                        {
                            string vStr = m.Groups[1].Value.Trim();
                            if (Version.TryParse(vStr, out var pv))
                            {
                                return (vStr, pv);
                            }
                        }
                    }
                    current = Directory.GetParent(current)?.FullName;
                }
            }
            catch { }

            // 2. Secondary: assembly metadata when running deployed unpackaged app
            try
            {
                var asm = typeof(AppUpdateService).Assembly;
                var infoAttr = (System.Reflection.AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                string? raw = infoAttr?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    int plusIdx = raw.IndexOf('+');
                    if (plusIdx >= 0) raw = raw.Substring(0, plusIdx);
                    raw = raw.Trim().TrimStart('v', 'V');
                    if (Version.TryParse(raw, out var v1) && v1 > new Version(0, 0, 0, 0))
                    {
                        return (raw, v1);
                    }
                }

                var asmVer = asm.GetName().Version;
                if (asmVer != null && asmVer > new Version(0, 0, 0, 0))
                {
                    string vStr = $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}.{Math.Max(0, asmVer.Revision)}";
                    return (vStr, asmVer);
                }
            }
            catch { }

            return ("0.0.8.0", new Version(0, 0, 8, 0));
        }

        // Non-redirecting client to intercept the 302 redirect Location header without downloading pages
        private static readonly HttpClient DefaultRedirectInterceptorClient;
        private static readonly SemaphoreSlim CheckLock = new(1, 1);

        // Strict tag format validator to prevent path traversal or URL manipulation attacks
        private static readonly Regex SafeTagPattern = new(@"^[a-zA-Z0-9._\-+]+$", RegexOptions.Compiled);

        static AppUpdateService()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };
            DefaultRedirectInterceptorClient = new HttpClient(handler);
            DefaultRedirectInterceptorClient.DefaultRequestHeaders.Add("User-Agent", "TableLamp-App");
        }

        /// <summary>
        /// Compares two version strings (ignoring leading 'v' or 'V').
        /// Returns > 0 if versionA > versionB, < 0 if versionA < versionB, or 0 if equal.
        /// </summary>
        public static int CompareVersions(string versionA, string versionB)
        {
            var parsedA = ParseVersion(versionA);
            var parsedB = ParseVersion(versionB);
            return parsedA.CompareTo(parsedB);
        }

        /// <summary>
        /// Parses a semver / quad-dot version string into a System.Version instance.
        /// </summary>
        public static Version ParseVersion(string rawVersion)
        {
            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return new Version(0, 0, 0, 0);
            }

            string clean = rawVersion.Trim();
            if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(1);
            }

            // Strip any build metadata or pre-release suffix (e.g. 0.0.7-beta)
            int dashIdx = clean.IndexOf('-');
            if (dashIdx >= 0) clean = clean.Substring(0, dashIdx);

            int plusIdx = clean.IndexOf('+');
            if (plusIdx >= 0) clean = clean.Substring(0, plusIdx);

            string[] parts = clean.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int v0) ? v0 : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int v1) ? v1 : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out int v2) ? v2 : 0;
            int revision = parts.Length > 3 && int.TryParse(parts[3], out int v3) ? v3 : 0;

            return new Version(major, minor, build, revision);
        }

        /// <summary>
        /// Validates that a release tag string is well-formed and safe against path traversal.
        /// </summary>
        public static bool IsValidTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            string trimmed = tag.Trim();
            if (trimmed.Length > 128) return false;
            if (trimmed.Contains("..") || trimmed.Contains('/') || trimmed.Contains('\\')) return false;
            return SafeTagPattern.IsMatch(trimmed);
        }

        /// <summary>
        /// Validates that a URI uses HTTPS and points to an authentic GitHub domain.
        /// </summary>
        public static bool IsTrustedGitHubUrl(Uri? uri)
        {
            if (uri == null) return false;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;

            string host = uri.Host.ToLowerInvariant();
            return host == "github.com" ||
                   host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
                   host == "objects.githubusercontent.com" ||
                   host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Mathematically constructs the direct release page URL without API calls.
        /// </summary>
        public static string BuildReleaseUrl(string owner, string repo, string tag)
        {
            return $"https://github.com/{owner}/{repo}/releases/tag/{tag}";
        }

        /// <summary>
        /// Mathematically constructs the direct CDN asset download URL without API calls.
        /// Blueprint: https://github.com/{owner}/{repo}/releases/download/{version}/{filename}
        /// </summary>
        public static string BuildAssetDownloadUrl(string owner, string repo, string tag, string filename)
        {
            return $"https://github.com/{owner}/{repo}/releases/download/{tag}/{filename}";
        }

        /// <summary>
        /// Extracts the release tag from a GitHub release URL (e.g. .../releases/tag/{tag}).
        /// Validates that the tag is safe against injection or path traversal.
        /// </summary>
        public static string? ExtractTagFromReleaseUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            int tagIdx = url.IndexOf("/releases/tag/", StringComparison.OrdinalIgnoreCase);
            if (tagIdx < 0) return null;

            string tagPart = url.Substring(tagIdx + "/releases/tag/".Length);

            // Strip trailing slash, query parameters, or fragments
            int delimiterIdx = tagPart.IndexOfAny(new[] { '/', '?', '#' });
            if (delimiterIdx >= 0)
            {
                tagPart = tagPart.Substring(0, delimiterIdx);
            }

            string decoded = Uri.UnescapeDataString(tagPart.Trim());
            return IsValidTag(decoded) ? decoded : null;
        }

        /// <summary>
        /// Extracts the release tag from an HTTP response, checking the 302 Location header
        /// or the final RequestUri if auto-redirect was active.
        /// </summary>
        public static string? ExtractTagFromResponse(HttpResponseMessage response)
        {
            // 1. Check Location header (when redirect is intercepted)
            if (response.Headers.Location != null)
            {
                var loc = response.Headers.Location;
                if (!loc.IsAbsoluteUri || IsTrustedGitHubUrl(loc))
                {
                    string? tag = ExtractTagFromReleaseUrl(loc.ToString());
                    if (!string.IsNullOrWhiteSpace(tag)) return tag;
                }
            }

            // 2. Check final RequestUri (when redirect was followed)
            if (response.RequestMessage?.RequestUri != null)
            {
                var reqUri = response.RequestMessage.RequestUri;
                if (IsTrustedGitHubUrl(reqUri))
                {
                    string? tag = ExtractTagFromReleaseUrl(reqUri.ToString());
                    if (!string.IsNullOrWhiteSpace(tag)) return tag;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks GitHub for the latest release of Table Lamp using the zero-API 302 redirect trick.
        /// Guaranteed zero REST API calls and zero rate limit restrictions.
        /// </summary>
        public static async Task<AppUpdateResult> CheckForUpdatesAsync(HttpClient? httpClient = null, CancellationToken ct = default)
        {
            // Concurrency guard: ignore duplicate burst calls if already checking
            if (!await CheckLock.WaitAsync(0, ct))
            {
                return new AppUpdateResult
                {
                    Success = false,
                    CurrentVersion = CurrentVersionString,
                    ErrorMessage = "An update check is already in progress. Please wait a moment."
                };
            }

            var client = httpClient ?? DefaultRedirectInterceptorClient;
            string pingUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15)); // 15-second timeout

            try
            {
                // Send HEAD request (0-byte payload, fastest possible response)
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
                    // Fallback to GET if some network proxies intercept or disallow HEAD
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
                        return new AppUpdateResult
                        {
                            Success = false,
                            CurrentVersion = CurrentVersionString,
                            ErrorMessage = $"Release repository not found: '{RepoOwner}/{RepoName}'. Please check your settings."
                        };
                    }

                    string? latestTag = ExtractTagFromResponse(response);

                    if (string.IsNullOrWhiteSpace(latestTag))
                    {
                        // Repository exists, but no tagged release has been published yet
                        return new AppUpdateResult
                        {
                            Success = true,
                            IsUpdateAvailable = false,
                            CurrentVersion = CurrentVersionString,
                            LatestVersion = CurrentVersionString,
                            ReleaseTitle = "No releases published",
                            ReleaseUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases"
                        };
                    }

                    var latestParsed = ParseVersion(latestTag);
                    bool isUpdateAvailable = latestParsed > CurrentVersion;
                    string releasePage = BuildReleaseUrl(RepoOwner, RepoName, latestTag);
                    string downloadUrl = BuildAssetDownloadUrl(RepoOwner, RepoName, latestTag, $"TableLamp-Setup-{latestTag}.exe");

                    return new AppUpdateResult
                    {
                        Success = true,
                        IsUpdateAvailable = isUpdateAvailable,
                        CurrentVersion = CurrentVersionString,
                        LatestVersion = latestTag,
                        ReleaseTitle = $"Table Lamp {latestTag}",
                        ReleaseUrl = releasePage,
                        DownloadUrl = downloadUrl
                    };
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new AppUpdateResult
                {
                    Success = false,
                    CurrentVersion = CurrentVersionString,
                    ErrorMessage = "Update check timed out after 15 seconds. Please check your internet connection."
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                return new AppUpdateResult
                {
                    Success = false,
                    CurrentVersion = CurrentVersionString,
                    ErrorMessage = $"Network error while checking for updates: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new AppUpdateResult
                {
                    Success = false,
                    CurrentVersion = CurrentVersionString,
                    ErrorMessage = $"Failed to check for updates: {ex.Message}"
                };
            }
            finally
            {
                CheckLock.Release();
            }
        }

        /// <summary>
        /// Backward-compatible legacy parser for tests or fallback scenarios.
        /// </summary>
        [Obsolete("Use zero-API redirect checking instead.")]
        public static AppUpdateResult ParseReleaseJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                string title = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";
                DateTime? publishedAt = null;
                if (root.TryGetProperty("published_at", out var pubElem) && pubElem.TryGetDateTime(out var dt))
                {
                    publishedAt = dt;
                }

                var latestParsed = ParseVersion(tagName);
                bool isUpdateAvailable = latestParsed > CurrentVersion;

                return new AppUpdateResult
                {
                    Success = true,
                    IsUpdateAvailable = isUpdateAvailable,
                    CurrentVersion = CurrentVersionString,
                    LatestVersion = string.IsNullOrWhiteSpace(tagName) ? "0.0.0.0" : tagName,
                    ReleaseTitle = string.IsNullOrWhiteSpace(title) ? tagName : title,
                    ReleaseNotes = body,
                    ReleaseUrl = htmlUrl,
                    PublishedAt = publishedAt
                };
            }
            catch (Exception ex)
            {
                return new AppUpdateResult
                {
                    Success = false,
                    CurrentVersion = CurrentVersionString,
                    ErrorMessage = $"Invalid release data format: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Downloads the installer package from the specified GitHub download URL,
        /// reporting percentage progress via the provided IProgress callback.
        /// </summary>
        public static async Task<bool> DownloadInstallerAsync(
            string downloadUrl,
            string destinationPath,
            IProgress<double>? progress = null,
            HttpClient? httpClient = null,
            CancellationToken ct = default)
        {
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || !IsTrustedGitHubUrl(uri))
            {
                return false;
            }

            var client = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
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
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

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
    }
}
