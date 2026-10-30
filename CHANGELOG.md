# Changelog

## 0.2.0

- Add the System widget with CPU, RAM, GPU/VRAM and network monitoring on native glass.
- Add the SSH widget, automatic config import, terminal launch, connection settings, drag reordering and status checks.
- Reuse Reminders scrolling for SSH, including drag, inertia, elastic overscroll and native composition animations.
- Refresh SSH availability immediately after saving a connection, without waiting for the regular interval.
- Preserve the installation directory and installer task selections when upgrading. Install directly over 0.1.0; user data remains separate.

## 0.1.0

First public release for Windows 11 x64.

- Native live glass cards with rounded clipping: Clock, Calendar and Reminders.
- Widget gallery, layout editing, snapping and monitor-relative position storage.
- Shared local reminders with animated ordering, elastic drag/wheel scrolling and keyboard input.
- Styled tray menu with gallery, autostart and full exit actions.
- Per-user installer with the .NET runtime included, optional shortcuts/autostart, graceful upgrade and uninstall that preserves data.
- Pinned local build toolchain and automated Windows build/tests.

The installer is unsigned. Installation has been confirmed by the project owner on Windows; see [validation limits](docs/validation.md) for scenarios that still need broader testing.
