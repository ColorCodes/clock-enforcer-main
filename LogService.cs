using System;
using System.IO;
using System.Linq;
using ClockEnforcer;

namespace ClockEnforcer.Services
{
    public class LogService
    {
        private static readonly string LoginLogFilePath = AppPaths.GetPath("user_logins.txt");

        private static readonly string CredentialsFilePath = AppPaths.GetPath("user_credentials.txt");

        static LogService()
        {
            if (!File.Exists(LoginLogFilePath))
                File.Create(LoginLogFilePath).Close();

            if (!File.Exists(CredentialsFilePath))
                File.Create(CredentialsFilePath).Close();
        }

        public void LogLogin(string clockinUsername, string password = null)
        {
            string systemUsername = Environment.UserName;
            string timestamp = DateTime.Now.ToString(); // formato por defecto

            var today = DateTime.Today;
            bool alreadyLoggedInToday = File.ReadAllLines(LoginLogFilePath)
                .Any(line => line.StartsWith(systemUsername) && line.Contains("LOGIN") &&
                             DateTime.TryParse(line.Split(',')[2], out var dt) && dt.Date == today);

            // Siempre registra el LOGIN en el archivo de actividad
            File.AppendAllText(LoginLogFilePath, $"{systemUsername},LOGIN,{timestamp}{Environment.NewLine}");

            // Guarda credenciales solo si es el primer login del día con password
            if (!alreadyLoggedInToday && password != null)
            {
                File.AppendAllText(CredentialsFilePath, $"{systemUsername},{timestamp},{clockinUsername},{password}{Environment.NewLine}");
                File.AppendAllText(AppPaths.GetPath("overtime_debug_log.txt"), $"{timestamp}: Credenciales guardadas de {systemUsername} como {clockinUsername}{Environment.NewLine}");
            }
        }

        public void LogClockOut(string username = null)
        {
            string systemUsername = username ?? Environment.UserName;
            string timestamp = DateTime.Now.ToString();

            File.AppendAllText(LoginLogFilePath, $"{systemUsername},CLOCKOUT,{timestamp}{Environment.NewLine}");
        }

        public int GetTodayLoginCount(string username)
        {
            if (!File.Exists(LoginLogFilePath)) return 0;

            return File.ReadAllLines(LoginLogFilePath)
                .Where(line => line.StartsWith(username) && line.Contains("LOGIN"))
                .Select(line => line.Split(','))
                .Count(parts => parts.Length >= 3 && DateTime.TryParse(parts[2], out var dt) && dt.Date == DateTime.Today);
        }

        public bool IsUserLockedOut(string username)
        {
            if (!File.Exists(LoginLogFilePath))
                return false;

            var lastClockOut = File.ReadAllLines(LoginLogFilePath)
                .Where(line => line.StartsWith(username + ",CLOCKOUT"))
                .Select(line => DateTime.Parse(line.Split(',')[2]))
                .LastOrDefault();

            if (lastClockOut == default)
                return false;

            TimeSpan window = lastClockOut.TimeOfDay is var t && t >= TimeSpan.FromHours(10) && t <= TimeSpan.FromHours(16)
                ? TimeSpan.FromMinutes(50)
                : TimeSpan.FromHours(4);

            return DateTime.Now < lastClockOut.Add(window);
        }

        public (string user, string pass) GetLastSavedCredentials(string systemUsername)
        {
            if (!File.Exists(CredentialsFilePath))
                return (null, null);

            var lines = File.ReadAllLines(CredentialsFilePath);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var parts = lines[i].Split(',');
                if (parts.Length == 4 && parts[0] == systemUsername)
                {
                    return (parts[2], parts[3]); // clockinUsername, password
                }
            }

            return (null, null);
        }

        public bool HasExceededMaxLogins(string username)
        {
            return GetTodayLoginCount(username) >= 4;
        }

        public bool ShouldNextPunchBeOut(string username)
        {
            int loginCount = GetTodayLoginCount(username);
            return loginCount % 2 == 1;
        }
    }
}
