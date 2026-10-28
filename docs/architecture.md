# Desktop integration and research

## Current Acrylic backend update

User screenshots confirmed SystemBackdrop returned an opaque gray surface while inactive. This is consistent with [Microsoft's inactive Acrylic fallback](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic).

The tray now offers Solid, System Acrylic, and Native Acrylic (experimental). Native mode uses an independently implemented P/Invoke for undocumented SetWindowCompositionAttribute / WCA_ACCENT_POLICY, ACCENT_ENABLE_ACRYLICBLURBEHIND, and a translucent ABGR tint. It runs only against this application's own windows. No Windhawk injection, layered style, screen capture or focus activation is involved. System backdrop and accent policy are reset before changing backends; native memory is freed in finally. Transparency preference and high contrast suppress the effect.

This is a separate experimental compositor route, not a guarantee of equivalence to supported Desktop Acrylic. The API may change between Windows builds. Successful API calls still need visual verification with moving wallpaper. Existing enabled Acrylic settings without a backend field now select Native Acrylic; disabled clocks remain solid. Both selections persist per clock.

The existence of the accent-policy approach was cross-checked against the [Windhawk Translucent Windows project](https://windhawk.net/mods/translucent-windows); no implementation code was copied.

## Reference review

Reviewed DeskWidgets `core.py`, `main.py` and MIT license from main on 2026-10-28:

- `main.py` computes nearest X/Y candidates for work-area edges and adjacent cards. Its configuration gives widgets independent identities.
- `core.py` pins top-level windows by assigning Progman as owner and forcing bottom Z-order; it retries periodically.
- Its Backdrop loads the wallpaper file and scales a blurred image. Theme/backdrop polling and per-widget paint timers are inappropriate for this project's live-composition requirement.

We retain the UX concepts of independent cards, work-area snapping and edit mode. We add center alignment, deterministic tie handling, hysteresis and monitor-relative storage. We do not copy reference code, bitmap assets or its static-blur approach.

Sources: [core.py](https://github.com/VaticUI/DeskWidgets/blob/main/core.py), [main.py](https://github.com/VaticUI/DeskWidgets/blob/main/main.py), [MIT license](https://github.com/VaticUI/DeskWidgets/blob/main/LICENSE).

## Shell placement

The undocumented Progman 0x052c message is sent with a timeout. EnumWindows locates the actual top-level host of SHELLDLL_DefView instead of hard-coding a WorkerW sibling. A widget changes from WS_POPUP to WS_CHILD and is parented into that host, above its siblings. It never uses HWND_TOPMOST. Coordinate conversion uses ScreenToClient for the host origin.

This is an experimental shell strategy, not a Windows contract. It may fail with alternate shell layouts, icon settings or wallpaper renderers. Missing hosts hide the widgets. The manager retains a separate top-level broadcast listener for TaskbarCreated and recreates child windows after Explorer recovery; retries are bounded to 15 attempts at 2-second intervals. No periodic position polling runs in steady state.

Normal mode disables the HWND to allow the shell to receive input across process boundaries; HTTRANSPARENT alone would not reliably do that. Edit mode enables the child, retains WS_EX_NOACTIVATE and handles WM_MOUSEACTIVATE with MA_NOACTIVATE. Native rounding is applied to desktop children because DWM window rounding is not available there.

See [SetParent documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent): caller must manage WS_CHILD/WS_POPUP; cross-process DPI differences can reset awareness. Mixed-DPI behavior therefore requires hardware validation despite the PerMonitorV2 manifest and scale-aware layout math.

## Native backdrop

WPF uses `AllowsTransparency=false`, a transparent composition target and extended DWM frame. DWMWA_SYSTEMBACKDROP_TYPE=DWMSBT_TRANSIENTWINDOW requests Desktop Acrylic. No WS_EX_LAYERED is enabled.

[Microsoft's enumeration](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type) identifies this material on Windows 11 build 22621+. [Acrylic guidance](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic) documents solid fallback for inactive windows, disabled transparency and power-saving scenarios. A successful HRESULT cannot establish active live blur.

Our local experiment reproduces an API failure after SetParent. The app logs this and uses a solid theme background. Inactive Acrylic and Wallpaper Engine live sampling are not solved by this implementation.

Potential next experiments, not implemented or validated:

1. A top-level desktop-owned HWND with event-driven Z-order enforcement could preserve DWM eligibility, but needs Win+D/fullscreen/focus testing before replacing the child strategy.
2. Windows App SDK DesktopAcrylicController with explicit activation state might solve inactive rendering, but does not establish support for shell child HWNDs and introduces a runtime dependency.
3. Undocumented SetWindowCompositionAttribute accent blur is not a stable substitute for Desktop Acrylic. Do not advertise it as a guaranteed fix across 24H2–26H2.

## Coordinates, scheduling and resource use

Native rectangles and snapping are physical pixels; thresholds are scaled from DIP. Persistence stores the display name and work-area-relative DIP offsets. Missing displays fall back to the primary monitor and positions are clamped. Original disconnected-display placement is not retained separately; reconnecting does not automatically return the clock to that display. Display names are not EDID-stable hardware identities.

Each clock has one DispatcherTimer, scheduled for the next second or minute boundary. Text is only assigned when changed. System time/resume events reschedule; theme and display changes are event-driven. Dragging computes directly from the original pointer offset, so snapped positions do not accumulate pointer drift. Guides exist only inside the card during editing.

No capture loop, render loop, per-widget process, animation or shader is present. Native rounding transfers region ownership to Windows; event subscriptions, source hooks, timers, menus and tray icons are released at shutdown.

## Rendering correction after user feedback

The shell-child implementation remained invisible on the user's desktop, including after opaque software rendering. It has been replaced by a top-level WS_POPUP owned by the discovered desktop host (GWLP_HWNDPARENT), with HWND_BOTTOM enforced on Z-order changes. SetParent is no longer used for attachment. Desktop mode currently uses a solid WPF surface. Build, 17 tests and native smoke pass; visual visibility and Win+D/fullscreen behavior of this replacement remain unverified. Earlier child-host measurements and claims below are historical, not validation of the replacement.

## Current appearance controls

Desktop-owned popup visibility was confirmed by the user. Removed the competing HRGN (16 DIP), WPF rounded border (16 DIP) and DWM contour: only DWM now owns rounding and the outer border. WPF paints a rectangular client surface, clipped by the system contour.

Tray → Clock N → Acrylic (experimental) toggles DWMSBT_TRANSIENTWINDOW for that clock and persists `Acrylic` in layout.json. Existing clocks default to solid. Switching off resets both the system backdrop and extended frame before restoring opaque WPF rendering. API failure falls back to solid and triggers a tray notice. Native smoke exercises on/off; visual live blur while inactive remains unverified. The earlier claim that Acrylic is always disabled in desktop mode is superseded by this control.

## Gallery update (current)

The user confirmed native Acrylic visibly works; the SystemBackdrop rendering path and backend selector were removed. Acrylic is now native-only, with solid as the per-clock off state. The tray is only a gallery launcher (either mouse button).

The reference editor design is recreated in WPF with Clock only, two live digital previews, search, native dark backdrop, and Done. Unlike the reference's Escape handling, Escape does not close this editor, as explicitly requested. The editor alone is topmost, uses WS_EX_TOOLWINDOW with APPWINDOW cleared, has no draggable caption or resize border, and rejects system move/resize/minimize/close commands. Normal close attempts are canceled; Done hides the editor and locks clock input. Application shutdown is allowed to dispose it.

The gallery's 26-DIP outer contour uses one native region with the separate DWM border/corner disabled. This is scoped to the editor, not the clocks; there is no second outer WPF radius or border. Desktop clock contours remain entirely DWM-controlled.

Gallery design attribution and the original MIT notice are in THIRD-PARTY-NOTICES.md. Historical alternatives below remain as investigation records and do not describe the current user controls.

Current gallery contour supersedes the earlier region-based design: HRGN clipping was reported to produce opaque corners with AccentPolicy. The gallery now clears the region and delegates its outer contour to DWM, as clocks do. Its radius is system-defined rather than 26 DIP. Clock context menus use solid dark WPF popup templates, not Acrylic or simulated background images.

Global appearance supersedes per-clock Acrylic controls. Manager loads appearance.json before creating any clocks and applies one AppearanceSettings object to existing, new and recovered widgets. The gallery has a blur switch and 0–95% background transparency slider; the editor's own backdrop remains fixed for readability. Blur uses AccentPolicy state 4; no-blur transparency uses state 2. Tint alpha is derived from transparency, not Window.Opacity, so text is not faded. 0% uses a solid fallback. The selected native pipeline exposes no blur-radius control; the UI does not simulate one. Slider events are coalesced to at most one update per 50ms while interacting; saves debounce for 350ms and Done flushes both. No steady-state appearance polling is added.

Right-column / opacity correction: global controls now occupy a separate right-hand column after widget previews. Native tint alpha was observed by the user to have little effect with Acrylic. The widget now requests minimal native tint and paints its independently adjustable background tint once in WPF above the live native blur; text remains unmodified. The editor retains its fixed tint. The separate Blur slider is explicitly two-position Off / Native, not a fake 0–100 blur strength. Continuous radius remains unimplemented in the current AccentPolicy pipeline. A true radius would require a different compositor effect graph, e.g. Windows.UI.Composition GaussianBlurEffect (https://github.com/microsoft/Windows.UI.Composition-Win32-Samples); no capture-based workaround was introduced.
