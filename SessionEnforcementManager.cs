using System;
using System.IO;
using System.Threading;
using ClockEnforcer.Services;

namespace ClockEnforcer
{
    internal sealed class SessionEnforcementManager : IDisposable
    {
        private readonly TimeSpan preLoginGrace = TimeSpan.FromMinutes(2);
        private readonly TimeSpan lunchWindow = TimeSpan.FromHours(5);
        private readonly TimeSpan defaultShiftLength = TimeSpan.FromHours(8);
        private readonly PCLoginEnforcer enforcer = new PCLoginEnforcer();
        private readonly object gate = new object();
        private readonly string debugLogPath;

        private Timer preLoginTimer;
        private Timer lunchTimer;
        private Timer forcedLogoutTimer;
        private DateTime? shiftStart;
        private TimeSpan totalShiftLength;
        private readonly string username;

        public SessionEnforcementManager(string username)
        {
            this.username = username ?? throw new ArgumentNullException(nameof(username));
            totalShiftLength = defaultShiftLength;
            debugLogPath = Path.Combine(LogService.ApplicationFolder, "session_debug_log.txt");
        }

        public event Action<string> WarningIssued;

        public void StartPreLoginCountdown()
        {
            lock (gate)
            {
                ResetShiftLocked();
                RestartTimer(ref preLoginTimer, PreLoginExpired, preLoginGrace);
            }
        }

        public void CancelPreLoginCountdown()
        {
            lock (gate)
            {
                CancelTimer(ref preLoginTimer);
            }
        }

        public void OnPunchIn()
        {
            lock (gate)
            {
                CancelTimer(ref preLoginTimer);
                StartLunchTimerLocked();
                EnsureForcedLogoutTimerLocked(totalShiftLength);
            }
        }

        public void OnPunchOut(bool isEndOfDay)
        {
            lock (gate)
            {
                CancelTimer(ref lunchTimer);
                if (isEndOfDay)
                {
                    ResetShiftLocked();
                }
            }
        }

        public void StartForcedLogoutTimer(double totalHours)
        {
            lock (gate)
            {
                totalShiftLength = TimeSpan.FromHours(totalHours);
                EnsureForcedLogoutTimerLocked(totalShiftLength);
            }
        }

        private void EnsureForcedLogoutTimerLocked(TimeSpan shiftLength)
        {
            if (shiftStart == null || shiftStart.Value.Date < DateTime.Today)
            {
                shiftStart = DateTime.Now;
            }

            TimeSpan due = shiftStart.Value.Add(shiftLength) - DateTime.Now;
            if (due < TimeSpan.Zero)
            {
                due = TimeSpan.Zero;
            }

            RestartTimer(ref forcedLogoutTimer, ForcedLogoutExpired, due);
        }

        private void StartLunchTimerLocked()
        {
            RestartTimer(ref lunchTimer, LunchExpired, lunchWindow);
        }

        private void PreLoginExpired(object state)
        {
            WarningIssued?.Invoke("You did not clock in within 2 minutes. Locking workstation.");
            WriteDebug("Pre-login grace expired. Locking workstation.");
            enforcer.ForceUserLogOff();
        }

        private void LunchExpired(object state)
        {
            WarningIssued?.Invoke("Lunch window exceeded. Forcing clock out for lunch and locking workstation.");
            WriteDebug("Lunch timer expired. Locking workstation.");
            enforcer.ForceUserLogOff();
        }

        private void ForcedLogoutExpired(object state)
        {
            WarningIssued?.Invoke("Shift limit reached. Locking workstation.");
            WriteDebug("Forced shift timer expired. Locking workstation.");
            enforcer.ForceUserLogOff();
            lock (gate)
            {
                ResetShiftLocked();
            }
        }

        private static void RestartTimer(ref Timer timer, TimerCallback callback, TimeSpan dueTime)
        {
            timer?.Dispose();
            timer = new Timer(callback, null, dueTime, Timeout.InfiniteTimeSpan);
        }

        private static void CancelTimer(ref Timer timer)
        {
            timer?.Dispose();
            timer = null;
        }

        private void ResetShiftLocked()
        {
            CancelTimer(ref forcedLogoutTimer);
            CancelTimer(ref lunchTimer);
            shiftStart = null;
            totalShiftLength = defaultShiftLength;
        }

        private void ResetAllTimers()
        {
            lock (gate)
            {
                CancelTimer(ref preLoginTimer);
                ResetShiftLocked();
            }
        }

        private void WriteDebug(string message)
        {
            try
            {
                File.AppendAllText(debugLogPath, $"{DateTime.Now}: [{username}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                CancelTimer(ref preLoginTimer);
                CancelTimer(ref lunchTimer);
                CancelTimer(ref forcedLogoutTimer);
            }
        }
    }
}
