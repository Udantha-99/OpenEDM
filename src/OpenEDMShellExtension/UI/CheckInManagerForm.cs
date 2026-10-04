using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenEDMShellExtension.Core;
using OpenEDMShellExtension.Logging;

namespace OpenEDMShellExtension.UI
{
    public partial class CheckInManagerForm : Form
    {
        private readonly string _breadcrumbRoot;
        private readonly string _serverRoot;
        private List<FileChangeInfo> _changes;

        public OperationResult CheckInResult { get; private set; }

        public CheckInManagerForm(string breadcrumbRoot)
        {
            _breadcrumbRoot = breadcrumbRoot ?? throw new ArgumentNullException(nameof(breadcrumbRoot));
            _serverRoot = BreadcrumbTracker.ReadBreadcrumb(breadcrumbRoot);
            _changes = new List<FileChangeInfo>();

            InitializeComponent();

            txtServerPath.Text = _serverRoot ?? "(breadcrumb not found)";
            
            this.Shown += (s, e) => RefreshScan();
        }

        public static OperationResult ShowCheckInDialog(string breadcrumbRoot)
        {
            try
            {
                using (var form = new CheckInManagerForm(breadcrumbRoot))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        return form.CheckInResult;
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Check-in dialog error: {ex.Message}");
            }
        }

        private void RefreshScan()
        {
            lblStatus.Text = "Scanning files...";
            lblStatus.ForeColor = Color.Blue;
            btnCheckIn.Enabled = false;
            pnlFiles.Controls.Clear();
            this.Refresh();

            try
            {
                _changes = FileOperations.AnalyzeChanges(_breadcrumbRoot);
                PopulateList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error scanning files:\n{ex.Message}", "OpenEDM Check-In Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Scan failed.";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private List<FileRowControl> _rows = new List<FileRowControl>();

        private void PopulateList()
        {
            int changeCount = 0;
            pnlFiles.Controls.Clear();
            _rows.Clear();

            int y = 5;
            foreach (var change in _changes.OrderBy(c => c.ChangeType).ThenBy(c => c.FileName))
            {
                bool hasConflict = change.ChangeType == FileChangeType.Modified || change.ChangeType == FileChangeType.Conflict;
                bool serverFileExists = System.IO.File.Exists(change.ServerPath);
                
                var row = new FileRowControl(change, serverFileExists);
                row.Location = new Point(5, y);
                row.ValidationChanged += (s, e) => UpdateCheckInButtonState();
                row.ChkInclude.CheckedChanged += (s, e) => UpdateCheckInButtonState();
                
                pnlFiles.Controls.Add(row);
                _rows.Add(row);
                
                y += row.Height + 2;

                if (change.ChangeType != FileChangeType.Unchanged)
                {
                    changeCount++;
                }
            }

            if (_changes.Count == 0)
            {
                lblStatus.Text = "No files found to check in.";
                lblStatus.ForeColor = Color.Red;
            }
            else if (changeCount == 0)
            {
                lblStatus.Text = "No files have been modified.";
                lblStatus.ForeColor = Color.Green;
            }
            else
            {
                lblStatus.Text = $"Found {changeCount} modified/new file(s).";
                lblStatus.ForeColor = Color.Green;
            }
            
            UpdateCheckInButtonState();
        }

        private void UpdateCheckInButtonState()
        {
            bool anyChecked = false;
            bool anyInvalid = false;
            foreach (var row in _rows)
            {
                if (row.ChkInclude.Checked)
                {
                    anyChecked = true;
                    if (!row.IsValid)
                    {
                        anyInvalid = true;
                    }
                }
            }
            btnCheckIn.Enabled = anyChecked && !anyInvalid;
        }

        private async void btnCheckIn_Click(object sender, EventArgs e)
        {
            string notes = txtNotes.Text.Trim();
            if (string.IsNullOrEmpty(notes))
            {
                MessageBox.Show(this, "Check-In Notes are required.", "Missing Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNotes.Focus();
                return;
            }

            var selectedFiles = new List<string>();
            var localRenames = new List<(string OriginalPath, string NewPath)>();

            foreach (var row in _rows)
            {
                if (row.ChkInclude.Checked)
                {
                    if (row.HasConflict)
                    {
                        string newLocalPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(row.ChangeInfo.LocalPath), row.SelectedNewName);
                        if (!string.Equals(row.ChangeInfo.LocalPath, newLocalPath, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                System.IO.File.Move(row.ChangeInfo.LocalPath, newLocalPath);
                                localRenames.Add((row.ChangeInfo.LocalPath, newLocalPath));
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, $"Failed to rename local file {row.ChangeInfo.FileName}:\n{ex.Message}", "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                
                                // Rollback local renames that already succeeded in this batch
                                foreach (var rename in localRenames)
                                {
                                    try
                                    {
                                        System.IO.File.Move(rename.NewPath, rename.OriginalPath);
                                    }
                                    catch { /* best effort rollback */ }
                                }
                                return;
                            }
                        }
                        selectedFiles.Add(newLocalPath);
                    }
                    else
                    {
                        selectedFiles.Add(row.ChangeInfo.LocalPath);
                    }
                }
            }

            if (selectedFiles.Count == 0)
            {
                MessageBox.Show(this, "No files are selected for check-in.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lblStatus.Text = "Pushing files to server...";
            lblStatus.ForeColor = Color.Blue;
            btnCheckIn.Enabled = false;
            btnCancel.Enabled = false;
            
            var pb = new ProgressBar { Minimum = 0, Maximum = 100, Location = new Point(140, 15), Size = new Size(200, 20) };
            if (this.Controls.Find("pnlFooter", true).FirstOrDefault() is Panel pnlFooter)
            {
                pnlFooter.Controls.Add(pb);
            }
            var progress = new Progress<int>(percent => { pb.Value = percent; });
            
            this.Refresh();

            try
            {
                OperationResult result = await System.Threading.Tasks.Task.Run(() => FileOperations.CheckIn(_breadcrumbRoot, selectedFiles, progress));

                if (result.AffectedFiles != null && result.AffectedFiles.Count > 0)
                {
                                        string logError = LogManager.AppendCheckInEntry(_serverRoot, result.AffectedFiles, notes, "File Checked In");
                    
                    // Release orphaned locks for files renamed in the UI
                    var originalRenames = localRenames.Select(r => r.OriginalPath).ToList();
                    if (originalRenames.Count > 0)
                    {
                        foreach (string orig in originalRenames)
                        {
                            string srvOrig = BreadcrumbTracker.ResolveServerPath(orig, _breadcrumbRoot);
                            if (srvOrig != null)
                            {
                                try { System.IO.File.Delete(FileOperations.GetLockFilePath(srvOrig)); } catch { }
                            }
                        }
                        LogManager.AppendCheckInEntry(_serverRoot, originalRenames, "File renamed and checked in as new revision", "File Checked In");
                    }
                    if (logError != null)
                    {
                        MessageBox.Show(this, $"Files were pushed successfully, but the history log could not be updated:\n\n{logError}", "History Log Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                CheckInResult = result;
                if (result.Success)
                {
                    MessageBox.Show(this, result.Message, "Check-In Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    string detail = result.Message;
                    if (result.Errors != null && result.Errors.Count > 0)
                    {
                        detail += "\n\nErrors:\n• " + string.Join("\n• ", result.Errors);
                    }
                    MessageBox.Show(this, detail, "Check-In Completed with Issues", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    
                    RefreshScan();
                    btnCancel.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unexpected error during check-in:\n{ex.Message}", "Check-In Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnCancel.Enabled = true;
                UpdateCheckInButtonState();
            }
        }
        
        public class FileRowControl : UserControl
        {
            public CheckBox ChkInclude { get; private set; }
            public FileChangeInfo ChangeInfo { get; private set; }
            
            private TextBox txtNewName;
            private Label lblExtension;
            private string originalBaseName;
            private string originalExt;

            public bool HasConflict { get; private set; }
            public bool IsValid { get; private set; } = true;
            public string SelectedNewName => HasConflict ? (txtNewName.Text + originalExt) : ChangeInfo.FileName;
            
            public event EventHandler ValidationChanged;

            public FileRowControl(FileChangeInfo info, bool hasConflict)
            {
                ChangeInfo = info;
                HasConflict = hasConflict;
                
                this.Height = 35;
                this.Width = 540;
                
                ChkInclude = new CheckBox
                {
                    Text = info.RelativePath + (hasConflict ? "" : $"  [{info.ChangeTypeDisplay}]"),
                    AutoSize = true,
                    Location = new Point(5, 8),
                    Checked = true
                };
                this.Controls.Add(ChkInclude);

                if (hasConflict)
                {
                    ChkInclude.Text = "";
                    ChkInclude.AutoSize = false;
                    ChkInclude.Width = 20;

                    originalBaseName = System.IO.Path.GetFileNameWithoutExtension(info.FileName);
                    originalExt = System.IO.Path.GetExtension(info.FileName);

                    txtNewName = new TextBox { Width = 300, Margin = new Padding(2, 2, 0, 0), Text = originalBaseName };
                    lblExtension = new Label { Text = originalExt, AutoSize = true, Margin = new Padding(2, 5, 0, 0) };

                    var flowPanel = new FlowLayoutPanel
                    {
                        FlowDirection = FlowDirection.LeftToRight,
                        AutoSize = true,
                        Location = new Point(25, 5),
                        WrapContents = false
                    };
                    flowPanel.Controls.Add(txtNewName);
                    flowPanel.Controls.Add(lblExtension);
                    
                    var conflictWarning = new Label { Text = "⚠ Exists on server", ForeColor = Color.Red, AutoSize = true, Margin = new Padding(10, 5, 0, 0) };
                    flowPanel.Controls.Add(conflictWarning);

                    this.Controls.Add(flowPanel);

                    txtNewName.TextChanged += async (s, e) => await ValidateNameAsync();

                    _ = ValidateNameAsync();
                }
            }
            
            private async System.Threading.Tasks.Task ValidateNameAsync()
            {
                string newServerPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ChangeInfo.ServerPath), SelectedNewName);
                bool exists = await System.Threading.Tasks.Task.Run(() => System.IO.File.Exists(newServerPath));
                
                if (this.IsDisposed) return;
                
                if (exists)
                {
                    txtNewName.BackColor = Color.LightPink;
                    IsValid = false;
                }
                else
                {
                    txtNewName.BackColor = SystemColors.Window;
                    IsValid = true;
                }
                ValidationChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

