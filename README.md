ClockEnforcer Project

This project consists of several C# classes (*.cs files) that work together to enforce time‑clock rules on a Windows environment. Below is an overview of each file and its primary responsibility.

-Program.cs
Entry point of the application.
Sets up a singleton mutex to prevent multiple instances. (Users were running multiple sessions of this app, this fixes it)
Installs the app to the user's Startup registry key (once).
Creates a PCLoginEnforcer to check lockout rules immediately on user login.
Launches the WinForms LoginForm.


-LoginForm.cs / LoginForm.Designer.cs
WinForms UI for user interaction:
Username/password fields
Login button
Request Overtime button
Status message box

Handles:
Authentication via AuthService
Auto clock‑in after login, delaying 1s
2‑minute auto‑logout guard if no punch-in
5‑hour auto‑logout guard if no punch-out
Punch action (clock‑in/out) via PunchService
Launching the OvertimeRequestForm

-AuthService.cs
Wraps the HTTP login call to the time‑clock API.
Stores and exposes the authentication token for subsequent requests.

-PunchService.cs
Wraps the HTTP punch (clock‑in/out) API call.
Automatically includes the stored bearer token.
Parses the JSON response to determine PUNCH_IN vs PUNCH_OUT.

-LogService.cs
Maintains a local text log (user_logins.txt) of LOGIN and CLOCKOUT events.
Provides IsUserLockedOut to enforce lockout periods based on last clock‑out time:
50 min lockout if clock out between 10 AM–4 PM
4 hr lockout otherwise

-PCLoginEnforcer.cs
Called at process startup (via Program.cs) and on explicit calls.
Uses LogService.IsUserLockedOut to decide whether to immediately log off the Windows session.

-SessionEnforcementManager.cs
Centralizes all session timers:
2 min auto‑clock‑in timer
5 hr lunch enforcement timer
8 hr full‑shift enforcement timer (flexible via StartForcedLogoutTimer)
Exposes StartSessionTimers() after a successful punch‑in to kick off lunch + full‑shift logic.
Provides OnPunchOut() to cancel the lunch timer once the user clocks out.
Handles auto‑clock‑in attempts if credentials are saved.

-OvertimeRequestForm.cs / OvertimeRequestForm.Designer.cs
A modal dialog where users:
Enter an overtime note
Select hours (1–12) and minutes (5,10,15,30,45) from dropdowns
Click Send Request
Uses OvertimeRequestService to POST a JSON payload
Polls the webhook every 60 seconds for an approved flag
On approval, calls sessionManager.StartForcedLogoutTimer(8 + approvedHours) to extend the shift

-OvertimeRequestService.cs
Posts the overtime request JSON to a webhook endpoint (/overtime).
Returns a requestId and provides a CheckApprovalStatusAsync(requestId) to poll for approval.

-StartupHelper.cs
Adds or checks the application's Run registry key under HKCU\Software\Microsoft\Windows\CurrentVersion\Run.

Usage:

1 - Build and install via the Inno Setup installer. (Or however you want) 

2 - Log in to the WinForm — the app auto‑launches on Windows login.

3 - After authentication, the app auto‑punches in and starts session timers. (only if a credential is saved)

4 - To request overtime, click Request Overtime, fill out the form, and wait for approval.
