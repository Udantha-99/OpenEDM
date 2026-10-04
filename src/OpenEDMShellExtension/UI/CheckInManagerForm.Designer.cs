namespace OpenEDMShellExtension.UI
{
    partial class CheckInManagerForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlHeader        = new System.Windows.Forms.Panel();
            this.lblTitle         = new System.Windows.Forms.Label();
            this.lblPathCaption   = new System.Windows.Forms.Label();
            this.txtServerPath    = new System.Windows.Forms.TextBox();
            this.lblFiles         = new System.Windows.Forms.Label();
            this.pnlFiles         = new System.Windows.Forms.Panel();
            this.lblNotes         = new System.Windows.Forms.Label();
            this.txtNotes         = new System.Windows.Forms.TextBox();
            this.pnlFooter        = new System.Windows.Forms.Panel();
            this.lblStatus        = new System.Windows.Forms.Label();
            this.btnCancel        = new System.Windows.Forms.Button();
            this.btnCheckIn       = new System.Windows.Forms.Button();
            this.pnlHeader.SuspendLayout();
            this.pnlFooter.SuspendLayout();
            this.SuspendLayout();

            // ── Header Panel ─────────────────────────────────────────────
            this.pnlHeader.BackColor  = System.Drawing.Color.FromArgb(0, 99, 177);   // Windows 11 accent blue
            this.pnlHeader.Dock       = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Height     = 52;
            this.pnlHeader.Name       = "pnlHeader";
            this.pnlHeader.Controls.Add(this.lblTitle);

            // lblTitle
            this.lblTitle.AutoSize  = false;
            this.lblTitle.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Font      = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Regular);
            this.lblTitle.Text      = "  Check-In to Server";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblTitle.Name      = "lblTitle";

            // ── Server path (readonly textbox — selectable, scrollable) ──
            this.lblPathCaption.AutoSize  = true;
            this.lblPathCaption.Location  = new System.Drawing.Point(14, 64);
            this.lblPathCaption.ForeColor = System.Drawing.Color.FromArgb(96, 96, 96);
            this.lblPathCaption.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblPathCaption.Text      = "SERVER PATH";
            this.lblPathCaption.Name      = "lblPathCaption";

            this.txtServerPath.Location    = new System.Drawing.Point(14, 82);
            this.txtServerPath.ReadOnly    = true;
            this.txtServerPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtServerPath.BackColor   = System.Drawing.Color.FromArgb(243, 243, 243);
            this.txtServerPath.ForeColor   = System.Drawing.Color.FromArgb(30, 30, 30);
            this.txtServerPath.Size        = new System.Drawing.Size(572, 23);
            this.txtServerPath.TabStop     = false;
            this.txtServerPath.Name        = "txtServerPath";
            this.txtServerPath.Font        = new System.Drawing.Font("Segoe UI", 9F);

            // ── Files list ───────────────────────────────────────────────
            this.lblFiles.AutoSize  = true;
            this.lblFiles.Location  = new System.Drawing.Point(14, 118);
            this.lblFiles.ForeColor = System.Drawing.Color.FromArgb(96, 96, 96);
            this.lblFiles.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblFiles.Text      = "FILES TO CHECK IN";
            this.lblFiles.Name      = "lblFiles";

            this.btnSelectAll = new System.Windows.Forms.Button();
            this.btnSelectAll.Location = new System.Drawing.Point(410, 110);
            this.btnSelectAll.Size = new System.Drawing.Size(80, 24);
            this.btnSelectAll.Text = "Select All";
            this.btnSelectAll.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnSelectAll.Click += (s, e) => { foreach (var r in _rows) r.ChkInclude.Checked = true; };

            this.btnDeselectAll = new System.Windows.Forms.Button();
            this.btnDeselectAll.Location = new System.Drawing.Point(495, 110);
            this.btnDeselectAll.Size = new System.Drawing.Size(90, 24);
            this.btnDeselectAll.Text = "Deselect All";
            this.btnDeselectAll.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnDeselectAll.Click += (s, e) => { foreach (var r in _rows) r.ChkInclude.Checked = false; };

            this.pnlFiles.Location           = new System.Drawing.Point(14, 136);
            this.pnlFiles.Name               = "pnlFiles";
            this.pnlFiles.Size               = new System.Drawing.Size(572, 160);
            this.pnlFiles.BorderStyle        = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlFiles.TabIndex           = 1;
            this.pnlFiles.AutoScroll         = true;

            // ── Notes ────────────────────────────────────────────────────
            this.lblNotes.AutoSize  = true;
            this.lblNotes.Location  = new System.Drawing.Point(14, 308);
            this.lblNotes.ForeColor = System.Drawing.Color.FromArgb(96, 96, 96);
            this.lblNotes.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblNotes.Text      = "CHECK-IN NOTES (REQUIRED)";
            this.lblNotes.Name      = "lblNotes";

            this.txtNotes.Location    = new System.Drawing.Point(14, 326);
            this.txtNotes.Multiline   = true;
            this.txtNotes.Name        = "txtNotes";
            this.txtNotes.Size        = new System.Drawing.Size(572, 56);
            this.txtNotes.TabIndex    = 2;
            this.txtNotes.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtNotes.Font        = new System.Drawing.Font("Segoe UI", 9F);

            // ── Footer Panel ─────────────────────────────────────────────
            this.pnlFooter.BackColor  = System.Drawing.Color.FromArgb(243, 243, 243);
            this.pnlFooter.Dock       = System.Windows.Forms.DockStyle.Bottom;
            this.pnlFooter.Height     = 50;
            this.pnlFooter.Name       = "pnlFooter";
            this.pnlFooter.Controls.Add(this.lblStatus);
            this.pnlFooter.Controls.Add(this.btnCheckIn);
            this.pnlFooter.Controls.Add(this.btnCancel);

            // lblStatus
            this.lblStatus.AutoSize  = false;
            this.lblStatus.Location  = new System.Drawing.Point(14, 15);
            this.lblStatus.Size      = new System.Drawing.Size(340, 20);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(0, 99, 177);
            this.lblStatus.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStatus.Text      = "Scanning files...";
            this.lblStatus.Name      = "lblStatus";

            // btnCancel
            this.btnCancel.DialogResult      = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.FlatStyle         = System.Windows.Forms.FlatStyle.System;
            this.btnCancel.Location          = new System.Drawing.Point(462, 12);
            this.btnCancel.Name              = "btnCancel";
            this.btnCancel.Size              = new System.Drawing.Size(90, 28);
            this.btnCancel.TabIndex          = 4;
            this.btnCancel.Text              = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;

            // btnCheckIn
            this.btnCheckIn.FlatStyle        = System.Windows.Forms.FlatStyle.System;
            this.btnCheckIn.Location         = new System.Drawing.Point(360, 12);
            this.btnCheckIn.Name             = "btnCheckIn";
            this.btnCheckIn.Size             = new System.Drawing.Size(96, 28);
            this.btnCheckIn.TabIndex         = 3;
            this.btnCheckIn.Text             = "Check In";
            this.btnCheckIn.UseVisualStyleBackColor = true;
            this.btnCheckIn.Click           += new System.EventHandler(this.btnCheckIn_Click);

            // ── Form ─────────────────────────────────────────────────────
            this.AcceptButton        = this.btnCheckIn;
            this.CancelButton        = this.btnCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(600, 440);
            this.Font                = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle     = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox         = false;
            this.MinimizeBox         = false;
            this.Name                = "CheckInManagerForm";
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text                = "OpenEDM Check-In";
            this.BackColor           = System.Drawing.Color.White;

            this.Controls.Add(this.pnlHeader);
            this.Controls.Add(this.lblPathCaption);
            this.Controls.Add(this.txtServerPath);
            this.Controls.Add(this.lblFiles);
            this.Controls.Add(this.btnSelectAll);
            this.Controls.Add(this.btnDeselectAll);
            this.Controls.Add(this.pnlFiles);
            this.Controls.Add(this.lblNotes);
            this.Controls.Add(this.txtNotes);
            this.Controls.Add(this.pnlFooter);

            this.pnlHeader.ResumeLayout(false);
            this.pnlFooter.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Panel          pnlHeader;
        private System.Windows.Forms.Label          lblTitle;
        private System.Windows.Forms.Label          lblPathCaption;
        private System.Windows.Forms.TextBox        txtServerPath;
        private System.Windows.Forms.Label          lblFiles;
        private System.Windows.Forms.Button         btnSelectAll;
        private System.Windows.Forms.Button         btnDeselectAll;
        private System.Windows.Forms.Panel          pnlFiles;
        private System.Windows.Forms.Label          lblNotes;
        private System.Windows.Forms.TextBox        txtNotes;
        private System.Windows.Forms.Panel          pnlFooter;
        private System.Windows.Forms.Label          lblStatus;
        private System.Windows.Forms.Button         btnCancel;
        private System.Windows.Forms.Button         btnCheckIn;
    }
}
