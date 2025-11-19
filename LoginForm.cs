
// LoginForm.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using ClockEnforcer.Services;
using System.Runtime.InteropServices;

namespace ClockEnforcer
{
    public partial class LoginForm : Form
    {
        private readonly AuthService authService;
        private readonly PunchService punchService;
        private readonly LogService logService = new LogService();
        private readonly SessionEnforcementManager sessionManager;
        private readonly OvertimeRequestService overtimeService = new OvertimeRequestService();
        private readonly string overtimeLogPath;
        private readonly string errorLogPath;

        private System.Windows.Forms.Timer overtimeCheckTimer;
        private System.Windows.Forms.Timer loginReenableTimer;

        private bool overtimeAdded = false;
        private const int WM_COPYDATA = 0x004A;
        private NotifyIcon trayIcon;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_COPYDATA && m.LParam != IntPtr.Zero)
            {
                COPYDATASTRUCT cds = Marshal.PtrToStructure<COPYDATASTRUCT>(m.LParam);
                string command = cds.cbData > 0 && cds.lpData != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(cds.lpData, cds.cbData)?.TrimEnd('\0')
                    : null;

                if (string.Equals(command, "SHOW", StringComparison.OrdinalIgnoreCase))
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        this.Show();
                        this.WindowState = FormWindowState.Normal;
                        this.ShowInTaskbar = true;
                        this.BringToFront();
                    }));
                }
            }

            base.WndProc(ref m);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }


        public LoginForm()
        {
            InitializeComponent();

            Text = "Clock Enforcer";

            authService = new AuthService(SaashrConfig.ApiKey);
            punchService = new PunchService(authService);
            sessionManager = new SessionEnforcementManager(Environment.UserName);
            SessionContext.SessionManager = sessionManager;
            sessionManager.WarningIssued += SessionManager_WarningIssued;
            sessionManager.StartPreLoginCountdown();

            overtimeLogPath = Path.Combine(LogService.ApplicationFolder, "overtime_debug_log.txt");
            errorLogPath = Path.Combine(LogService.ApplicationFolder, "error_log.txt");

            statusTextBox.Text = "Please log in. You have 2 minutes to clock in before forced lock.";

            Load += LoginForm_Load;
            FormClosed += LoginForm_FormClosed;
            SetupTrayIcon();
            Resize += LoginForm_Resize;
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            string user = Environment.UserName;
            btnRequestOvertime.Enabled = logService.GetTodayLoginCount(user) >= 2;
        }

        private void LoginForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            StopOvertimeMonitoring();

            if (loginReenableTimer != null)
            {
                loginReenableTimer.Stop();
                loginReenableTimer.Dispose();
                loginReenableTimer = null;
            }

            sessionManager.WarningIssued -= SessionManager_WarningIssued;
            sessionManager.Dispose();
            if (ReferenceEquals(SessionContext.SessionManager, sessionManager))
            {
                SessionContext.SessionManager = null;
            }

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        private void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Clock Enforcer Running"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.BringToFront();
            });
            menu.Items.Add("Exit", null, (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });
            trayIcon.ContextMenuStrip = menu;

            trayIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.BringToFront();
            };
        }

        private void SessionManager_WarningIssued(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)(() => DisplayWarning(message)));
            }
            else
            {
                DisplayWarning(message);
            }
        }

        private void DisplayWarning(string message)
        {
            statusTextBox.Text = message;
            trayIcon?.ShowBalloonTip(3000, "Clock Enforcer", message, ToolTipIcon.Warning);
            MessageBox.Show(message, "Clock Enforcer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void LoginForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
        private async void OvertimeCheckTimer_Tick(object sender, EventArgs e)
        {
            if (overtimeAdded)
            {
                return;
            }

            string user = Environment.UserName;

            try
            {
                var result = await overtimeService.GetTodayOvertimeStatusAsync(user);
                if (result?.Data?.Accepted == true)
                {
                    overtimeAdded = true;
                    double totalHours = 8 + (double)result.Data.Hours;
                    sessionManager.StartForcedLogoutTimer(totalHours);
                    File.AppendAllText(overtimeLogPath,
                        $"{DateTime.Now}: Overtime approved for {user} -> {result.Data.Hours}h extension{Environment.NewLine}");
                    trayIcon?.ShowBalloonTip(3000, "Clock Enforcer", "Overtime approved. Shift extended.", ToolTipIcon.Info);
                    statusTextBox.Text = "Overtime approved. Shift limit extended.";
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(overtimeLogPath, $"{DateTime.Now}: ERROR: {ex.Message}{Environment.NewLine}");
            }
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            btnLogin.Enabled = false;

            try
            {
                string user = txtUsername.Text;
                string pass = txtPassword.Text;
                string systemUser = Environment.UserName;

                if (logService.IsUserLockedOut(systemUser))
                {
                    statusTextBox.Text = "Access Denied: Locked out.";
                    return;
                }

                bool authenticated = await authService.AuthenticateUserAsync(user, pass);
                if (!authenticated)
                {
                    statusTextBox.Text = "Invalid credentials.";
                    return;
                }

                statusTextBox.Text = "Login successful. Punching in...";
                await Task.Delay(1000);
                await ProcessPunchAsync(user, pass);
            }
            catch (Exception ex)
            {
                statusTextBox.Text = "An error occurred during login. Please try again.";
                File.AppendAllText(errorLogPath, $"{DateTime.Now}: {ex}{Environment.NewLine}");
            }
            finally
            {
                // Re-enable only if loginReenableTimer hasn't taken over (e.g., after punch-in success)
                if (loginReenableTimer == null)
                {
                    btnLogin.Enabled = true;
                }
            }
        }

        private async Task ProcessPunchAsync(string user, string pass)
        {
            string resp = await punchService.PunchAsync(user, pass);
            statusTextBox.Text = resp;
            string systemUser = Environment.UserName;

            if (resp.Contains("PUNCH_OUT", StringComparison.OrdinalIgnoreCase))
            {
                sessionManager.OnPunchOut(logService.HasCompletedShiftForToday(systemUser));
                StopOvertimeMonitoring();
                if (loginReenableTimer != null)
                {
                    loginReenableTimer.Stop();
                    loginReenableTimer.Dispose();
                    loginReenableTimer = null;
                }

                btnLogin.Enabled = true;
                await Task.Delay(5000);
                new PCLoginEnforcer().ForceUserLogOff();
            }
            else if (resp.Contains("PUNCH_IN", StringComparison.OrdinalIgnoreCase))
            {
                sessionManager.OnPunchIn();
                btnRequestOvertime.Enabled = logService.GetTodayLoginCount(systemUser) >= 2;

                StartOvertimeMonitoring();

                loginReenableTimer?.Stop();
                loginReenableTimer?.Dispose();
                loginReenableTimer = new System.Windows.Forms.Timer { Interval = 1_800_000 };
                loginReenableTimer.Tick += (s, e) =>
                {
                    loginReenableTimer.Stop();
                    loginReenableTimer.Dispose();
                    loginReenableTimer = null;

                    if (btnLogin.InvokeRequired)
                    {
                        btnLogin.Invoke((MethodInvoker)(() => btnLogin.Enabled = true));
                    }
                    else
                    {
                        btnLogin.Enabled = true;
                    }

                    trayIcon.ShowBalloonTip(3000, "Clock Enforcer", "Login reenabled after 30 minutes.", ToolTipIcon.Info);
                };
                loginReenableTimer.Start();
            }
        }

        private void StartOvertimeMonitoring()
        {
            overtimeAdded = false;
            StopOvertimeMonitoring();
            overtimeCheckTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            overtimeCheckTimer.Tick += OvertimeCheckTimer_Tick;
            overtimeCheckTimer.Start();
        }

        private void StopOvertimeMonitoring()
        {
            if (overtimeCheckTimer != null)
            {
                overtimeCheckTimer.Stop();
                overtimeCheckTimer.Tick -= OvertimeCheckTimer_Tick;
                overtimeCheckTimer.Dispose();
                overtimeCheckTimer = null;
            }
        }

    }
}
