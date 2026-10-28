# Validation record — 2026-10-28

Latest correction: the user confirmed the System Acrylic result was solid gray. Added an alternative AccentPolicy backend and three-way Background menu. Release build, 17 unit checks and native smoke passed after the change. API acceptance is logged separately from visual success; moving-wallpaper blur has not yet been confirmed. Historical results below refer to their respective earlier implementations.

Environment: Windows 11 Pro, registry DisplayVersion 26H2, build 26300; x64; SDK 10.0.403; runtime 10.0.14. Wallpaper Engine processes `wallpaper64` and `wallpaperservice64` were running. No shell restart, wallpaper setting, autostart or global SDK change was performed.

## Completed checks

| Check | Actual result |
|---|---|
| Release solution build | Passed, zero warnings/errors |
| Executable unit harness | 17/17 passed |
| Ordinary WPF clock | Launched in interactive Windows session; visual inspection confirmed HH:mm, localized date and rounded card |
| DWM API before parenting | Frame and Acrylic calls both returned S_OK in normal and noactivate modes |
| Desktop child attachment | Parent verified by GetParent; discovered Progman hosting SHELLDLL_DefView |
| DWM API after parenting | Frame 0x80070057 (E_INVALIDARG); backdrop 0x80070006 (invalid handle HRESULT) |
| Native smoke | Passed: real HWNDs, parent, input disabled/enabled/disabled, independent instances, JSON round-trip |
| Restart persistence | Second smoke started with two previously saved clocks and passed |
| Single-instance manager | Second interactive process exited within 3-second wait |
| Source control | Local Git repository initialized; no remotes, no push |

Unit coverage: nearest candidate; all four neighboring gaps; top-left and bottom-right monitor corners; X/Y centers; ties independent of enumeration order; threshold rejection; hysteresis release; negative monitor origins at 150% scaling; clamp after unplug; second/minute boundary timing.

## Acrylic limitation reproduced

Representative log sequence:

```text
mode=desktop
parent=0 frame=0x00000000 acrylic=0x00000000
Desktop attached parent=12020E class=Progman
parent=12020E frame=0x80070057 acrylic=0x80070006
```

The same calls succeed before changing the top-level HWND to a shell child and fail afterward. This reproduces an API-level incompatibility on this machine; it does not prove that all native composition approaches are impossible. Production mode deliberately uses solid fill after failure. Live Desktop Acrylic, including preservation while inactive and sampling Wallpaper Engine, is an **unfinished requirement**.

The normal-window screenshot showed a tinted system surface but was insufficient to prove dynamic background blur. No claim of successful live blur is made. No visual inspection of the desktop child through a full desktop screenshot was completed; native parenting and input state were checked programmatically.

## Resource measurements

One desktop clock, seconds hidden, Release, solid fallback:

- 30.0147 seconds idle: Private Bytes **71.97 MiB**, Working Set **111.03 MiB**.
- TotalProcessorTime delta **0 ms** over that interval. This is below the sampling resolution, not a guarantee of zero CPU.
- 607 handles, 18 threads in that sample.
- Manager initialization: **385–520 ms** across observed runs, measured from Manager.Start to tray setup; excludes CLR/process startup and is not time to first rendered frame.
- Ten 1-second GPU Engine samples filtered to the app PID produced a sum of 0.0. See `gpu-counter-availability.json` for whether matching counters were exposed. This does not include DWM composition cost or establish total system GPU overhead.
- Wallpaper Engine was running during GPU sampling. No static-wallpaper baseline or isolated DWM/Wallpaper Engine comparison was taken.

Raw samples: `performance-sample.json`, `gpu-sample.json`, `gpu-counter-availability.json`. Measurements predate any long-idle validation and are not benchmarks against DeskWidgets.

## Still unverified / limitations

- Actual Win+D behavior, Alt+Tab enumeration, fullscreen applications, shell click-through and focus while interacting with other applications.
- Tray-driven dragging/add/remove end-to-end with real mouse input. Native mode transitions and pure snapping are tested separately.
- Live Explorer termination/restart and recovery (implementation exists; user shell was not disrupted).
- Two physical displays, mixed DPI, primary-display changes, disconnect/reconnect and resolution changes. Unit geometry does not replace these tests.
- Stable physical monitor identity: current device name can change on connector re-enumeration. A missing monitor falls back and overwrites saved placement.
- Windows 11 24H2, 25H2 and other 26H2 builds.
- Light-theme/high-contrast visual review, long idle, suspend/resume and overnight resource stability.
- GPU comparison with static wallpaper; original DeskWidgets resource comparison.
- Guides are short segments inside cards. No desktop-spanning guide surface is implemented.

## Next stage

First compare a top-level desktop-owned host against the current child host using the same Acrylic POC, then evaluate DesktopAcrylicController activation handling if warranted. Test Win+D, fullscreen and focus before adopting another host strategy. Add stable display identities and execute the real multi-monitor / Explorer-restart matrix. Finally measure DWM-inclusive GPU overhead and overnight idle behavior.

The project is a buildable and runnable MVP prototype with explicit gaps, not a declaration that every Definition of Done condition has passed.

## Rendering correction after user feedback

The shell-child implementation remained invisible on the user's desktop, including after opaque software rendering. It has been replaced by a top-level WS_POPUP owned by the discovered desktop host (GWLP_HWNDPARENT), with HWND_BOTTOM enforced on Z-order changes. SetParent is no longer used for attachment. Desktop mode currently uses a solid WPF surface. Build, 17 tests and native smoke pass; visual visibility and Win+D/fullscreen behavior of this replacement remain unverified. Earlier child-host measurements and claims below are historical, not validation of the replacement.

## Current appearance controls

Desktop-owned popup visibility was confirmed by the user. Removed the competing HRGN (16 DIP), WPF rounded border (16 DIP) and DWM contour: only DWM now owns rounding and the outer border. WPF paints a rectangular client surface, clipped by the system contour.

Tray → Clock N → Acrylic (experimental) toggles DWMSBT_TRANSIENTWINDOW for that clock and persists `Acrylic` in layout.json. Existing clocks default to solid. Switching off resets both the system backdrop and extended frame before restoring opaque WPF rendering. API failure falls back to solid and triggers a tray notice. Native smoke exercises on/off; visual live blur while inactive remains unverified. The earlier claim that Acrylic is always disabled in desktop mode is superseded by this control.

## Gallery validation (current implementation)

The user confirmed native Acrylic visually works before this change. System Acrylic has now been removed. New gallery implementation passed a clean Release build (zero warnings/errors), all 17 unit checks and an expanded native smoke run with exit code 0.

The smoke run checked real editor TOOLWINDOW/TOPMOST flags with APPWINDOW absent, ignored WM_CLOSE, search empty/results states, both preview click handlers creating distinct clocks with correct seconds options, Done hiding the editor and disabling every clock HWND, and reopening the same editor instance. The first shutdown attempt exposed a blocked native close path; this was corrected, and the final smoke exited normally.

Inspected `.local/gallery-validation/gallery-layout.png`, rendered from the actual WPF visual tree at 1000 × 520: sidebar, title, previews, labels and footer fit without clipping. This is layout QA, not a screenshot proving the DWM backdrop. Actual mouse dragging, Alt+Tab enumeration and live backdrop appearance of the gallery have not been separately visually tested. The final app was launched with --gallery using the user's existing layout; isolated smoke clocks remain in their test data directories.

Final regression run: 18/18 unit checks passed, including loading a legacy layout with NativeAcrylic=false while preserving Acrylic on/off, seconds/date, monitor and coordinates. The native gallery smoke exited 0 after additional WM_SYSCOMMAND close/move/resize/minimize/maximize requests left visibility and bounds unchanged. Search with no results and Done both stopped the preview timer. The gallery tool window was absent from the UI automation window inventory, so no live desktop screenshot was captured through that tool. The release application was reopened with the gallery visible.

Corner/menu correction: user reported opaque gallery corner artifacts. Removed its custom 26-DIP region and switched to the same DWM-owned rounded contour used by the working clocks (the system chooses the radius). The menu now uses fully replaced ContextMenu/MenuItem templates: dark rounded surface, hover rows, compact blue switches and a red removal action, with no system check gutter. Inspected the rendered clock-menu-layout.png in .local/corners-menu-validation. Release build, 18/18 unit checks and native gallery/menu smoke passed. The gallery corner appearance still needs visual confirmation on the actual desktop; the WPF layout render excludes native DWM clipping.

Global appearance: clean Release build; 20/20 unit checks including clamping/non-finite opacity and JSON round-trip. Native smoke verified opacity 0 disables composition on all instances, slider/switch changes update the global model, saved appearance.json restores the chosen values, and normal gallery behavior remains intact. Inspected .local/global-appearance-test/gallery-layout.png; controls fit the sidebar and the percentage updates. Native transparent-tint mode was accepted by Windows; its live visual appearance has not been independently confirmed.

Right-column update: Release build and 20/20 tests passed. Native smoke passed with an added WPF pixel-render regression: with blur enabled, changing transparency from 20% to 80% changes the tint pixel alpha substantially (see .local/appearance-column-final/diagnostics.log). This proves the tint layer changes independently, not the appearance of the sampled live desktop. The right-column layout was rendered and inspected; the user-facing Blur strip remains Off/Native only. Continuous blur strength is still an open requirement.
