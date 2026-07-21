import { useEffect, useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { FolderOpen, GripVertical, Plus, Sparkles, Trash2 } from 'lucide-react'
import { expandContent, fadeUp, pageVariants, confirmSwap, scaleIn, springSnappy } from '../lib/motion'
import { confidenceBarClass, confidenceTone } from '../lib/colors'
import { nativeBridge, subscribe } from '../services/nativeBridge'
import type { AutomationRule, DiscoveredRule, RulesDiscoveredPayload } from '../types'

interface RulesEditorProps {
  rules: AutomationRule[]
  onSave: (rule: AutomationRule) => void
  onDelete: (id: string) => void
  onReorder: (orderedIds: string[]) => void
  onClearAll: () => void
}

function emptyRule(): AutomationRule {
  return {
    id: crypto.randomUUID().replaceAll('-', ''),
    name: 'New Rule',
    enabled: true,
    priority: 0,
    extension: '',
    nameContains: '',
    domainContains: '',
    destinationFolder: '',
  }
}

function SortableRuleRow({
  rule,
  selected,
  onSelect,
}: {
  rule: AutomationRule
  selected: boolean
  onSelect: () => void
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: rule.id,
  })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.6 : 1,
  }

  return (
    <motion.div
      ref={setNodeRef}
      style={style}
      layout
      layoutId={`rule-row-${rule.id}`}
      whileHover={{ scale: isDragging ? 1 : 1.008, x: isDragging ? 0 : 2 }}
      whileTap={{ scale: 0.985 }}
      transition={springSnappy}
      className={`flex items-center gap-2.5 rounded-md border px-3 py-2.5 transition-colors ${
        selected
          ? 'border-rule/40 bg-rule/10 shadow-[inset_2px_0_0_0] shadow-rule'
          : 'border-stroke bg-surface hover:border-stroke-strong'
      }`}
    >
      <button
        type="button"
        className="cursor-grab text-text-tertiary active:cursor-grabbing"
        {...attributes}
        {...listeners}
        aria-label="Drag to reorder"
      >
        <GripVertical size={14} strokeWidth={1.5} />
      </button>
      <button type="button" className="min-w-0 flex-1 text-left" onClick={onSelect}>
        <div className="truncate text-sm">{rule.name}</div>
        <div className="truncate font-mono text-[10px] text-text-tertiary">
          {rule.extension || '*'} · {rule.destinationFolder || '—'}
        </div>
      </button>
      <span className={`font-mono text-[10px] uppercase ${rule.enabled ? 'text-success' : 'text-text-tertiary'}`}>
        {rule.enabled ? 'On' : 'Off'}
      </span>
    </motion.div>
  )
}

export function RulesEditor({ rules, onSave, onDelete, onReorder, onClearAll }: RulesEditorProps) {
  const [localRules, setLocalRules] = useState(rules)
  const [selectedId, setSelectedId] = useState<string | null>(rules[0]?.id ?? null)
  const [draft, setDraft] = useState<AutomationRule | null>(rules[0] ?? null)
  const [discovering, setDiscovering] = useState(false)
  const [discoverStatus, setDiscoverStatus] = useState('')
  const [discovered, setDiscovered] = useState<DiscoveredRule[]>([])
  const [discoverSummary, setDiscoverSummary] = useState('')
  const [selectedDiscovered, setSelectedDiscovered] = useState<Set<string>>(new Set())
  const [discoverError, setDiscoverError] = useState('')
  const [confirmClearAll, setConfirmClearAll] = useState(false)

  useEffect(() => {
    setLocalRules(rules)
    if (rules.length === 0) {
      setSelectedId(null)
      setDraft(null)
      setConfirmClearAll(false)
      return
    }

    if (!selectedId && rules[0]) {
      setSelectedId(rules[0].id)
      setDraft(rules[0])
    } else if (selectedId) {
      const match = rules.find((r) => r.id === selectedId)
      if (match) setDraft(match)
      else {
        setSelectedId(rules[0].id)
        setDraft(rules[0])
      }
    }
  }, [rules, selectedId])

  useEffect(() => {
    return subscribe((envelope) => {
      if (envelope.type === 'FOLDER_PICKED') {
        const payload = envelope.payload as { path: string; purpose: string }
        if (payload.purpose !== 'rule') return
        setDraft((current) => (current ? { ...current, destinationFolder: payload.path } : current))
        return
      }

      if (envelope.type === 'DISCOVER_RULES_PROGRESS') {
        const payload = envelope.payload as { phase: string; detail: string }
        setDiscoverStatus(payload.detail)
        return
      }

      if (envelope.type === 'RULES_DISCOVERED') {
        const payload = envelope.payload as RulesDiscoveredPayload
        setDiscovering(false)
        setDiscoverStatus('')
        setDiscoverError('')
        setDiscoverSummary(payload.summary)
        setDiscovered(payload.rules ?? [])
        setSelectedDiscovered(new Set((payload.rules ?? []).map((r) => r.id)))
        return
      }

      if (envelope.type === 'ERROR' && discovering) {
        const payload = envelope.payload as { message?: string }
        setDiscovering(false)
        setDiscoverStatus('')
        setDiscoverError(payload.message ?? 'Discovery failed.')
      }
    })
  }, [discovering])

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  const onDragEnd = (event: DragEndEvent) => {
    const { active, over } = event
    if (!over || active.id === over.id) return
    const oldIndex = localRules.findIndex((r) => r.id === active.id)
    const newIndex = localRules.findIndex((r) => r.id === over.id)
    const next = arrayMove(localRules, oldIndex, newIndex).map((r, i) => ({ ...r, priority: i }))
    setLocalRules(next)
    onReorder(next.map((r) => r.id))
  }

  const createRule = () => {
    const rule = emptyRule()
    setDraft(rule)
    setSelectedId(rule.id)
  }

  const startDiscovery = () => {
    setDiscoverError('')
    setDiscoverSummary('')
    setDiscovered([])
    setDiscovering(true)
    setDiscoverStatus('Starting…')
    nativeBridge.discoverRules()
  }

  const toggleDiscovered = (id: string) => {
    setSelectedDiscovered((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const applyDiscovered = () => {
    const toApply = discovered.filter((r) => selectedDiscovered.has(r.id))
    if (toApply.length === 0) return
    nativeBridge.applyDiscoveredRules(toApply)
    setDiscovered([])
    setDiscoverSummary('')
    setSelectedDiscovered(new Set())
  }

  return (
    <motion.div
      className="mx-auto flex h-full min-h-0 w-full max-w-4xl flex-col"
      variants={pageVariants}
      initial="initial"
      animate="animate"
    >
      <div className="nothing-scroll nothing-scroll-fade min-h-0 flex-1 overflow-y-auto pr-1">
        <div className="flex flex-col gap-6 pb-6">
      <motion.section
        variants={scaleIn}
        className="nothing-card nothing-card-accent-gemini border border-dashed border-gemini/30 p-5 sm:p-6"
      >
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0 flex-1">
            <div className="mb-2 flex items-center gap-2">
              <Sparkles size={14} className="text-gemini" strokeWidth={1.5} />
              <p className="nothing-label !text-gemini">Beta</p>
            </div>
            <h2 className="text-sm font-medium">Discover existing rules via AI</h2>
            <p className="mt-1.5 max-w-xl font-mono text-[11px] leading-relaxed text-text-tertiary">
              Scans Downloads, sorted folders, Documents, and media libraries to infer how you
              already organize files, then proposes matching automation rules.
            </p>
            {discoverStatus && (
              <p className="mt-2 font-mono text-[11px] text-text-secondary">{discoverStatus}</p>
            )}
            {discoverError && (
              <p className="mt-2 font-mono text-[11px] text-accent">{discoverError}</p>
            )}
          </div>
          <motion.button
            type="button"
            className="nothing-btn nothing-btn-primary shrink-0 !border-gemini/40 !bg-gemini !text-white hover:!bg-[#9580ff]"
            disabled={discovering}
            onClick={startDiscovery}
            whileHover={{ scale: discovering ? 1 : 1.03 }}
            whileTap={{ scale: discovering ? 1 : 0.97 }}
          >
            <motion.span
              animate={discovering ? { rotate: 360 } : { rotate: 0 }}
              transition={{ duration: 1.2, repeat: discovering ? Infinity : 0, ease: 'linear' }}
            >
              <Sparkles size={13} strokeWidth={1.5} />
            </motion.span>
            {discovering ? 'Discovering…' : 'Discover rules'}
          </motion.button>
        </div>

        <AnimatePresence>
          {discovered.length > 0 && (
            <motion.div
              variants={expandContent}
              initial="initial"
              animate="animate"
              exit="exit"
              className="overflow-hidden"
            >
              <div className="mt-5 border-t border-stroke pt-5">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3 px-0.5">
              <p className="font-mono text-[11px] text-text-secondary">{discoverSummary}</p>
              <button
                type="button"
                className="nothing-btn nothing-btn-ghost !py-1"
                onClick={applyDiscovered}
                disabled={selectedDiscovered.size === 0}
              >
                Add selected ({selectedDiscovered.size})
              </button>
            </div>
            <ul className="flex flex-col gap-2.5">
              {discovered.map((rule, index) => (
                <motion.li
                  key={rule.id}
                  initial={{ opacity: 0, x: -12 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: index * 0.05, ...springSnappy }}
                  className="flex gap-3 rounded-md border border-stroke bg-surface px-4 py-3 transition-colors hover:border-gemini/30 hover:bg-gemini/5"
                >
                  <input
                    type="checkbox"
                    checked={selectedDiscovered.has(rule.id)}
                    onChange={() => toggleDiscovered(rule.id)}
                    className="mt-0.5 accent-gemini"
                  />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm">{rule.name}</span>
                      <span className="nothing-tag nothing-tag-rule">{rule.extension || 'any'}</span>
                      <span className={`font-mono text-[10px] ${confidenceTone(rule.confidence)}`}>
                        {(rule.confidence * 100).toFixed(0)}%
                      </span>
                    </div>
                    <div className="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-surface-elevated">
                      <div
                        className={`h-full rounded-full ${confidenceBarClass(rule.confidence)}`}
                        style={{ width: `${Math.round(rule.confidence * 100)}%` }}
                      />
                    </div>
                    {rule.nameContains && (
                      <p className="mt-0.5 font-mono text-[10px] text-text-tertiary">
                        contains &quot;{rule.nameContains}&quot;
                      </p>
                    )}
                    <p className="mt-1 truncate font-mono text-[10px] text-info">
                      → {rule.destinationFolder}
                    </p>
                    {rule.reason && (
                      <p className="mt-1 text-[11px] text-text-secondary">{rule.reason}</p>
                    )}
                  </div>
                </motion.li>
              ))}
            </ul>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.section>

      <motion.div
        className="grid gap-6 lg:grid-cols-[260px_1fr]"
        variants={fadeUp}
      >
        <motion.section variants={fadeUp} className="nothing-card nothing-card-accent-warning flex min-h-[280px] flex-col gap-3 p-4 sm:p-5">
          <div className="flex items-center justify-between gap-2 px-0.5">
            <div className="flex items-center gap-2">
              <span className="nothing-section-dot bg-rule" aria-hidden />
              <p className="nothing-label">Rules</p>
            </div>
            <div className="flex items-center gap-1.5">
              {localRules.length > 0 && (
                <AnimatePresence mode="wait">
                  {confirmClearAll ? (
                    <motion.div
                      key="confirm-clear"
                      variants={confirmSwap}
                      initial="initial"
                      animate="animate"
                      exit="exit"
                      className="flex items-center gap-1.5"
                    >
                      <span className="font-mono text-[10px] text-accent">Clear all rules?</span>
                      <button
                        type="button"
                        className="nothing-btn nothing-btn-ghost !px-2 !py-1 text-[10px] !text-accent"
                        onClick={() => {
                          onClearAll()
                          setConfirmClearAll(false)
                        }}
                      >
                        Yes
                      </button>
                      <button
                        type="button"
                        className="nothing-btn nothing-btn-ghost !px-2 !py-1 text-[10px]"
                        onClick={() => setConfirmClearAll(false)}
                      >
                        No
                      </button>
                    </motion.div>
                  ) : (
                    <motion.button
                      key="clear-all"
                      type="button"
                      className="nothing-btn nothing-btn-ghost !px-2 !py-1 text-[10px]"
                      onClick={() => setConfirmClearAll(true)}
                      aria-label="Clear all rules"
                      variants={confirmSwap}
                      initial="initial"
                      animate="animate"
                      exit="exit"
                      whileHover={{ scale: 1.04 }}
                      whileTap={{ scale: 0.96 }}
                    >
                      <Trash2 size={12} strokeWidth={1.5} />
                      Clear all
                    </motion.button>
                  )}
                </AnimatePresence>
              )}
              <button type="button" className="nothing-btn nothing-btn-ghost !px-2.5 !py-1.5" onClick={createRule}>
                <Plus size={13} strokeWidth={1.5} />
              </button>
            </div>
          </div>
          {localRules.length === 0 ? (
            <motion.p
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              className="px-1 py-6 text-center font-mono text-[11px] text-text-tertiary"
            >
              No rules yet.
            </motion.p>
          ) : (
            <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
              <SortableContext items={localRules.map((r) => r.id)} strategy={verticalListSortingStrategy}>
                <div className="flex flex-col gap-2 pr-0.5">
                  {localRules.map((rule) => (
                    <SortableRuleRow
                      key={rule.id}
                      rule={rule}
                      selected={selectedId === rule.id}
                      onSelect={() => {
                        setSelectedId(rule.id)
                        setDraft(rule)
                      }}
                    />
                  ))}
                </div>
              </SortableContext>
            </DndContext>
          )}
        </motion.section>

        <motion.section
          layout
          className="nothing-card nothing-card-accent-info p-5 sm:p-6"
          transition={springSnappy}
        >
          <AnimatePresence mode="wait">
            {!draft ? (
              <motion.p
                key="empty"
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -8 }}
                className="py-8 text-center font-mono text-xs text-text-tertiary"
              >
                Select or create a rule.
              </motion.p>
            ) : (
              <motion.div
                key={draft.id}
                initial={{ opacity: 0, x: 16, scale: 0.98, filter: 'blur(4px)' }}
                animate={{ opacity: 1, x: 0, scale: 1, filter: 'blur(0px)' }}
                exit={{ opacity: 0, x: -12, scale: 0.99, filter: 'blur(3px)' }}
                transition={springSnappy}
                className="flex flex-col gap-5"
              >
              <p className="nothing-label">Rule builder</p>
              <label className="block">
                <span className="nothing-label mb-2 block">Name</span>
                <input
                  className="nothing-input"
                  value={draft.name}
                  onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                />
              </label>
              <div className="grid gap-5 sm:grid-cols-2">
                <label className="block">
                  <span className="nothing-label mb-2 block">Extension</span>
                  <input
                    className="nothing-input font-mono"
                    value={draft.extension ?? ''}
                    onChange={(e) => setDraft({ ...draft, extension: e.target.value })}
                    placeholder=".pdf"
                  />
                </label>
                <label className="block">
                  <span className="nothing-label mb-2 block">Name contains</span>
                  <input
                    className="nothing-input font-mono"
                    value={draft.nameContains ?? ''}
                    onChange={(e) => setDraft({ ...draft, nameContains: e.target.value })}
                    placeholder="Invoice"
                  />
                </label>
              </div>
              <label className="block">
                <span className="nothing-label mb-2 block">Destination</span>
                <div className="flex gap-2">
                  <input
                    className="nothing-input font-mono text-xs"
                    value={draft.destinationFolder}
                    onChange={(e) => setDraft({ ...draft, destinationFolder: e.target.value })}
                  />
                  <button
                    type="button"
                    className="nothing-btn nothing-btn-ghost shrink-0"
                    onClick={() => nativeBridge.pickFolder('rule')}
                    aria-label="Browse destination folder"
                  >
                    <FolderOpen size={13} strokeWidth={1.5} />
                  </button>
                </div>
              </label>
              <label className="flex items-center gap-2 text-sm text-text-secondary">
                <input
                  type="checkbox"
                  checked={draft.enabled}
                  onChange={(e) => setDraft({ ...draft, enabled: e.target.checked })}
                  className="accent-accent"
                />
                Enabled
              </label>
              <div className="flex gap-2 border-t border-stroke pt-4">
                <button
                  type="button"
                  className="nothing-btn nothing-btn-primary"
                  onClick={() => {
                    if (!draft.destinationFolder.trim()) return
                    onSave(draft)
                  }}
                >
                  Save
                </button>
                <button type="button" className="nothing-btn nothing-btn-ghost" onClick={() => onDelete(draft.id)}>
                  <Trash2 size={13} strokeWidth={1.5} />
                </button>
              </div>
              </motion.div>
            )}
          </AnimatePresence>
        </motion.section>
      </motion.div>
        </div>
      </div>
    </motion.div>
  )
}
