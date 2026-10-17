using System.Runtime.InteropServices;
using System.Windows.Input;
using KeyboardTouchpadLock.Models;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Blocks keyboard input using a low-level keyboard hook (WH_KEYBOARD_LL).
///
/// WHY NOT BlockInput()? Windows' BlockInput() API can only block "keyboard
/// AND mouse" as a single unit - there is no OS-level way to block only the
/// keyboard while leaving a pointing device usable (confirmed against the
/// official AutoHotkey/Win32 documentation). A low-level hook that inspects
/// and selectively swallows individual key events is the standard technique
/// for keyboard-only blocking, and it is what this service does.
///
/// SAFETY DESIGN: while blocking is active, the hook always lets through
/// the physical key-down/key-up events that belong to any of the
/// "escape hotkeys" passed in (the individual Keyboard-lock hotkey and the
/// master Both-lock hotkey). Those specific keystrokes are never swallowed,
/// so Windows' own hotkey subsystem still sees them and still fires the
/// corresponding RegisterHotKey callback - guaranteeing the user can always
/// get out, even if settings are misconfigured. Ctrl+Alt+Delete is a
/// Secure Attention Sequence handled entirely inside the OS and cannot be
/// intercepted or blocked by any hook, so it always remains available too.
/// </summary>
public sealed class KeyboardBlockerService : IDisposable
{
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    // Keep the delegate alive for the lifetime of the hook - if it gets
    // garbage collected the hook callback pointer becomes invalid.
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookHandle = IntPtr.Zero;

    private bool _isBlocking;
    private readonly HashSet<int> _allowedVirtualKeys = new();

    public KeyboardBlockerService()
    {
        _proc = HookCallback;
    }

    /// <summary>
    /// Starts or stops blocking. <paramref name="escapeHotkeys"/> lists every
    /// hotkey combo that must remain able to fire while blocking is on
    /// (typically: the Keyboard-lock hotkey and the master Both-lock hotkey).
    /// Their modifier keys and trigger key are always let through.
    /// </summary>
    public void SetBlocking(bool enabled, IEnumerable<HotkeyDefinition> escapeHotkeys)
    {
        _allowedVirtualKeys.Clear();
        foreach (var hotkey in escapeHotkeys)
        {
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _allowedVirtualKeys.Add(0xA2); // VK_LCONTROL
                _allowedVirtualKeys.Add(0xA3); // VK_RCONTROL
            }
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                _allowedVirtualKeys.Add(0xA4); // VK_LMENU
                _allowedVirtualKeys.Add(0xA5); // VK_RMENU
            }
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                _allowedVirtualKeys.Add(0xA0); // VK_LSHIFT
                _allowedVirtualKeys.Add(0xA1); // VK_RSHIFT
            }
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows))
            {
                _allowedVirtualKeys.Add(0x5B); // VK_LWIN
                _allowedVirtualKeys.Add(0x5C); // VK_RWIN
            }
            _allowedVirtualKeys.Add(KeyInterop.VirtualKeyFromKey(hotkey.Key));
        }

        _isBlocking = enabled;

        if (enabled && _hookHandle == IntPtr.Zero)
        {
            using var curModule = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName!), 0);
        }
        else if (!enabled && _hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    /// <summary>Immediately restores full keyboard input. Called on toggle-off,
    /// and unconditionally on app shutdown/crash as a safety net.</summary>
    public void ForceUnblock()
    {
        _isBlocking = false;
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isBlocking &&
            (wParam == WM_KEYDOWN || wParam == WM_KEYUP || wParam == WM_SYSKEYDOWN || wParam == WM_SYSKEYUP))
        {
            int vkCode = Marshal.ReadInt32(lParam); // first field of KBDLLHOOKSTRUCT is vkCode

            if (_allowedVirtualKeys.Contains(vkCode))
            {
                // Part of an escape hotkey - let it flow through normally so
                // Windows' hotkey subsystem (RegisterHotKey) still sees it.
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            // Swallow every other key: return a non-zero value and do NOT
            // call CallNextHookEx, which prevents the keystroke from
            // reaching any application, including this one.
            return (IntPtr)1;
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => ForceUnblock();
}
