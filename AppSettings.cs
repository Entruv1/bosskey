using System.Text.Json;

namespace BossKey;

internal enum HideMode
{
    Hide = 0,
    Minimize = 1,
    MoveOffScreen = 2
}

internal sealed class AppSettings
{
    public string? HotkeyDisplay { get; set; }
    public int HotkeyCode { get; set; }
    public bool HotkeyCtrl { get; set; }
    public bool HotkeyShift { get; set; }
    public bool HotkeyAlt { get; set; }
    public bool HotkeyWin { get; set; }

    public HideMode HideMode { get; set; } = HideMode.Hide;
    public bool HideSelf { get; set; } = true;
    public bool HideTray { get; set; }
    public bool PreventRestore { get; set; } = true;

    public string? TargetProcessName { get; set; }
    public string? TargetTitle { get; set; }
    public bool AutoFindTarget { get; set; } = true;

    public bool AutoStart { get; set; }

    private static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "bosskey.settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                    return loaded;
            }
        }
        catch
        {
            // 配置文件损坏时按默认值启动。
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 保存失败不影响主功能，只是下次不再记忆设置。
        }
    }
}
