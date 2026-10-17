using System.Windows.Input;

namespace KeyboardTouchpadLock.Models;

/// <summary>
/// A serializable representation of a global hotkey (modifiers + one key),
/// e.g. Ctrl+Alt+1. Stored in settings.json and re-registered at startup.
/// </summary>
public class HotkeyDefinition
{
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;
    public Key Key { get; set; } = Key.D1;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(FriendlyKeyName(Key));
        return string.Join(" + ", parts);
    }

    private static string FriendlyKeyName(Key key)
    {
        var s = key.ToString();
        // Key.D1 -> "1", Key.D2 -> "2", etc.
        if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s[1].ToString();
        return s;
    }

    public static HotkeyDefinition Default1 => new() { Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.D1 }; // Keyboard
    public static HotkeyDefinition Default2 => new() { Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.D2 }; // Touchpad
    public static HotkeyDefinition Default3 => new() { Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.D3 }; // Both (master)
}
