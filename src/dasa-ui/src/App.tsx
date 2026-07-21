import { useEffect, useMemo, useState } from 'react'
import { AnimatePresence, MotionConfig, motion } from 'framer-motion'
import { FolderCog, Info, LayoutDashboard, Settings as SettingsIcon } from 'lucide-react'
import { AboutPanel } from './components/AboutPanel'
import { Dashboard } from './components/Dashboard'
import { RulesEditor } from './components/RulesEditor'
import { SettingsPanel } from './components/SettingsPanel'
import { Sidebar } from './components/Sidebar'
import { TitleBar } from './components/TitleBar'
import { fadeIn, pageVariants, springSnappy } from './lib/motion'
import type { TabId } from './lib/colors'
import { isNativeHostAvailable, nativeBridge, subscribe } from './services/nativeBridge'
import type {
  ActivityHistoryPayload,
  AutomationRule,
  FileProcessedPayload,
  MalwareDetectedPayload,
  SettingsViewModel,
  StateSnapshot,
} from './types'

const defaultSettings: SettingsViewModel = {
  watchFolder: '',
  defaultSortRoot: '',
  quarantineFolder: '',
  hasGeminiApiKey: false,
  monitoringEnabled: true,
  amsiProtectionEnabled: true,
  autoStartWithWindows: false,
  darkMode: true,
  userTaxonomy: 'Documents, Invoices, Images, Videos, Archives, Installers, Spreadsheets, Presentations, Music, Other',
  waitTimeMinutes: 0,
  smartSubfoldersEnabled: false,
  showMoveNotificationsEnabled: true,
}

export default function App() {
  const [tab, setTab] = useState<TabId>('dashboard')
  const [settings, setSettings] = useState<SettingsViewModel>(defaultSettings)
  const [rules, setRules] = useState<AutomationRule[]>([])
  const [history, setHistory] = useState<FileProcessedPayload[]>([])
  const [historyTotal, setHistoryTotal] = useState(0)
  const [loadingHistory, setLoadingHistory] = useState(false)
  const [quarantine, setQuarantine] = useState<MalwareDetectedPayload[]>([])
  const [monitoring, setMonitoring] = useState(true)
  const [connected, setConnected] = useState(false)
  const [isMaximized, setIsMaximized] = useState(false)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    const native = isNativeHostAvailable()
    setConnected(native)

    const unsubscribe = subscribe((envelope) => {
      switch (envelope.type) {
        case 'STATE_SNAPSHOT': {
          const snap = envelope.payload as StateSnapshot
          setSettings(snap.settings)
          setRules(snap.rules ?? [])
          setHistory(snap.history ?? [])
          setHistoryTotal(snap.historyTotal ?? snap.history?.length ?? 0)
          setQuarantine(snap.quarantineEvents ?? [])
          setMonitoring(snap.settings.monitoringEnabled)
          break
        }
        case 'WINDOW_STATE_CHANGED': {
          const state = envelope.payload as { isMaximized: boolean }
          setIsMaximized(state.isMaximized)
          break
        }
        case 'FILE_PROCESSED': {
          const item = envelope.payload as FileProcessedPayload
          setHistory((prev) => {
            const exists = prev.some((h) => h.id === item.id)
            if (!exists) {
              setHistoryTotal((total) => total + 1)
            }
            return [item, ...prev.filter((h) => h.id !== item.id)]
          })
          if (item.quarantined) {
            setQuarantine((prev) => [
              {
                filePath: item.originalPath,
                quarantinePath: item.destinationPath,
                detail: 'AMSI detected malicious content',
                timestamp: item.timestamp,
              },
              ...prev,
            ].slice(0, 50))
          }
          break
        }
        case 'MALWARE_DETECTED': {
          const item = envelope.payload as MalwareDetectedPayload
          setQuarantine((prev) => [item, ...prev].slice(0, 50))
          break
        }
        case 'WATCHER_STATUS_CHANGED': {
          const status = envelope.payload as { monitoring: boolean; watchFolder: string }
          setMonitoring(status.monitoring)
          setSettings((prev) => ({ ...prev, watchFolder: status.watchFolder, monitoringEnabled: status.monitoring }))
          break
        }
        case 'SETTINGS_UPDATED': {
          const next = envelope.payload as SettingsViewModel
          setSettings(next)
          setMonitoring(next.monitoringEnabled)
          break
        }
        case 'RULES_UPDATED': {
          setRules((envelope.payload as AutomationRule[]) ?? [])
          break
        }
        case 'RULES_DISCOVERED':
          break
        case 'ACTIVITY_HISTORY': {
          const page = envelope.payload as ActivityHistoryPayload
          setHistory((prev) => {
            const seen = new Set(prev.map((item) => item.id))
            const merged = [...prev]
            for (const item of page.items ?? []) {
              if (!seen.has(item.id)) {
                seen.add(item.id)
                merged.push(item)
              }
            }
            return merged
          })
          setHistoryTotal(page.total ?? 0)
          setLoadingHistory(false)
          break
        }
        default:
          break
      }
    })

    if (native) {
      nativeBridge.getState()
    } else {
      setRules([])
      setSettings({
        ...defaultSettings,
        watchFolder: 'C:\\Users\\You\\Downloads',
        defaultSortRoot: 'C:\\Users\\You\\Downloads\\DASA Sorted',
        quarantineFolder: 'C:\\Users\\You\\AppData\\Local\\DASA\\Quarantine',
      })
    }

    const timer = window.setTimeout(() => setReady(true), 40)
    return () => {
      window.clearTimeout(timer)
      unsubscribe()
    }
  }, [])

  const tabs = useMemo(
    () => [
      { id: 'dashboard' as const, label: 'Dashboard', icon: LayoutDashboard },
      { id: 'rules' as const, label: 'Rules', icon: FolderCog },
      { id: 'settings' as const, label: 'Settings', icon: SettingsIcon },
      { id: 'about' as const, label: 'About', icon: Info },
    ],
    [],
  )

  const loadMoreHistory = () => {
    if (loadingHistory || history.length >= historyTotal) return
    setLoadingHistory(true)
    nativeBridge.getActivityHistory(history.length)
  }

  return (
    <MotionConfig reducedMotion="user" transition={springSnappy}>
      <motion.div
        className="flex h-full flex-col bg-surface text-text"
        initial={{ opacity: 0 }}
        animate={{ opacity: ready ? 1 : 0 }}
        transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
      >
        <TitleBar connected={connected} isMaximized={isMaximized} />

        <div className="flex min-h-0 flex-1">
          <Sidebar
            tab={tab}
            onTabChange={setTab}
            tabs={tabs}
            historyCount={historyTotal}
            rulesCount={rules.length}
            connected={connected}
          />

          <main className="relative flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden p-6">
            <AnimatePresence mode="wait">
              <motion.div
                key={tab}
                className="flex h-full min-h-0 flex-col"
                variants={pageVariants}
                initial="initial"
                animate="animate"
                exit="exit"
              >
                {tab === 'dashboard' && (
                  <Dashboard
                    monitoring={monitoring}
                    settings={settings}
                    history={history}
                    historyTotal={historyTotal}
                    loadingHistory={loadingHistory}
                    onLoadMoreHistory={loadMoreHistory}
                    quarantine={quarantine}
                    onToggleMonitoring={(enabled) => {
                      setMonitoring(enabled)
                      nativeBridge.setMonitoring(enabled)
                    }}
                    onManualScan={() => nativeBridge.triggerManualScan()}
                    onUndo={(token) => nativeBridge.undoMove(token)}
                    onClearActivity={() => {
                      nativeBridge.clearActivity()
                      setHistoryTotal(0)
                    }}
                  />
                )}
                {tab === 'rules' && (
                  <RulesEditor
                    rules={rules}
                    onSave={(rule) => nativeBridge.saveRule(rule)}
                    onDelete={(id) => nativeBridge.deleteRule(id)}
                    onReorder={(ids) => nativeBridge.reorderRules(ids)}
                    onClearAll={() => nativeBridge.clearAllRules()}
                  />
                )}
                {tab === 'settings' && (
                  <SettingsPanel
                    settings={settings}
                    onSave={(payload) => nativeBridge.updateSettings(payload)}
                    onPickFolder={(purpose) => nativeBridge.pickFolder(purpose)}
                  />
                )}
                {tab === 'about' && <AboutPanel />}
              </motion.div>
            </AnimatePresence>
          </main>
        </div>

        <motion.div
          className="pointer-events-none fixed inset-0 -z-10 overflow-hidden"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ duration: 1.2, delay: 0.2 }}
          aria-hidden
          variants={fadeIn}
        >
          <div className="absolute -left-24 top-8 h-72 w-72 rounded-full bg-success/8 blur-3xl nothing-orb" />
          <div className="absolute bottom-12 right-0 h-64 w-64 rounded-full bg-gemini/10 blur-3xl nothing-orb nothing-orb-delay-1" />
          <div className="absolute left-1/3 top-1/2 h-56 w-56 -translate-y-1/2 rounded-full bg-info/6 blur-3xl nothing-orb nothing-orb-delay-2" />
          <div className="absolute bottom-0 left-1/4 h-40 w-40 rounded-full bg-rule/8 blur-3xl nothing-orb nothing-orb-delay-3" />
          <div className="absolute right-1/4 top-0 h-32 w-32 rounded-full bg-accent/6 blur-3xl nothing-orb nothing-orb-delay-4" />
        </motion.div>
      </motion.div>
    </MotionConfig>
  )
}
