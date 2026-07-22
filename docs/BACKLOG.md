# Backlog

Planned improvements and fixes not yet implemented. Items here are documented for future work — not shipped yet.

---

## Startup behavior

### Start minimized to tray when auto-start is enabled

**Status:** To fix  
**Priority:** Medium

When **Auto-start** is enabled in Settings, DASA should launch at Windows login **minimized to the system tray** instead of opening the main window.

**Current behavior:** The app may show the main window on startup when launched via auto-start.

**Expected behavior:**

- User enables **Auto-start** in Settings.
- On next Windows login, DASA starts in the background.
- Only the tray icon is visible; the main window stays hidden until the user opens it from the tray.

**Relevant areas (for implementation):**

- `App.xaml.cs` — application startup / single-instance handling
- `MainWindow.xaml.cs` — window visibility on load (`HideToTray()` or start hidden)
- `SettingsService.cs` — `SetAutoStart()` (registry `Run` key)
- Settings: `autoStartWithWindows` in `AppSettings.cs` / IPC contracts

---
