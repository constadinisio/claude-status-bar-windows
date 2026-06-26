using Microsoft.Win32;

namespace ClaudeStatusBar.App;

// Persists the "Play Completion Sound" toggle under HKCU (same approach as AutoStart).
// Off by default — the value is absent until the user enables it.
public static class SoundSetting
{
    private const string Key = @"Software\ClaudeStatusBar";
    private const string Name = "CompletionSound";

    public static bool IsEnabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key);
            return k?.GetValue(Name) is int v && v != 0;
        }
    }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        k.SetValue(Name, enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}
