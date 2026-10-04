using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

namespace OpenEDMShellExtension.Core
{
    /// <summary>
    /// Static utility class that handles all file I/O for the OpenEDM.
    /// Every public method returns an <see cref="OperationResult"/> — nothing throws
    /// unhandled exceptions into explorer.exe.
    /// </summary>
    public static class FileOperations
    {
        /// <summary>
        /// OpenEDM root path for the current user, read from configuration.
        /// e.g. <c>A:\IDS-029</c>
        /// </summary>
        public static string OpenEDMRoot => Configuration.OpenEDMPath;

        // ════════════════════════════════════════════════════════════════
        //  SYNC TO OpenEDM   (Z:\CLIENTS\… → C:\OpenEDM\…)
        // ════════════════════════════════════════════════════════════════

        public static string GetLockFilePath(string path)
        {
            string lockDir = Configuration.LockPath;
            if (!Directory.Exists(lockDir)) Directory.CreateDirectory(lockDir);
            string safeName = path.Replace(":", "_").Replace("\\", "_").Replace("/", "_");
            return Path.Combine(lockDir, safeName + ".openedmlock");
        }

        public static OperationResult SyncToOpenEDM(IEnumerable<string> serverPaths, IProgress<int> progress = null)
        {
            if (serverPaths == null || !serverPaths.Any())
                return OperationResult.Fail("No files or folders were selected for sync.");

            var paths = serverPaths.ToList();
            var copiedFiles = new List<string>();
            var errors = new List<string>();

            try
            {
                if (!Directory.Exists(OpenEDMRoot))
                    Directory.CreateDirectory(OpenEDMRoot);

                string commonParent = GetCommonParentDirectory(paths);
                if (string.IsNullOrEmpty(commonParent))
                    return OperationResult.Fail("Could not determine a common parent directory for the selected items.");

                string folderName = Path.GetFileName(commonParent.TrimEnd(Path.DirectorySeparatorChar));
                string destinationRoot = Path.Combine(OpenEDMRoot, folderName);
                int suffix = 2;
                while (Directory.Exists(destinationRoot))
                {
                    string existingBreadcrumb = BreadcrumbTracker.ReadBreadcrumb(destinationRoot);
                    if (string.Equals(existingBreadcrumb, commonParent, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    destinationRoot = Path.Combine(OpenEDMRoot, $"{folderName}_{suffix}");
                    suffix++;
                }

                Directory.CreateDirectory(destinationRoot);

                var allDirs = new List<string>();
                var allFiles = new List<string>();

                foreach (string sourcePath in paths)
                {
                    if (Directory.Exists(sourcePath))
                    {
                        allDirs.Add(sourcePath);
                        allDirs.AddRange(Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories));
                        allFiles.AddRange(Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories));
                    }
                    else if (File.Exists(sourcePath))
                    {
                        allFiles.Add(sourcePath);
                    }
                    else
                    {
                        errors.Add($"Path not found: {sourcePath}");
                    }
                }

                foreach (var dir in allDirs)
                {
                    string relativePath = GetRelativePath(commonParent, dir);
                    string destDir = Path.Combine(destinationRoot, relativePath);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                }

                int totalFiles = allFiles.Count;
                int currentFile = 0;

                foreach (string sourceFile in allFiles)
                {
                    currentFile++;
                    progress?.Report((int)((double)currentFile / Math.Max(1, totalFiles) * 100));

                    string relativePath = GetRelativePath(commonParent, sourceFile);
                    string destFile = Path.Combine(destinationRoot, relativePath);

                    string destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    CopySingleFileReadOnly(sourceFile, destFile, errors, copiedFiles);
                }

                BreadcrumbTracker.WriteBreadcrumb(destinationRoot, commonParent);

                // Write .openedmstate
                var stateLines = new List<string>();
                foreach (string localFile in copiedFiles)
                {
                    string relativePath = GetRelativePath(destinationRoot, localFile);
                    string serverPath = Path.Combine(commonParent, relativePath);
                    if (File.Exists(serverPath))
                    {
                        long ticks = File.GetLastWriteTimeUtc(serverPath).Ticks;
                        stateLines.Add($"{relativePath}|{ticks}");
                    }
                }
                File.WriteAllLines(Path.Combine(destinationRoot, ".openedmstate"), stateLines);

                if (errors.Count > 0)
                {
                    return OperationResult.Partial(
                        $"Sync completed. {copiedFiles.Count} file(s) copied, {errors.Count} error(s).",
                        copiedFiles, new List<string>(), errors);
                }

                return OperationResult.Ok(
                    $"Successfully synced {copiedFiles.Count} file(s) to:\n{destinationRoot}",
                    copiedFiles);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Sync failed unexpectedly: {ex.Message}");
            }
        }

        public static OperationResult AcquireLock(string localFilePath)
        {
            try
            {
                string breadcrumbRoot = BreadcrumbTracker.FindBreadcrumbRoot(localFilePath);
                if (breadcrumbRoot == null)
                    return OperationResult.Fail("No .sourcepath.txt breadcrumb found.");

                string serverRoot = BreadcrumbTracker.ReadBreadcrumb(breadcrumbRoot);
                if (string.IsNullOrEmpty(serverRoot))
                    return OperationResult.Fail("Breadcrumb is empty or corrupt.");

                string serverFilePath = BreadcrumbTracker.ResolveServerPath(localFilePath, breadcrumbRoot);
                if (serverFilePath == null)
                    return OperationResult.Fail("Could not resolve server path.");

                string lockFile = GetLockFilePath(serverFilePath);
                string currentUser = Environment.UserDomainName + "\\" + Environment.UserName;

                try
                {
                    using (var fs = new FileStream(lockFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(fs))
                    {
                        writer.Write(currentUser);
                    }
                }
                catch (IOException)
                {
                    string lockedBy = File.Exists(lockFile) ? File.ReadAllText(lockFile).Trim() : "someone else";
                    if (!string.Equals(lockedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                    {
                        return OperationResult.Fail($"Locked by {lockedBy}");
                    }
                }

                var attributes = File.GetAttributes(localFilePath);

                string openedmStatePath = Path.Combine(breadcrumbRoot, ".openedmstate");
                if (File.Exists(openedmStatePath))
                {
                    var stateDict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    foreach (var line in File.ReadAllLines(openedmStatePath))
                    {
                        var parts = line.Split('|');
                        if (parts.Length == 2 && long.TryParse(parts[1], out long ticks))
                        {
                            stateDict[parts[0]] = ticks;
                        }
                    }

                    string relativePath = GetRelativePath(breadcrumbRoot, localFilePath);
                    if (relativePath != null && stateDict.TryGetValue(relativePath, out long originalTicks))
                    {
                        long currentLocalTicks = File.GetLastWriteTimeUtc(localFilePath).Ticks;
                        if (currentLocalTicks > originalTicks)
                        {
                            return OperationResult.Fail("Local file has unauthorized edits. Please back up your work before acquiring a lock.");
                        }
                    }
                }

                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(localFilePath, attributes & ~FileAttributes.ReadOnly);
                }

                // EDGE CASE FIX: Always pull the absolute latest from the server before editing!
                // This guarantees that if someone else updated the file since you initially synced,
                // you don't start editing an outdated local copy.
                if (File.Exists(serverFilePath))
                {
                    File.Copy(serverFilePath, localFilePath, true);
                }

                OpenEDMShellExtension.Logging.LogManager.AppendCheckInEntry(serverRoot, new List<string> { localFilePath }, "", "Lock Acquired");

                return OperationResult.Ok("Lock acquired successfully.", new List<string> { localFilePath });
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"AcquireLock failed: {ex.Message}");
            }
        }

        public static OperationResult CheckInFile(string localFilePath)
        {
            try
            {
                string breadcrumbRoot = BreadcrumbTracker.FindBreadcrumbRoot(localFilePath);
                if (breadcrumbRoot == null)
                    return OperationResult.Fail("No .sourcepath.txt breadcrumb found.");

                string serverRoot = BreadcrumbTracker.ReadBreadcrumb(breadcrumbRoot);
                string serverFilePath = BreadcrumbTracker.ResolveServerPath(localFilePath, breadcrumbRoot);
                if (serverFilePath == null)
                    return OperationResult.Fail("Could not resolve server path.");

                if (File.Exists(serverFilePath))
                {
                    return OperationResult.Partial("FILE_EXISTS", new List<string> { localFilePath }, new List<string>(), new List<string>());
                }

                bool isNewFile = true;
                string lockFile = GetLockFilePath(serverFilePath);
                string currentUser = Environment.UserDomainName + "\\" + Environment.UserName;

                string destDir = Path.GetDirectoryName(serverFilePath);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(localFilePath, serverFilePath, true);
                
                if (!isNewFile)
                {
                    try { File.Delete(lockFile); } catch { }
                }

                if (Configuration.OpenEDMCleanup)
                {
                    var attributes = File.GetAttributes(localFilePath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(localFilePath, attributes & ~FileAttributes.ReadOnly);
                    }
                    try { File.Delete(localFilePath); } catch { }

                    try
                    {
                        var remainingFiles = Directory.GetFiles(breadcrumbRoot, "*.*", SearchOption.AllDirectories)
                            .Where(f => !f.EndsWith(".sourcepath.txt", StringComparison.OrdinalIgnoreCase) && 
                                        !f.EndsWith(".openedmstate", StringComparison.OrdinalIgnoreCase) &&
                                        !f.EndsWith("Thumbs.db", StringComparison.OrdinalIgnoreCase) &&
                                        !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (remainingFiles.Count == 0)
                        {
                            SetAttributesNormal(new DirectoryInfo(breadcrumbRoot));
                            Directory.Delete(breadcrumbRoot, true);
                        }
                    }
                    catch { }
                }
                else
                {
                    var attributes = File.GetAttributes(localFilePath);
                    File.SetAttributes(localFilePath, attributes | FileAttributes.ReadOnly);
                }

                OpenEDMShellExtension.Logging.LogManager.AppendCheckInEntry(serverRoot, new List<string> { localFilePath }, "", "File Checked In");

                return OperationResult.Ok("Check-In complete.", new List<string> { localFilePath });
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"CheckInFile failed: {ex.Message}");
            }
        }

        public static OperationResult ReleaseLock(string localFilePath)
        {
            try
            {
                string breadcrumbRoot = BreadcrumbTracker.FindBreadcrumbRoot(localFilePath);
                if (breadcrumbRoot == null)
                    return OperationResult.Fail("No .sourcepath.txt breadcrumb found.");

                string serverRoot = BreadcrumbTracker.ReadBreadcrumb(breadcrumbRoot);
                string serverFilePath = BreadcrumbTracker.ResolveServerPath(localFilePath, breadcrumbRoot);
                if (serverFilePath == null)
                    return OperationResult.Fail("Could not resolve server path.");

                string lockFile = GetLockFilePath(serverFilePath);
                
                if (File.Exists(lockFile))
                {
                    try { File.Delete(lockFile); } catch { }
                }

                if (File.Exists(serverFilePath))
                {
                    // Ensure the local file isn't readonly so we can overwrite it.
                    var localAttrs = File.GetAttributes(localFilePath);
                    if ((localAttrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(localFilePath, localAttrs & ~FileAttributes.ReadOnly);
                    }
                    File.Copy(serverFilePath, localFilePath, true);
                }
                else
                {
                    if (File.Exists(localFilePath))
                    {
                        var localAttrs = File.GetAttributes(localFilePath);
                        if ((localAttrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(localFilePath, localAttrs & ~FileAttributes.ReadOnly);
                        }
                        File.Delete(localFilePath);
                    }
                }

                if (File.Exists(localFilePath))
                {
                    var attributes = File.GetAttributes(localFilePath);
                    File.SetAttributes(localFilePath, attributes | FileAttributes.ReadOnly);
                }

                OpenEDMShellExtension.Logging.LogManager.AppendCheckInEntry(serverRoot, new List<string> { localFilePath }, "", "Lock Released (Undo)");

                return OperationResult.Ok("Lock released successfully.", new List<string> { localFilePath });
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"ReleaseLock failed: {ex.Message}");
            }
        }

        public static void ReleaseLockForce(string serverFilePath)
        {
            string lockFile = GetLockFilePath(serverFilePath);
            if (File.Exists(lockFile))
            {
                try { File.Delete(lockFile); } catch { }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  CHECK-IN   (C:\OpenEDM\… → Z:\CLIENTS\…)
        //  Only pushes NEW or MODIFIED files. TrueNAS will reject
        //  overwrites, so we catch UnauthorizedAccessException per-file.
        // ════════════════════════════════════════════════════════════════

        public static OperationResult CheckIn(string openedmDirectoryOrFile,
            IEnumerable<string> specificFiles = null, IProgress<int> progress = null)
        {
            // 1. Locate the breadcrumb root.
            string breadcrumbRoot = BreadcrumbTracker.FindBreadcrumbRoot(openedmDirectoryOrFile);
            if (breadcrumbRoot == null)
                return OperationResult.Fail(
                    "No .sourcepath.txt breadcrumb found. This directory was not checked out through the OpenEDM system.");

            string serverRoot = BreadcrumbTracker.ReadBreadcrumb(breadcrumbRoot);
            if (string.IsNullOrEmpty(serverRoot))
                return OperationResult.Fail(
                    "The .sourcepath.txt breadcrumb is empty or corrupt. Cannot determine the server origin.");

            string openedmStatePath = Path.Combine(breadcrumbRoot, ".openedmstate");
            var stateDict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(openedmStatePath))
            {
                foreach (var line in File.ReadAllLines(openedmStatePath))
                {
                    var parts = line.Split('|');
                    if (parts.Length == 2 && long.TryParse(parts[1], out long ticks))
                    {
                        stateDict[parts[0]] = ticks;
                    }
                }
            }

            // 2. Gather the files to evaluate.
            List<string> filesToEvaluate;
            if (specificFiles != null && specificFiles.Any())
            {
                filesToEvaluate = specificFiles.ToList();
            }
            else
            {
                filesToEvaluate = BreadcrumbTracker.EnumerateWorkingFiles(breadcrumbRoot);
            }

            if (filesToEvaluate.Count == 0)
                return OperationResult.Fail("No files found to check in.");

            var pushed = new List<string>();
            var skipped = new List<string>();
            var errors = new List<string>();

            int totalFiles = filesToEvaluate.Count;
            int currentFile = 0;

            foreach (string localFile in filesToEvaluate)
            {
                currentFile++;
                progress?.Report((int)((double)currentFile / Math.Max(1, totalFiles) * 100));

                string serverPath = BreadcrumbTracker.ResolveServerPath(localFile, breadcrumbRoot);
                if (serverPath == null)
                {
                    errors.Add($"Could not resolve server path for: {localFile}");
                    continue;
                }

                string relativePath = GetRelativePath(breadcrumbRoot, localFile);
                if (relativePath != null && stateDict.TryGetValue(relativePath, out long originalTicks))
                {
                    if (File.Exists(serverPath))
                    {
                        long currentServerTicks = File.GetLastWriteTimeUtc(serverPath).Ticks;
                        if (currentServerTicks > originalTicks)
                        {
                            errors.Add($"CONFLICT: '{Path.GetFileName(localFile)}' was modified on the server since you checked it out.");
                            continue;
                        }
                    }
                }

                try
                {
                    string currentUser = Environment.UserDomainName + "\\" + Environment.UserName;
                    string fileLockPath = GetLockFilePath(serverPath);

                    if (File.Exists(fileLockPath))
                    {
                        string lockedBy = File.ReadAllText(fileLockPath).Trim();
                        if (!string.Equals(lockedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Cannot check in: '{Path.GetFileName(localFile)}' is locked by {lockedBy}.");
                            continue;
                        }
                    }
                    else if (File.Exists(serverPath)) // If it's an existing file on the server, we must hold the lock!
                    {
                        errors.Add($"Cannot check in: You do not hold the active lock for '{Path.GetFileName(localFile)}'. Please rename your local file and check it in as a new revision.");
                        continue;
                    }

                    if (File.Exists(serverPath))
                    {
                        if (AreFilesIdentical(localFile, serverPath))
                        {
                            skipped.Add(localFile);
                            
                            try { File.Delete(fileLockPath); } catch { }
                            
                            try 
                            { 
                                if (Configuration.OpenEDMCleanup)
                                {
                                    var attrs = File.GetAttributes(localFile);
                                    if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                        File.SetAttributes(localFile, attrs & ~FileAttributes.ReadOnly);
                                    File.Delete(localFile); 
                                }
                            } catch { }

                            continue;
                        }
                    }

                    string serverDir = Path.GetDirectoryName(serverPath);
                    if (!string.IsNullOrEmpty(serverDir) && !Directory.Exists(serverDir))
                        Directory.CreateDirectory(serverDir);

                    File.Copy(localFile, serverPath, overwrite: true);
                    pushed.Add(localFile);
                    
                    try { File.Delete(fileLockPath); } catch { }
                    
                    // Remove from local OpenEDM once pushed
                    try 
                    { 
                        if (Configuration.OpenEDMCleanup)
                        {
                            var attrs = File.GetAttributes(localFile);
                            if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                File.SetAttributes(localFile, attrs & ~FileAttributes.ReadOnly);
                            File.Delete(localFile); 
                        }
                    } 
                    catch { }
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add(
                        $"ACCESS DENIED: \"{Path.GetFileName(localFile)}\" already exists on the server " +
                        $"and TrueNAS ACLs prevent overwriting. Save as a new revision (e.g., add _v2) and try again.");
                }
                catch (SecurityException)
                {
                    errors.Add(
                        $"PERMISSION ERROR: Insufficient permissions to write \"{Path.GetFileName(localFile)}\" to the server.");
                }
                catch (IOException ex)
                {
                    errors.Add(
                        $"IO ERROR on \"{Path.GetFileName(localFile)}\": {ex.Message}");
                }
            }

            if (Configuration.OpenEDMCleanup)
            {
                try
                {
                    var remainingFiles = Directory.GetFiles(breadcrumbRoot, "*.*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".sourcepath.txt", StringComparison.OrdinalIgnoreCase) && 
                                    !f.EndsWith(".openedmstate", StringComparison.OrdinalIgnoreCase) &&
                                    !f.EndsWith("Thumbs.db", StringComparison.OrdinalIgnoreCase) &&
                                    !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (remainingFiles.Count == 0)
                    {
                        SetAttributesNormal(new DirectoryInfo(breadcrumbRoot));
                        Directory.Delete(breadcrumbRoot, true);
                    }
                }
                catch { }
            }

            if (errors.Count == 0)
            {
                return OperationResult.Ok(
                    $"Check-in complete. {pushed.Count} file(s) pushed, {skipped.Count} unchanged file(s) skipped.",
                    pushed);
            }

            return OperationResult.Partial(
                $"Check-in finished with issues. {pushed.Count} pushed, {skipped.Count} skipped, {errors.Count} error(s).",
                pushed, skipped, errors);
        }

        // ════════════════════════════════════════════════════════════════
        //  UNDO CHECK-OUT
        // ════════════════════════════════════════════════════════════════

        public static OperationResult UndoCheckOut(string openedmRoot)
        {
            try
            {
                string serverRoot = BreadcrumbTracker.ReadBreadcrumb(openedmRoot);
                if (!string.IsNullOrEmpty(serverRoot))
                {
                    string lockFile = GetLockFilePath(serverRoot);
                    if (File.Exists(lockFile))
                    {
                        try { File.Delete(lockFile); } catch { }
                    }
                    
                    OpenEDMShellExtension.Logging.LogManager.AppendCheckInEntry(serverRoot, new List<string>(), "", "UNDO CHECK-OUT");
                }

                if (Directory.Exists(openedmRoot))
                {
                    // Ensure we can delete readonly files
                    SetAttributesNormal(new DirectoryInfo(openedmRoot));
                    Directory.Delete(openedmRoot, true);
                }

                return OperationResult.Ok("Undo Check-Out completed successfully.", new List<string>());
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Undo Check-Out failed: {ex.Message}");
            }
        }

        private static void SetAttributesNormal(DirectoryInfo dir)
        {
            foreach (var subDir in dir.GetDirectories())
                SetAttributesNormal(subDir);
            foreach (var file in dir.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
            }
            dir.Attributes = FileAttributes.Normal;
        }

        // ════════════════════════════════════════════════════════════════
        //  ANALYSIS HELPERS   (used by the Check-In Manager UI)
        // ════════════════════════════════════════════════════════════════

        public static List<FileChangeInfo> AnalyzeChanges(string breadcrumbRoot)
        {
            var result = new List<FileChangeInfo>();

            string serverRoot = BreadcrumbTracker.ReadBreadcrumb(breadcrumbRoot);
            if (serverRoot == null)
                return result;

            string currentUser = Environment.UserDomainName + "\\" + Environment.UserName;
            string lockFile = GetLockFilePath(serverRoot);
            if (File.Exists(lockFile))
            {
                string lockedBy = File.ReadAllText(lockFile).Trim();
                if (!string.Equals(lockedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Check-In blocked! Folder is locked by {lockedBy}");
                }
            }

            string openedmStatePath = Path.Combine(breadcrumbRoot, ".openedmstate");
            var stateDict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(openedmStatePath))
            {
                foreach (var line in File.ReadAllLines(openedmStatePath))
                {
                    var parts = line.Split('|');
                    if (parts.Length == 2 && long.TryParse(parts[1], out long ticks))
                    {
                        stateDict[parts[0]] = ticks;
                    }
                }
            }

            // Auto-release locks for files that were deleted or manually renamed locally
            if (Configuration.OpenEDMCleanup)
            {
                foreach (var kvp in stateDict)
                {
                    string expectedLocalPath = Path.Combine(breadcrumbRoot, kvp.Key);
                    if (!File.Exists(expectedLocalPath))
                    {
                        string missingServerPath = BreadcrumbTracker.ResolveServerPath(expectedLocalPath, breadcrumbRoot);
                        if (missingServerPath != null)
                        {
                            try { File.Delete(GetLockFilePath(missingServerPath)); } catch { }
                            OpenEDMShellExtension.Logging.LogManager.AppendCheckInEntry(serverRoot, new List<string> { expectedLocalPath }, "File manually deleted/renamed locally", "Lock Released (Undo)");
                        }
                    }
                }
            }

            var files = BreadcrumbTracker.EnumerateWorkingFiles(breadcrumbRoot);

            foreach (string localFile in files)
            {
                string serverPath = BreadcrumbTracker.ResolveServerPath(localFile, breadcrumbRoot);
                if (serverPath == null)
                    continue;

                var info = new FileChangeInfo
                {
                    LocalPath = localFile,
                    ServerPath = serverPath,
                    RelativePath = GetRelativePath(breadcrumbRoot, localFile),
                    FileName = Path.GetFileName(localFile),
                    LocalLastModified = File.GetLastWriteTime(localFile),
                };

                try
                {
                    var fileInfo = new FileInfo(localFile);
                    info.FileSizeBytes = fileInfo.Length;
                }
                catch { /* non-critical */ }

                bool isConflict = false;
                if (info.RelativePath != null && stateDict.TryGetValue(info.RelativePath, out long originalTicks))
                {
                    if (File.Exists(serverPath))
                    {
                        long currentServerTicks = File.GetLastWriteTimeUtc(serverPath).Ticks;
                        if (currentServerTicks > originalTicks)
                        {
                            isConflict = true;
                        }
                    }
                }

                if (isConflict)
                {
                    info.ChangeType = FileChangeType.Conflict;
                }
                else if (!File.Exists(serverPath))
                {
                    info.ChangeType = FileChangeType.New;
                }
                else
                {
                    var serverInfo = new FileInfo(serverPath);
                    if (serverInfo.Exists && serverInfo.Length == info.FileSizeBytes &&
                        File.GetLastWriteTimeUtc(localFile) == File.GetLastWriteTimeUtc(serverPath))
                    {
                        info.ChangeType = FileChangeType.Unchanged;
                    }
                    else if (!AreFilesIdentical(localFile, serverPath))
                    {
                        info.ChangeType = FileChangeType.Modified;
                    }
                    else
                    {
                        info.ChangeType = FileChangeType.Unchanged;
                    }
                }

                result.Add(info);
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════

        private static void CopyDirectoryRecursiveReadOnly(string sourceDir, string destinationRoot,
            string commonParent, List<string> copiedFiles, List<string> errors)
        {
            string relativePath = GetRelativePath(commonParent, sourceDir);
            string destDir = Path.Combine(destinationRoot, relativePath);

            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                CopySingleFileReadOnly(file, destFile, errors, copiedFiles);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursiveReadOnly(subDir, destinationRoot, commonParent, copiedFiles, errors);
            }
        }

        private static void CopySingleFileReadOnly(string source, string destination,
            List<string> errors, List<string> copiedFiles)
        {
            try
            {
                if (File.Exists(destination))
                {
                    var existingAttrs = File.GetAttributes(destination);
                    if ((existingAttrs & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
                    {
                        // The user has this file checked out (writable). Do not overwrite their work!
                        return;
                    }
                    else
                    {
                        // Temporarily remove ReadOnly so we can overwrite it safely
                        File.SetAttributes(destination, existingAttrs & ~FileAttributes.ReadOnly);
                    }
                }

                File.Copy(source, destination, overwrite: true);
                var attributes = File.GetAttributes(destination);
                File.SetAttributes(destination, attributes | FileAttributes.ReadOnly);
                copiedFiles.Add(destination);
            }
            catch (Exception ex)
            {
                errors.Add($"Error copying \"{Path.GetFileName(source)}\": {ex.Message}");
            }
        }

        private static bool AreFilesIdentical(string pathA, string pathB)
        {
            try
            {
                var infoA = new FileInfo(pathA);
                var infoB = new FileInfo(pathB);

                if (!infoA.Exists || !infoB.Exists)
                    return false;

                if (infoA.Length != infoB.Length)
                    return false;

                if (infoA.Length == 0)
                    return true;

                const int bufferSize = 81920;
                var bufA = new byte[bufferSize];
                var bufB = new byte[bufferSize];

                using (var streamA = new FileStream(pathA, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite, bufferSize, FileOptions.SequentialScan))
                using (var streamB = new FileStream(pathB, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite, bufferSize, FileOptions.SequentialScan))
                {
                    int bytesReadA, bytesReadB;
                    do
                    {
                        bytesReadA = ReadFully(streamA, bufA, bufferSize);
                        bytesReadB = ReadFully(streamB, bufB, bufferSize);

                        if (bytesReadA != bytesReadB)
                            return false;

                        for (int i = 0; i < bytesReadA; i++)
                        {
                            if (bufA[i] != bufB[i])
                                return false;
                        }
                    } while (bytesReadA > 0);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, totalRead, count - totalRead);
                if (read == 0)
                    break;
                totalRead += read;
            }
            return totalRead;
        }

        private static string GetCommonParentDirectory(List<string> paths)
        {
            if (paths.Count == 0) return null;
            if (paths.Count == 1)
            {
                string single = paths[0].TrimEnd(Path.DirectorySeparatorChar);
                if (Directory.Exists(single)) return single;
                return Path.GetDirectoryName(single);
            }

            char sep = Path.DirectorySeparatorChar;
            string[][] segments = paths.Select(p => p.TrimEnd(sep).Split(sep)).ToArray();
            int minLen = segments.Min(s => s.Length);
            var common = new List<string>();

            for (int i = 0; i < minLen; i++)
            {
                string seg = segments[0][i];
                if (segments.All(s => string.Equals(s[i], seg, StringComparison.OrdinalIgnoreCase)))
                    common.Add(seg);
                else
                    break;
            }

            if (common.Count == 0) return null;

            string result = string.Join(sep.ToString(), common);
            if (result.Length == 2 && result[1] == ':') result += sep;
            if (File.Exists(result)) result = Path.GetDirectoryName(result);

            return result;
        }

        private static string GetPathRelativeToDrive(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return string.Empty;
            string root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root)) return fullPath.Substring(root.Length);
            return fullPath;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath)) return null;
            if (string.Equals(basePath.TrimEnd(Path.DirectorySeparatorChar), fullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            string baseNorm = basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(baseNorm, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(baseNorm.Length);
            return null;
        }
    }

    public enum FileChangeType
    {
        New,
        Modified,
        Unchanged,
        Conflict
    }

    public class FileChangeInfo
    {
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public string LocalPath { get; set; }
        public string ServerPath { get; set; }
        public FileChangeType ChangeType { get; set; }
        public DateTime LocalLastModified { get; set; }
        public long FileSizeBytes { get; set; }

        public string ChangeTypeDisplay
        {
            get
            {
                switch (ChangeType)
                {
                    case FileChangeType.New: return "✚ NEW";
                    case FileChangeType.Modified: return "✎ MODIFIED";
                    case FileChangeType.Unchanged: return "— Unchanged";
                    case FileChangeType.Conflict: return "⚠ CONFLICT";
                    default: return "?";
                }
            }
        }

        public string FileSizeDisplay
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1048576) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / 1048576.0:F1} MB";
            }
        }
    }
}


