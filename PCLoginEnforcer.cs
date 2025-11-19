using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using ClockEnforcer.Services;

namespace ClockEnforcer
{
    class PCLoginEnforcer
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        private readonly AuthService authService;
        private readonly PunchService punchService;
        private readonly LogService logService = new LogService();

        private const string ApiKey = "iibh7b86dlces64stxqwm15n65kvkhf3";

        public PCLoginEnforcer()
        {
            authService = new AuthService(ApiKey);
            punchService = new PunchService(authService);
        }

        public void EnforceLoginRestrictions(string username)
        {
            if (logService.HasCompletedShiftForToday(username))
            {
                ForceUserLogOff();
                return;
            }

            int todayCount = logService.GetTodayLoginCount(username);
            if (todayCount % 2 != 0)
            {
                _ = ForcePunchOutAsync(username);
            }
        }

        public void ForceUserLogOff()
        {
            _ = ForceLogOffAsync();
        }

        private async Task ForceLogOffAsync()
        {
            string debugLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ClockEnforcer",
                "enforcer_debug_log.txt");

            try
            {
                string systemUsername = Environment.UserName;
                (string lastUser, string lastPass) = logService.GetLastSavedCredentials(systemUsername);

                if (string.IsNullOrEmpty(lastUser) || string.IsNullOrEmpty(lastPass))
                {
                    File.AppendAllText(debugLogPath,
                        $"{DateTime.Now}: No credentials found for {systemUsername}{Environment.NewLine}");
                }
                else
                {
                    bool authenticated = await authService.AuthenticateUserAsync(lastUser, lastPass);
                    if (!authenticated)
                    {
                        File.AppendAllText(debugLogPath,
                            $"{DateTime.Now}: Auth failed for {lastUser}{Environment.NewLine}");
                    }
                    else
                    {
                        string resp = await punchService.PunchAsync();
                        File.AppendAllText(debugLogPath,
                            $"{DateTime.Now}: Forced punch response: {resp}{Environment.NewLine}");

                        logService.LogClockOut(systemUsername);
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(debugLogPath,
                    $"{DateTime.Now}: ERROR in ForceLogOffAsync: {ex.Message}{Environment.NewLine}");
            }

            await Task.Delay(10000);
            try
            {
                if (!LockWorkStation())
                {
                    File.AppendAllText(debugLogPath,
                        $"{DateTime.Now}: ERROR locking workstation: Win32 error {Marshal.GetLastWin32Error()}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(debugLogPath,
                    $"{DateTime.Now}: ERROR locking workstation: {ex.Message}{Environment.NewLine}");
            }
        }

        private async Task ForcePunchOutAsync(string user)
        {
            string debugLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ClockEnforcer",
                "enforcer_debug_log.txt");

            try
            {
                (string lastUser, string lastPass) = logService.GetLastSavedCredentials(user);
                if (string.IsNullOrEmpty(lastUser) || string.IsNullOrEmpty(lastPass))
                {
                    File.AppendAllText(debugLogPath,
                        $"{DateTime.Now}: No credentials found for {user}{Environment.NewLine}");
                    return;
                }

                bool authenticated = await authService.AuthenticateUserAsync(lastUser, lastPass);
                if (!authenticated)
                {
                    File.AppendAllText(debugLogPath,
                        $"{DateTime.Now}: Auth failed for {lastUser}{Environment.NewLine}");
                    return;
                }

                string resp = await punchService.PunchAsync();
                File.AppendAllText(debugLogPath,
                    $"{DateTime.Now}: Forced punch response: {resp}{Environment.NewLine}");

                logService.LogClockOut(user);
            }
            catch (Exception ex)
            {
                File.AppendAllText(debugLogPath,
                    $"{DateTime.Now}: ERROR in ForcePunchOutAsync: {ex.Message}{Environment.NewLine}");
            }
        }
    }
}
