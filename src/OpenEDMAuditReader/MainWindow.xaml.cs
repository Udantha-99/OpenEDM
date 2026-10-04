using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OpenEDMShellExtension.Core;
using MessageBox = System.Windows.MessageBox;

namespace OpenEDMAuditReader
{
    public partial class MainWindow : Window
    {
        private List<OpenEDMSession> _allSessions = new List<OpenEDMSession>();
        private bool _logsLoaded = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
        }

        private string GetGlobalConfigPath()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string dir = Path.Combine(programData, "OpenEDMShellExtension");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "openedm-config.txt");
        }

        private void LoadConfig()
        {
            string configPath = GetGlobalConfigPath();
            if (File.Exists(configPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(configPath);
                    string currentSection = null;
                    List<string> drives = new List<string>();
                    string openedmPath = "";
                    string auditLogPath = "";
                    string lockPath = "";
                    bool openedmCleanup = false;

                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            currentSection = line.Substring(1, line.Length - 2).Trim().ToUpperInvariant();
                            continue;
                        }

                        switch (currentSection)
                        {
                            case "SOURCEDRIVES":
                                drives.Add(line);
                                break;
                            case "OpenEDMPATH":
                                if (string.IsNullOrWhiteSpace(openedmPath)) openedmPath = line;
                                break;
                            case "AUDITLOGPATH":
                                if (string.IsNullOrWhiteSpace(auditLogPath)) auditLogPath = line;
                                break;
                            case "LOCKPATH":
                                if (string.IsNullOrWhiteSpace(lockPath)) lockPath = line;
                                break;
                            case "OpenEDMCLEANUP":
                                if (bool.TryParse(line, out bool b)) openedmCleanup = b;
                                break;
                        }
                    }

                    txtSourceDrives.Text = string.Join(Environment.NewLine, drives);
                    txtOpenEDMPath.Text = openedmPath;
                    txtAuditLogPath.Text = auditLogPath;
                    txtLockPath.Text = lockPath;
                    chkOpenEDMCleanup.IsChecked = openedmCleanup;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading config: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configPath = GetGlobalConfigPath();
                List<string> lines = new List<string>();

                lines.Add("[SourceDrives]");
                string[] drives = txtSourceDrives.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string drive in drives)
                {
                    string d = drive.Trim();
                    if (!string.IsNullOrEmpty(d))
                        lines.Add(d);
                }
                lines.Add("");

                lines.Add("[OpenEDMPath]");
                if (!string.IsNullOrWhiteSpace(txtOpenEDMPath.Text))
                    lines.Add(txtOpenEDMPath.Text.Trim());
                lines.Add("");

                lines.Add("[AuditLogPath]");
                if (!string.IsNullOrWhiteSpace(txtAuditLogPath.Text))
                    lines.Add(txtAuditLogPath.Text.Trim());
                lines.Add("");

                lines.Add("[LockPath]");
                if (!string.IsNullOrWhiteSpace(txtLockPath.Text))
                    lines.Add(txtLockPath.Text.Trim());
                lines.Add("");

                lines.Add("[OpenEDMCleanup]");
                lines.Add((chkOpenEDMCleanup.IsChecked == true).ToString());

                File.WriteAllLines(configPath, lines);
                Configuration.Reload();
                MessageBox.Show("Configuration saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving config: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnLoadLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string privateKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "private_key.xml");
                if (!File.Exists(privateKeyPath))
                {
                    MessageBox.Show("Private key not found at: " + privateKeyPath, "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string privateKeyXml = File.ReadAllText(privateKeyPath);

                string auditLogPath = Configuration.AuditLogPath;

                if (string.IsNullOrWhiteSpace(auditLogPath))
                {
                    MessageBox.Show("Audit log directory is not configured in openedm-config.txt.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Directory.Exists(auditLogPath))
                {
                    MessageBox.Show("Audit log directory does not exist or is not reachable: " + auditLogPath, "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _allSessions = new List<OpenEDMSession>();
                    _logsLoaded = false;
                    ApplyFilter();
                    return;
                }

                if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;

                try
                {
                    var newSessions = new List<OpenEDMSession>();

                    await Task.Run(() =>
                    {
                        var rawEvents = new List<AuditEvent>();

                        using (var rsa = new RSACryptoServiceProvider())
                        {
                            rsa.FromXmlString(privateKeyXml);

                            foreach (string file in Directory.EnumerateFiles(auditLogPath, "*.json"))
                            {
                                try
                                {
                                    string content = File.ReadAllText(file).Trim();
                                    if (string.IsNullOrWhiteSpace(content)) continue;

                                    string decryptedJson = CryptoHelper.DecryptLogEntry(rsa, content);
                                using (JsonDocument doc = JsonDocument.Parse(decryptedJson))
                                {
                                    var root = doc.RootElement;
                                    var timestampStr = root.GetProperty("Timestamp").GetString();
                                    if (!DateTime.TryParse(timestampStr, out var ts)) continue;

                                    var fProp = root.TryGetProperty("Files", out var f) ? f : 
                                                (root.TryGetProperty("FileNames", out var fn) ? fn : default);
                                    
                                    var fileNames = new List<string>();
                                    if (fProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var item in fProp.EnumerateArray())
                                            fileNames.Add(item.GetString() ?? "");
                                    }
                                    else if (fProp.ValueKind == JsonValueKind.String)
                                    {
                                        string s = fProp.GetString() ?? "";
                                        fileNames = s.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                    }

                                    if (fileNames.Count == 0) fileNames.Add("");

                                    foreach (var fileNameItem in fileNames)
                                    {
                                        rawEvents.Add(new AuditEvent
                                        {
                                            Timestamp = ts,
                                            User = root.GetProperty("Username").GetString() ?? "",
                                            Action = root.GetProperty("Action").GetString() ?? "",
                                            Project = root.TryGetProperty("Project", out var p) ? (p.GetString() ?? "") : "",
                                            File = fileNameItem
                                        });
                                    }
                                }
                            }
                            catch
                            {
                                // Skip corrupted/invalid files
                            }
                        }
                        }

                        newSessions = CorrelateEvents(rawEvents);
                    });

                    _allSessions = newSessions;
                    _logsLoaded = true;
                    ApplyFilter();
                }
                finally
                {
                    if (sender is System.Windows.Controls.Button b) b.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading logs: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<OpenEDMSession> CorrelateEvents(List<AuditEvent> rawEvents)
        {
            var sessions = new List<OpenEDMSession>();
            var grouped = rawEvents.GroupBy(e => new { 
                Project = e.Project.ToLowerInvariant(), 
                File = e.File.ToLowerInvariant() 
            });

            foreach (var group in grouped)
            {
                var sorted = group.OrderBy(e => e.Timestamp)
                                  .ThenBy(e => e.Action == "Lock Acquired" ? 1 : 0)
                                  .ToList();
                OpenEDMSession? activeSession = null;

                foreach (var ev in sorted)
                {
                    if (ev.Action == "Lock Acquired")
                    {
                        if (activeSession != null)
                        {
                            activeSession.Status = "Abandoned";
                        }
                        activeSession = new OpenEDMSession
                        {
                            Project = ev.Project,
                            User = ev.User,
                            File = ev.File,
                            CheckOutTime = ev.Timestamp,
                            Status = "Checked Out"
                        };
                        sessions.Add(activeSession);
                    }
                    else if (ev.Action == "File Checked In")
                    {
                        if (activeSession != null)
                        {
                            activeSession.CheckInTime = ev.Timestamp;
                            activeSession.Status = "Checked In";
                            activeSession = null;
                        }
                    }
                    else if (ev.Action == "Lock Released (Undo)")
                    {
                        if (activeSession != null)
                        {
                            activeSession.CheckInTime = ev.Timestamp;
                            activeSession.Status = "Aborted";
                            activeSession = null;
                        }
                    }
                }
            }

            return sessions.OrderByDescending(s => s.CheckOutTime).ToList();
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string search = txtSearch.Text.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(search))
            {
                dgvSessions.ItemsSource = _allSessions;
            }
            else
            {
                dgvSessions.ItemsSource = _allSessions.Where(s => 
                    (s.Project != null && s.Project.ToLowerInvariant().Contains(search)) ||
                    (s.User != null && s.User.ToLowerInvariant().Contains(search)) ||
                    (s.File != null && s.File.ToLowerInvariant().Contains(search))
                ).ToList();
            }
        }

        private void BtnArchive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string auditLogPath = Configuration.AuditLogPath;
                if (string.IsNullOrWhiteSpace(auditLogPath) || !Directory.Exists(auditLogPath))
                {
                    MessageBox.Show("Audit log directory not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string lockFile = Path.Combine(auditLogPath, "_archive.lock");
                try
                {
                    using (var fs = new FileStream(lockFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(fs))
                    {
                        writer.Write(Environment.UserName);
                    }
                }
                catch (IOException)
                {
                    MessageBox.Show("Archiving is currently being performed by another user.", "Archiving in Progress", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var cutoffDate = DateTime.Now.AddDays(-90);
                    var oldFiles = new List<string>();

                    foreach (var file in Directory.GetFiles(auditLogPath, "*.json"))
                    {
                        if (File.GetLastWriteTime(file) < cutoffDate)
                        {
                            oldFiles.Add(file);
                        }
                    }

                    if (oldFiles.Count == 0)
                    {
                        MessageBox.Show("No logs older than 90 days found.", "Archive", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    string zipPath = Path.Combine(auditLogPath, $"AuditArchive_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

                    using (var zipStream = new FileStream(zipPath, FileMode.Create))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    {
                        foreach (var file in oldFiles)
                        {
                            archive.CreateEntryFromFile(file, Path.GetFileName(file));
                        }
                    }

                    foreach (var file in oldFiles)
                    {
                        File.Delete(file);
                    }

                    MessageBox.Show($"Archived {oldFiles.Count} log files.", "Archive Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally
                {
                    if (File.Exists(lockFile))
                    {
                        try { File.Delete(lockFile); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error archiving logs: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"Audit_Report_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (dialog.ShowDialog() == true)
                {
                    var view = dgvSessions.ItemsSource as IEnumerable<OpenEDMSession>;
                    if (view == null) return;

                    var lines = new List<string> { "Project,File,User,Check-Out Time,Check-In Time,Status" };
                    foreach (var s in view)
                    {
                        lines.Add($"\"{s.Project}\",\"{s.File}\",\"{s.User}\",\"{s.CheckOutTimeDisplay}\",\"{s.CheckInTimeDisplay}\",\"{s.Status}\"");
                    }
                    File.WriteAllLines(dialog.FileName, lines);
                    MessageBox.Show("Export complete.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting CSV: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnScanLocks_Click(object sender, RoutedEventArgs e)
        {
            string lockPath = Configuration.LockPath;
            if (string.IsNullOrWhiteSpace(lockPath) || !Directory.Exists(lockPath))
            {
                MessageBox.Show("Lock directory does not exist or is not reachable: " + lockPath, "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                dgvActiveLocks.ItemsSource = new List<ActiveLockInfo>();
                return;
            }

            var locks = new List<ActiveLockInfo>();
            foreach (var file in Directory.GetFiles(lockPath, "*.openedmlock", SearchOption.AllDirectories))
            {
                try
                {
                    string username = File.ReadAllText(file).Trim();
                    locks.Add(new ActiveLockInfo
                    {
                        FilePath = file,
                        Username = username,
                        CreationDate = File.GetCreationTime(file)
                    });
                }
                catch { }
            }
            dgvActiveLocks.ItemsSource = locks;
        }

        private void BtnForceUnlock_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgvActiveLocks.SelectedItem as ActiveLockInfo;
            if (selected == null)
            {
                MessageBox.Show("Please select a lock to force unlock.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (MessageBox.Show($"Are you sure you want to force unlock {selected.FilePath}?", "Confirm Unlock", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    File.Delete(selected.FilePath);
                    OpenEDMShellExtension.Logging.LogManager.AppendCheckInEntry("ForceUnlock", new List<string> { selected.FilePath }, "Admin Force Unlock", "Admin Force Unlock");
                    MessageBox.Show("Lock removed.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnScanLocks_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error removing lock: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnRefreshAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (!_logsLoaded)
            {
                MessageBox.Show("Please load Audit Logs first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var staleLocks = _allSessions
                .Where(s => s.Status == "Checked Out" && (DateTime.Now - s.CheckOutTime).TotalDays > 7)
                .Select(s => new StaleLockInfo { User = s.User, File = s.File, DaysOut = Math.Round((DateTime.Now - s.CheckOutTime).TotalDays, 1) })
                .OrderByDescending(s => s.DaysOut)
                .ToList();
            dgvStaleLocks.ItemsSource = staleLocks;

            var avgTurnaround = _allSessions
                .Where(s => s.Status == "Checked In" && s.CheckInTime.HasValue)
                .GroupBy(s => s.User)
                .Select(g => new AvgTurnaroundInfo
                {
                    User = g.Key,
                    AvgTimeHours = Math.Round(g.Average(s => (s.CheckInTime.Value - s.CheckOutTime).TotalHours), 1)
                })
                .OrderBy(s => s.AvgTimeHours)
                .ToList();
            dgvAvgTurnaround.ItemsSource = avgTurnaround;

            var projectHeatmap = _allSessions
                .Where(s => s.Status == "Checked In")
                .GroupBy(s => s.Project)
                .Select(g => new ProjectHeatmapInfo { Project = g.Key, CheckInCount = g.Count() })
                .OrderByDescending(s => s.CheckInCount)
                .ToList();
            dgvProjectHeatmap.ItemsSource = projectHeatmap;
        }

        private void BtnBrowseIntegrityFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtIntegrityFolder.Text = dialog.SelectedPath;
            }
        }

        private void BtnRunIntegrityScan_Click(object sender, RoutedEventArgs e)
        {
            string folder = txtIntegrityFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show("Please select a valid server project folder.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_logsLoaded)
            {
                MessageBox.Show("Please load Audit Logs first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var issues = new List<IntegrityIssueInfo>();
            try
            {
                var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (file.EndsWith(".openedmlock", StringComparison.OrdinalIgnoreCase)) continue;

                    var lastWrite = File.GetLastWriteTime(file);
                    var relatedSessions = _allSessions.Where(s => s.Status == "Checked In" && s.File != null && s.File.Equals(System.IO.Path.GetFileName(file), StringComparison.OrdinalIgnoreCase)).ToList();

                    if (relatedSessions.Count == 0)
                    {
                        issues.Add(new IntegrityIssueInfo { File = file, Issue = "No matching encrypted check-in log" });
                    }
                    else
                    {
                        var lastCheckIn = relatedSessions.Max(s => s.CheckInTime);
                        if (lastCheckIn.HasValue && lastWrite > lastCheckIn.Value.AddMinutes(5)) // add buffer
                        {
                            issues.Add(new IntegrityIssueInfo { File = file, Issue = "File is significantly newer than the last logged Check-In (Suspicious / Untracked Modification)" });
                        }
                    }
                }
                
                dgvIntegrityResults.ItemsSource = issues;
                
                if (issues.Count == 0)
                {
                    MessageBox.Show("Scan complete. No integrity issues found.", "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error running scan: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class AuditEvent
    {
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
    }

    public class OpenEDMSession
    {
        public string Project { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public DateTime CheckOutTime { get; set; }
        public DateTime? CheckInTime { get; set; }
        public string Status { get; set; } = string.Empty;

        public string CheckOutTimeDisplay => CheckOutTime.ToString("yyyy-MM-dd HH:mm:ss");
        public string CheckInTimeDisplay => CheckInTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
    }
    
    public class ActiveLockInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public string CreationDateDisplay => CreationDate.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public class StaleLockInfo
    {
        public string User { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public double DaysOut { get; set; }
    }

    public class AvgTurnaroundInfo
    {
        public string User { get; set; } = string.Empty;
        public double AvgTimeHours { get; set; }
    }

    public class ProjectHeatmapInfo
    {
        public string Project { get; set; } = string.Empty;
        public int CheckInCount { get; set; }
    }

    public class IntegrityIssueInfo
    {
        public string File { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
    }
}


