using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenEDMShellExtension.Core;

namespace OpenEDMShellExtension.Logging
{
    /// <summary>
    /// Appends structured audit entries to a <c>_Project_History.csv</c> file
    /// in the project root on the server (Z:\). Thread-safe for concurrent
    /// shell extension invocations.
    /// </summary>
    public static class LogManager
    {
        public const string HistoryFileName = "_Project_History.csv";

        private const string CsvHeader =
            "Timestamp,Username,Action,FileCount,FileNames,Notes";

        /// <summary>
        /// The object used to synchronise writes so concurrent explorer
        /// context-menu invocations don't corrupt the CSV.
        /// </summary>
        private static readonly object _writeLock = new object();

        /// <summary>
        /// Appends a check-in entry to the project history CSV on the server.
        /// <summary>
        /// Appends a check-in entry to the central encrypted audit log on TrueNAS.
        /// </summary>
        /// <param name="serverProjectRoot">
        /// The root directory on Z:\ where the files were pushed (for tracking).
        /// </param>
        /// <param name="pushedFiles">
        /// List of local file paths that were successfully pushed.
        /// </param>
        /// <param name="notes">
        /// The engineer's check-in notes (required by the UI).
        /// </param>
        /// <returns>
        /// Null on success, or a human-readable error string on failure.
        /// </returns>
        public static string AppendCheckInEntry(
            string serverProjectRoot,
            IReadOnlyList<string> pushedFiles,
            string notes,
            string action = "CHECK-IN")
        {
            try
            {
                string logDir = Configuration.AuditLogPath;
                if (string.IsNullOrWhiteSpace(logDir))
                    return "Audit log path is not configured.";

                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                string username = GetCurrentUsername();
                string safeUsername = username.Replace("\\", "_").Replace("/", "_");
                string timestampFile = DateTime.Now.ToString("yyyyMMddHHmmss");
                string logFile = Path.Combine(logDir, $"audit_{safeUsername}_{timestampFile}_{Guid.NewGuid()}.json");

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                int fileCount = pushedFiles?.Count ?? 0;
                string fileNames = pushedFiles != null && pushedFiles.Count > 0
                    ? string.Join("; ", pushedFiles)
                    : "(none)";

                // Construct a raw JSON string for the payload
                string rawJson = string.Join(",", 
                    "\"Timestamp\":\"" + EscapeJson(timestamp) + "\"",
                    "\"Username\":\"" + EscapeJson(username) + "\"",
                    "\"Action\":\"" + EscapeJson(action) + "\"",
                    "\"Project\":\"" + EscapeJson(serverProjectRoot) + "\"",
                    "\"FileCount\":" + fileCount,
                    "\"FileNames\":\"" + EscapeJson(fileNames) + "\"",
                    "\"Notes\":\"" + EscapeJson(notes ?? string.Empty) + "\""
                );
                rawJson = "{" + rawJson + "}";

                string encryptedPayload = CryptoHelper.EncryptLogEntry(rawJson);

                File.WriteAllText(logFile, encryptedPayload, Encoding.UTF8);
                return null;
            }
            catch (Exception ex)
            {
                return $"Failed to write encrypted audit log: {ex.Message}";
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
        private static string GetCurrentUsername()
        {
            try
            {
                // Returns DOMAIN\Username for domain-joined machines.
                return Environment.UserDomainName + @"\" + Environment.UserName;
            }
            catch
            {
                return Environment.UserName;
            }
        }
    }
}
