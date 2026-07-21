import { useEffect, useMemo, useState } from 'react'
import { AnimatePresence, MotionConfig, motion } from 'framer-motion'
import { FolderCog, LayoutDashboard, Settings as SettingsIcon } from 'lucide-react'
import { Dashboard } from './components/Dashboard'
import { RulesEditor } from './components/RulesEditor'
import { SettingsPanel } from './components/SettingsPanel'
import { TitleBar } from './components/TitleBar'
import { fadeIn, navItem, navStagger, pageVariants } from './lib/motion'
import { isNativeHostAvailable, nativeBridge, subscribe } from './services/nativeBridge'
import type {
  AutomationRule,
  FileProcessedPayload,
  MalwareDetectedPayload,
  SettingsViewModel,
  StateSnapshot,
} from './types'

type TabId = 'dashboard' | 'rules' | 'settings'

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
}

export default function App() {
  const [tab, setTab] = useState<TabId>('dashboard')
  const [settings, setSettings] = useState<SettingsViewModel>(defaultSettings)
  const [rules, setRules] = useState<AutomationRule[]>([])
  const [history, setHistory] = useState<FileProcessedPayload[]>([])
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
          setHistory((prev) => [item, ...prev.filter((h) => h.id !== item.id)].slice(0, 100))
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
    ],
    [],
  )

  return (
    <MotionConfig reducedMotion="user">
      <motion.div
        className="flex h-full flex-col bg-surface text-text"
        initial={{ opacity: 0 }}
        animate={{ opacity: ready ? 1 : 0 }}
        transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
      >
        <TitleBar connected={connected} isMaximized={isMaximized} />

        <div className="flex min-h-0 flex-1">
          <motion.nav
            className="flex w-44 shrink-0 flex-col gap-0.5 border-r border-stroke bg-surface-alt p-2"
            initial="initial"
            animate="animate"
            variants={navStagger}
          >
            <motion.p className="nothing-label mb-2 px-2 pt-1" variants={navItem}>
              Menu
            </motion.p>
            {tabs.map(({ id, label, icon: Icon }) => {
              const active = tab === id
              return (
                <motion.button
                  key={id}
                  type="button"
                  className="nothing-nav-item relative"
                  data-active={active ? 'true' : 'false'}
                  onClick={() => setTab(id)}
                  variants={navItem}
                  whileHover={{ x: 2 }}
                  whileTap={{ scale: 0.98 }}
                >
                  {active && (
                    <motion.span
                      layoutId="nav-active-bg"
                      className="absolute inset-0 rounded border border-stroke bg-surface-elevated"
                      transition={{ type: 'spring', stiffness: 380, damping: 32 }}
                      style={{ zIndex: 0 }}
                    />
                  )}
                  {active && (
                    <motion.span
                      layoutId="nav-active-dot"
                      className="absolute left-2.5 top-1/2 z-10 h-1 w-1 -translate-y-1/2 rounded-full bg-accent"
                      transition={{ type: 'spring', stiffness: 500, damping: 30 }}
                    />
                  )}
                  <Icon size={15} strokeWidth={1.5} className="relative z-10 pl-2" />
                  <span className="relative z-10">{label}</span>
                </motion.button>
              )
            })}
          </motion.nav>

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
                    quarantine={quarantine}
                    onToggleMonitoring={(enabled) => {
                      setMonitoring(enabled)
                      nativeBridge.setMonitoring(enabled)
                    }}
                    onManualScan={() => nativeBridge.triggerManualScan()}
                    onUndo={(token) => nativeBridge.undoMove(token)}
                    onClearActivity={() => nativeBridge.clearActivity()}
                  />
                )}
                {tab === 'rules' && (
                  <RulesEditor
                    rules={rules}
                    onSave={(rule) => nativeBridge.saveRule(rule)}
                    onDelete={(id) => nativeBridge.deleteRule(id)}
                    onReorder={(ids) => nativeBridge.reorderRules(ids)}
                  />
                )}
                {tab === 'settings' && (
                  <SettingsPanel
                    settings={settings}
                    onSave={(payload) => nativeBridge.updateSettings(payload)}
                    onPickFolder={(purpose) => nativeBridge.pickFolder(purpose)}
                  />
                )}
              </motion.div>
            </AnimatePresence>
          </main>
        </div>

        <motion.div
          className="pointer-events-none fixed inset-0 -z-10"
          initial={{ opacity: 0 }}
          animate={{ opacity: 0.35 }}
          transition={{ duration: 1.2, delay: 0.2 }}
          aria-hidden
          variants={fadeIn}
        >
          <div className="absolute -left-32 top-0 h-64 w-64 rounded-full bg-accent/5 blur-3xl" />
          <div className="absolute bottom-0 right-0 h-48 w-48 rounded-full bg-accent/3 blur-3xl" />
        </motion.div>
      </motion.div>
    </MotionConfig>
  )
}
