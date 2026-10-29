# AxiWidgets

Windows 11 desktop clocks with a C# / .NET 10 / WPF manager and C++/WinRT Windows Composition cards. One process, independent clocks, live backdrop blur, magnetic alignment and portable JSON persistence. No Python or browser runtime.

The user confirmed the native Composition prototype works with Wallpaper Engine. Desktop clocks now use that renderer; the existing WPF gallery and menus are retained. The gallery still uses AccentPolicy. See [validation](docs/validation.md) for earlier host checks.

## Build and run

Requires Windows x64 and .NET SDK **10.0.403**. The workspace SDK is isolated in `.tools/dotnet` and ignored by Git. A fresh checkout requires the official SDK installed globally or placed there.

The native build also uses the local MSVC 14.44 and Windows SDK 10.0.26100 toolchain under `.tools/native-toolchain`. `src/AxiWidgets.Native/Build.ps1` defines its paths; MSBuild builds and copies the DLL automatically. No separate widget process is started.

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
- Every clock uses HostBackdropBrush with a native Gaussian blur and a fixed 15% `#1E1E22` tint. A shared 30-DIP Composition clip covers glass, text, and the fine 0.6-DIP outline; pixels outside the rounded card are transparent. The gallery has no appearance controls.

Only the gallery is topmost. Desktop clocks remain below ordinary applications. The gallery is reusable, and its preview timer stops while hidden. Exiting the operating-system session or terminating the application still tears it down; Done does not exit the manager.

## Data and diagnostics

Settings and diagnostics are in `.local` beside the executable. Global appearance is in `appearance.json`; widget identities and positions stay in `layout.json`. Appearance is saved after a short idle delay and flushed by Done. Override with `--data=C:\path\to\writable-folder`. Writes use a temporary file followed by replacement; write errors are logged and notified.

Native test run, after closing the normal app:

```powershell
./.tools/dotnet/dotnet src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.dll --smoke --data=.local/smoke
```

The smoke harness verifies real HWND ownership/input transitions, persistence, editor tool/topmost styles, close suppression, search, preview actions and Done locking. It writes `gallery-layout.png` using the WPF renderer for layout inspection (the image excludes the live DWM background). It modifies only its selected data folder and exits. Each run adds test clocks.

The 21 pure unit tests are an executable harness, not `dotnet test`:

```powershell
./.tools/dotnet/dotnet run --project tests/AxiWidgets.Tests -c Release
```

`--poc=normal`, `--poc=noactivate` and `--poc=desktop` remain as host diagnostics; all use the native Composition card. Only desktop mode attaches the popup to the desktop owner. The gray SystemBackdrop variant is not selectable.

## Architecture and limits

- `AxiWidgets.Core`: independent geometry and boundary-based clock scheduling.
- `DesktopHost`: desktop ownership, coordinates and window styles; the gallery's legacy backdrop.
- `AxiWidgets.Native`: one native HWND per clock, shared compositor/graphics device, live backdrop and DirectWrite text. Text redraws only at the displayed time boundary or after a settings/DPI change.
- `ClockWindow`: native card lifecycle, scheduling, drag and instance settings.
- `GalleryWindow`: fixed editor and clock previews.
- `Manager`: lifecycle, tray launcher, persistence and shell recovery.

Desktop clocks are top-level WS_POPUP windows owned by the discovered desktop host, with bottom Z-order enforced. SetParent shell children were abandoned after invisible rendering was reproduced. Monitor IDs use display-device names, not stable hardware identities. Mixed-DPI hardware, Win+D/fullscreen and Explorer restart behavior still need the full validation matrix. Native Acrylic success on this machine is not a cross-version guarantee.

[Architecture history](docs/architecture.md) · [Validation](docs/validation.md)

Gallery design adapted from [VaticUI/DeskWidgets](https://github.com/VaticUI/DeskWidgets). The WPF implementation contains no Python runtime or reference bitmap assets. See [third-party notice](THIRD-PARTY-NOTICES.md). Source is MIT licensed.



