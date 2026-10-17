using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyboardTouchpadLock.Models;
using KeyboardTouchpadLock.Services;

namespace KeyboardTouchpadLock.Views;

/// <summary>
/// The small circular "Grammarly-style" always-on-top status widget.
/// Purely a view: it renders whatever state it's told via UpdateState()
/// and raises events for user interaction; it owns no lock logic itself.
/// </summary>
public partial class FloatingIndicator : Window
{
    private readonly SettingsService _settingsService;
    private System.Windows.Forms.ContextMenuStrip? _contextMenu;

    public event Action? LockKeyboardRequested;
    public event Action? LockTouchpadRequested;
    public event Action? LockBothRequested;
    public event Action? UnlockAllRequested;
    public event Action? SettingsRequested;

    public FloatingIndicator(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        ApplySizeAndOpacity();
        RestorePosition();
        BuildContextMenu();

        LocationChanged += (_, _) => SavePositionDebounced();
    }

    private void ApplySizeAndOpacity()
    {
        var s = _settingsService.Current;
        Width = s.WidgetSize;
        Height = s.WidgetSize;
        BackgroundCircle.Opacity = s.WidgetOpacity;
    }

    private void RestorePosition()
    {
        var s = _settingsService.Current;
        if (s.WidgetLeft.HasValue && s.WidgetTop.HasValue && IsOnScreen(s.WidgetLeft.Value, s.WidgetTop.Value))
        {
            Left = s.WidgetLeft.Value;
            Top = s.WidgetTop.Value;
        }
        else
        {
            // Default to bottom-right corner of the primary screen, Grammarly-style.
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 24;
            Top = area.Bottom - Height - 24;
        }
    }

    private static bool IsOnScreen(double left, double top)
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var b = screen.Bounds;
            if (left >= b.Left - 50 && left <= b.Right + 50 && top >= b.Top - 50 && top <= b.Bottom + 50)
                return true;
        }
        return false;
    }

    private System.Threading.Timer? _saveTimer;
    private void SavePositionDebounced()
    {
        _saveTimer?.Dispose();
        _saveTimer = new System.Threading.Timer(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                _settingsService.Current.WidgetLeft = Left;
                _settingsService.Current.WidgetTop = Top;
                _settingsService.Save();
            });
        }, null, 400, System.Threading.Timeout.Infinite);
    }

    public void UpdateState(LockState state)
    {
var color = (System.Windows.Media.Color)
    System.Windows.Media.ColorConverter.ConvertFromString(state.ToColorHex())!;
        var glyph = state.ToGlyph();

        if (_settingsService.Current.AnimationsEnabled)
        {
            AnimateToState(color, glyph);
        }
        else
        {
            BackgroundCircle.Fill = new SolidColorBrush(color);
            IconText.Text = glyph;
        }

        ToolTip = state.ToTooltip();
    }

private void AnimateToState(System.Windows.Media.Color newColor, string newGlyph)
    {
        var duration = TimeSpan.FromMilliseconds(250);

        // Scale down slightly, swap color+glyph at the midpoint, scale back up.
        var shrink = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        shrink.Completed += (_, _) =>
        {
            if (BackgroundCircle.Fill is not SolidColorBrush currentBrush || currentBrush.IsFrozen)
                BackgroundCircle.Fill = new SolidColorBrush(newColor);
            else
                currentBrush.Color = newColor;

            IconText.Text = newGlyph;

            var grow = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        };

        ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);

        // Also cross-fade the whole widget briefly for extra polish.
        var fade = new DoubleAnimation(1.0, 1.0, duration); // placeholder if further opacity choreography is desired
        BeginAnimation(OpacityProperty, fade);

        _ = duration; // duration reserved for future easing tweaks; suppress unused warning
    }

    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { /* button released mid-call; safe to ignore */ }
        }
    }

    private void RootGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _contextMenu?.Show(System.Windows.Forms.Cursor.Position);
    }

    private void BuildContextMenu()
    {
        _contextMenu = new System.Windows.Forms.ContextMenuStrip();
        _contextMenu.Items.Add("Lock Keyboard", null, (_, _) => LockKeyboardRequested?.Invoke());
        _contextMenu.Items.Add("Lock Touchpad", null, (_, _) => LockTouchpadRequested?.Invoke());
        _contextMenu.Items.Add("Lock Keyboard + Touchpad", null, (_, _) => LockBothRequested?.Invoke());
        _contextMenu.Items.Add("Unlock All", null, (_, _) => UnlockAllRequested?.Invoke());
        _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _contextMenu.Items.Add("Settings...", null, (_, _) => SettingsRequested?.Invoke());
    }

    protected override void OnClosed(EventArgs e)
    {
        _settingsService.Current.WidgetLeft = Left;
        _settingsService.Current.WidgetTop = Top;
        _settingsService.Save();
        base.OnClosed(e);
    }
}
