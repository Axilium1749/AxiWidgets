# Validation scope

Run `./build.ps1 -Test` for pure geometry, time boundaries, calendar month/year handling, task storage/order and scrolling physics tests. The harness is a dependency-free executable.

Run `--smoke --data=.local/smoke` only after closing the normal app. Smoke checks real HWND ownership and input policies, gallery controls, tray menu and Exit, task input/actions, shared persistence, capture cleanup, and short/overflowing scroll lists. It writes only to the selected data directory. These automated checks do not establish subjective animation quality or every desktop integration scenario.

Native cards have been visually confirmed with Wallpaper Engine on a Windows 11 development machine. Finite samples showed changing backdrop pixels inside the native clip. This is not a cross-version guarantee or a quantitative blur measurement.

Before a public binary release, manually verify:

- Startup and exit with and without Wallpaper Engine; transparent corners and inactive live backdrop.
- Win+D, ordinary window cover/uncover and fullscreen applications.
- Explorer restart and bounded recovery without stranded input or duplicate widgets.
- Mixed-DPI displays, negative display origins, hotplug and sleep/resume.
- Physical mouse/precision touchpad scrolling, reverse-direction spring interruption and high-refresh animation quality.
- Clean installation/update/uninstall and actual sign-in autostart on target machines.

Historical per-machine CPU/GPU samples and experimental host screenshots are archived locally and are not current performance claims. No full hardware/long-idle validation is implied by a successful build or smoke run.

Installer automation has verified a separate test AppId: self-contained launch without a configured .NET installation, optional Run entry and shortcuts, update while the app is running, graceful shutdown, and uninstall preserving data while removing its Run entry and shortcuts. Smoke checks autostart enable/disable in an isolated registry key and non-destructive legacy data migration. A real Windows sign-out/sign-in was not performed.
