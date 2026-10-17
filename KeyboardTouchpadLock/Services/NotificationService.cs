using System.Windows.Forms;
using Microsoft.Toolkit.Uwp.Notifications;
using KeyboardTouchpadLock.Models;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Shows a native Windows 10/11 toast for every state change. Falls back to
/// a system tray balloon tip if toast delivery fails for any reason (e.g.
/// notifications disabled in Windows Settings, or running in an environment
/// without an AppUserModelID) so the user always gets *some* feedback.
/// </summary>
public class NotificationService
{
    private readonly NotifyIcon _trayIconForFallback;

    public NotificationService(NotifyIcon trayIconForFallback)
    {
        _trayIconForFallback = trayIconForFallback;
    }

    public void NotifyStateChange(LockState state)
    {
        string title = state.ToNotificationTitle();
        string message = state.ToTooltip();

        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch
        {
            // Toast delivery can fail on non-packaged apps without a proper
            // AppUserModelID/shortcut registration. Fall back to a balloon
            // tip so the user is never left without feedback.
            _trayIconForFallback.BalloonTipTitle = title;
            _trayIconForFallback.BalloonTipText = message;
            _trayIconForFallback.ShowBalloonTip(2500);
        }
    }
}
