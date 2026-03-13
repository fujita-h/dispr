using Microsoft.Win32;

namespace Dispr;

internal class Settings
{
    private const string RegistryKeyPath = @"SOFTWARE\dispr";

    public bool Enabled { get; set; } = true;
    public bool PreventSleep { get; set; }
    public bool OnlyOnAcPower { get; set; }
    public bool AutoStart { get; set; }

    public static Settings Load()
    {
        var settings = new Settings();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key != null)
            {
                settings.Enabled = GetBool(key, nameof(Enabled), true);
                settings.PreventSleep = GetBool(key, nameof(PreventSleep), false);
                settings.OnlyOnAcPower = GetBool(key, nameof(OnlyOnAcPower), false);
                settings.AutoStart = GetBool(key, nameof(AutoStart), false);
            }
        }
        catch
        {
            // If registry access fails, return defaults
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue(nameof(Enabled), Enabled ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(nameof(PreventSleep), PreventSleep ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(nameof(OnlyOnAcPower), OnlyOnAcPower ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(nameof(AutoStart), AutoStart ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
            // Registry access may fail in restricted environments
        }
    }

    private static bool GetBool(RegistryKey key, string name, bool defaultValue)
    {
        return key.GetValue(name) is int value ? value != 0 : defaultValue;
    }
}
