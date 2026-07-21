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
| [`demo.gif`](media/demo.gif) | UI preview — tab switching (README) | README preview section |
| [`demo.mp4`](media/demo.mp4) | Source recording for regenerating the GIF | — |
| [`dasa-dashboard.png`](media/dasa-dashboard.png) | Dashboard empty / idle state | README screenshots |
| [`dasa-rules.png`](media/dasa-rules.png) | Rules tab with list, editor, and AI discovery | README screenshots |
| [`dasa-settings.png`](media/dasa-settings.png) | Settings — folders, features, save bar | README screenshots |

### Adding new media

1. Export PNG at a consistent width (1280px or 1440px recommended).
2. Save to `docs/media/` with a clear name: `dasa-<view>.png`.
3. Update this table and the Screenshots section in [README.md](../README.md).
4. Keep file sizes reasonable; compress PNGs before committing.

### UI preview clip

The README uses [`demo.gif`](media/demo.gif) because GitHub does not render `<video>` tags from repo paths. Keep [`demo.mp4`](media/demo.mp4) as the source; regenerate the GIF after re-recording:

```powershell
ffmpeg -y -i docs/media/demo.mp4 -vf "fps=12,scale=720:-1:flags=lanczos,palettegen=max_colors=128:stats_mode=diff" $env:TEMP\dasa-palette.png
ffmpeg -y -i docs/media/demo.mp4 -i $env:TEMP\dasa-palette.png -lavfi "fps=12,scale=720:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3" -loop 0 docs/media/demo.gif
```

---

## UI map (for screenshots)

| Tab | What to capture |
|-----|-----------------|
| **Dashboard** | Monitoring toggle, watch path, activity log |
| **Rules** | Rule list, editor panel, optional AI discovery results |
| **Settings** | Folder paths, feature toggles, save bar |
| **About** | Version, links, project description |

---
