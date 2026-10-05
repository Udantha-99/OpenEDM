using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenEDMShellExtension.Core
{
    /// <summary>
    /// Manages the ".sourcepath.txt" breadcrumb files that track the origin
    /// of checked-out directories. This is the single source of truth for
    /// mapping C:\OpenEDM paths back to their Z:\CLIENTS origin.
    /// </summary>
    public static class BreadcrumbTracker
    {
        /// <summary>
        /// The hidden breadcrumb file name placed at the root of every checked-out directory.
        /// </summary>
        public const string BreadcrumbFileName = ".sourcepath.txt";

        /// <summary>
        /// Writes a breadcrumb file into <paramref name="localDirectory"/> recording
        /// the original server path. The file is marked Hidden+System so users
        /// don't accidentally delete it.
        /// </summary>
        /// <param name="localDirectory">The C:\OpenEDM subdirectory that was checked out.</param>
        /// <param name="serverSourcePath">The full Z:\ path the directory was copied from.</param>
        public static void WriteBreadcrumb(string localDirectory, string serverSourcePath)
        {
            if (string.IsNullOrWhiteSpace(localDirectory))
                throw new ArgumentNullException(nameof(localDirectory));
            if (string.IsNullOrWhiteSpace(serverSourcePath))
                throw new ArgumentNullException(nameof(serverSourcePath));

            string breadcrumbPath = Path.Combine(localDirectory, BreadcrumbFileName);

            if (File.Exists(breadcrumbPath))
            {
                var attrs = File.GetAttributes(breadcrumbPath);
                File.SetAttributes(breadcrumbPath, attrs & ~(FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly));
            }
            File.WriteAllText(breadcrumbPath, serverSourcePath.TrimEnd('\\') + Environment.NewLine);

            // Mark hidden + system so it stays out of the user's way.
            File.SetAttributes(breadcrumbPath, FileAttributes.Hidden | FileAttributes.System);
        }

        /// <summary>
        /// Reads the server origin path from a breadcrumb file in the specified directory.
        /// Returns null if the breadcrumb does not exist or is empty/corrupt.
        /// </summary>
        public static string ReadBreadcrumb(string localDirectory)
        {
            if (string.IsNullOrWhiteSpace(localDirectory))
                return null;

            string breadcrumbPath = Path.Combine(localDirectory, BreadcrumbFileName);

            if (!File.Exists(breadcrumbPath))
                return null;

            try
            {
                string content = File.ReadAllText(breadcrumbPath).Trim();
                return string.IsNullOrEmpty(content) ? null : content;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Walks up the directory tree from <paramref name="startPath"/> looking for the
        /// nearest directory containing a breadcrumb file. Returns null if none is found
        /// or if we leave the C:\OpenEDM tree.
        /// </summary>
        /// <param name="startPath">
        /// A file or directory path somewhere inside C:\OpenEDM.
        /// </param>
        /// <returns>
        /// The full path to the directory containing the breadcrumb, or null.
        /// </returns>
        public static string FindBreadcrumbRoot(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
                return null;

            // Normalise: if startPath is a file, start from its parent directory.
            string current;
            if (File.Exists(startPath))
                current = Path.GetDirectoryName(startPath);
            else if (Directory.Exists(startPath))
                current = startPath;
            else
                current = Path.GetDirectoryName(startPath);

            // Safety bound — never walk above the OpenEDM root itself.
            string openedmRoot = Configuration.OpenEDMPath;

            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(current, BreadcrumbFileName);
                if (File.Exists(candidate))
                    return current;

                // Stop climbing once we reach or pass the OpenEDM root.
                if (string.Equals(current, openedmRoot, StringComparison.OrdinalIgnoreCase))
                    break;

                current = Path.GetDirectoryName(current);
            }

            return null;
        }

        /// <summary>
        /// Given a file inside a checked-out OpenEDM directory, resolves the full
        /// destination path on the server by combining the breadcrumb origin
        /// with the relative path of the file within the breadcrumb root.
        /// </summary>
        /// <param name="localFilePath">Full path to a file under C:\OpenEDM.</param>
        /// <param name="breadcrumbRootDir">
        /// The local directory that contains the breadcrumb file.
        /// </param>
        /// <returns>The resolved Z:\ path, or null if resolution fails.</returns>
        public static string ResolveServerPath(string localFilePath, string breadcrumbRootDir)
        {
            string serverRoot = ReadBreadcrumb(breadcrumbRootDir);
            if (serverRoot == null)
                return null;

            // Compute the relative path from the breadcrumb root to the file.
            string relativePath = GetRelativePath(breadcrumbRootDir, localFilePath);
            if (string.IsNullOrEmpty(relativePath))
                return null;

            return Path.Combine(serverRoot, relativePath);
        }

        /// <summary>
        /// Collects all non-hidden, non-breadcrumb files under a directory tree.
        /// </summary>
        public static List<string> EnumerateWorkingFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return new List<string>();

            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string name = Path.GetFileName(f);
                    // Skip breadcrumb files, openedmstate, and hidden/system files.
                    if (string.Equals(name, BreadcrumbFileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, ".openedmstate", StringComparison.OrdinalIgnoreCase))
                        return false;

                    try
                    {
                        var attrs = File.GetAttributes(f);
                        return (attrs & (FileAttributes.Hidden | FileAttributes.System)) == 0;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();
        }

        // ───────── Private Helpers ─────────

        /// <summary>
        /// Computes a relative path from <paramref name="basePath"/> to
        /// <paramref name="fullPath"/>. Uses Uri for reliability across edge cases.
        /// </summary>
        private static string GetRelativePath(string basePath, string fullPath)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
                return null;

            if (string.Equals(basePath.TrimEnd(Path.DirectorySeparatorChar), fullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            string baseNorm = basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(baseNorm, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(baseNorm.Length);
            return null;
        }
    }
}
