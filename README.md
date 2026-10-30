<p align="center"><img src="src/AxiWidgets/Assets/AxiWidgets.png" width="112" alt="AxiWidgets logo"></p>

# AxiWidgets

[![Windows build](https://github.com/Axilium1749/AxiWidgets/actions/workflows/build.yml/badge.svg)](https://github.com/Axilium1749/AxiWidgets/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**[Download for Windows](https://github.com/Axilium1749/AxiWidgets/releases/latest)** · [Report a bug](https://github.com/Axilium1749/AxiWidgets/issues/new/choose) · [Contributing](CONTRIBUTING.md)

Native glass widgets for the Windows desktop: Clock, Calendar, Reminders, System and SSH.
A C# / .NET 10 / WPF manager hosts C++/WinRT Windows Composition cards in one process.

## Install

Download `AxiWidgets-Setup-0.2.0-x64.exe` from [Releases](https://github.com/Axilium1749/AxiWidgets/releases/latest) and run it. The installer includes .NET, installs for the current user without elevation, and offers optional autostart. Use the tray icon to open the gallery or exit. The installer is currently unsigned.

## Requirements

- Windows 11 22H2 or later, x64 (build 22621+). Live backdrop composition uses Windows 11 APIs; behavior on other Windows builds and wallpaper renderers needs testing.
- 64-bit Windows PowerShell 5.1 or PowerShell 7 for building.
- Internet access for the first toolchain download, and approximately 4 GB of free disk space.

## Build from a clean clone

Clone the repository and run from its root:

```powershell
git clone https://github.com/Axilium1749/AxiWidgets.git
cd AxiWidgets
```

```powershell
./scripts/Bootstrap.ps1
./build.ps1 -Test
./build.ps1 -Run
```

Bootstrap downloads Microsoft .NET SDK 10.0.403, MSVC 14.44 and Windows SDK 10.0.26100 packages into ignored `.tools/`. Versions, download URLs and SHA-256/SHA-512 checksums are pinned in [toolchain.lock.json](scripts/toolchain.lock.json). Every cached package is checked before extraction. The script installs no global SDK and changes no system PATH, registry or shell settings. Microsoft's license terms apply to downloaded tools; their binaries are not part of this repository.

Downloads are cached in `.tools/downloads/`. To reuse a previously downloaded cache, use `./scripts/Bootstrap.ps1 -DownloadCache C:\path\to\cache`. After bootstrap, application restore and build work offline: the managed projects have no external NuGet package dependencies. `global.json` requires the pinned SDK. Native compilation runs automatically as part of the managed build.

Output: `src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.exe`, with its managed and native dependencies. Keep the complete output directory together. To launch it directly:

```powershell
$env:DOTNET_ROOT = Join-Path $PWD '.tools/dotnet'
./src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.exe --gallery
```

Close an existing instance first; the manager allows only one instance. The installer includes the .NET runtime; installed users do not need the SDK.

## Installer and autostart

Run `./scripts/Build-Installer.ps1` after bootstrap. It creates `artifacts/installer/AxiWidgets-Setup-0.2.0-x64.exe` with the .NET 10.0.14 runtime included. The first publish downloads pinned runtime packs from NuGet; the Inno Setup compiler is downloaded with a verified SHA-256 and prepared in portable mode under `.tools/inno`. The compiler version/hash is in `installer/compiler.lock.json`.

Installation is per user under `%LOCALAPPDATA%\Programs\AxiWidgets`, without administrator rights. The wizard offers a desktop shortcut and autostart, both optional. The Start menu shortcut and Windows uninstall entry are included. Run the new installer directly over an existing installation; uninstalling first is not required. It keeps the existing installation directory and asks the running app to save and exit. Layout, reminders and SSH connections remain in the separate user data directory. Uninstall removes the program, shortcuts and its autostart registration, while preserving user data. The current installer is unsigned.

Right-click the tray icon and check **Автозапуск** to enable launch on sign-in; uncheck it to disable. This registers the quoted executable path with `--startup` in the current user's Run key. Autostart opens desktop widgets without showing the gallery. No system-wide service or scheduled task is installed.

## Using widgets

- Left-click the tray icon to open the widget gallery. Right-click for the styled gallery/Exit menu.
- Choose Clock, Calendar, Reminders, System or SSH. Each preview creates an independent widget.
- Opening the gallery enables layout editing. Drag widgets to snap to display edges, centers and neighboring cards. Right-click a widget for its settings or removal.
- Done closes the gallery, locks widget positions and saves the layout. The gallery is a fixed topmost tool window; outside clicks and Alt+F4 do not dismiss it. Exit in the tray menu closes the entire application.
- Clocks offer date and seconds toggles and update at displayed time boundaries.
- Calendar uses the local Windows calendar, a Monday-first grid, and updates at midnight and time/resume events.
- Reminders work in normal mode. Tasks are shared across all Reminders instances. Enter adds a task, Escape cancels input, and clicking outside the input strip ends editing. New tasks appear first; completed tasks follow active tasks, with the most recently completed first. Text is limited to 200 characters.
- System shows CPU load/frequency, RAM usage, GPU/VRAM and network rates in Kbit/s or Mbit/s. One shared background collector samples every two seconds and stops when no System cards remain.
- SSH imports new connections from `~/.ssh/config` on activation, without duplicating existing aliases. Click a server to open Windows Terminal (PowerShell fallback); `+` opens connection settings. Drag settings rows to reorder and Save to apply. Passwords are never stored. An ICMP ping runs every 15 seconds, with an immediate pass after saving changes; hosts blocking ICMP can appear offline even when SSH works. Windows OpenSSH Client is required for connections.
- Scroll Reminders and SSH with the wheel or by dragging the list. Relative mouse input keeps dragging at screen edges while the ordinary cursor remains visible. Elastic scrolling, inertia and row animations stop updating when idle.

Cards use a live HostBackdropBrush, native Gaussian blur, 55% `#1E1E22` tint, a shared 30-DIP rounded clip and a thin outline. No wallpaper image or screen capture supplies the background. The gallery has an opaque dark background and system-rounded corners. Calendar/Reminders are 364 x 384 DIP (physical pixels at 100% scaling); clocks are 300 x 160 DIP; System/SSH are 364 x 162 DIP.

## Local data and diagnostics

Data is stored in `%LOCALAPPDATA%\AxiWidgets`: `layout.json`, `appearance.json`, shared `reminders.json`, `ssh.json` and `diagnostics.log`. Override with `--data=C:\path\to\folder`. These files, build outputs, SDK caches, dumps and local credentials are excluded from Git. On first use, legacy settings/tasks beside the executable in `.local/` are copied without deleting the old data or overwriting existing user data.

After closing the normal app, run the isolated native smoke harness:

```powershell
$env:DOTNET_ROOT = Join-Path $PWD '.tools/dotnet'
& "$env:DOTNET_ROOT/dotnet.exe" src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.dll --smoke --data=.local/smoke
```

`./build.ps1 -Test` runs the dependency-free test executable; `dotnet test` is not the test command. Smoke exercises actual HWND/input lifecycle, gallery/tray actions, task editing and scrolling, persistence and full shutdown. It modifies only its selected data directory and exits. Its WPF layout PNGs exclude the desktop backdrop. `--live-probe` is an opt-in, finite diagnostic pixel comparison, not a rendering loop or a blur-strength measurement.

See [architecture](docs/architecture.md) and [validation limits](docs/validation.md). Mixed-DPI hardware, Explorer recovery and Win+D/fullscreen behavior require explicit visual testing; API acceptance alone is not proof of live blur. Archived experiments and machine-specific measurements are not part of the source tree.

## License

MIT; see [LICENSE](LICENSE). Gallery design is adapted from [VaticUI/DeskWidgets](https://github.com/VaticUI/DeskWidgets); its notice is retained in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
