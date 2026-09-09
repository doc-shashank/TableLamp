using System;
using System.Linq;
using System.Net.Http.Headers;

namespace TableLamp.Services
{
    /// <summary>
    /// Tracks GitHub API rate limit usage across the app.
    /// (NOTE: THIS WILL BE REMOVED IN FUTURE VERSIONS)
    /// </summary>
    public static class GitHubRateLimitTracker
    {
        public static int RequestsUsedPastHour { get; set; } = 0;
        public static int RequestsRemaining { get; set; } = 60;
        public static int RateLimitMax { get; set; } = 60;
        public static DateTime? ResetTime { get; set; }

        public static event Action? RateLimitUpdated;

        public static void RecordResponseHeaders(HttpResponseHeaders headers)
        {
            if (headers == null) return;

            bool changed = false;

            if (headers.TryGetValues("X-RateLimit-Used", out var usedVals) &&
                int.TryParse(usedVals.FirstOrDefault(), out int used))
            {
                RequestsUsedPastHour = used;
                changed = true;
            }

            if (headers.TryGetValues("X-RateLimit-Remaining", out var remVals) &&
                int.TryParse(remVals.FirstOrDefault(), out int rem))
            {
                RequestsRemaining = rem;
                changed = true;
            }

            if (headers.TryGetValues("X-RateLimit-Limit", out var limitVals) &&
                int.TryParse(limitVals.FirstOrDefault(), out int limit))
            {
                RateLimitMax = limit;
                changed = true;
            }

            if (headers.TryGetValues("X-RateLimit-Reset", out var resetVals) &&
                long.TryParse(resetVals.FirstOrDefault(), out long resetEpoch))
            {
                ResetTime = DateTimeOffset.FromUnixTimeSeconds(resetEpoch).LocalDateTime;
                changed = true;
            }

            if (changed)
            {
                RateLimitUpdated?.Invoke();
            }
        }

        public static string GetStatusText()
        {
            if (ResetTime.HasValue)
            {
                return $"GitHub Requests (past hour): {RequestsUsedPastHour}/{RateLimitMax} ({RequestsRemaining} remaining, resets at {ResetTime.Value:HH:mm:ss})";
            }
            return $"GitHub Requests (past hour): {RequestsUsedPastHour}/{RateLimitMax}";
        }
    }
}
