# Windows Quick Scaler

Windows Quick Scaler is a small Windows desktop utility for changing display scaling and quickly applying saved scaling presets from either its dashboard or the notification-area (system tray) icon.

**Current version: 0.9**

## Features

- Detects connected displays and shows their relative layout in the dashboard.
- Lets you select one or more displays by clicking their cards.
- Applies a scale to the selected displays.
- Saves named-in-the-UI presets containing a scale and either all displays or the selected display devices.
- Persists presets between launches in `%LOCALAPPDATA%\WindowsQuickScaler\presets.json`.
- Adds the same saved presets to the tray icon's right-click menu.
- Includes a default **100% — All monitors** preset when run for the first time.
- Can minimize to the system tray when the dashboard is closed.
- Can optionally add or remove itself from the current user's Windows startup apps.

## Requirements

- Windows 10 or later with active displays.
- .NET 8 Desktop Runtime.
- Inno Setup 6 to compile the installer.
- The app does not require administrator privileges for per-user presets or startup registration. Windows or a graphics driver may impose its own limitations on available scaling settings.

## Use

1. Launch Windows Quick Scaler.
2. Select the scale percentage from **Target Scale**.
3. Click the monitor cards to choose which displays should be affected. The blue cards are selected.
4. Use **Apply Selected** to apply the chosen scale now, or **Save Preset** to save the scale and selected displays for later.
5. Select a saved preset and use **Apply Selected**, or right-click the tray icon and select a preset there.

The tray menu and dashboard use the same saved preset collection. Preset names are generated from their scale and target displays. The default all-monitor preset applies to all displays connected when it is used. A preset targeting specific displays applies to the matching connected displays and reports any saved targets that are disconnected.

Scaling availability varies by display and graphics driver. If a change is not reflected immediately, sign out of Windows and sign back in.

## Build from source

Open a PowerShell terminal in the project directory and run:

```powershell
dotnet restore .\WindowsQuickScaler.csproj
dotnet build .\WindowsQuickScaler.csproj -c Release -warnaserror
```

Publish a Windows x64 build:

```powershell
dotnet publish .\WindowsQuickScaler.csproj -c Release -r win-x64 -warnaserror
```

The published application is written to:

```text
bin\Release\net8.0-windows\win-x64\publish\
```

The publish is framework-dependent; install the .NET 8 Desktop Runtime on a machine that does not already have it.

## Build the installer

After publishing, compile `installer.iss` with Inno Setup 6:

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" .\installer.iss
```

The installer script packages the x64 publish output and uses the app icon from `Assets\WindowsQuickScaler.ico`. The installer output is `WindowsQuickScaler_Setup_0.9.exe` in the project directory.

## Project layout

| File | Purpose |
| --- | --- |
| `App.xaml`, `App.xaml.cs` | WPF application entry point. |
| `MainWindow.xaml`, `MainWindow.xaml.cs` | Dashboard, display selection, tray icon and menu, and startup registration. |
| `ScaleManager.cs` | Windows Display Configuration interop for resolving displays and setting their scale. |
| `PresetStore.cs` | Preset model, validation, and per-user JSON persistence. |
| `Assets\WindowsQuickScaler.ico` | Multi-resolution application, tray, shortcut, and installer icon. |
| `installer.iss` | Inno Setup installer definition. |

## Data and troubleshooting

- Presets are stored in `%LOCALAPPDATA%\WindowsQuickScaler\presets.json`. Back up this file to preserve or transfer your presets.
- If a preset file is invalid or cannot be read or saved, the app displays an error instead of silently replacing the saved presets.
- If a preset targets a monitor that is disconnected, the app applies it to matching connected displays and reports the missing target.
- If Windows rejects a scale change, the app reports the display-specific error. The supported scale range depends on each display.
- If the tray icon is hidden, open Windows **Settings → Personalization → Taskbar → Other system tray icons** and enable Windows Quick Scaler.

## License

No license has been added yet. Until one is added, all rights remain with the copyright holder; reuse and redistribution are not granted by this repository.
