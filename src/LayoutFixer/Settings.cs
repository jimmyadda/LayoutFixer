using System.IO;
using System.Text.Json;

namespace LayoutFixer;

public sealed class Settings
{
    public string? LangA { get; set; } = "he-IL";
    public string? LangB { get; set; } = "en-US";
    public string Hotkey { get; set; } = "F9";
    public bool RunAtStartup { get; set; } = false;
}

public static class SettingsService
{
    private static string AppDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LayoutFixer");

    private static string SettingsPath => Path.Combine(AppDir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s != null) return s;
            }
        }
        catch { /* ignore */ }
        return new Settings();
    }

    public static void Save(Settings s)
    {
        Directory.CreateDirectory(AppDir);
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
