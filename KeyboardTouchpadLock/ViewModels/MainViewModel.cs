using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyboardTouchpadLock.Models;
using KeyboardTouchpadLock.Services;

namespace KeyboardTouchpadLock.ViewModels;

/// <summary>
/// The single source of truth for "what is currently locked". Every entry
/// point (hotkeys, tray menu, floating indicator context menu) calls into
/// this class rather than touching the services directly, so lock state
/// transitions happen in exactly one place.
///
/// STATE MODEL: keyboard-lock and touchpad-lock are tracked as independent
/// facts and combined into the four-value LockState for display:
///   - ToggleKeyboard() flips only the keyboard fact.
///   - ToggleTouchpad() flips only the touchpad fact.
///   - ToggleBoth() is a master switch: if anything is locked, it unlocks
///     everything; if nothing is locked, it locks everything. Its hotkey is
///     always left un-blocked by the keyboard hook (see KeyboardBlockerService),
///     so it is always available as a guaranteed way out.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private readonly KeyboardBlockerService _keyboardBlocker;
    private readonly TouchpadControlService _touchpadControl;
    private readonly TrayIconService _trayIcon;
    private readonly NotificationService _notifications;

    private int _keyboardHotkeyId = -1;
    private int _touchpadHotkeyId = -1;
    private int _bothHotkeyId = -1;

    private bool _keyboardLocked;
    private bool _touchpadLocked;

    public LockState CurrentState =>
        (_keyboardLocked, _touchpadLocked) switch
        {
            (true, true) => LockState.Both,
            (true, false) => LockState.KeyboardLocked,
            (false, true) => LockState.TouchpadLocked,
            _ => LockState.None
        };

    public event Action<LockState>? StateChanged;

    public MainViewModel(
        SettingsService settingsService,
        HotkeyService hotkeyService,
        KeyboardBlockerService keyboardBlocker,
        TouchpadControlService touchpadControl,
        TrayIconService trayIcon,
        NotificationService notifications)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _keyboardBlocker = keyboardBlocker;
        _touchpadControl = touchpadControl;
        _trayIcon = trayIcon;
        _notifications = notifications;

        RegisterHotkeys();
        WireTrayMenu();
    }

    private void RegisterHotkeys()
    {
        var s = _settingsService.Current;

        _hotkeyService.Unregister(_keyboardHotkeyId);
        _hotkeyService.Unregister(_touchpadHotkeyId);
        _hotkeyService.Unregister(_bothHotkeyId);

        _keyboardHotkeyId = _hotkeyService.Register(s.KeyboardHotkey, () => ToggleKeyboard());
        _touchpadHotkeyId = _hotkeyService.Register(s.TouchpadHotkey, () => ToggleTouchpad());
        _bothHotkeyId = _hotkeyService.Register(s.BothHotkey, () => ToggleBoth());
    }

    /// <summary>Call after Settings changes the hotkey bindings.</summary>
    public void ReloadHotkeys() => RegisterHotkeys();

    private void WireTrayMenu()
    {
        _trayIcon.LockKeyboardRequested += () => ToggleKeyboard();
        _trayIcon.LockTouchpadRequested += () => ToggleTouchpad();
        _trayIcon.LockBothRequested += () => ToggleBoth();
        _trayIcon.UnlockAllRequested += UnlockAll;
    }

    public async void ToggleKeyboard()
    {
        _keyboardLocked = !_keyboardLocked;
        await ApplyStateAsync();
    }

    public async void ToggleTouchpad()
    {
        _touchpadLocked = !_touchpadLocked;
        await ApplyStateAsync();
    }

    public async void ToggleBoth()
    {
        bool anythingLocked = _keyboardLocked || _touchpadLocked;
        _keyboardLocked = !anythingLocked;
        _touchpadLocked = !anythingLocked;
        await ApplyStateAsync();
    }

    public async void UnlockAll()
    {
        _keyboardLocked = false;
        _touchpadLocked = false;
        await ApplyStateAsync();
    }

    /// <summary>Synchronous, best-effort emergency unlock used from crash/exit
    /// handlers where we cannot reliably await async work.</summary>
    public void ForceUnlockAllSync()
    {
        _keyboardLocked = false;
        _touchpadLocked = false;
        _keyboardBlocker.ForceUnblock();
        // Fire-and-forget: on process exit there is no guarantee this
        // completes, but Windows itself never permanently disables a PnP
        // device just because the process that disabled it died, so the
        // touchpad is not "stuck off" even if this doesn't finish in time.
        _ = _touchpadControl.SetTouchpadEnabledAsync(true);
    }

    private async Task ApplyStateAsync()
    {
        var s = _settingsService.Current;

        // The escape hotkeys that must remain reachable while the keyboard
        // hook is active: the individual keyboard toggle, and the master
        // "both" toggle (always, regardless of which lock combo got us here).
        var escapeHotkeys = new[] { s.KeyboardHotkey, s.BothHotkey };
        _keyboardBlocker.SetBlocking(_keyboardLocked, escapeHotkeys);

        bool touchpadOk = await _touchpadControl.SetTouchpadEnabledAsync(!_touchpadLocked);
        if (!touchpadOk && _touchpadLocked)
        {
            // Could not disable the touchpad (e.g. UAC declined, or no
            // recognizable touchpad device). Do not report a lock that
            // didn't actually happen - roll the touchpad fact back.
            _touchpadLocked = false;
        }

        var state = CurrentState;
        _trayIcon.UpdateState(state);
        _notifications.NotifyStateChange(state);
        StateChanged?.Invoke(state);
        OnPropertyChanged(nameof(CurrentState));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
