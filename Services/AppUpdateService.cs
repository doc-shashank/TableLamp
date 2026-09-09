using System;
using System.Net.Http;
using System.Text.Json;
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
        public DateTime? PublishedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service responsible for checking application updates against GitHub Releases.
    /// </summary>
    public static class AppUpdateService
    {
        // =========================================================================
        // GITHUB REPOSITORY CONFIGURATION FOR APP UPDATES
        // (Edit the owner and repository name below to target your GitHub repo)
        // =========================================================================
        public static string RepoOwner { get; set; } = "doc-shashank";
        public static string RepoName { get; set; } = "table-lamp";

        public static readonly string CurrentVersionString = "0.0.7.0";
        public static readonly Version CurrentVersion = new(0, 0, 7, 0);

        private static readonly HttpClient DefaultHttpClient = new();

        static AppUpdateService()
        {
            if (!DefaultHttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                DefaultHttpClient.DefaultRequestHeaders.Add("User-Agent", "TableLamp-App");
            }
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
        /// Checks GitHub repository releases for the latest version of Table Lamp.
        /// </summary>
        public static async Task<AppUpdateResult> CheckForUpdatesAsync(HttpClient? httpClient = null, CancellationToken ct = default)
        {
            var client = httpClient ?? DefaultHttpClient;
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

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
                    return new AppUpdateResult
                    {
                        Success = false,
                        CurrentVersion = CurrentVersionString,
                        ErrorMessage = $"Release repository not found: '{RepoOwner}/{RepoName}'. Please check the repository settings."
                    };
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return new AppUpdateResult
                    {
                        Success = false,
                        CurrentVersion = CurrentVersionString,
                        ErrorMessage = "GitHub API rate limit exceeded or access denied. Please try again later."
                    };
                }

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(ct);
                return ParseReleaseJson(json);
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
        }

        /// <summary>
        /// Parses the GitHub latest release JSON payload into an AppUpdateResult.
        /// </summary>
        public static AppUpdateResult ParseReleaseJson(string json)
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
    }
}
