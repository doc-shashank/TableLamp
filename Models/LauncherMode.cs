using System;

namespace TableLamp.Models
{
    /// <summary>
    /// Constants and helper for launcher invocation modes.
    /// </summary>
    public static class LauncherMode
    {
        public const string Basic = "Basic";
        public const string Advanced = "Advanced";
        public const string Generator = "Generator";

        /// <summary>
        /// Validates and normalizes launcher mode arguments.
        /// </summary>
        public static string Normalize(string? argument)
        {
            if (string.IsNullOrWhiteSpace(argument)) return Basic;

            string clean = argument.Trim();
            if (clean.Equals(Basic, StringComparison.OrdinalIgnoreCase)) return Basic;
            if (clean.Equals(Advanced, StringComparison.OrdinalIgnoreCase)) return Advanced;
            if (clean.Equals(Generator, StringComparison.OrdinalIgnoreCase)) return Generator;

            return Basic;
        }
    }
}
