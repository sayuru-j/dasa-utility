# D.A.S.A documentation

Documentation for **Download Automation & Security Assistant**.

| Document | Description |
|----------|-------------|
| [../README.md](../README.md) | Project overview, features, dev setup, IPC reference |
| [PRODUCTION.md](PRODUCTION.md) | Production builds, GitHub releases, signing |

---

## Media assets

All screenshots and the UI preview clip live in [`docs/media/`](media/).

| File | Description | Used in |
|------|-------------|---------|
| [`demo.mp4`](media/demo.mp4) | UI preview — tab switching and layout | README preview section |
| [`dasa-dashboard.png`](media/dasa-dashboard.png) | Dashboard empty / idle state | README screenshots |
| [`dasa-rules.png`](media/dasa-rules.png) | Rules tab with list, editor, and AI discovery | README screenshots |
| [`dasa-settings.png`](media/dasa-settings.png) | Settings — folders, features, save bar | README screenshots |

### Adding new media

1. Export PNG at a consistent width (1280px or 1440px recommended).
2. Save to `docs/media/` with a clear name: `dasa-<view>.png`.
3. Update this table and the Screenshots section in [README.md](../README.md).
4. Keep file sizes reasonable; compress PNGs before committing.

### UI preview clip

[`demo.mp4`](media/demo.mp4) is a short silent loop for the README — tab switching and general UI look. Re-record when major layout or navigation changes. Keep it short (under ~15s); no need to convert to GIF (MP4 is smaller and sharper on GitHub).

---

## UI map (for screenshots)

| Tab | What to capture |
|-----|-----------------|
| **Dashboard** | Monitoring toggle, watch path, activity log |
| **Rules** | Rule list, editor panel, optional AI discovery results |
| **Settings** | Folder paths, feature toggles, save bar |
| **About** | Version, links, project description |

---
