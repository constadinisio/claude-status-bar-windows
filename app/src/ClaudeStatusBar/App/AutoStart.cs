using Microsoft.Win32;

namespace ClaudeStatusBar.App;

public static class AutoStart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "ClaudeStatusBar";

    public static bool IsEnabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key);
            return k?.GetValue(Name) != null;
        }
    }

    public static void Enable()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key, writable: true);
        k?.SetValue(Name, $"\"{Environment.ProcessPath}\"");
    }

    public static void Disable()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key, writable: true);
        if (k?.GetValue(Name) != null) k.DeleteValue(Name);
    }
}
