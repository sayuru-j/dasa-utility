# dasa-ui

React + TypeScript frontend for **D.A.S.A**, embedded in the .NET WPF host via WebView2.

## Stack

- React 19, TypeScript, Vite 8
- Tailwind CSS 4
- Framer Motion
- `@dnd-kit` for rule reordering
- Lucide icons

## Development

```powershell
npm install
npm run dev
```

Vite serves at `http://localhost:5173`. Run `dotnet run` in `src/DASA.Host` (Debug) to load the dev server in WebView2.

## Production build

```powershell
npm ci
npm run build
```

Output goes to `dist/`. The .NET host copies this into `ui/` during publish.

## App structure

| Path | Purpose |
|------|---------|
| `src/App.tsx` | Tab routing, IPC subscription, layout |
| `src/components/Dashboard.tsx` | Monitoring, activity log, quarantine |
| `src/components/RulesEditor.tsx` | Rules CRUD, drag reorder, AI discovery |
| `src/components/SettingsPanel.tsx` | Settings form and save bar |
| `src/components/AboutPanel.tsx` | Version, credits, external links |
| `src/components/Sidebar.tsx` | Navigation |
| `src/services/nativeBridge.ts` | WebView2 `postMessage` bridge |

## Documentation

- [Project README](../../README.md)
- [Docs index & media](../../docs/README.md)
