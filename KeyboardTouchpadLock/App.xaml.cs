using System.Windows;
using System.Windows.Threading;
using KeyboardTouchpadLock.Services;
using KeyboardTouchpadLock.ViewModels;
using KeyboardTouchpadLock.Views;

namespace KeyboardTouchpadLock;

public partial class App : Application
{
    private SettingsService _settingsService = null!;
    private HotkeyService _hotkeyService = null!;
    private KeyboardBlockerService _keyboardBlocker = null!;
    private TouchpadControlService _touchpadControl = null!;
    private TrayIconService _trayIcon = null!;
    private NotificationService _notifications = null!;
    private MainViewModel _mainViewModel = null!;
    private FloatingIndicator? _floatingIndicator;

    private bool _shuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ---- Crash safety net: no matter how the app dies, inputs must
        // come back. These handlers run on any unhandled exception path. ----
        AppDomain.CurrentDomain.UnhandledException += (_, args) => EmergencyRestoreAndExit();
        DispatcherUnhandledException += (_, args) =>
        {
            EmergencyRestoreAndExit();
            args.Handled = true;
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => EmergencyRestoreAndExit();

        _settingsService = new SettingsService();
        _settingsService.Load();

        // Optional: relaunch elevated once, to avoid a UAC prompt every time
        // the touchpad is toggled during this session.
        if (_settingsService.Current.AlwaysRunAsAdmin && !IsRunningElevated() && RelaunchElevated())
        {
            Shutdown();
            return;
        }

        _hotkeyService = new HotkeyService();
        _keyboardBlocker = new KeyboardBlockerService();
        _touchpadControl = new TouchpadControlService();

        _trayIcon = new TrayIconService(StartupService.IsEnabled());
        _notifications = new NotificationService(_trayIcon.NotifyIcon);

        _mainViewModel = new MainViewModel(
            _settingsService, _hotkeyService, _keyboardBlocker, _touchpadControl, _trayIcon, _notifications);

        _mainViewModel.StateChanged += state => _floatingIndicator?.UpdateState(state);

        _trayIcon.ToggleIndicatorRequested += ToggleIndicatorVisibility;
        _trayIcon.SettingsRequested += OpenSettings;
        _trayIcon.LaunchAtStartupToggled += enabled =>
        {
            StartupService.SetEnabled(enabled);
            _settingsService.Current.LaunchAtStartup = enabled;
            _settingsService.Save();
        };
        _trayIcon.DoubleClicked += ToggleIndicatorVisibility;
        _trayIcon.ExitRequested += () => Shutdown();

        if (_settingsService.Current.ShowFloatingIndicator)
        {
            CreateFloatingIndicator();
        }
    }

    private void CreateFloatingIndicator()
    {
        if (_floatingIndicator != null) return;

        _floatingIndicator = new FloatingIndicator(_settingsService);
        _floatingIndicator.LockKeyboardRequested += () => _mainViewModel.ToggleKeyboard();
        _floatingIndicator.LockTouchpadRequested += () => _mainViewModel.ToggleTouchpad();
        _floatingIndicator.LockBothRequested += () => _mainViewModel.ToggleBoth();
        _floatingIndicator.UnlockAllRequested += () => _mainViewModel.UnlockAll();
        _floatingIndicator.SettingsRequested += OpenSettings;
        _floatingIndicator.UpdateState(_mainViewModel.CurrentState);
        _floatingIndicator.Show();
    }

    private void ToggleIndicatorVisibility()
    {
        if (_floatingIndicator is null)
        {
            CreateFloatingIndicator();
            _settingsService.Current.ShowFloatingIndicator = true;
        }
        else
        {
            _floatingIndicator.Close();
            _floatingIndicator = null;
            _settingsService.Current.ShowFloatingIndicator = false;
        }
        _settingsService.Save();
    }

    private SettingsWindow? _settingsWindow;
    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsService);
        _settingsWindow.SettingsSaved += () =>
        {
            _mainViewModel.ReloadHotkeys();
            _trayIcon.SetLaunchAtStartupChecked(_settingsService.Current.LaunchAtStartup);

            // Re-apply live indicator preferences without requiring a restart.
            if (_settingsService.Current.ShowFloatingIndicator && _floatingIndicator is null)
                CreateFloatingIndicator();
            else if (!_settingsService.Current.ShowFloatingIndicator && _floatingIndicator != null)
            {
                _floatingIndicator.Close();
                _floatingIndicator = null;
            }
            else if (_floatingIndicator != null)
            {
                _floatingIndicator.Width = _settingsService.Current.WidgetSize;
                _floatingIndicator.Height = _settingsService.Current.WidgetSize;
            }
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private static bool IsRunningElevated() =>
        new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    private static bool RelaunchElevated()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is null) return false;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User declined the UAC prompt - continue running non-elevated.
            return false;
        }
    }

    /// <summary>Guarantees keyboard and touchpad are both restored before the
    /// process actually goes away, regardless of exit path.</summary>
    private void EmergencyRestoreAndExit()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;

        try { _mainViewModel?.ForceUnlockAllSync(); } catch { /* best-effort */ }
        try { _keyboardBlocker?.ForceUnblock(); } catch { /* best-effort */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        EmergencyRestoreAndExit();

        _floatingIndicator?.Close();
        _trayIcon?.Dispose();
        _hotkeyService?.Dispose();
        _keyboardBlocker?.Dispose();

        base.OnExit(e);
    }
}
