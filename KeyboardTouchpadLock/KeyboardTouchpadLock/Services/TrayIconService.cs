using System.Windows.Forms;
using KeyboardTouchpadLock.Models;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Owns the NotifyIcon (system tray icon) and its right-click context menu.
/// All the menu actions are exposed as events so MainViewModel/App can wire
/// up the actual behavior - this class only knows about UI, not app logic.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    public NotifyIcon NotifyIcon { get; }

    public event Action? LockKeyboardRequested;
    public event Action? LockTouchpadRequested;
    public event Action? LockBothRequested;
    public event Action? UnlockAllRequested;
    public event Action? ToggleIndicatorRequested;
    public event Action? SettingsRequested;
    public event Action<bool>? LaunchAtStartupToggled;
    public event Action? ExitRequested;
    public event Action? DoubleClicked;

    private readonly ToolStripMenuItem _launchAtStartupItem;

    public TrayIconService(bool launchAtStartupChecked)
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("Lock Keyboard", null, (_, _) => LockKeyboardRequested?.Invoke());
        menu.Items.Add("Lock Touchpad", null, (_, _) => LockTouchpadRequested?.Invoke());
        menu.Items.Add("Lock Keyboard + Touchpad", null, (_, _) => LockBothRequested?.Invoke());
        menu.Items.Add("Unlock All", null, (_, _) => UnlockAllRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show/Hide Floating Indicator", null, (_, _) => ToggleIndicatorRequested?.Invoke());
        menu.Items.Add("Settings...", null, (_, _) => SettingsRequested?.Invoke());

        _launchAtStartupItem = new ToolStripMenuItem("Launch at Startup")
        {
            CheckOnClick = true,
            Checked = launchAtStartupChecked
        };
        _launchAtStartupItem.Click += (_, _) => LaunchAtStartupToggled?.Invoke(_launchAtStartupItem.Checked);
        menu.Items.Add(_launchAtStartupItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        NotifyIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateStateIcon(LockState.None),
            Visible = true,
            Text = LockState.None.ToTooltip(),
            ContextMenuStrip = menu
        };

        NotifyIcon.DoubleClick += (_, _) => DoubleClicked?.Invoke();
    }

    public void UpdateState(LockState state)
    {
        var oldIcon = NotifyIcon.Icon;
        NotifyIcon.Icon = IconFactory.CreateStateIcon(state);
        NotifyIcon.Text = state.ToTooltip(); // NotifyIcon.Text max length is 63 chars - all our strings fit
        oldIcon?.Dispose();
    }

    public void SetLaunchAtStartupChecked(bool value) => _launchAtStartupItem.Checked = value;

    public void Dispose()
    {
        NotifyIcon.Visible = false;
        NotifyIcon.Dispose();
    }
}
