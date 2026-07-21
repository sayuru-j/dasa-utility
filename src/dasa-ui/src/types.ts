export interface AutomationRule {
  id: string
  name: string
  enabled: boolean
  priority: number
  extension?: string | null
  nameContains?: string | null
  domainContains?: string | null
  destinationFolder: string
  renamePattern?: string | null
}

export interface DiscoveredRule {
  id: string
  name: string
  extension?: string | null
  nameContains?: string | null
  destinationFolder: string
  confidence: number
  reason: string
}

export interface RulesDiscoveredPayload {
  summary: string
  foldersScanned: number
  rules: DiscoveredRule[]
}

export interface FileProcessedPayload {
  id: string
  originalPath: string
  destinationPath: string
  fileName: string
  category: string
  source: string
  confidence?: number | null
  undoToken?: string | null
  timestamp: string
  quarantined: boolean
}

export interface MalwareDetectedPayload {
  filePath: string
  quarantinePath: string
  detail: string
  timestamp: string
}

export interface SettingsViewModel {
  watchFolder: string
  defaultSortRoot: string
  quarantineFolder: string
  hasGeminiApiKey: boolean
  monitoringEnabled: boolean
  amsiProtectionEnabled: boolean
  autoStartWithWindows: boolean
  darkMode: boolean
  userTaxonomy: string
  waitTimeMinutes: number
  smartSubfoldersEnabled: boolean
  showMoveNotificationsEnabled: boolean
}

export interface StateSnapshot {
  settings: SettingsViewModel
  rules: AutomationRule[]
  history: FileProcessedPayload[]
  quarantineEvents: MalwareDetectedPayload[]
}

export interface WatcherStatusPayload {
  monitoring: boolean
  watchFolder: string
}

export type HostToUiType =
  | 'FILE_PROCESSED'
  | 'MALWARE_DETECTED'
  | 'WATCHER_STATUS_CHANGED'
  | 'STATE_SNAPSHOT'
  | 'ERROR'
  | 'SETTINGS_UPDATED'
  | 'RULES_UPDATED'
  | 'WINDOW_STATE_CHANGED'
  | 'FOLDER_PICKED'
  | 'DISCOVER_RULES_PROGRESS'
  | 'RULES_DISCOVERED'

export type UiToHostType =
  | 'SAVE_RULE'
  | 'DELETE_RULE'
  | 'REORDER_RULES'
  | 'UPDATE_SETTINGS'
  | 'TRIGGER_MANUAL_SCAN'
  | 'UNDO_MOVE'
  | 'SET_MONITORING'
  | 'GET_STATE'
  | 'PICK_FOLDER'
  | 'WINDOW_MINIMIZE'
  | 'WINDOW_MAXIMIZE'
  | 'WINDOW_CLOSE'
  | 'WINDOW_DRAG'
  | 'DISCOVER_RULES'
  | 'APPLY_DISCOVERED_RULES'
  | 'CLEAR_ACTIVITY'
  | 'OPEN_IN_EXPLORER'

export interface IpcEnvelope<T = unknown> {
  type: string
  requestId?: string | null
  payload?: T
}
