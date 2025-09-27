using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ClockEnforcer
{
    internal static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        private const int WM_COPYDATA = 0x004A;

        [STAThread]
        static void Main()
        {
            EnvLoader.LoadIfPresent();

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "ClockEnforcerAppMutex", out createdNew))
            {
                if (!createdNew)
                {
                    // Send "SHOW" message to the already running instance
                    IntPtr hWnd = FindWindow(null, "Clock Enforcer"); // This must match the form's title
                    if (hWnd != IntPtr.Zero)
                    {
                        const string showCommand = "SHOW";
                        byte[] payload = Encoding.ASCII.GetBytes(showCommand + '\0');
                        IntPtr buffer = Marshal.AllocHGlobal(payload.Length);

                        try
                        {
                            Marshal.Copy(payload, 0, buffer, payload.Length);

                            COPYDATASTRUCT cds = new COPYDATASTRUCT
                            {
                                dwData = IntPtr.Zero,
                                cbData = payload.Length,
                                lpData = buffer
                            };

                            _ = SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
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

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }
    }
}
