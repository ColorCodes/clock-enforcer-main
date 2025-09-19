using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClockEnforcer
{
    internal static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        private const int WM_COPYDATA = 0x004A;

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "ClockEnforcerAppMutex", out createdNew))
            {
                if (!createdNew)
                {
                    // Send "SHOW" message to the already running instance
                    IntPtr hWnd = FindWindow(null, "Clock Enforcer"); // This must match the form's title
                    if (hWnd != IntPtr.Zero)
                    {
                        byte[] sarr = System.Text.Encoding.UTF8.GetBytes("SHOW");
                        COPYDATASTRUCT cds = new COPYDATASTRUCT
                        {
                            dwData = IntPtr.Zero,
                            cbData = sarr.Length + 1,
                            lpData = "SHOW"
                        };

                        SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
                    }
                    return;
                }

                ApplicationConfiguration.Initialize();

                if (!IsStartupEntryPresent())
                {
                    StartupHelper.AddApplicationToStartup();
                    StartupHelper.CreateStartupShortcutUsingPowerShell();
                }

                PCLoginEnforcer enforcer = new PCLoginEnforcer();
                string currentUser = Environment.UserName;
                enforcer.EnforceLoginRestrictions(currentUser);

                Application.Run(new LoginForm());
            }
        }

        private static bool IsStartupEntryPresent()
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            var value = key?.GetValue("ClockEnforcer") as string;
            return string.Equals(value, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpData;
        }
    }
}
