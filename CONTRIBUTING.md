# Contributing

Bug reports and focused pull requests are welcome. Check existing issues before opening a new one and describe a concrete reproduction, Windows build and AxiWidgets version.

## Development

Use Windows 11 x64 and 64-bit PowerShell. From a clean clone:

```powershell
./scripts/Bootstrap.ps1
./build.ps1 -Test
```

The toolchain is local and pinned; do not commit `.tools`, `.local`, user data, credentials, binaries or machine-specific logs. Keep changes scoped and explain how they were tested.

Core logic must stay independent of WPF and Win32. Preserve the native live backdrop, non-activating desktop placement, DPI conversions, clipping and idle resource behavior. Avoid new widget processes or screen-capture rendering. Read [AGENTS.md](AGENTS.md) for repository rules and [architecture](docs/architecture.md) for the current design.

For HWND/input lifecycle changes, close the normal app and run the isolated native smoke command from the README. Hosted CI builds/tests the code but cannot verify desktop rendering, Wallpaper Engine, Win+D or physical input. Report what you actually checked.

The installer can be built with `./scripts/Build-Installer.ps1`. Do not change system autostart settings or install over another user's working copy while testing; use a separate test AppId via `-TestInstall`.
