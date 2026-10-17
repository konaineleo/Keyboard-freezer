using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using KeyboardTouchpadLock.Models;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Wraps the Win32 RegisterHotKey API so any key combo can be bound to a
/// callback, system-wide, regardless of which window has focus.
///
/// Implementation note: WPF's ModifierKeys enum (None=0, Alt=1, Control=2,
/// Shift=4, Windows=8) uses the exact same bit values as the Win32
/// MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN constants, so it can be cast
/// directly without a lookup table.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly HwndSource _messageWindow;
    private readonly Dictionary<int, Action> _callbacks = new();
    private int _nextId = 0xB000; // arbitrary namespace to avoid clashing with other apps' hotkey ids

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("KeyboardTouchpadLock_HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,        // no visible window
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE: message-only window, never shown, never in taskbar
        };
        _messageWindow = new HwndSource(parameters);
        _messageWindow.AddHook(WndProc);
    }

    /// <summary>Registers a hotkey and returns an id you can later pass to Unregister.
    /// Returns -1 if registration failed (e.g. combo already claimed by another app).</summary>
    public int Register(HotkeyDefinition hotkey, Action onPressed)
    {
        int id = _nextId++;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key);
        uint mod = (uint)hotkey.Modifiers | MOD_NOREPEAT;

        if (RegisterHotKey(_messageWindow.Handle, id, mod, vk))
        {
            _callbacks[id] = onPressed;
            return id;
        }
        return -1;
    }

    public void Unregister(int id)
    {
        if (id < 0) return;
        UnregisterHotKey(_messageWindow.Handle, id);
        _callbacks.Remove(id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys.ToList())
            UnregisterHotKey(_messageWindow.Handle, id);
        _callbacks.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var action))
            {
                handled = true;
                action.Invoke();
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
    }
}
