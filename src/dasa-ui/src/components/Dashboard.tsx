import { useEffect, useRef, useState } from 'react'
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
import { sourceMeta } from '../lib/colors'
import type { FileProcessedPayload, MalwareDetectedPayload, SettingsViewModel } from '../types'

interface DashboardProps {
  monitoring: boolean
  settings: SettingsViewModel
  history: FileProcessedPayload[]
  historyTotal: number
  loadingHistory: boolean
  onLoadMoreHistory: () => void
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
  return sourceMeta(source).label
}

function sourceTagClass(source: string) {
  return sourceMeta(source).tagClass
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
  const sourceClass = sourceTagClass(item.source)

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
        className="flex w-full items-center gap-1.5 py-2 text-left transition-colors hover:bg-surface-elevated/40"
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
          <ChevronRight size={12} strokeWidth={1.5} />
        </motion.span>
        <span className="min-w-0 flex-1 truncate text-xs text-text-secondary">{item.fileName}</span>
        <span className={`nothing-tag shrink-0 !px-1.5 !py-0.5 !text-[9px] ${sourceClass}`}>{sourceLabel(item.source)}</span>
        {item.quarantined && (
          <motion.span
            initial={{ scale: 0.8, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            className="nothing-tag nothing-tag-amsi shrink-0 !px-1.5 !py-0.5 !text-[9px]"
          >
            Quarantine
          </motion.span>
        )}
        {!expanded && (
          <span className="nothing-path-chip hidden max-w-[9rem] truncate !px-1.5 !py-0.5 !text-[9px] sm:inline">
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
            <div className="px-3 py-2 pl-7">
              <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                <motion.div
                  className="min-w-0 flex-1 space-y-1.5 font-mono text-[10px]"
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
                    {item.category && (
                      <span className="nothing-tag nothing-tag-info !normal-case">{item.category}</span>
                    )}
                    {item.confidence != null && (
                      <span className={item.confidence >= 0.85 ? 'text-success' : item.confidence >= 0.6 ? 'text-rule' : 'text-text-tertiary'}>
                        {(item.confidence * 100).toFixed(0)}% confidence
                      </span>
                    )}
                  </div>
                </motion.div>
                {item.undoToken && !item.quarantined && (
                  <motion.button
                    type="button"
                    className="nothing-btn nothing-btn-ghost shrink-0 !px-2 !py-1 !text-[10px]"
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
                    <RotateCcw size={11} strokeWidth={1.5} />
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
  historyTotal,
  loadingHistory,
  onLoadMoreHistory,
  quarantine,
  onToggleMonitoring,
  onManualScan,
  onUndo,
  onClearActivity,
}: DashboardProps) {
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set())
  const [confirmClear, setConfirmClear] = useState(false)
  const [scanning, setScanning] = useState(false)
  const scrollRef = useRef<HTMLDivElement>(null)
  const loadMoreRef = useRef<HTMLDivElement>(null)

  const hasMore = history.length < historyTotal

  useEffect(() => {
    const root = scrollRef.current
    const target = loadMoreRef.current
    if (!root || !target || !hasMore || loadingHistory) return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          onLoadMoreHistory()
        }
      },
      { root, rootMargin: '120px', threshold: 0 },
    )

    observer.observe(target)
    return () => observer.disconnect()
  }, [hasMore, loadingHistory, onLoadMoreHistory, history.length])

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
        className={`nothing-card flex shrink-0 flex-wrap items-center justify-between gap-4 p-5 ${
          monitoring ? 'nothing-card-accent-success' : ''
        }`}
      >
        <div>
          <div className="mb-1 flex items-center gap-2">
            <span
              className={`nothing-section-dot ${monitoring ? 'bg-success' : 'bg-text-tertiary'}`}
              aria-hidden
            />
            <p className="nothing-label">Monitoring</p>
          </div>
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
              data-tone="success"
              aria-label="Toggle monitoring"
              onClick={() => onToggleMonitoring(!monitoring)}
              whileTap={{ scale: 0.92 }}
            />
          </div>
          <motion.button
            type="button"
            className="nothing-btn nothing-btn-info"
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
          <div className="flex items-center gap-2">
            <span className="nothing-section-dot bg-info" aria-hidden />
            <p className="nothing-label">Activity</p>
          </div>
          <div className="flex items-center gap-2">
            <motion.span
              key={historyTotal}
              initial={{ scale: 1.2, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              className="nothing-tag nothing-tag-info !text-[9px]"
            >
              {historyTotal} sorted
            </motion.span>
            <AnimatePresence mode="wait">
              {historyTotal > 0 && (
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

        <div ref={scrollRef} className="min-h-0 flex-1 overflow-y-auto">
          <AnimatePresence mode="popLayout" initial={false}>
            {historyTotal === 0 ? (
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
                {hasMore && (
                  <div ref={loadMoreRef} className="py-3 text-center font-mono text-[10px] text-text-tertiary">
                    {loadingHistory ? 'Loading more…' : 'Scroll for more'}
                  </div>
                )}
              </motion.ul>
            )}
          </AnimatePresence>
        </div>
      </motion.section>
    </motion.div>
  )
}
