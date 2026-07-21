import { useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { ChevronRight, RefreshCw, RotateCcw, Trash2 } from 'lucide-react'
import {
  activityItem,
  expandContent,
  fadeUp,
  pageVariants,
  pulseDot,
  scaleIn,
  springSnappy,
} from '../lib/motion'
import type { FileProcessedPayload, MalwareDetectedPayload, SettingsViewModel } from '../types'

interface DashboardProps {
  monitoring: boolean
  settings: SettingsViewModel
  history: FileProcessedPayload[]
  quarantine: MalwareDetectedPayload[]
  onToggleMonitoring: (enabled: boolean) => void
  onManualScan: () => void
  onUndo: (token: string) => void
  onClearActivity: () => void
}

function formatTime(value: string) {
  try {
    return new Date(value).toLocaleString()
  } catch {
    return value
  }
}

function sourceLabel(source: string) {
  switch (source) {
    case 'amsi':
      return 'AMSI'
    case 'rule':
      return 'Rule'
    case 'gemini':
      return 'Gemini'
    default:
      return 'Default'
  }
}

function shortDestination(path: string) {
  const normalized = path.replace(/[/\\]+$/, '')
  const parts = normalized.split(/[/\\]/).filter(Boolean)
  if (parts.length === 0) return path
  if (parts.length === 1) return parts[0]
  return `${parts[parts.length - 2]}/${parts[parts.length - 1]}`
}

function ActivityItem({
  item,
  expanded,
  onToggle,
  onUndo,
}: {
  item: FileProcessedPayload
  expanded: boolean
  onToggle: () => void
  onUndo: (token: string) => void
}) {
  return (
    <motion.li
      layout
      variants={activityItem}
      initial="initial"
      animate="animate"
      exit="exit"
      className="overflow-hidden border-b border-stroke last:border-b-0"
    >
      <motion.button
        type="button"
        className="flex w-full items-center gap-2 py-3 text-left transition-colors hover:bg-surface-elevated/40"
        onClick={onToggle}
        aria-expanded={expanded}
        whileHover={{ backgroundColor: 'rgba(22, 22, 22, 0.4)' }}
        whileTap={{ scale: 0.995 }}
      >
        <motion.span
          animate={{ rotate: expanded ? 90 : 0 }}
          transition={springSnappy}
          className="shrink-0 text-text-tertiary"
        >
          <ChevronRight size={14} strokeWidth={1.5} />
        </motion.span>
        <span className="min-w-0 flex-1 truncate text-sm">{item.fileName}</span>
        <span className="nothing-tag shrink-0">{sourceLabel(item.source)}</span>
        {item.quarantined && (
          <motion.span
            initial={{ scale: 0.8, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            className="nothing-tag shrink-0 !border-accent/40 !text-accent"
          >
            Quarantine
          </motion.span>
        )}
        {!expanded && (
          <span className="hidden truncate font-mono text-[10px] text-text-tertiary sm:inline">
            → {shortDestination(item.destinationPath)}
          </span>
        )}
      </motion.button>

      <AnimatePresence initial={false}>
        {expanded && (
          <motion.div
            variants={expandContent}
            initial="initial"
            animate="animate"
            exit="exit"
            className="overflow-hidden border-t border-stroke bg-surface-alt/50"
          >
            <div className="px-3 py-3 pl-8">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <motion.div
                  className="min-w-0 flex-1 space-y-2 font-mono text-[11px]"
                  initial={{ opacity: 0, y: 6 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.06, ...springSnappy }}
                >
                  <div>
                    <p className="nothing-label mb-1">From</p>
                    <p className="break-all text-text-secondary">{item.originalPath}</p>
                  </div>
                  <div>
                    <p className="nothing-label mb-1">To</p>
                    <p className="break-all text-text-secondary">{item.destinationPath}</p>
                  </div>
                  <div className="flex flex-wrap gap-x-4 gap-y-1 text-text-tertiary">
                    <span>{formatTime(item.timestamp)}</span>
                    {item.category && <span>Category: {item.category}</span>}
                    {item.confidence != null && <span>{(item.confidence * 100).toFixed(0)}% confidence</span>}
                  </div>
                </motion.div>
                {item.undoToken && !item.quarantined && (
                  <motion.button
                    type="button"
                    className="nothing-btn nothing-btn-ghost shrink-0"
                    onClick={(e) => {
                      e.stopPropagation()
                      onUndo(item.undoToken!)
                    }}
                    initial={{ opacity: 0, x: 8 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: 0.1 }}
                    whileHover={{ scale: 1.03 }}
                    whileTap={{ scale: 0.97 }}
                  >
                    <RotateCcw size={13} strokeWidth={1.5} />
                    Undo
                  </motion.button>
                )}
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.li>
  )
}

export function Dashboard({
  monitoring,
  settings,
  history,
  quarantine,
  onToggleMonitoring,
  onManualScan,
  onUndo,
  onClearActivity,
}: DashboardProps) {
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set())
  const [confirmClear, setConfirmClear] = useState(false)
  const [scanning, setScanning] = useState(false)

  const toggleExpanded = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const handleScan = () => {
    setScanning(true)
    onManualScan()
    window.setTimeout(() => setScanning(false), 1200)
  }

  return (
    <motion.div
      className="mx-auto flex h-full min-h-0 w-full max-w-4xl flex-col gap-6"
      variants={pageVariants}
    >
      <motion.section
        variants={fadeUp}
        className="nothing-card flex shrink-0 flex-wrap items-center justify-between gap-4 p-5"
      >
        <div>
          <p className="nothing-label mb-1">Monitoring</p>
          <p className="font-mono text-sm text-text-secondary">{settings.watchFolder || '—'}</p>
        </div>
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2.5">
            <motion.span
              key={monitoring ? 'active' : 'paused'}
              initial={{ opacity: 0, y: 4 }}
              animate={{ opacity: 1, y: 0 }}
              className={`font-mono text-[11px] uppercase tracking-wider ${monitoring ? 'text-success' : 'text-text-tertiary'}`}
            >
              {monitoring ? 'Active' : 'Paused'}
            </motion.span>
            <motion.button
              type="button"
              className="nothing-toggle"
              data-on={monitoring ? 'true' : 'false'}
              aria-label="Toggle monitoring"
              onClick={() => onToggleMonitoring(!monitoring)}
              whileTap={{ scale: 0.92 }}
            />
          </div>
          <motion.button
            type="button"
            className="nothing-btn nothing-btn-ghost"
            onClick={handleScan}
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.97 }}
          >
            <motion.span animate={scanning ? { rotate: 360 } : { rotate: 0 }} transition={{ duration: 0.8, ease: 'linear' }}>
              <RefreshCw size={13} strokeWidth={1.5} />
            </motion.span>
            Scan
          </motion.button>
        </div>
      </motion.section>

      <AnimatePresence>
        {quarantine.length > 0 && (
          <motion.section
            variants={scaleIn}
            initial="initial"
            animate="animate"
            exit="exit"
            className="nothing-card shrink-0 border-accent/30 bg-danger-bg p-5"
          >
            <div className="mb-3 flex items-center gap-2">
              <motion.span
                className="h-1.5 w-1.5 rounded-full bg-accent"
                variants={pulseDot}
                animate="animate"
              />
              <p className="nothing-label !text-accent">Quarantine</p>
            </div>
            <ul className="space-y-3">
              {quarantine.slice(0, 5).map((item, index) => (
                <motion.li
                  key={`${item.quarantinePath}-${index}`}
                  initial={{ opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: index * 0.05 }}
                  className="font-mono text-xs leading-relaxed text-text-secondary"
                >
                  <span className="text-text">{item.filePath}</span>
                  <span className="mx-2 text-text-tertiary">→</span>
                  <span>{item.quarantinePath}</span>
                </motion.li>
              ))}
            </ul>
          </motion.section>
        )}
      </AnimatePresence>

      <motion.section
        variants={fadeUp}
        className="nothing-card flex min-h-0 flex-1 flex-col overflow-hidden p-5"
      >
        <div className="mb-2 flex shrink-0 items-center justify-between gap-3">
          <p className="nothing-label">Activity</p>
          <div className="flex items-center gap-2">
            <motion.span
              key={history.length}
              initial={{ scale: 1.2, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              className="font-mono text-[10px] text-text-tertiary"
            >
              {history.length} events
            </motion.span>
            <AnimatePresence mode="wait">
              {history.length > 0 && (
                confirmClear ? (
                  <motion.div
                    key="confirm"
                    initial={{ opacity: 0, x: 8 }}
                    animate={{ opacity: 1, x: 0 }}
                    exit={{ opacity: 0, x: -8 }}
                    className="flex items-center gap-1.5"
                  >
                    <span className="font-mono text-[10px] text-text-tertiary">Clear all?</span>
                    <button
                      type="button"
                      className="nothing-btn nothing-btn-ghost !px-2 !py-1 text-[10px]"
                      onClick={() => {
                        onClearActivity()
                        setConfirmClear(false)
                        setExpandedIds(new Set())
                      }}
                    >
                      Yes
                    </button>
                    <button
                      type="button"
                      className="nothing-btn nothing-btn-ghost !px-2 !py-1 text-[10px]"
                      onClick={() => setConfirmClear(false)}
                    >
                      No
                    </button>
                  </motion.div>
                ) : (
                  <motion.button
                    key="clear"
                    type="button"
                    className="nothing-btn nothing-btn-ghost !px-2 !py-1"
                    onClick={() => setConfirmClear(true)}
                    aria-label="Clear activity"
                    initial={{ opacity: 0, x: 8 }}
                    animate={{ opacity: 1, x: 0 }}
                    exit={{ opacity: 0, x: -8 }}
                    whileHover={{ scale: 1.04 }}
                    whileTap={{ scale: 0.96 }}
                  >
                    <Trash2 size={12} strokeWidth={1.5} />
                    Clear
                  </motion.button>
                )
              )}
            </AnimatePresence>
          </div>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto">
          <AnimatePresence mode="popLayout" initial={false}>
            {history.length === 0 ? (
              <motion.p
                key="empty"
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0 }}
                className="py-12 text-center font-mono text-xs text-text-tertiary"
              >
                No files processed yet.
              </motion.p>
            ) : (
              <motion.ul key="list" layout>
                <AnimatePresence initial={false}>
                  {history.map((item) => (
                    <ActivityItem
                      key={item.id}
                      item={item}
                      expanded={expandedIds.has(item.id)}
                      onToggle={() => toggleExpanded(item.id)}
                      onUndo={onUndo}
                    />
                  ))}
                </AnimatePresence>
              </motion.ul>
            )}
          </AnimatePresence>
        </div>
      </motion.section>
    </motion.div>
  )
}
