using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string path = @"src\OpenEDMAuditReader\MainWindow.xaml.cs";
        string code = File.ReadAllText(path);

        // Replace BtnScanLocks_Click
        string oldScanLocks = @"private void BtnScanLocks_Click\(object sender, RoutedEventArgs e\)\s*\{\s*string lockPath = Configuration\.LockPath;\s*if \(string\.IsNullOrWhiteSpace\(lockPath\) \|\| !Directory\.Exists\(lockPath\)\)\s*\{\s*MessageBox\.Show\(""Lock directory does not exist or is not reachable: "" \+ lockPath, ""Directory Not Found"", MessageBoxButton\.OK, MessageBoxImage\.Warning\);\s*dgvActiveLocks\.ItemsSource = new List<ActiveLockInfo>\(\);\s*return;\s*\}\s*var locks = new List<ActiveLockInfo>\(\);\s*foreach \(var file in Directory\.GetFiles\(lockPath, ""\*\.openedmlock"", SearchOption\.AllDirectories\)\)\s*\{\s*try\s*\{\s*string username = File\.ReadAllText\(file\)\.Trim\(\);\s*locks\.Add\(new ActiveLockInfo\s*\{\s*FilePath = file,\s*Username = username,\s*CreationDate = File\.GetCreationTime\(file\)\s*\}\);\s*\}\s*catch \{ \}\s*\}\s*dgvActiveLocks\.ItemsSource = locks;\s*\}";
        
        string newScanLocks = @"private async void BtnScanLocks_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;
            try
            {
                string lockPath = Configuration.LockPath;
                if (string.IsNullOrWhiteSpace(lockPath) || !Directory.Exists(lockPath))
                {
                    MessageBox.Show(""Lock directory does not exist or is not reachable: "" + lockPath, ""Directory Not Found"", MessageBoxButton.OK, MessageBoxImage.Warning);
                    dgvActiveLocks.ItemsSource = new List<ActiveLockInfo>();
                    return;
                }

                var locks = await Task.Run(() =>
                {
                    var result = new List<ActiveLockInfo>();
                    foreach (var file in Directory.GetFiles(lockPath, ""*.openedmlock"", SearchOption.AllDirectories))
                    {
                        try
                        {
                            string username = File.ReadAllText(file).Trim();
                            result.Add(new ActiveLockInfo
                            {
                                FilePath = file,
                                Username = username,
                                CreationDate = File.GetCreationTime(file)
                            });
                        }
                        catch { }
                    }
                    return result;
                });
                dgvActiveLocks.ItemsSource = locks;
            }
            finally
            {
                if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = true;
            }
        }";
        code = Regex.Replace(code, oldScanLocks, newScanLocks);

        // Replace BtnForceUnlock_Click
        string oldForceUnlock = @"private void BtnForceUnlock_Click\(object sender, RoutedEventArgs e\)\s*\{\s*var selectedItems = dgvActiveLocks\.SelectedItems\.Cast<ActiveLockInfo>\(\)\.ToList\(\);\s*if \(selectedItems\.Count == 0\)\s*\{\s*MessageBox\.Show\(""Please select a lock to force unlock\."", ""No Selection"", MessageBoxButton\.OK, MessageBoxImage\.Warning\);\s*return;\s*\}\s*if \(MessageBox\.Show\(\$""Are you sure you want to force unlock \{selectedItems\.Count\} lock\(s\)\?"", ""Confirm Unlock"", MessageBoxButton\.YesNo, MessageBoxImage\.Question\) == MessageBoxResult\.Yes\)\s*\{\s*try\s*\{\s*foreach \(var selected in selectedItems\)\s*\{\s*File\.Delete\(selected\.FilePath\);\s*OpenEDMShellExtension\.Logging\.LogManager\.AppendCheckInEntry\(""ForceUnlock"", new List<string> \{ selected\.FilePath \}, ""Admin Force Unlock"", ""Admin Force Unlock""\);\s*\}\s*MessageBox\.Show\(\$""\{selectedItems\.Count\} lock\(s\) removed\."", ""Success"", MessageBoxButton\.OK, MessageBoxImage\.Information\);\s*BtnScanLocks_Click\(null, null\);\s*\}\s*catch \(Exception ex\)\s*\{\s*MessageBox\.Show\(""Error removing lock: "" \+ ex\.Message, ""Error"", MessageBoxButton\.OK, MessageBoxImage\.Error\);\s*\}\s*\}\s*\}";
        string newForceUnlock = @"private async void BtnForceUnlock_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dgvActiveLocks.SelectedItems.Cast<ActiveLockInfo>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(""Please select a lock to force unlock."", ""No Selection"", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (MessageBox.Show($""Are you sure you want to force unlock {selectedItems.Count} lock(s)?"", ""Confirm Unlock"", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;
                try
                {
                    await Task.Run(() =>
                    {
                        foreach (var selected in selectedItems)
                        {
                            File.Delete(selected.FilePath);
                            OpenEDMShellExtension.Logging.LogManager.AppendCheckInEntry(""ForceUnlock"", new List<string> { selected.FilePath }, ""Admin Force Unlock"", ""Admin Force Unlock"");
                        }
                    });
                    MessageBox.Show($""{selectedItems.Count} lock(s) removed."", ""Success"", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnScanLocks_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(""Error removing lock: "" + ex.Message, ""Error"", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = true;
                }
            }
        }";
        code = Regex.Replace(code, oldForceUnlock, newForceUnlock);

        // Replace BtnRunIntegrityScan_Click
        string oldIntegrityScan = @"private void BtnRunIntegrityScan_Click\(object sender, RoutedEventArgs e\)\s*\{\s*string folder = txtIntegrityFolder\.Text\.Trim\(\);\s*if \(string\.IsNullOrWhiteSpace\(folder\) \|\| !Directory\.Exists\(folder\)\)\s*\{\s*MessageBox\.Show\(""Please select a valid server project folder\."", ""Invalid Folder"", MessageBoxButton\.OK, MessageBoxImage\.Warning\);\s*return;\s*\}\s*if \(!_logsLoaded\)\s*\{\s*MessageBox\.Show\(""Please load Audit Logs first\."", ""No Data"", MessageBoxButton\.OK, MessageBoxImage\.Warning\);\s*return;\s*\}\s*var issues = new List<IntegrityIssueInfo>\(\);\s*try\s*\{\s*var files = Directory\.GetFiles\(folder, ""\*\.\*"", SearchOption\.AllDirectories\);\s*foreach \(var file in files\)\s*\{\s*if \(file\.EndsWith\(""\.openedmlock"", StringComparison\.OrdinalIgnoreCase\)\) continue;\s*var lastWrite = File\.GetLastWriteTime\(file\);\s*var relatedSessions = _allSessions\.Where\(s => s\.Status == ""Checked In"" && s\.File != null && s\.File\.Equals\(System\.IO\.Path\.GetFileName\(file\), StringComparison\.OrdinalIgnoreCase\)\)\.ToList\(\);\s*if \(relatedSessions\.Count == 0\)\s*\{\s*issues\.Add\(new IntegrityIssueInfo \{ File = file, Issue = ""No matching encrypted check-in log"" \}\);\s*\}\s*else\s*\{\s*var lastCheckIn = relatedSessions\.Max\(s => s\.CheckInTime\);\s*if \(lastCheckIn\.HasValue && lastWrite > lastCheckIn\.Value\.AddMinutes\(5\)\)\s*\{\s*issues\.Add\(new IntegrityIssueInfo \{ File = file, Issue = ""File is significantly newer than the last logged Check-In \(Suspicious / Untracked Modification\)"" \}\);\s*\}\s*\}\s*\}\s*dgvIntegrityResults\.ItemsSource = issues;\s*if \(issues\.Count == 0\)\s*\{\s*MessageBox\.Show\(""Scan complete\. No integrity issues found\."", ""Scan Complete"", MessageBoxButton\.OK, MessageBoxImage\.Information\);\s*\}\s*\}\s*catch \(Exception ex\)\s*\{\s*MessageBox\.Show\(""Error running scan: "" \+ ex\.Message, ""Error"", MessageBoxButton\.OK, MessageBoxImage\.Error\);\s*\}\s*\}";
        string newIntegrityScan = @"private async void BtnRunIntegrityScan_Click(object sender, RoutedEventArgs e)
        {
            string folder = txtIntegrityFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(""Please select a valid server project folder."", ""Invalid Folder"", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_logsLoaded)
            {
                MessageBox.Show(""Please load Audit Logs first."", ""No Data"", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;
            var issues = new List<IntegrityIssueInfo>();
            try
            {
                var files = await Task.Run(() => Directory.GetFiles(folder, ""*.*"", SearchOption.AllDirectories));
                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        if (file.EndsWith("".openedmlock"", StringComparison.OrdinalIgnoreCase)) continue;

                        var lastWrite = File.GetLastWriteTime(file);
                        // Also check full paths instead of just filenames
                        var relatedSessions = _allSessions.Where(s => s.Status == ""Checked In"" && s.File != null && (s.File.Equals(System.IO.Path.GetFileName(file), StringComparison.OrdinalIgnoreCase) || file.EndsWith(s.File, StringComparison.OrdinalIgnoreCase))).ToList();

                        if (relatedSessions.Count == 0)
                        {
                            issues.Add(new IntegrityIssueInfo { File = file, Issue = ""No matching encrypted check-in log"" });
                        }
                        else
                        {
                            var lastCheckIn = relatedSessions.Max(s => s.CheckInTime);
                            if (lastCheckIn.HasValue && lastWrite > lastCheckIn.Value.AddMinutes(5)) // add buffer
                            {
                                issues.Add(new IntegrityIssueInfo { File = file, Issue = ""File is significantly newer than the last logged Check-In (Suspicious / Untracked Modification)"" });
                            }
                        }
                    }
                });
                
                dgvIntegrityResults.ItemsSource = issues;
                
                if (issues.Count == 0)
                {
                    MessageBox.Show(""Scan complete. No integrity issues found."", ""Scan Complete"", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(""Error running scan: "" + ex.Message, ""Error"", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is System.Windows.Controls.Button b) b.IsEnabled = true;
            }
        }";
        code = Regex.Replace(code, oldIntegrityScan, newIntegrityScan);

        File.WriteAllText(path, code);
    }
}
