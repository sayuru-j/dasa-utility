import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import {
  Bell,
  Clock3,
  FolderInput,
  FolderOutput,
  KeyRound,
  Layers,
  Lock,
  Power,
  Save,
  Shield,
  Sparkles,
  Tags,
} from 'lucide-react'
import { fadeUp, pageVariants } from '../lib/motion'
import type { SettingsViewModel } from '../types'

interface SettingsPanelProps {
  settings: SettingsViewModel
  onSave: (payload: Record<string, unknown>) => void
  onPickFolder: (purpose: 'watch' | 'sort') => void
}

const WAIT_TIME_OPTIONS = [0, 1, 2, 5, 10, 15, 30, 60] as const

const accentDotClass = {
  gemini: 'bg-gemini',
  success: 'bg-success',
  info: 'bg-info',
  rule: 'bg-rule',
  accent: 'bg-accent',
} as const

const toggleActiveClass = {
  info: 'border-info/30 bg-info/5',
  gemini: 'border-gemini/30 bg-gemini/5',
  warning: 'border-warning/30 bg-warning/5',
  success: 'border-success/30 bg-success/5',
} as const

const toggleIconClass = {
  info: 'text-info bg-info/10 border-info/20',
  gemini: 'text-gemini bg-gemini/10 border-gemini/20',
  warning: 'text-warning bg-warning/10 border-warning/20',
  success: 'text-success bg-success/10 border-success/20',
} as const

const fieldVariants = {
  initial: { opacity: 0, y: 12 },
  animate: { opacity: 1, y: 0 },
}

function SettingsSection({
  title,
  description,
  accent,
  children,
}: {
  title: string
  description?: string
  accent: keyof typeof accentDotClass
  children: ReactNode
}) {
  return (
    <section className="nothing-card flex flex-col gap-4 p-5">
      <div>
        <div className="mb-1 flex items-center gap-2">
          <span className={`nothing-section-dot ${accentDotClass[accent]}`} aria-hidden />
          <p className="nothing-label">{title}</p>
        </div>
        {description && <p className="font-mono text-[11px] leading-relaxed text-text-tertiary">{description}</p>}
      </div>
      {children}
    </section>
  )
}

function Field({
  label,
  hint,
  accent,
  children,
}: {
  label: string
  hint?: string
  accent: keyof typeof accentDotClass
  children: ReactNode
}) {
  return (
    <label className="block">
      <span className="mb-1.5 flex items-center gap-2">
        <span className={`nothing-section-dot ${accentDotClass[accent]}`} aria-hidden />
        <span className="nothing-label">{label}</span>
      </span>
      {children}
      {hint && <p className="mt-1.5 font-mono text-[10px] leading-relaxed text-text-tertiary">{hint}</p>}
    </label>
  )
}

function FolderField({
  label,
  value,
  onChange,
  onBrowse,
  accent,
  hint,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  onBrowse: () => void
  accent: keyof typeof accentDotClass
  hint?: string
}) {
  return (
    <Field label={label} hint={hint} accent={accent}>
      <div className="flex gap-2">
        <input className="nothing-input font-mono text-xs" value={value} onChange={(e) => onChange(e.target.value)} />
        <motion.button
          type="button"
          className="nothing-btn nothing-btn-info shrink-0"
          onClick={onBrowse}
          whileHover={{ scale: 1.04 }}
          whileTap={{ scale: 0.96 }}
          aria-label={`Browse ${label.toLowerCase()}`}
        >
          Browse
        </motion.button>
      </div>
    </Field>
  )
}

export function SettingsPanel({ settings, onSave, onPickFolder }: SettingsPanelProps) {
  const [watchFolder, setWatchFolder] = useState(settings.watchFolder)
  const [defaultSortRoot, setDefaultSortRoot] = useState(settings.defaultSortRoot)
  const [geminiApiKey, setGeminiApiKey] = useState('')
  const [amsiProtectionEnabled, setAmsiProtectionEnabled] = useState(settings.amsiProtectionEnabled)
  const [autoStartWithWindows, setAutoStartWithWindows] = useState(settings.autoStartWithWindows)
  const [userTaxonomy, setUserTaxonomy] = useState(settings.userTaxonomy)
  const [waitTimeMinutes, setWaitTimeMinutes] = useState(settings.waitTimeMinutes ?? 0)
  const [smartSubfoldersEnabled, setSmartSubfoldersEnabled] = useState(settings.smartSubfoldersEnabled ?? false)
  const [showMoveNotificationsEnabled, setShowMoveNotificationsEnabled] = useState(
    settings.showMoveNotificationsEnabled ?? true,
  )
  const [savedFlash, setSavedFlash] = useState(false)

  useEffect(() => {
    setWatchFolder(settings.watchFolder)
    setDefaultSortRoot(settings.defaultSortRoot)
    setAmsiProtectionEnabled(settings.amsiProtectionEnabled)
    setAutoStartWithWindows(settings.autoStartWithWindows)
    setUserTaxonomy(settings.userTaxonomy)
    setWaitTimeMinutes(settings.waitTimeMinutes ?? 0)
    setSmartSubfoldersEnabled(settings.smartSubfoldersEnabled ?? false)
    setShowMoveNotificationsEnabled(settings.showMoveNotificationsEnabled ?? true)
  }, [settings])

  const isDirty = useMemo(
    () =>
      watchFolder !== settings.watchFolder ||
      defaultSortRoot !== settings.defaultSortRoot ||
      amsiProtectionEnabled !== settings.amsiProtectionEnabled ||
      autoStartWithWindows !== settings.autoStartWithWindows ||
      userTaxonomy !== settings.userTaxonomy ||
      waitTimeMinutes !== (settings.waitTimeMinutes ?? 0) ||
      smartSubfoldersEnabled !== (settings.smartSubfoldersEnabled ?? false) ||
      showMoveNotificationsEnabled !== (settings.showMoveNotificationsEnabled ?? true) ||
      geminiApiKey.trim().length > 0,
    [
      watchFolder,
      defaultSortRoot,
      amsiProtectionEnabled,
      autoStartWithWindows,
      userTaxonomy,
      waitTimeMinutes,
      smartSubfoldersEnabled,
      showMoveNotificationsEnabled,
      geminiApiKey,
      settings,
    ],
  )

  const save = () => {
    const payload: Record<string, unknown> = {
      watchFolder,
      defaultSortRoot,
      amsiProtectionEnabled,
      autoStartWithWindows,
      userTaxonomy,
      waitTimeMinutes: Math.max(0, Math.min(1440, waitTimeMinutes)),
      smartSubfoldersEnabled,
      showMoveNotificationsEnabled,
    }
    if (geminiApiKey.trim()) {
      payload.geminiApiKey = geminiApiKey.trim()
    }
    onSave(payload)
    setGeminiApiKey('')
    setSavedFlash(true)
    window.setTimeout(() => setSavedFlash(false), 1600)
  }

  const toggles = [
    {
      label: 'Move notifications',
      value: showMoveNotificationsEnabled,
      set: setShowMoveNotificationsEnabled,
      hint: 'Screen-edge popup when a file is sorted.',
      tone: 'info' as const,
      icon: Bell,
    },
    {
      label: 'Smart subfolders',
      value: smartSubfoldersEnabled,
      set: setSmartSubfoldersEnabled,
      hint: 'Movies/Shrek/Shrek.mp4 style paths.',
      tone: 'gemini' as const,
      icon: Layers,
    },
    {
      label: 'AMSI protection',
      value: amsiProtectionEnabled,
      set: setAmsiProtectionEnabled,
      hint: 'Scan executables before sorting.',
      tone: 'warning' as const,
      icon: Shield,
    },
    {
      label: 'Auto-start',
      value: autoStartWithWindows,
      set: setAutoStartWithWindows,
      hint: 'Launch DASA at Windows login.',
      tone: 'success' as const,
      icon: Power,
    },
  ]

  const statusPills = [
    {
      label: settings.hasGeminiApiKey ? 'API key saved' : 'No API key',
      className: settings.hasGeminiApiKey ? 'nothing-tag-gemini' : 'nothing-tag',
    },
    {
      label: amsiProtectionEnabled ? 'AMSI on' : 'AMSI off',
      className: amsiProtectionEnabled ? 'nothing-tag-amsi' : 'nothing-tag',
    },
    {
      label: showMoveNotificationsEnabled ? 'Toasts on' : 'Toasts off',
      className: showMoveNotificationsEnabled ? 'nothing-tag-info' : 'nothing-tag',
    },
  ]

  return (
    <motion.div
      className="mx-auto flex h-full min-h-0 w-full max-w-4xl flex-col"
      variants={pageVariants}
    >
      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="flex flex-col gap-5 pb-5">
      <motion.section variants={fadeUp} className="nothing-card nothing-card-accent-info p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="mb-2 flex items-center gap-2">
              <span className="flex h-8 w-8 items-center justify-center rounded-md border border-info/30 bg-info/10 text-info">
                <Sparkles size={15} strokeWidth={1.5} />
              </span>
              <div>
                <h2 className="text-sm font-medium text-text">Settings</h2>
                <p className="font-mono text-[10px] text-text-tertiary">Configure sorting, security, and folders</p>
              </div>
            </div>
            <div className="flex items-center gap-2 font-mono text-[10px] text-text-tertiary">
              <Lock size={11} strokeWidth={1.5} />
              API keys encrypted via Windows DPAPI
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {statusPills.map((pill) => (
              <span key={pill.label} className={`nothing-tag ${pill.className}`}>
                {pill.label}
              </span>
            ))}
          </div>
        </div>
      </motion.section>

      <div className="grid gap-5 lg:grid-cols-2">
        <motion.div className="flex flex-col gap-5" variants={fadeUp}>
          <SettingsSection
            title="AI sorting"
            description="Gemini powers smart categorization and rule discovery."
            accent="gemini"
          >
            <Field label="Gemini API key" accent="gemini" hint="Required for AI sorting and rule discovery. Stored encrypted locally.">
              <div className="relative">
                <KeyRound size={13} strokeWidth={1.5} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-gemini/70" />
                <input
                  className="nothing-input pl-9 font-mono text-xs"
                  type="password"
                  autoComplete="off"
                  placeholder={settings.hasGeminiApiKey ? '•••••••• saved — paste to replace' : 'Paste API key'}
                  value={geminiApiKey}
                  onChange={(e) => setGeminiApiKey(e.target.value)}
                />
              </div>
            </Field>

            <Field label="Taxonomy" accent="gemini" hint="Comma-separated folder hints sent to Gemini when categorizing files.">
              <div className="relative">
                <Tags size={13} strokeWidth={1.5} className="pointer-events-none absolute left-3 top-3 text-gemini/70" />
                <textarea
                  className="nothing-input min-h-24 resize-y pl-9 font-mono text-xs"
                  value={userTaxonomy}
                  onChange={(e) => setUserTaxonomy(e.target.value)}
                />
              </div>
            </Field>
          </SettingsSection>

          <SettingsSection title="Folders" description="Where DASA watches and where sorted files land." accent="success">
            <FolderField
              label="Watch folder"
              value={watchFolder}
              onChange={setWatchFolder}
              onBrowse={() => onPickFolder('watch')}
              accent="success"
              hint="Downloads and new files are monitored here."
            />
            <FolderField
              label="Sort root"
              value={defaultSortRoot}
              onChange={setDefaultSortRoot}
              onBrowse={() => onPickFolder('sort')}
              accent="info"
              hint="Top-level destination for organized files."
            />
            <Field label="Quarantine" accent="accent" hint="AMSI-flagged files are moved here automatically.">
              <div className="nothing-input flex items-center gap-2 font-mono text-xs text-text-tertiary">
                <Shield size={13} strokeWidth={1.5} className="shrink-0 text-accent/80" />
                <span className="truncate">{settings.quarantineFolder || '—'}</span>
              </div>
            </Field>
          </SettingsSection>
        </motion.div>

        <motion.div className="flex flex-col gap-5" variants={fadeUp}>
          <SettingsSection title="Timing" description="Control when files are moved after download." accent="rule">
            <Field label="Wait time" accent="rule" hint="Delay before moving newly downloaded files. Manual scans are not delayed.">
              <div className="relative">
                <Clock3 size={13} strokeWidth={1.5} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-rule/80" />
                <select
                  className="nothing-input pl-9 font-mono text-xs"
                  value={waitTimeMinutes}
                  onChange={(e) => setWaitTimeMinutes(Number(e.target.value))}
                >
                  {WAIT_TIME_OPTIONS.map((minutes) => (
                    <option key={minutes} value={minutes}>
                      {minutes === 0 ? 'Immediate (0 min)' : `${minutes} minute${minutes === 1 ? '' : 's'}`}
                    </option>
                  ))}
                </select>
              </div>
            </Field>
          </SettingsSection>

          <SettingsSection title="Features" description="Toggle automation behavior and system integration." accent="info">
            <div className="grid gap-2 sm:grid-cols-2">
              {toggles.map(({ label, value, set, hint, tone, icon: Icon }, index) => (
                <motion.div
                  key={label}
                  className={`flex flex-col justify-between rounded-md border p-3 transition-colors ${
                    value ? toggleActiveClass[tone] : 'border-stroke bg-surface'
                  }`}
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.08 + index * 0.04 }}
                >
                  <div className="mb-3 flex items-start justify-between gap-2">
                    <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-md border ${toggleIconClass[tone]}`}>
                      <Icon size={14} strokeWidth={1.5} />
                    </span>
                    <motion.button
                      type="button"
                      className="nothing-toggle shrink-0"
                      data-on={value ? 'true' : 'false'}
                      data-tone={tone}
                      onClick={() => set((v) => !v)}
                      aria-label={`Toggle ${label}`}
                      whileTap={{ scale: 0.9 }}
                    />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-text-secondary">{label}</p>
                    <p className="mt-1 font-mono text-[10px] leading-relaxed text-text-tertiary">{hint}</p>
                  </div>
                </motion.div>
              ))}
            </div>
          </SettingsSection>

          <motion.section variants={fieldVariants} className="nothing-card border border-dashed border-stroke-strong p-4">
            <div className="flex flex-wrap items-center gap-3 font-mono text-[10px] text-text-tertiary">
              <span className="inline-flex items-center gap-1.5">
                <FolderInput size={12} strokeWidth={1.5} className="text-success" />
                Watch
              </span>
              <span className="inline-flex items-center gap-1.5">
                <FolderOutput size={12} strokeWidth={1.5} className="text-info" />
                Sort
              </span>
              <span className="inline-flex items-center gap-1.5">
                <Shield size={12} strokeWidth={1.5} className="text-accent" />
                Quarantine
              </span>
            </div>
          </motion.section>
        </motion.div>
      </div>
        </div>
      </div>

      <motion.section
        variants={fadeUp}
        className="nothing-card nothing-card-accent-info shrink-0 border-t border-stroke p-4"
      >
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="nothing-label mb-1">Save changes</p>
            <p className="font-mono text-[11px] text-text-tertiary">
              {isDirty ? (
                <span className="text-rule">You have unsaved changes</span>
              ) : (
                <span>All changes saved</span>
              )}
            </p>
          </div>
          <div className="flex items-center gap-3">
            <AnimatePresence>
              {savedFlash && (
                <motion.span
                  initial={{ opacity: 0, x: 8 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: -8 }}
                  className="font-mono text-xs text-success"
                >
                  Saved
                </motion.span>
              )}
            </AnimatePresence>
            <motion.button
              type="button"
              className="nothing-btn nothing-btn-primary"
              onClick={save}
              disabled={!isDirty}
              whileHover={isDirty ? { scale: 1.02, y: -1 } : undefined}
              whileTap={isDirty ? { scale: 0.98 } : undefined}
            >
              <Save size={13} strokeWidth={1.5} />
              Save settings
            </motion.button>
          </div>
        </div>
      </motion.section>
    </motion.div>
  )
}
