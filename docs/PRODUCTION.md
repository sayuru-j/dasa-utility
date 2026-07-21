# Production & GitHub Release Guide

This document describes how to build, package, publish, and maintain **D.A.S.A** for production use and GitHub distribution.

**Author:** [Sayuru .J Silva](https://github.com/sayuru-j)  
**GitHub profile:** [github.com/sayuru-j](https://github.com/sayuru-j)  
**Repository:** [github.com/sayuru-j/dasa-utility](https://github.com/sayuru-j/dasa-utility)  
**License:** [MIT](../LICENSE) — open source, built for fun.

---

## Table of contents

1. [Pre-release checklist](#pre-release-checklist)
2. [Version numbering](#version-numbering)
3. [Production build](#production-build)
4. [Verify the build](#verify-the-build)
5. [Publishing to GitHub](#publishing-to-github)
6. [End-user installation](#end-user-installation)
7. [GitHub Actions CI](#github-actions-ci)
8. [Secrets & security](#secrets--security)
9. [Optional: code signing](#optional-code-signing)
10. [Optional: installer (MSIX / Inno Setup)](#optional-installer-msix--inno-setup)
11. [Updating a release](#updating-a-release)
12. [Support matrix](#support-matrix)

---

## Pre-release checklist

Before tagging a release, confirm:

- [ ] `npm run build` succeeds in `src/dasa-ui`
- [ ] `dotnet publish -c Release` succeeds in `src/DASA.Host`
- [ ] `DASA.exe` runs on a clean machine (or VM) with WebView2 installed
- [ ] UI loads from packaged `ui/` (not white screen) — **do not** set `DASA_UI_URL` in production
- [ ] Tray icon, window icon, and taskbar icon display correctly
- [ ] Settings save/load works; API key persists after restart
- [ ] File watcher moves a test download correctly
- [ ] AMSI path works for a test file (or is safely skipped for non-executables)
- [ ] No API keys, `.env`, or `%LOCALAPPDATA%\DASA` data in the repository
- [ ] `README.md` and version in `DASA.Host.csproj` are updated
- [ ] Release notes drafted

---

## Version numbering

Version is defined in `src/DASA.Host/DASA.Host.csproj`:

```xml
<Version>1.0.0</Version>
```

Use [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`.

| Change type | Example bump |
|-------------|----------------|
| Bug fix | 1.0.0 → 1.0.1 |
| New feature | 1.0.1 → 1.1.0 |
| Breaking change | 1.1.0 → 2.0.0 |

Keep the git tag aligned with the version: `v1.0.0`.

---

## Production build

All commands assume PowerShell from the **repository root**.

### Step 1 — Build the frontend

```powershell
cd src\dasa-ui
npm ci
npm run build
```

Output: `src/dasa-ui/dist/` (HTML, JS, CSS).

> **Important:** The .NET host copies `dist/` into `ui/` during build/publish. Always build the UI **before** publishing the host.

### Step 2 — Publish the Windows host

**Framework-dependent (smaller download, requires .NET 10 Runtime on the PC):**

```powershell
cd ..\DASA.Host
dotnet publish -c Release -r win-x64 --self-contained false -o .\publish
```

**Self-contained (larger download, no separate .NET install):**

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish
```

### Step 3 — Package for distribution

```powershell
cd publish
Compress-Archive -Path * -DestinationPath ..\DASA-v1.0.0-win-x64.zip -Force
```

The zip should contain at minimum:

```
DASA.exe
DASA.dll
Assets/
  icon.ico
ui/
  index.html
  assets/
  icon.ico          (from Vite public/)
... (runtime dependencies)
```

---

## Verify the build

Run from the publish folder **without** Vite running and **without** `DASA_UI_URL` set:

```powershell
cd src\DASA.Host\publish
.\DASA.exe
```

Confirm:

1. App opens (no white screen).
2. Dashboard, Rules, and Settings tabs work.
3. App minimizes to tray on close.
4. Tray icon shows the red D.A.S.A icon.

Test on a second user account or VM if possible to catch path/permission issues.

---

## Publishing to GitHub

### 1. Current repository

This project is currently published at:

- Repository: [github.com/sayuru-j/dasa-utility](https://github.com/sayuru-j/dasa-utility)
- Owner profile: [github.com/sayuru-j](https://github.com/sayuru-j)

Clone it with:

```powershell
git clone https://github.com/sayuru-j/dasa-utility.git
cd dasa-utility
```

### 2. Push a new remote repository (optional)

```powershell
git init
git add .
git commit -m "Initial release: D.A.S.A v1.0.0"
git branch -M main
git remote add origin https://github.com/<your-username>/dasa-utility.git
git push -u origin main
```

Replace `<your-username>` with your GitHub username or organization.

### 3. Recommended repository settings

On GitHub → **Settings**:

| Setting | Recommendation |
|---------|----------------|
| Default branch | `main` |
| Branch protection | Require PR reviews for `main` (optional) |
| Actions | Enabled (for CI workflow) |
| Security → Dependabot | Enable for NuGet + npm |

### 4. Create a GitHub Release

1. Go to **Releases → Draft a new release**.
2. Tag: `v1.0.0` (must match `Version` in csproj).
3. Title: `D.A.S.A v1.0.0`
4. Upload `DASA-v1.0.0-win-x64.zip` as a release asset.
5. Paste release notes (see template below).

**Release notes template:**

```markdown
## D.A.S.A v1.0.0

Download Automation & Security Assistant for Windows.

### Install
1. Download `DASA-v1.0.0-win-x64.zip`
2. Extract to a folder (e.g. `C:\Program Files\DASA`)
3. Run `DASA.exe`
4. Add your Gemini API key in Settings

### Requirements
- Windows 10/11 x64
- WebView2 Runtime ([download](https://developer.microsoft.com/microsoft-edge/webview2/))
- .NET 10 Runtime (if using framework-dependent build)

### Highlights
- AMSI malware scanning + quarantine
- Gemini-powered file sorting
- User rules + AI rule discovery
- Smart subfolders, wait time, activity undo

**Author:** Sayuru .J Silva
```

### 5. Using GitHub CLI (optional)

```powershell
gh release create v1.0.0 `
  .\src\DASA.Host\DASA-v1.0.0-win-x64.zip `
  --title "D.A.S.A v1.0.0" `
  --notes-file RELEASE_NOTES.md
```

---

## End-user installation

### Manual install (current approach)

1. Download the release zip from GitHub.
2. Extract to a permanent location, e.g. `C:\Program Files\DASA\`.
3. Run `DASA.exe`.
4. Optional: enable **Auto-start** in Settings to run at Windows login.
5. Optional: pin to Start or create a desktop shortcut to `DASA.exe`.

### First-run configuration

| Setting | Default | Notes |
|---------|---------|-------|
| Watch folder | `%USERPROFILE%\Downloads` | Folder to monitor |
| Sort root | `%USERPROFILE%\Downloads\DASA Sorted` | Where sorted files go |
| Gemini API key | (empty) | Required for AI sorting |
| AMSI protection | On | Recommended |
| Smart subfolders | Off | Enable for `Movies/Title/` style paths |
| Wait time | 0 min | Delay before moving new downloads |

### Uninstall

1. Exit DASA from the tray menu (**Exit**).
2. Delete the install folder.
3. Optionally delete user data: `%LOCALAPPDATA%\DASA\`

---

## GitHub Actions CI

The repository includes `.github/workflows/build.yml`, which on every push/PR:

- Builds the React UI (`npm ci` + `npm run build`)
- Publishes the .NET host (`dotnet publish -c Release`)
- Uploads the publish folder as a CI artifact (retained 7 days)

CI artifacts are for **verification**, not public distribution. Official releases should still be built locally or from a tagged workflow, then uploaded manually to GitHub Releases until you add a release automation step.

### Triggering a release build in CI

Push a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

You can extend the workflow later to attach the zip automatically to GitHub Releases.

---

## Secrets & security

### Never commit

| Item | Why |
|------|-----|
| Gemini API keys | User secret; use Settings UI + DPAPI |
| `.env` files | May contain keys |
| `%LOCALAPPDATA%\DASA\` | User data |
| `bin/`, `obj/`, `dist/`, `node_modules/` | Build output (already in `.gitignore`) |

### Repository hygiene

- Rotate any API key accidentally committed immediately.
- Use GitHub **Secret scanning** (enabled by default on public repos).
- Review `SettingsService` — keys are stored encrypted per Windows user, not in the repo.

### Gemini in production

- Users supply their own API key in Settings.
- Optional server-side proxy is **not** included; all Gemini calls go directly from the client with the user's key.
- Override model via environment variable: `DASA_GEMINI_MODEL=gemini-2.5-flash`

---

## Optional: code signing

Unsigned Windows apps may trigger SmartScreen ("Windows protected your PC").

To reduce warnings:

1. Obtain a code signing certificate (EV recommended for immediate SmartScreen trust).
2. Sign after publish:

```powershell
signtool sign /fd SHA256 /a /t http://timestamp.digicert.com .\publish\DASA.exe
```

3. Re-zip and upload the signed build to GitHub Releases.

---

## Optional: installer (MSIX / Inno Setup)

The project currently ships as a **portable zip**. For wider distribution consider:

| Option | Pros |
|--------|------|
| **MSIX** | Store-like install, clean uninstall, auto-update hooks |
| **Inno Setup** | Classic wizard installer, Start Menu shortcuts |
| **winget** | `winget install` for power users |

A future `installer/` folder can wrap the `publish/` output. The production build steps above remain the same; only the packaging layer changes.

---

## Updating a release

1. Bump `<Version>` in `DASA.Host.csproj`.
2. Update `README.md` if features changed.
3. Run the [production build](#production-build) steps.
4. Verify on a test machine.
5. Commit, tag (`v1.0.1`), push, create GitHub Release with new zip.

Users with auto-start enabled replace files in their install folder while DASA is **exited from the tray**, then restart.

---

## Support matrix

| Environment | Supported |
|-------------|-----------|
| Windows 11 x64 | Yes (primary) |
| Windows 10 x64 | Yes |
| ARM64 Windows | Not tested (build with `-r win-arm64` at your own risk) |
| macOS / Linux | No (WPF + WebView2 + AMSI are Windows-only) |

---

## Quick reference

```powershell
# Full production build from repo root
cd src\dasa-ui && npm ci && npm run build
cd ..\DASA.Host && dotnet publish -c Release -r win-x64 -o .\publish
Compress-Archive -Path .\publish\* -DestinationPath .\DASA-v1.0.0-win-x64.zip -Force
```

**Author:** Sayuru .J Silva
