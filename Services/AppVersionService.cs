using System;
using System.IO;
using System.Text.RegularExpressions;

namespace TableLamp.Services
{
    /// <summary>
    /// Service providing application version information and version comparisons.
    /// Decoupled from any network or auto-updater mechanisms.
    /// </summary>
    public static class AppVersionService
    {
        private static readonly (string VersionStr, Version Ver) DetectedVersion = ResolveVersion();

        public static string CurrentVersionString => DetectedVersion.VersionStr;
        public static Version CurrentVersion => DetectedVersion.Ver;

        private static (string VersionStr, Version Ver) ResolveVersion()
        {
            // 1. Primary: pull version directly from TableLamp.csproj dynamically
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
                var asm = typeof(AppVersionService).Assembly;
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

            return ("0.0.8.2", new Version(0, 0, 8, 2));
        }

        public static Version ParseVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString)) return new Version(0, 0, 0, 0);

            string clean = versionString.Trim().TrimStart('v', 'V');
            int hyphenIdx = clean.IndexOf('-');
            if (hyphenIdx > 0)
            {
                clean = clean.Substring(0, hyphenIdx);
            }

            var parts = clean.Split('.');
            if (parts.Length == 1 && int.TryParse(parts[0], out int major))
            {
                return new Version(major, 0, 0, 0);
            }
            if (parts.Length == 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out int minor))
            {
                return new Version(major, minor, 0, 0);
            }
            if (parts.Length == 3 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor) && int.TryParse(parts[2], out int build))
            {
                return new Version(major, minor, build, 0);
            }
            if (parts.Length >= 4 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor) && int.TryParse(parts[2], out build) && int.TryParse(parts[3], out int rev))
            {
                return new Version(major, minor, build, rev);
            }

            if (Version.TryParse(clean, out var parsed))
            {
                return parsed;
            }

            return new Version(0, 0, 0, 0);
        }

        public static int CompareVersions(string v1, string v2)
        {
            var parsed1 = ParseVersion(v1);
            var parsed2 = ParseVersion(v2);
            return parsed1.CompareTo(parsed2);
        }
    }
}
