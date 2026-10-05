using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using OpenEDMShellExtension.Core;
using OpenEDMShellExtension.UI;

namespace OpenEDMShellExtension.Shell
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFilesAndFolders)]
    [COMServerAssociation(AssociationType.Directory)]
    [DisplayName("OpenEDM File Manager")]
    [Guid("B4E7F1A2-3C8D-4E5F-9A0B-1C2D3E4F5A6B")]
    public class OpenEDMContextMenu : SharpContextMenu
    {
        private enum MenuMode { None, SyncProject, AcquireLock, CheckInOrRelease, BatchFolder }
        private MenuMode _mode = MenuMode.None;

        protected override bool CanShowMenu()
        {
            try
            {
                var paths = SelectedItemPaths?.ToList();
                if (paths == null || paths.Count == 0)
                    return false;

                string firstPath = paths[0];

                if (Configuration.IsOnSourceDrive(firstPath))
                {
                    try
                    {
                        if (File.GetAttributes(firstPath).HasFlag(FileAttributes.Directory))
                        {
                            _mode = MenuMode.SyncProject;
                            return true;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }

                if (Configuration.IsUnderOpenEDMPath(firstPath))
                {
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(firstPath);
                    }
                    catch
                    {
                        return false;
                    }

                    if (!attributes.HasFlag(FileAttributes.Directory))
                    {
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            _mode = MenuMode.AcquireLock;
                        }
                        else
                        {
                            _mode = MenuMode.CheckInOrRelease;
                        }
                        return true;
                    }
                    else
                    {
                        _mode = MenuMode.BatchFolder;
                        return true;
                    }
                }

                _mode = MenuMode.None;
                return false;
            }
            catch
            {
                _mode = MenuMode.None;
                return false;
            }
        }

        protected override ContextMenuStrip CreateMenu()
        {
            var menu = new ContextMenuStrip();

            try
            {
                switch (_mode)
                {
                    case MenuMode.SyncProject:
                        menu.Items.Add(CreateMenuItem("Check Out", "sync", string.Join("|", SelectedItemPaths), GetSyncIcon()));
                        break;
                    case MenuMode.AcquireLock:
                        menu.Items.Add(CreateMenuItem("Acquire Lock", "acquire", SelectedItemPaths.First(), GetCheckOutIcon()));
                        break;
                    case MenuMode.CheckInOrRelease:
                        menu.Items.Add(CreateMenuItem("Check In", "checkin", SelectedItemPaths.First(), GetCheckInIcon()));
                        menu.Items.Add(CreateMenuItem("Release Lock", "release", SelectedItemPaths.First(), GetUndoIcon()));
                        break;
                    case MenuMode.BatchFolder:
                        menu.Items.Add(CreateMenuItem("Check In", "batchcheckin", SelectedItemPaths.First(), GetCheckInIcon()));
                        menu.Items.Add(CreateMenuItem("Acquire Lock", "batchacquire", SelectedItemPaths.First(), GetCheckOutIcon()));
                        break;
                }
            }
            catch
            {
                // Never crash explorer
            }

            return menu;
        }

        private ToolStripMenuItem CreateMenuItem(string text, string command, string arg, Image icon)
        {
            var item = new ToolStripMenuItem { Text = text, Image = icon };
            item.Click += (s, e) => LaunchHelper(command, arg);
            return item;
        }

        private static Image DrawIcon(Color color, Action<Graphics> drawAction)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                drawAction(g);
            }
            return bmp;
        }

        private static Image _syncIcon;
        private static Image _checkOutIcon;
        private static Image _checkInIcon;
        private static Image _undoIcon;

        private static Image GetSyncIcon() => _syncIcon ?? (_syncIcon = DrawIcon(Color.DodgerBlue, g => {
            using (var p = new Pen(Color.DodgerBlue, 2)) {
                g.DrawLine(p, 8, 2, 8, 14); g.DrawLine(p, 8, 14, 4, 10); g.DrawLine(p, 8, 14, 12, 10);
            }
        }));

        private static Image GetCheckOutIcon() => _checkOutIcon ?? (_checkOutIcon = DrawIcon(Color.MediumSeaGreen, g => {
            using (var p = new Pen(Color.MediumSeaGreen, 2)) {
                g.DrawLine(p, 2, 14, 6, 14); g.DrawLine(p, 2, 14, 2, 10);
                g.DrawLine(p, 2, 10, 10, 2); g.DrawLine(p, 10, 2, 14, 6); g.DrawLine(p, 14, 6, 6, 14);
            }
        }));

        private static Image GetCheckInIcon() => _checkInIcon ?? (_checkInIcon = DrawIcon(Color.DodgerBlue, g => {
            using (var p = new Pen(Color.DodgerBlue, 2)) {
                g.DrawLine(p, 3, 8, 7, 12); g.DrawLine(p, 7, 12, 14, 3);
            }
        }));

        private static Image GetUndoIcon() => _undoIcon ?? (_undoIcon = DrawIcon(Color.Crimson, g => {
            using (var p = new Pen(Color.Crimson, 2)) {
                g.DrawArc(p, 2, 2, 12, 12, 90, 270); g.DrawLine(p, 2, 8, 6, 8); g.DrawLine(p, 2, 8, 2, 4);
            }
        }));

        private void LaunchHelper(string command, string arg)
        {
            try
            {
                string helperPath = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "OpenEDMHelper.exe");
                if (!File.Exists(helperPath)) return;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = helperPath,
                    Arguments = $"{command} \"{arg}\"",
                    UseShellExecute = true
                });
            }
            catch { /* never crash explorer */ }
        }
    }
}
