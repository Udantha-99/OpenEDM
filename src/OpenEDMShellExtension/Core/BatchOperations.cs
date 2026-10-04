using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenEDMShellExtension.Core
{
    public class RenameResult
    {
        public string NewPath { get; set; }
        public bool ApplyToAll { get; set; }
    }

    public static class BatchOperations
    {
        public static OperationResult BatchCheckIn(string localFolderPath, Func<string, RenameResult> renameFileCallback = null)
        {
            try
            {
                var files = Directory.GetFiles(localFolderPath, "*", SearchOption.AllDirectories);
                var successCount = 0;
                var errors = new List<string>();
                bool autoAppendV2 = false;

                foreach (var f in files)
                {
                    var file = f;
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
                    {
                        var result = FileOperations.CheckInFile(file);
                        
                        string originalName = Path.GetFileNameWithoutExtension(file);
                        string originalExt = Path.GetExtension(file);
                        string dir = Path.GetDirectoryName(file);
                        int suffix = 2;

                        while (!result.Success && result.Message == "FILE_EXISTS")
                        {
                            string newPath = null;
                            if (autoAppendV2)
                            {
                                do
                                {
                                    newPath = Path.Combine(dir, $"{originalName}_v{suffix}{originalExt}");
                                    suffix++;
                                } while (File.Exists(newPath));
                            }
                            else if (renameFileCallback != null)
                            {
                                var renameResult = renameFileCallback(file);
                                if (renameResult != null)
                                {
                                    newPath = renameResult.NewPath;
                                    if (renameResult.ApplyToAll)
                                    {
                                        autoAppendV2 = true;
                                    }
                                }
                            }

                            if (newPath == null)
                            {
                                errors.Add(Path.GetFileName(file) + ": File exists on server and was not renamed.");
                                break;
                            }
                            try
                            {
                                if (File.Exists(newPath)) 
                                {
                                    // Should only happen if manual rename callback gave an existing file
                                    throw new IOException($"Local file {Path.GetFileName(newPath)} already exists.");
                                }
                                File.Move(file, newPath);
                                file = newPath;
                                result = FileOperations.CheckInFile(file);
                            }
                            catch (Exception ex)
                            {
                                result = OperationResult.Fail($"Failed to rename or check in - {ex.Message}");
                                break;
                            }
                        }

                        if (result.Success) successCount++;
                        else if (result.Message != "FILE_EXISTS" || renameFileCallback == null)
                            errors.Add(Path.GetFileName(file) + ": " + (result.Message == "FILE_EXISTS" ? "File exists on server." : result.Message));
                    }
                }

                if (errors.Count > 0)
                    return OperationResult.Partial($"Checked in {successCount} items. ({errors.Count} items encountered issues).", new List<string>(), new List<string>(), errors);
                
                return OperationResult.Ok($"Successfully checked in {successCount} items.", new List<string>());
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Process failed: {ex.Message}");
            }
        }

        public static OperationResult BatchAcquire(string localFolderPath)
        {
            try
            {
                var files = Directory.GetFiles(localFolderPath, "*", SearchOption.AllDirectories);
                var successCount = 0;
                var errors = new List<string>();

                foreach (var file in files)
                {
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        var result = FileOperations.AcquireLock(file);
                        if (result.Success) successCount++;
                        else errors.Add(Path.GetFileName(file) + ": " + result.Message);
                    }
                }

                if (errors.Count > 0)
                    return OperationResult.Partial($"Checked out {successCount} items. ({errors.Count} items encountered issues).", new List<string>(), new List<string>(), errors);
                
                return OperationResult.Ok($"Successfully checked out {successCount} items.", new List<string>());
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Process failed: {ex.Message}");
            }
        }
    }
}
