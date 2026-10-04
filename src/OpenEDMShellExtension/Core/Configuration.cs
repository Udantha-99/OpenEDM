using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenEDMShellExtension.Core
{
    /// <summary>
    /// Reads configuration from a simple text file stored alongside the DLL
    /// or in <c>C:\ProgramData\OpenEDMShellExtension\config.txt</c>.
    ///
    /// The config defines which mapped drives are "source" (server) drives
    /// and where the per-user OpenEDM folder lives.
    ///
    /// <para><b>Config file format:</b></para>
    /// <code>
    /// [SourceDrives]
    /// I
    /// J
    /// Z
    ///
    /// [OpenEDMPath]
    /// A:\{USERNAME}
    /// </code>
    /// </summary>
    public static class Configuration
    {
        // ── Defaults (overridden by config file) ──
        private static readonly HashSet<char> _defaultSourceDrives =
            new HashSet<char> { 'I' };

        private const string DefaultOpenEDMPathTemplate = @"A:\{USERNAME}";

        // ── Cached values (loaded once per explorer.exe session) ──
        private static readonly object _lock = new object();
        private static bool _loaded;
        private static HashSet<char> _sourceDrives;
        private static string _openedmPathTemplate;
        private static string _resolvedOpenEDMPath;
        private static string _auditLogPath;
        private static string _lockPath;
        private static bool _openedmCleanup;

        /// <summary>
        public const string ConfigFileName = "openedm-config.txt";

        // ════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the set of drive letters (uppercase, no colon) that are
        /// considered "source" / server drives. Right-clicking files on
        /// these drives shows "Check-Out to OpenEDM".
        /// </summary>
        public static HashSet<char> SourceDrives
        {
            get { EnsureLoaded(); return _sourceDrives; }
        }

        /// <summary>
        /// Returns the resolved OpenEDM path for the current user,
        /// e.g. <c>A:\IDS-029</c>.
        /// </summary>
        public static string OpenEDMPath
        {
            get { EnsureLoaded(); return _resolvedOpenEDMPath; }
        }

        /// <summary>
        /// Returns the central directory for encrypted audit logs.
        /// Defaults to A:\OpenEDM_Admin_Logs if not specified.
        /// </summary>
        public static string AuditLogPath
        {
            get { EnsureLoaded(); return _auditLogPath; }
        }

        public static string LockPath
        {
            get { EnsureLoaded(); return _lockPath; }
        }

        public static bool OpenEDMCleanup
        {
            get { EnsureLoaded(); return _openedmCleanup; }
        }

        /// <summary>
        /// Checks whether a given file/folder path is on one of the
        /// configured source drives.
        /// </summary>
        public static bool IsOnSourceDrive(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 2 || path[1] != ':')
                return false;

            char drive = char.ToUpperInvariant(path[0]);
            return SourceDrives.Contains(drive);
        }

        /// <summary>
        /// Checks whether a given file/folder path is under the OpenEDM path.
        /// </summary>
        public static bool IsUnderOpenEDMPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string openedm = OpenEDMPath;
            if (string.IsNullOrEmpty(openedm))
                return false;

            // Ensure trailing backslash for prefix match.
            string openedmPrefix = openedm.TrimEnd('\\') + "\\";
            return path.StartsWith(openedmPrefix, StringComparison.OrdinalIgnoreCase)
                || path.Equals(openedm, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Forces a reload of the config file on next access.
        /// Useful after the config is edited.
        /// </summary>
        public static void Reload()
        {
            lock (_lock) { _loaded = false; }
        }

        /// <summary>
        /// Returns all locations searched for the config file, and which one
        /// was loaded. Useful for diagnostics.
        /// </summary>
        public static string GetDiagnostics()
        {
            EnsureLoaded();
            string configPath = FindConfigFilePath();
            return $"Config file: {configPath ?? "(not found — using defaults)"}\n" +
                   $"Source drives: {string.Join(", ", SourceDrives.Select(d => d + ":\\"))}\n" +
                   $"OpenEDM path: {OpenEDMPath}\n" +
                   $"Audit log path: {AuditLogPath}\n" +
                   $"Username: {Environment.UserName}";
        }

        // ════════════════════════════════════════════════════════════════
        //  LOADING
        // ════════════════════════════════════════════════════════════════

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            lock (_lock)
            {
                if (_loaded) return;

                // Start with defaults.
                _sourceDrives = new HashSet<char>(_defaultSourceDrives);
                _openedmPathTemplate = DefaultOpenEDMPathTemplate;
                _auditLogPath = @"A:\OpenEDM_Admin_Logs";
                _lockPath = @"A:\OpenEDM_Locks";
                _openedmCleanup = false;

                // Try to load from file.
                string configPath = FindConfigFilePath();
                if (configPath != null)
                {
                    try
                    {
                        ParseConfigFile(configPath);
                    }
                    catch
                    {
                        // Config is corrupt — keep defaults. Shell extensions
                        // must never crash.
                    }
                }

                // Resolve the {USERNAME} placeholder.
                _resolvedOpenEDMPath = ResolveOpenEDMPath(_openedmPathTemplate);

                _loaded = true;
            }
        }

        /// <summary>
        /// Searches for the config file in these locations (first match wins):
        /// 1. Next to the DLL (for dev/testing)
        /// 2. C:\ProgramData\OpenEDMShellExtension\ (for enterprise deployment)
        /// </summary>
        private static string FindConfigFilePath()
        {
            // 1. Next to the DLL.
            try
            {
                string dllDir = AppDomain.CurrentDomain.BaseDirectory;

                if (!string.IsNullOrEmpty(dllDir))
                {
                    string candidate = Path.Combine(dllDir, ConfigFileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch { /* swallow */ }

            // 2. ProgramData.
            try
            {
                string programData = Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData);
                string candidate = Path.Combine(programData, "OpenEDMShellExtension", ConfigFileName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch { /* swallow */ }

            return null;
        }

        private static void ParseConfigFile(string path)
        {
            string[] lines = File.ReadAllLines(path);
            string currentSection = null;
            var drives = new HashSet<char>();
            bool hasDriveSection = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                // Skip empty lines and comments.
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Section header.
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim().ToUpperInvariant();
                    continue;
                }

                switch (currentSection)
                {
                    case "SOURCEDRIVES":
                        hasDriveSection = true;
                        // Accept "I", "I:", "I:\", etc.
                        char driveLetter = char.ToUpperInvariant(line[0]);
                        if (char.IsLetter(driveLetter))
                            drives.Add(driveLetter);
                        break;

                    case "OpenEDMPATH":
                        // Take the first non-empty line as the template.
                        if (!string.IsNullOrWhiteSpace(line))
                            _openedmPathTemplate = line;
                        break;
                        
                    case "AUDITLOGPATH":
                        if (!string.IsNullOrWhiteSpace(line))
                            _auditLogPath = line;
                        break;

                    case "LOCKPATH":
                        if (!string.IsNullOrWhiteSpace(line))
                            _lockPath = line;
                        break;
                        
                    case "OpenEDMCLEANUP":
                        if (bool.TryParse(line, out bool bCleanup))
                            _openedmCleanup = bCleanup;
                        break;
                }
            }

            if (hasDriveSection && drives.Count > 0)
                _sourceDrives = drives;
        }

        /// <summary>
        /// Replaces placeholders in the OpenEDM path template:
        ///   {USERNAME}    → Environment.UserName  (e.g., IDS-029)
        ///   {USERDOMAIN}  → Environment.UserDomainName
        /// </summary>
        private static string ResolveOpenEDMPath(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return @"A:\" + Environment.UserName;

            string result = template;
            result = result.Replace("{USERNAME}", Environment.UserName);
            result = result.Replace("{USERDOMAIN}", Environment.UserDomainName);

            return result.TrimEnd('\\');
        }
    }
}
