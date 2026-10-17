# Keyboard & Touchpad Lock

A Windows tray utility that lets you instantly lock the keyboard, the
touchpad, or both, via global hotkeys, with a floating Grammarly-style
status widget and full Fluent-ish settings UI.

This is a complete Visual Studio / `dotnet` project. It targets Windows
only (WPF + Windows Forms interop) and was written for **.NET 8**.

## Build & run

**Requirements:** Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd KeyboardTouchpadLock
dotnet restore
dotnet run
```

To produce a single portable `.exe` (no .NET install required on the
target machine):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output lands in `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`.

You can also just open the folder in **Visual Studio 2022** (it will
recognize the `.csproj`) and press F5.

## What it does

| Hotkey (default)  | Action                                   |
|--------------------|-------------------------------------------|
| Ctrl+Alt+1          | Toggle keyboard lock                      |
| Ctrl+Alt+2          | Toggle touchpad lock (external mice unaffected) |
| Ctrl+Alt+3          | Master toggle: lock/unlock **everything** |

All three are rebindable from the tray menu → **Settings**.

## Architecture

```
App.xaml.cs              composition root: wires services together,
                          owns the crash-safety net
Models/                   AppSettings, HotkeyDefinition, LockState (plain data)
Services/
  SettingsService         JSON persistence at %AppData%\KeyboardTouchpadLock
  HotkeyService            Win32 RegisterHotKey wrapper (global hotkeys)
  KeyboardBlockerService   WH_KEYBOARD_LL hook that selectively swallows keys
  TouchpadControlService   enables/disables the PnP touchpad device only
  TrayIconService          NotifyIcon + right-click menu
  NotificationService      toast notifications, tray-balloon fallback
  IconFactory              draws the colored circular tray icon at runtime
  StartupService           Windows "Run" registry key for launch-at-startup
ViewModels/MainViewModel   the single source of truth for lock state
Views/FloatingIndicator    the draggable circular status widget
Views/SettingsWindow       the settings page
```

## Why keyboard-only and touchpad-only blocking work the way they do

Windows has no single API that blocks "just the keyboard" or "just the
built-in touchpad" — this was checked directly against Win32/AutoHotkey
documentation before writing the code, rather than assumed:

- **Keyboard lock** uses a low-level keyboard hook (`WH_KEYBOARD_LL`) that
  inspects every key event and swallows it, *except* for the specific keys
  that make up your unlock hotkey(s) — those are always allowed through so
  Windows' own hotkey system still sees them and still fires the unlock.
  `BlockInput()`, by contrast, blocks keyboard **and** mouse together as one
  unit, which is exactly what this app needs to avoid for touchpad-independent
  locking.
- **Touchpad lock** disables the touchpad's own Plug-and-Play device (most
  laptops enumerate it separately from any external mouse, as "Precision
  Touchpad", "Synaptics", "ELAN", etc.), which requires a brief admin
  elevation the first time it's used per session. An external USB/Bluetooth
  mouse is a different PnP device entirely and is never touched.

## Safety guarantees implemented

- The hotkey that can fully unlock everything (default Ctrl+Alt+3) is
  **never** blocked by the keyboard hook, no matter what state you're in.
- `Ctrl+Alt+Delete` is a Windows Secure Attention Sequence handled entirely
  inside the OS kernel — no hook, including this app's, can intercept or
  block it.
- On any crash (`AppDomain.UnhandledException`, `DispatcherUnhandledException`)
  or normal process exit, the app forces the keyboard hook off and re-enables
  the touchpad before the process actually terminates.
- The app never persists "currently locked" across restarts — every launch
  starts fully unlocked, so a crash while locked can never leave you locked
  out the next time you sign in.
- Disabling a PnP device (touchpad) is a *driver-level* toggle, not a
  destructive action — Windows re-enables it normally on next boot even if
  this app never got the chance to turn it back on.

## Known limitations / things to verify on your hardware

- Touchpad detection matches on common friendly-name keywords (touchpad,
  precision touchpad, glidepoint, synaptics, elan). A small number of
  laptops with unusual OEM driver names may not be detected — the Settings
  page is where you'd extend the match list in `TouchpadControlService.cs`
  if that happens to you.
- Toast notifications use `Microsoft.Toolkit.Uwp.Notifications`, which works
  well for non-packaged desktop apps but can occasionally be suppressed by
  Windows if no shortcut with an AppUserModelID has been created yet; the
  app falls back to a tray balloon in that case automatically.
- I was not able to compile or run this on real Windows hardware from this
  environment (development sandbox is Linux-based) — please build it with
  `dotnet build` first and let me know if you hit any compiler errors so I
  can fix them directly.
