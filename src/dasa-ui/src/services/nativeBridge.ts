import type { IpcEnvelope, UiToHostType } from '../types'

type MessageHandler = (envelope: IpcEnvelope) => void

interface ChromeWebView {
  postMessage: (message: unknown) => void
  addEventListener: (type: 'message', listener: (event: MessageEvent) => void) => void
  removeEventListener: (type: 'message', listener: (event: MessageEvent) => void) => void
}

declare global {
  interface Window {
    chrome?: {
      webview?: ChromeWebView
    }
  }
}

const listeners = new Set<MessageHandler>()
let attached = false

function ensureAttached() {
  if (attached) return
  attached = true

  const webview = window.chrome?.webview
  if (!webview) return

  webview.addEventListener('message', (event: MessageEvent) => {
    const data = typeof event.data === 'string' ? safeParse(event.data) : event.data
    if (!data || typeof data !== 'object' || !('type' in data)) return
    const envelope = data as IpcEnvelope
    listeners.forEach((listener) => listener(envelope))
  })
}

function safeParse(raw: string): unknown {
  try {
    return JSON.parse(raw)
  } catch {
    return null
  }
}

export function isNativeHostAvailable(): boolean {
  return Boolean(window.chrome?.webview)
}

export function subscribe(handler: MessageHandler): () => void {
  ensureAttached()
  listeners.add(handler)
  return () => listeners.delete(handler)
}

export function postToHost<T = unknown>(type: UiToHostType, payload?: T): void {
  ensureAttached()
  const envelope: IpcEnvelope<T> = {
    type,
    requestId: crypto.randomUUID(),
    payload,
  }

  const webview = window.chrome?.webview
  if (webview) {
    webview.postMessage(envelope)
    return
  }

  console.info('[dasa-bridge:mock]', envelope)
}

export const nativeBridge = {
  getState: () => postToHost('GET_STATE'),
  saveRule: (rule: unknown) => postToHost('SAVE_RULE', rule),
  deleteRule: (id: string) => postToHost('DELETE_RULE', { id }),
  reorderRules: (orderedIds: string[]) => postToHost('REORDER_RULES', { orderedIds }),
  updateSettings: (payload: unknown) => postToHost('UPDATE_SETTINGS', payload),
  setMonitoring: (enabled: boolean) => postToHost('SET_MONITORING', { enabled }),
  triggerManualScan: () => postToHost('TRIGGER_MANUAL_SCAN'),
  undoMove: (undoToken: string) => postToHost('UNDO_MOVE', { undoToken }),
  pickFolder: (purpose: 'watch' | 'sort' | 'rule') => postToHost('PICK_FOLDER', { purpose }),
  windowMinimize: () => postToHost('WINDOW_MINIMIZE'),
  windowMaximize: () => postToHost('WINDOW_MAXIMIZE'),
  windowClose: () => postToHost('WINDOW_CLOSE'),
  windowDrag: () => postToHost('WINDOW_DRAG'),
  discoverRules: () => postToHost('DISCOVER_RULES'),
  applyDiscoveredRules: (rules: unknown[]) => postToHost('APPLY_DISCOVERED_RULES', { rules }),
  clearActivity: () => postToHost('CLEAR_ACTIVITY'),
  openInExplorer: (path: string, selectFile = true) => postToHost('OPEN_IN_EXPLORER', { path, selectFile }),
  getActivityHistory: (offset: number, limit = 50) => postToHost('GET_ACTIVITY_HISTORY', { offset, limit }),
}
