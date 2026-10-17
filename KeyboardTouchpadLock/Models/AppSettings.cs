namespace KeyboardTouchpadLock.Models;

/// <summary>
/// All user-configurable preferences. Persisted as JSON at
/// %AppData%\KeyboardTouchpadLock\settings.json.
///
/// NOTE: The current *lock state* is deliberately NOT persisted here.
/// Every launch starts from LockState.None (all inputs active). This is
/// a safety choice: if the app crashed while the keyboard was locked,
/// the next launch must never come back up already locked.
/// </summary>
public class AppSettings
{
    public HotkeyDefinition KeyboardHotkey { get; set; } = HotkeyDefinition.Default1;
    public HotkeyDefinition TouchpadHotkey { get; set; } = HotkeyDefinition.Default2;
    public HotkeyDefinition BothHotkey { get; set; } = HotkeyDefinition.Default3;

    public bool LaunchAtStartup { get; set; } = false;

    public bool ShowFloatingIndicator { get; set; } = true;
    public double WidgetSize { get; set; } = 36;      // 32-40 px per spec
    public double WidgetOpacity { get; set; } = 0.85;  // 80-90% per spec
    public bool AnimationsEnabled { get; set; } = true;

    // Last known screen position of the floating indicator (top-left corner).
    // Null until the user has moved it or the app has placed it once.
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }

    /// <summary>
    /// If true, the app relaunches itself elevated at startup so that
    /// touchpad enable/disable never triggers a UAC prompt mid-session.
    /// Off by default (principle of least privilege).
    /// </summary>
    public bool AlwaysRunAsAdmin { get; set; } = false;

    public static AppSettings CreateDefault() => new();
}
