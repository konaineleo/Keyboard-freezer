using System.IO;
using System.Text.Json;
using KeyboardTouchpadLock.Models;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Loads and saves AppSettings as JSON under %AppData%\KeyboardTouchpadLock.
/// Every write is atomic (write to temp file, then replace) so a crash
/// mid-save can never corrupt the settings file.
/// </summary>
public class SettingsService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KeyboardTouchpadLock");

    private static readonly string SettingsPath = Path.Combine(AppFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Current { get; private set; } = AppSettings.CreateDefault();

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    Current = loaded;
                    return Current;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable settings file -> fall back to defaults
            // rather than crashing the app.
        }

        Current = AppSettings.CreateDefault();
        return Current;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            var tempPath = SettingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, SettingsPath, overwrite: true);
            File.Delete(tempPath);
        }
        catch
        {
            // Best-effort persistence: a failed save should never crash
            // or block the running app.
        }
    }

    public void ResetToDefaults()
    {
        Current = AppSettings.CreateDefault();
        Save();
    }
}
