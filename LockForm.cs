using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeployKeyGitClient
{
    public class LockForm : Form
    {
        private readonly Func<string, bool> _verify;
        private Label lbl = null!;
        private TextBox txt;
        private Button btnOk;
        private Button btnCancel;
        private Label lblMsg;

        public int MaxAttempts { get; set; } = 5;

        public LockForm(Func<string, bool> verify, string title = "Unlock")
        {
            _verify = verify ?? throw new ArgumentNullException(nameof(verify));
            Text = title;
            Width = 420;
            Height = 180;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Initialize();
        }

        private void Initialize()
        {
            lbl = new Label { Text = "Enter unlock password:", Left = 12, Top = 12, AutoSize = true };
            txt = new TextBox { Left = 12, Top = 36, Width = 380, UseSystemPasswordChar = true };
            lblMsg = new Label { Left = 12, Top = 68, Width = 380, Height = 24, ForeColor = Color.Red, Text = "" };

            btnOk = new Button { Text = "Unlock", Left = 220, Width = 80, Top = 96, DialogResult = DialogResult.None };
            btnCancel = new Button { Text = "Cancel", Left = 312, Width = 80, Top = 96, DialogResult = DialogResult.Cancel };

            btnOk.Click += BtnOk_Click;
            txt.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };

            Controls.AddRange(new Control[] { lbl, txt, lblMsg, btnOk, btnCancel });
        }

        private int _attempts = 0;

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            var pw = txt.Text ?? "";
            if (_verify(pw))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            _attempts++;
            lblMsg.Text = $"Password incorrect. Attempts: {_attempts}/{MaxAttempts}";
            txt.Clear();
            txt.Focus();
            if (_attempts >= MaxAttempts)
            {
                MessageBox.Show("Maximum attempts reached. Closing application.", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Exit();
            }
        }
    }
}
