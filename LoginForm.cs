
// LoginForm.cs
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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
        private readonly string overtimeLogPath = AppPaths.GetPath("overtime_debug_log.txt");
        private readonly string errorLogPath = AppPaths.GetPath("error_log.txt");
        private bool loginAlreadyLogged = false;

        private System.Windows.Forms.Timer loginTimer;
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

            this.Text = "Clock Enforcer";




            // Leer la API key del entorno
            string apiKey = Environment.GetEnvironmentVariable("SAASHR_API_KEY", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Por favor define SAASHR_API_KEY en tus Environment Variables",
                                "Falta API Key",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            authService = new AuthService(apiKey);
            punchService = new PunchService(authService);

            statusTextBox.Text = "Please log in. You have 2 minutes to clock in before forced lock.";

            this.Load += LoginForm_Load;
            SetupTrayIcon();
            this.Resize += LoginForm_Resize;

            loginTimer = new System.Windows.Forms.Timer { Interval = 120_000 };
            loginTimer.Tick += LoginTimer_Tick;
            loginTimer.Start();
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            string user = Environment.UserName;
            btnRequestOvertime.Enabled = logService.GetTodayLoginCount(user) >= 2;
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

        

        private void LoginTimer_Tick(object sender, EventArgs e)
        {
            loginTimer.Stop();
            MessageBox.Show("You did not clock in within 2 minutes. Locking workstation.");
            new PCLoginEnforcer().ForceUserLogOff();
            loginTimer.Start();
        }

        private async void OvertimeCheckTimer_Tick(object sender, EventArgs e)
        {
            if (overtimeAdded) return;

            string user = Environment.UserName;
            string url = $"https://clock.adrianas.com/backend/timeclock/overtime/today?ad_username={user}";

            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                File.AppendAllText(overtimeLogPath, $"{DateTime.Now}: Raw JSON: {json}{Environment.NewLine}");

                if (!response.IsSuccessStatusCode) return;

                var result = JsonSerializer.Deserialize<OvertimeTodayResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result?.data?.accepted == true)
                {
                    overtimeAdded = true;
                    File.AppendAllText(overtimeLogPath, $"{DateTime.Now}: Overtime approved: {result.data.hours}h{Environment.NewLine}");
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

                if (logService.IsUserLockedOut(user))
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
            string resp = await punchService.PunchAsync();
            statusTextBox.Text = resp;

            if (resp.Contains("PUNCH_OUT"))
            {
                await Task.Delay(5000);
                new PCLoginEnforcer().ForceUserLogOff();
            }
            else if (resp.Contains("PUNCH_IN"))
            {
                loginTimer?.Stop();
                loginTimer?.Dispose();
                loginTimer = null;

                btnRequestOvertime.Enabled = logService.GetTodayLoginCount(user) >= 2;

                overtimeAdded = false;
                overtimeCheckTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
                overtimeCheckTimer.Tick += OvertimeCheckTimer_Tick;
                overtimeCheckTimer.Start();

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

    }

    public class OvertimeTodayResponse
    {
        public int status { get; set; }
        public string label { get; set; }
        public OvertimeData data { get; set; }
    }

    public class OvertimeData
    {
        public bool accepted { get; set; }
        public decimal hours { get; set; }
    }
}
