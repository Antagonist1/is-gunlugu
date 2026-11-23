using System.Diagnostics;
using Microsoft.Win32;

namespace IsGunlugu;

/// <summary>The per-user "run at sign-in" entry. Starts in the tray, without opening the window.</summary>
public sealed class StartupRegistration(string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    private const string ValueName = "IsGunlugu";

    public static string ExePath
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return process.MainModule!.FileName!;
        }
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(ValueName) is not null;
        }
    }

    public void Set(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        if (enabled) key!.SetValue(ValueName, $"\"{exePath}\" --tray");
        else key!.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Keeps the entry working after the exe was moved.</summary>
    public void Refresh(string exePath)
    {
        if (IsEnabled) Set(true, exePath);
    }
}
