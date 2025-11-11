using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClockEnforcer.Services;
using Microsoft.Win32;

namespace ClockEnforcer
{
    /// <summary>
    /// Performs background enforcement: forces punch-outs, replays stored credentials
    /// and finally locks the workstation when rules are violated.
    /// </summary>
    internal sealed class PCLoginEnforcer
    {
        private readonly LogService _logService;
        private readonly AuthService _authService;
        private readonly PunchService _punchService;
        private readonly string _debugLogPath;

        public PCLoginEnforcer()
            : this(new LogService())
        {
        }

        internal PCLoginEnforcer(LogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _authService = new AuthService(SaashrConfig.ApiKey);
            _punchService = new PunchService(_authService, _logService);
            _debugLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ClockEnforcer",
                "enforcer_debug_log.txt");
        }

        /// <summary>
        /// Ensures a user who should be punched out is forced out immediately.
        /// </summary>
        public void EnforceLoginRestrictions(string username)
        {
            int todayCount = _logService.GetTodayLoginCount(username);
            if (todayCount % 2 != 0)
            {
                _ = ForcePunchOutAsync(username);
            }
        }

        /// <summary>
        /// Public entry point used by the UI to lock the workstation after a grace period.
        /// </summary>
        public void ForceUserLogOff() => _ = ForceLogOffAsync();

        /// <summary>
        /// Forces the local machine to punch out and then locks Windows after a short delay.
        /// </summary>
        private async Task ForceLogOffAsync()
        {
            try
            {
                string systemUsername = Environment.UserName;
                (string lastUser, string lastPass) = _logService.GetLastSavedCredentials(systemUsername);

                if (string.IsNullOrEmpty(lastUser) || string.IsNullOrEmpty(lastPass))
                {
                    AppendDebug($"No credentials found for {systemUsername}");
                }
                else if (await _authService.AuthenticateUserAsync(lastUser, lastPass))
                {
                    string resp = await _punchService.PunchAsync();
                    AppendDebug($"Forced punch response: {resp}");
                    _logService.LogClockOut(systemUsername);
                }
                else
                {
                    AppendDebug($"Auth failed for {lastUser}");
                }
            }
            catch (Exception ex)
            {
                AppendDebug($"ERROR in ForceLogOffAsync: {ex.Message}");
            }

            await Task.Delay(10000);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "user32.dll,LockWorkStation",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                AppendDebug($"ERROR locking workstation: {ex.Message}");
            }
        }

        /// <summary>
        /// Replays saved credentials to punch out a user without prompting them.
        /// </summary>
        private async Task ForcePunchOutAsync(string user)
        {
            try
            {
                (string lastUser, string lastPass) = _logService.GetLastSavedCredentials(user);
                if (string.IsNullOrEmpty(lastUser) || string.IsNullOrEmpty(lastPass))
                {
                    AppendDebug($"No credentials found for {user}");
                    return;
                }

                if (!await _authService.AuthenticateUserAsync(lastUser, lastPass))
                {
                    AppendDebug($"Auth failed for {lastUser}");
                    return;
                }

                string resp = await _punchService.PunchAsync();
                AppendDebug($"Forced punch response: {resp}");
                _logService.LogClockOut(user);
            }
            catch (Exception ex)
            {
                AppendDebug($"ERROR in ForcePunchOutAsync: {ex.Message}");
            }
        }

        // Helper to keep enforcement diagnostics in a single log file.
        private void AppendDebug(string message)
        {
            File.AppendAllText(_debugLogPath, $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
    }

    /// <summary>
    /// Handles the application start-up registration for both registry and StartUp folder.
    /// </summary>
    public static class StartupHelper
    {
        public static void AddApplicationToStartup()
        {
            string exePath = Application.ExecutablePath;
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            registryKey.SetValue("ClockEnforcer", exePath);
        }

        public static void CreateStartupShortcutUsingPowerShell()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string exePath = Application.ExecutablePath;
            string shortcutPath = Path.Combine(startupFolder, "ClockEnforcer.lnk");
            string workingDirectory = Path.GetDirectoryName(exePath);

            string psCommand = $@"
            $WshShell = New-Object -ComObject WScript.Shell;
            $Shortcut = $WshShell.CreateShortcut('{shortcutPath}');
            $Shortcut.TargetPath = '{exePath}';
            $Shortcut.WorkingDirectory = '{workingDirectory}';
            $Shortcut.Description = 'Clock Enforcer App';
            $Shortcut.Save();";

            string escapedCommand = psCommand.Replace(Environment.NewLine, " ").Replace("  ", " ");

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{escapedCommand}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
        }
    }
}
