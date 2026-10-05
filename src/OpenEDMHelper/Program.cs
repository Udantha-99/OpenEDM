using System;
using System.Linq;
using System.Windows.Forms;
using OpenEDMShellExtension.Core;

namespace OpenEDMHelper
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length < 2) return;

            string command = args[0].ToLowerInvariant();
            string arg = args[1];

            try
            {
                if (command == "sync")
                {
                    var paths = arg.Split('|').ToList();
                    OperationResult result = null;

                    var form = new Form { Text = "Syncing to OpenEDM", Size = new System.Drawing.Size(400, 120), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, ControlBox = false };
                    var lbl = new Label { Text = "Copying files from server to OpenEDM...", Location = new System.Drawing.Point(20, 15), AutoSize = true };
                    var pb = new ProgressBar { Minimum = 0, Maximum = 100, Location = new System.Drawing.Point(20, 40), Size = new System.Drawing.Size(340, 25) };
                    form.Controls.Add(lbl);
                    form.Controls.Add(pb);

                    var progress = new Progress<int>(percent => { pb.Value = percent; });

                    form.Shown += async (s, e) =>
                    {
                        result = await System.Threading.Tasks.Task.Run(() => FileOperations.SyncToOpenEDM(paths, progress));
                        form.Close();
                    };
                    form.ShowDialog();

                    ShowResult(result, "Check Out", "Check Out Unsuccessful");
                }
                else if (command == "acquire")
                {
                    OperationResult result = FileOperations.AcquireLock(arg);
                    ShowResult(result, "Acquire Lock", "Lock Acquisition Unsuccessful");
                }
                else if (command == "checkin" || command == "batchcheckin")
                {
                    string root = BreadcrumbTracker.FindBreadcrumbRoot(arg);
                    if (string.IsNullOrEmpty(root))
                    {
                        ShowResult(OperationResult.Fail("No .sourcepath.txt breadcrumb found."), "Check In", "Check In Unsuccessful");
                    }
                    else
                    {
                        OperationResult result = OpenEDMShellExtension.UI.CheckInManagerForm.ShowCheckInDialog(root);
                        if (result != null)
                        {
                            ShowResult(result, "Check In", "Check In Unsuccessful");
                        }
                    }
                }
                else if (command == "release")
                {
                    OperationResult result = FileOperations.ReleaseLock(arg);
                    ShowResult(result, "Release Lock", "Release Unsuccessful");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred:\n\n{ex.Message}", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowResult(OperationResult result, string successTitle, string failTitle)
        {
            if (result.Success)
            {
                MessageBox.Show(result.Message, successTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string detail = result.Message;
                if (result.Errors != null && result.Errors.Count > 0)
                {
                    detail += "\n\nDetails:\n• " + string.Join("\n• ", result.Errors);
                }
                MessageBox.Show(detail, failTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
