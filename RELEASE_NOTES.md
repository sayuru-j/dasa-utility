## D.A.S.A v1.0.0 (Pre-release)

**Download Automation & Security Assistant** for Windows — watches your Downloads folder, scans risky files with AMSI, applies your rules, and uses Google Gemini for intelligent sorting.

> Pre-release: early build for testing and feedback. Expect rough edges.

### Install

1. Download **`DASA-v1.0.0-win-x64.zip`** from the assets below
2. Extract to a folder (e.g. `C:\Program Files\DASA`)
3. Run **`DASA.exe`**
4. Open **Settings** and add your [Gemini API key](https://aistudio.google.com/) (optional — needed for AI sorting and rule discovery)
5. Confirm **Watch folder** and **Sort root**, then save

Closing the window minimizes to the tray. Use **Exit** from the tray menu to quit.

### Requirements

- Windows 10/11 (x64)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (Evergreen)
- [.NET 10 Runtime](https://dotnet.microsoft.com/download) (framework-dependent build)

### Highlights

- Download folder monitoring with manual scan
- AMSI malware scanning and quarantine
- User-defined sort rules with drag-and-drop priority
- AI rule discovery (beta) and Gemini categorization
- Smart subfolders, configurable wait time, undo from activity log
- Screen-edge move notifications (optional)
- Custom UI with Dashboard, Rules, Settings, and About tabs
- Local-first storage; Gemini API key encrypted with Windows DPAPI

### Known limitations (pre-release)

- Windows only (WPF + WebView2 + AMSI)
- Unsigned build — SmartScreen may warn on first run
- Gemini model availability depends on your API key and Google’s model list
- AI rule discovery is beta

### Build from source

```powershell
git clone https://github.com/sayuru-j/dasa-utility.git
cd dasa-utility
.\deploy.ps1
```

**Author:** [Sayuru .J Silva](https://github.com/sayuru-j)  
**License:** MIT
