using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClockEnforcer
{
    public class LockWarningForm : Form
    {
        private readonly Label messageLabel;
        private readonly Button okButton;
        private readonly Timer countdownTimer;
        private readonly string baseButtonText = "OK";
        private int secondsRemaining;

        public LockWarningForm(string message, TimeSpan timeout)
        {
            secondsRemaining = (int)Math.Ceiling(timeout.TotalSeconds);
            if (secondsRemaining <= 0)
            {
                secondsRemaining = 1;
            }

            Text = "Clock Enforcer";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ControlBox = false;
            TopMost = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(20);

            messageLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Text = message,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10f)
            };

            okButton = new Button
            {
                DialogResult = DialogResult.OK,
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            UpdateOkButtonText();
            okButton.Click += (_, _) => CloseDialog();

            countdownTimer = new Timer { Interval = 1000 };
            countdownTimer.Tick += CountdownTimer_Tick;

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2
            };

            layout.Controls.Add(messageLabel, 0, 0);
            layout.Controls.Add(okButton, 0, 1);
            layout.SetColumnSpan(messageLabel, 1);
            layout.SetColumnSpan(okButton, 1);
            layout.Padding = new Padding(0, 0, 0, 10);

            Controls.Add(layout);

            Shown += (_, _) => countdownTimer.Start();
            AcceptButton = okButton;
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            secondsRemaining--;
            if (secondsRemaining <= 0)
            {
                countdownTimer.Stop();
                okButton.PerformClick();
                return;
            }

            UpdateOkButtonText();
        }

        private void UpdateOkButtonText()
        {
            okButton.Text = $"{baseButtonText} ({secondsRemaining})";
        }

        private void CloseDialog()
        {
            countdownTimer.Stop();
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                countdownTimer.Dispose();
                messageLabel.Dispose();
                okButton.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
