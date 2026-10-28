# AxiWidgets

Windows 11 desktop clocks in C# / .NET 10 / WPF + Win32. One process, independent clocks, native Acrylic, magnetic alignment and portable JSON persistence. No Python, browser runtime or external NuGet packages.

The user confirmed native Acrylic works with their live wallpaper on Windows 11 26H2. The gray SystemBackdrop variant has been removed. Native composition uses an undocumented Windows API and remains version-dependent. See [validation](docs/validation.md).

## Build and run

Requires Windows x64 and .NET SDK **10.0.403**. The workspace SDK is isolated in `.tools/dotnet` and ignored by Git. A fresh checkout requires the official SDK installed globally or placed there.

```powershell
./build.ps1 -Test
./build.ps1 -Run
```

To open the gallery immediately on launch:

```powershell
$env:DOTNET_ROOT="$PWD/.tools/dotnet"
./src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.exe --gallery
```

Close any existing instance first; a second process exits. There is no installer or autostart registration.

## Gallery and editing

Click the AxiWidgets notification icon (left or right button) to open the gallery. The icon may be in the taskbar overflow area. The old tray context menu is gone.

The gallery follows the reference's 1000 × 520 layout, dark translucent surface, 220-DIP sidebar, search, blue Clock selection, green add badges and Done button. Only digital clocks are available: **Digital** and **With seconds**. Both are the same widget type with different initial settings.

- Opening the gallery enables editing. It is a fixed tool window above other windows and excluded from the taskbar/Alt+Tab.
- It cannot be dragged or resized. Clicking outside, Escape and Alt+F4 do not dismiss it.
- Click a preview to add an independent clock. Search filters the Clock category.
- Drag desktop clocks to snap to edges, centers and neighboring gaps. Threshold/gap: 12 DIP; screen inset: 8 DIP; release radius: 18 DIP.
- Right-click a desktop clock while editing for **Show seconds**, **Show date**, and **Remove clock**.
- **Done** hides the gallery, releases mouse capture, removes guides, disables widget input and saves positions.
- Use the right-hand **Global appearance** column for the separate **Blur** strip (Off / Native) and **Transparency** slider (0–95%). The Blur strip has two discrete positions; continuous blur radius is not implemented. These settings apply immediately to every existing and future clock. At 0% the background is solid. Turning blur off selects native translucent tint without blur. Text stays opaque. The gallery keeps its own background for readability. Old per-clock Acrylic flags no longer override global appearance.

Only the gallery is topmost. Desktop clocks remain below ordinary applications. The gallery is reusable, and its preview timer stops while hidden. Exiting the operating-system session or terminating the application still tears it down; Done does not exit the manager.

## Data and diagnostics

Settings and diagnostics are in `.local` beside the executable. Global appearance is in `appearance.json`; widget identities and positions stay in `layout.json`. Appearance is saved after a short idle delay and flushed by Done. Override with `--data=C:\path\to\writable-folder`. Writes use a temporary file followed by replacement; write errors are logged and notified.

Native test run, after closing the normal app:

```powershell
./.tools/dotnet/dotnet src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.dll --smoke --data=.local/smoke
```

The smoke harness verifies real HWND ownership/input transitions, persistence, editor tool/topmost styles, close suppression, search, preview actions and Done locking. It writes `gallery-layout.png` using the WPF renderer for layout inspection (the image excludes the live DWM background). It modifies only its selected data folder and exits. Each run adds test clocks.

The 20 pure unit tests are an executable harness, not `dotnet test`:

```powershell
./.tools/dotnet/dotnet run --project tests/AxiWidgets.Tests -c Release
```

`--poc=normal`, `--poc=noactivate` and `--poc=desktop` remain as host diagnostics; they all use the native Acrylic implementation when enabled. Normal POC deliberately appears in the taskbar. System Acrylic is no longer selectable.

## Architecture and limits

- `AxiWidgets.Core`: independent geometry and boundary-based clock scheduling.
- `DesktopHost`: desktop ownership, coordinates, native composition and window styles.
- `ClockWindow`: clock rendering, drag and instance settings.
- `GalleryWindow`: fixed editor and clock previews.
- `Manager`: lifecycle, tray launcher, persistence and shell recovery.

Desktop clocks are top-level WS_POPUP windows owned by the discovered desktop host, with bottom Z-order enforced. SetParent shell children were abandoned after invisible rendering was reproduced. Monitor IDs use display-device names, not stable hardware identities. Mixed-DPI hardware, Win+D/fullscreen and Explorer restart behavior still need the full validation matrix. Native Acrylic success on this machine is not a cross-version guarantee.

[Architecture history](docs/architecture.md) · [Validation](docs/validation.md)

Gallery design adapted from [VaticUI/DeskWidgets](https://github.com/VaticUI/DeskWidgets). The WPF implementation contains no Python runtime or reference bitmap assets. See [third-party notice](THIRD-PARTY-NOTICES.md). Source is MIT licensed.



