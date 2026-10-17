namespace KeyboardTouchpadLock.Models;

/// <summary>
/// The four visual/functional states the app can be in.
/// Keyboard and touchpad locking are tracked as independent facts;
/// this enum is the *combined* state derived from both, used for
/// the tray icon, floating indicator color, and notifications.
/// </summary>
public enum LockState
{
    /// <summary>Green - everything active.</summary>
    None,

    /// <summary>Red - keyboard blocked, touchpad free.</summary>
    KeyboardLocked,

    /// <summary>Blue - touchpad disabled, keyboard free.</summary>
    TouchpadLocked,

    /// <summary>Orange - both blocked. Always reachable and always
    /// reversible via the master "Lock/Unlock Both" hotkey.</summary>
    Both
}

public static class LockStateExtensions
{
    public static bool IsKeyboardLocked(this LockState s) => s is LockState.KeyboardLocked or LockState.Both;
    public static bool IsTouchpadLocked(this LockState s) => s is LockState.TouchpadLocked or LockState.Both;

    public static string ToTooltip(this LockState s) => s switch
    {
        LockState.KeyboardLocked => "Keyboard Locked",
        LockState.TouchpadLocked => "Touchpad Locked",
        LockState.Both => "Keyboard & Touchpad Locked",
        _ => "All Inputs Active"
    };

    public static string ToNotificationTitle(this LockState s) => s switch
    {
        LockState.KeyboardLocked => "🔒 Keyboard Locked",
        LockState.TouchpadLocked => "🟦 Touchpad Locked",
        LockState.Both => "🟧 Keyboard & Touchpad Locked",
        _ => "✅ All Inputs Active"
    };

    /// <summary>Hex color per the spec.</summary>
    public static string ToColorHex(this LockState s) => s switch
    {
        LockState.KeyboardLocked => "#E53935",
        LockState.TouchpadLocked => "#1E88E5",
        LockState.Both => "#FB8C00",
        _ => "#43A047"
    };

    public static string ToGlyph(this LockState s) => s switch
    {
        LockState.KeyboardLocked => "\uD83D\uDD12",   // 🔒
        LockState.TouchpadLocked => "\uD83D\uDDB1",   // 🖱
        LockState.Both => "\uD83D\uDD12",             // 🔒 (keyboard glyph doubles as combined-lock glyph)
        _ => "\u2713"                                 // ✓
    };
}
