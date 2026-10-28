# AxiWidgets development rules

- C# / .NET 10 / WPF, x64 Windows. Only Digital Clock in this MVP.
- Core contains pure geometry and scheduling; never reference WPF or Win32 there.
- DesktopHost owns P/Invoke, shell discovery, native coordinates and DWM. Do not scatter native calls across new widget types.
- All native window positions and snap inputs use physical screen pixels. Convert margins/gaps using the destination display scale. Persist monitor-relative DIP positions.
- Never enable Always-on-top for desktop clocks, AllowsTransparency, WS_EX_LAYERED, wallpaper screenshots or continuous redraw/polling to hide a backdrop failure. The user explicitly requires the gallery editor to be topmost.
- DWM success is not proof of visible blur. Preserve diagnostic modes and report real HRESULTs. Solid fallback must be described as solid.
- Normal mode disables the clock HWND. Edit mode is explicit through the tray. On exit release capture and clear guides before disabling input.
- No new widget processes; one manager and one HWND per clock. Schedule clock updates at displayed time boundaries.
- Shell restart recovery must be event-driven with bounded retries. Keep the hidden broadcast listener independent of shell children.
- Dispose timers, native resources, tray menus and system-event subscriptions.
- Run `./build.ps1 -Test`. Tests are a dependency-free executable harness; `dotnet test` is not the test command.
- Run native smoke with `--smoke --data=.local/smoke` when changing HWND lifecycle; close the normal app first. Smoke modifies only its specified data folder.
- Do not claim manual Win+D, monitor hotplug, Explorer restart, GPU, Wallpaper Engine or long-idle validation without actually testing it.
- No GitHub remote, push, global SDK installation, shell termination or persistent system settings without user authorization.
- WFO0003 alone is suppressed: WinForms supplies NotifyIcon, but WPF owns startup and declares PerMonitorV2 in the manifest.
Current host uses desktop-owned top-level WS_POPUP, not SetParent children. Keep HWND_BOTTOM enforcement and verify real visual rendering; previous child HWND smoke did not establish visibility.

DWM alone owns desktop clock contours. The gallery also uses DWM rounding: custom HRGN with AccentPolicy produced opaque corner artifacts and has been removed. Do not layer another outer rounded WPF border. Blur and background transparency are global in appearance.json; do not reintroduce per-clock overrides. Native composition is experimental; resetting it must clear extended glass and restore opaque rendering.


Only native AccentPolicy Acrylic is supported. Do not restore the gray SystemBackdrop selector. Tray clicks open the fixed gallery; Done alone dismisses it and locks clocks. Respect application shutdown and stop preview timers while hidden.



