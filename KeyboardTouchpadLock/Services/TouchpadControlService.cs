using System.Diagnostics;
using System.Security.Principal;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Enables/disables the built-in laptop touchpad without affecting any
/// external USB/Bluetooth mouse.
///
/// APPROACH: Windows exposes no public API to distinguish "the mouse
/// button/wheel event that just happened" by originating device class in a
/// way that lets us selectively swallow only touchpad input the way the
/// keyboard hook does for keys. Precision Touchpad drivers, however, enumerate
/// as their own PnP device (HID-compliant device / "Mouse" class, friendly
/// name containing "Touchpad" or "Precision Touchpad" on most laptops:
/// Synaptics, Elan, Microsoft Precision, etc.). Disabling that specific PnP
/// device via the Device Manager (Enable-PnpDevice / Disable-PnpDevice)
/// turns the touchpad hardware off at the driver level while any separate
/// USB/Bluetooth mouse - a different PnP device - is completely unaffected.
///
/// This requires administrator rights, so the operation is run out-of-process
/// via an elevated, hidden PowerShell call (a single UAC prompt on first use,
/// per Windows session) rather than requiring the whole app to run as admin.
/// See AppSettings.AlwaysRunAsAdmin to relaunch the app elevated instead and
/// avoid repeat prompts.
/// </summary>
public class TouchpadControlService
{
    public bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>Attempts to enable/disable the internal touchpad device(s).
    /// Returns true if the operation completed successfully.</summary>
    public async Task<bool> SetTouchpadEnabledAsync(bool enable)
    {
        string verb = enable ? "Enable-PnpDevice" : "Disable-PnpDevice";

        // Matches common internal touchpad friendly names while excluding
        // generic "HID-compliant mouse" entries that external mice also use,
        // and restricts to the built-in Mouse/HIDClass device class.
        string script =
            "$devices = Get-PnpDevice -Class Mouse,HIDClass -Status OK,Error,Disabled | " +
            "Where-Object { $_.FriendlyName -match 'touchpad|touch pad|precision touchpad|glidepoint|synaptics|elan' }; " +
            $"foreach ($d in $devices) {{ {verb} -InstanceId $d.InstanceId -Confirm:$false -ErrorAction SilentlyContinue }}";

        return await RunPowerShellAsync(script, elevateIfNeeded: true);
    }

    /// <summary>Best-effort check for whether a recognizable internal touchpad
    /// device exists at all, so the UI can warn the user if not (e.g. desktop PC).</summary>
    public async Task<bool> TouchpadDeviceExistsAsync()
    {
        string script =
            "$d = Get-PnpDevice -Class Mouse,HIDClass | " +
            "Where-Object { $_.FriendlyName -match 'touchpad|touch pad|precision touchpad|glidepoint|synaptics|elan' }; " +
            "if ($d) { exit 0 } else { exit 1 }";
        return await RunPowerShellAsync(script, elevateIfNeeded: false);
    }

    private async Task<bool> RunPowerShellAsync(string script, bool elevateIfNeeded)
    {
        bool needsElevation = elevateIfNeeded && !IsElevated;

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{script.Replace("\"", "\\\"")}\"",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = needsElevation // required for the "runas" verb to trigger UAC
        };

        if (needsElevation)
        {
            psi.Verb = "runas";
        }
        else
        {
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled the UAC prompt, or PowerShell couldn't be launched.
            return false;
        }
    }
}
