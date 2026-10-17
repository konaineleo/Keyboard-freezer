using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyboardTouchpadLock.Models;
using KeyboardTouchpadLock.Services;

namespace KeyboardTouchpadLock.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;

    // Working copies, only committed to _settingsService.Current on Save.
    private HotkeyDefinition _keyboardHotkey = null!;
    private HotkeyDefinition _touchpadHotkey = null!;
    private HotkeyDefinition _bothHotkey = null!;

    private Button? _listeningButton;

    /// <summary>Raised after Save is clicked so the app can re-register
    /// hotkeys and push size/opacity/animation changes to the live indicator.</summary>
    public event Action? SettingsSaved;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Current;

        _keyboardHotkey = Clone(s.KeyboardHotkey);
        _touchpadHotkey = Clone(s.TouchpadHotkey);
        _bothHotkey = Clone(s.BothHotkey);

        KeyboardHotkeyButton.Content = _keyboardHotkey.ToString();
        TouchpadHotkeyButton.Content = _touchpadHotkey.ToString();
        BothHotkeyButton.Content = _bothHotkey.ToString();

        LaunchAtStartupCheck.IsChecked = s.LaunchAtStartup;
        AlwaysAdminCheck.IsChecked = s.AlwaysRunAsAdmin;
        ShowIndicatorCheck.IsChecked = s.ShowFloatingIndicator;
        SizeSlider.Value = s.WidgetSize;
        OpacitySlider.Value = s.WidgetOpacity;
        AnimationsCheck.IsChecked = s.AnimationsEnabled;
    }

    private static HotkeyDefinition Clone(HotkeyDefinition h) => new() { Modifiers = h.Modifiers, Key = h.Key };

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;

        if (_listeningButton != null)
        {
            _listeningButton.Content = CurrentHotkeyFor(_listeningButton.Tag as string).ToString();
        }

        _listeningButton = button;
        button.Content = "Press new shortcut...";
        button.Focus();
        Keyboard.Focus(button);
        button.PreviewKeyDown += CaptureHotkey_PreviewKeyDown;
    }

    private HotkeyDefinition CurrentHotkeyFor(string? tag) => tag switch
    {
        "Keyboard" => _keyboardHotkey,
        "Touchpad" => _touchpadHotkey,
        "Both" => _bothHotkey,
        _ => HotkeyDefinition.Default1
    };

    private void CaptureHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var button = (Button)sender;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore bare modifier presses - wait for a real trigger key.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                 or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            // Require at least one modifier to avoid capturing plain letter keys,
            // which would make normal typing impossible.
            e.Handled = true;
            return;
        }

        var newHotkey = new HotkeyDefinition { Modifiers = modifiers, Key = key };

        switch (button.Tag as string)
        {
            case "Keyboard": _keyboardHotkey = newHotkey; break;
            case "Touchpad": _touchpadHotkey = newHotkey; break;
            case "Both": _bothHotkey = newHotkey; break;
        }

        button.Content = newHotkey.ToString();
        button.PreviewKeyDown -= CaptureHotkey_PreviewKeyDown;
        _listeningButton = null;
        e.Handled = true;
    }

    private void AnySetting_Changed(object sender, RoutedEventArgs e) { /* live preview hook, currently no-op */ }
    private void AnySetting_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { /* slider variant */ }

    private void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Current.WidgetLeft = null;
        _settingsService.Current.WidgetTop = null;
        MessageBox.Show(this, "Widget position will reset to the default corner the next time it's shown.",
            "Position Reset", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this, "Reset all settings to their defaults?", "Confirm Reset",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _settingsService.ResetToDefaults();
        LoadFromSettings();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Current;

        s.KeyboardHotkey = _keyboardHotkey;
        s.TouchpadHotkey = _touchpadHotkey;
        s.BothHotkey = _bothHotkey;

        s.LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true;
        s.AlwaysRunAsAdmin = AlwaysAdminCheck.IsChecked == true;
        s.ShowFloatingIndicator = ShowIndicatorCheck.IsChecked == true;
        s.WidgetSize = SizeSlider.Value;
        s.WidgetOpacity = OpacitySlider.Value;
        s.AnimationsEnabled = AnimationsCheck.IsChecked == true;

        _settingsService.Save();
        StartupService.SetEnabled(s.LaunchAtStartup);

        SettingsSaved?.Invoke();
        Close();
    }
}
