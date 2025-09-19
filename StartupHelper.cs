using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

public static class StartupHelper
{
    /// <summary>
    /// Adds the application to startup by creating a registry entry.
    /// </summary>
    public static void AddApplicationToStartup()
    {
        // Get the path to the current executable.
        string exePath = Application.ExecutablePath;
        // Open the registry key for the current user's startup programs.
        RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        // Set the value (if the key already exists, this will update it).
        registryKey.SetValue("ClockEnforcer", exePath);
    }

    /// <summary>
    /// Creates a shortcut in the Startup folder using PowerShell.
    /// </summary>
    public static void CreateStartupShortcutUsingPowerShell()
    {
        // Get the Startup folder path and the current executable path.
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string exePath = Application.ExecutablePath;
        string shortcutPath = Path.Combine(startupFolder, "ClockEnforcer.lnk");
        string workingDirectory = Path.GetDirectoryName(exePath);

        // Build the PowerShell command to create the shortcut.
        string psCommand = $@"
            $WshShell = New-Object -ComObject WScript.Shell;
            $Shortcut = $WshShell.CreateShortcut('{shortcutPath}');
            $Shortcut.TargetPath = '{exePath}';
            $Shortcut.WorkingDirectory = '{workingDirectory}';
            $Shortcut.Description = 'Clock Enforcer App';
            $Shortcut.Save();";

        // Clean up the command string to remove extra line breaks.
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