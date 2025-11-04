using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ClockEnforcer.Services;

namespace ClockEnforcer
{
    class PCLoginEnforcer
    {
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
            int todayCount = logService.GetTodayLoginCount(username);
            if (todayCount % 2 != 0)
            {
                _ = ForcePunchOutAsync(username);
            }
        }

        public void ForceUserLogOff(bool lockImmediately = false)
        {
            _ = ForceLogOffAsync(lockImmediately);
        }

        private async Task ForceLogOffAsync(bool lockImmediately)
        {
            string debugLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ClockEnforcer",
                "enforcer_debug_log.txt");

            if (lockImmediately)
            {
                TryLockWorkstation(debugLogPath);
            }

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

            if (!lockImmediately)
            {
                await Task.Delay(10000);
                TryLockWorkstation(debugLogPath);
            }
        }

        private void TryLockWorkstation(string debugLogPath)
        {
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
