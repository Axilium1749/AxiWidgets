# Architecture

## Components

- `AxiWidgets.Core`: geometry, snapping, clock scheduling, calendar generation, local reminder storage and scroll physics. No WPF or Win32 dependencies.
- `AxiWidgets`: one WPF manager process, tray menu, widget gallery, state, scheduling and interaction. `ClockWindow` is the shared native-widget controller despite its historical name.
- `AxiWidgets.Native`: a C++/WinRT DLL with a shared compositor, D3D/D2D graphics device and DirectWrite resources. Each widget has one desktop HWND. List rows have separate composition visuals for movement and clipping.
- `DesktopHost`: shell discovery, ownership, native positions, input/window styles and centralized managed Win32 interop.

## Rendering and desktop placement

Widget windows are non-activating, desktop-owned top-level WS_POPUP windows with WS_EX_NOREDIRECTIONBITMAP and HWND_BOTTOM enforcement. They use HostBackdropBrush through a native Gaussian effect; glass, text and border share a 30-DIP composition clip. The backdrop is live composition, not a captured or cached wallpaper image. Clock rendering does not use WPF transparency, layered HWNDs, custom HRGNs or XAML Islands.

The gallery and context menus use WPF. The gallery has an opaque dark surface, DWM rounding, no taskbar entry, and is deliberately topmost. Desktop widgets are not topmost. Only Done dismisses the gallery during normal use; application shutdown can close it.

Normal mode disables Clock/Calendar input. Reminders retain input for task actions and scrolling without enabling widget movement. Explicit text entry uses a temporary clipped native edit control for Windows keyboard/clipboard behavior; visible text stays on the existing composition surface. Edit mode enables whole-widget dragging and snapping. Exit releases capture, unregisters relative input, stops timers and disposes windows and graphics resources.

## Coordinates and scheduling

Win32 bounds and snap inputs are physical pixels. Layout storage uses monitor-relative DIP coordinates. Gaps and widget dimensions are converted using the destination display scale. Missing displays fall back to a visible work area; display names are not persistent hardware identities.

Clocks update at second/minute boundaries; Calendar updates at local midnight and system events. Reminders redraw for state/input changes or virtualized row refills. Native keyframe trajectories play scroll inertia/spring return at compositor cadence. The managed motion timer only maintains virtualized content while movement is active and stops at rest. Wheel and drag behavior are configured in `ScrollParameters`.

The hidden broadcast listener is independent of desktop-owned widgets. Explorer recovery uses TaskbarCreated and bounded retries; there is no steady-state shell polling.

## Build

`scripts/toolchain.lock.json` pins the complete local toolchain. Bootstrap verifies archive hashes and extracts into `.tools/` without installing system components. MSBuild invokes the native build and copies the DLL beside the managed output. No experiment, external NuGet package or absolute developer path is required.

User data defaults to the current user's LocalApplicationData/AxiWidgets. Explicit --data overrides remain isolated for diagnostics. Autostart is a per-user Run entry. The installer uses the --shutdown event signal to request a graceful save/exit before upgrade or removal; it does not kill widget processes.
