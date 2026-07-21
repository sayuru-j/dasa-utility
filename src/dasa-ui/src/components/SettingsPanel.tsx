import { useEffect, useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { FolderOpen, Save } from 'lucide-react'
import { fadeUp, pageVariants, springSnappy } from '../lib/motion'
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

const fieldVariants = {
  initial: { opacity: 0, y: 12 },
  animate: { opacity: 1, y: 0 },
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
      hint: 'Show a screen-edge popup when a file is sorted, with a button to open the new location.',
      tone: 'info' as const,
    },
    {
      label: 'Smart subfolders',
      value: smartSubfoldersEnabled,
      set: setSmartSubfoldersEnabled,
      hint: 'e.g. Shrek.mp4 → Movies/Shrek/Shrek.mp4',
      tone: 'gemini' as const,
    },
    {
      label: 'AMSI protection',
      value: amsiProtectionEnabled,
      set: setAmsiProtectionEnabled,
      tone: 'warning' as const,
    },
    {
      label: 'Auto-start',
      value: autoStartWithWindows,
      set: setAutoStartWithWindows,
      tone: 'success' as const,
    },
  ]

  return (
    <motion.div
      className="mx-auto flex h-full min-h-0 max-w-xl flex-col gap-5 overflow-y-auto"
      variants={pageVariants}
    >
      <motion.section variants={fadeUp} className="nothing-card nothing-card-accent-info p-5">
        <div className="mb-1 flex items-center gap-2">
          <span className="nothing-section-dot bg-info" aria-hidden />
          <p className="nothing-label">Settings</p>
        </div>
        <p className="font-mono text-xs text-text-tertiary">API keys encrypted via Windows DPAPI.</p>
      </motion.section>

      <motion.section
        variants={fadeUp}
        className="nothing-card flex flex-col gap-4 p-5"
        initial="initial"
        animate="animate"
        transition={{ staggerChildren: 0.05, delayChildren: 0.08 }}
      >
        {[
          {
            key: 'api',
            label: 'Gemini API key',
            accent: 'gemini',
            node: (
              <input
                className="nothing-input font-mono text-xs"
                type="password"
                autoComplete="off"
                placeholder={settings.hasGeminiApiKey ? '•••••••• saved' : 'Paste API key'}
                value={geminiApiKey}
                onChange={(e) => setGeminiApiKey(e.target.value)}
              />
            ),
          },
          {
            key: 'watch',
            label: 'Watch folder',
            accent: 'success',
            node: (
              <div className="flex gap-2">
                <input
                  className="nothing-input font-mono text-xs"
                  value={watchFolder}
                  onChange={(e) => setWatchFolder(e.target.value)}
                />
                <motion.button
                  type="button"
                  className="nothing-btn nothing-btn-ghost shrink-0"
                  onClick={() => onPickFolder('watch')}
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                >
                  <FolderOpen size={13} strokeWidth={1.5} />
                </motion.button>
              </div>
            ),
          },
          {
            key: 'sort',
            label: 'Sort root',
            accent: 'info',
            node: (
              <div className="flex gap-2">
                <input
                  className="nothing-input font-mono text-xs"
                  value={defaultSortRoot}
                  onChange={(e) => setDefaultSortRoot(e.target.value)}
                />
                <motion.button
                  type="button"
                  className="nothing-btn nothing-btn-ghost shrink-0"
                  onClick={() => onPickFolder('sort')}
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                >
                  <FolderOpen size={13} strokeWidth={1.5} />
                </motion.button>
              </div>
            ),
          },
          {
            key: 'wait',
            label: 'Wait time',
            accent: 'rule',
            node: (
              <>
                <select
                  className="nothing-input font-mono text-xs"
                  value={waitTimeMinutes}
                  onChange={(e) => setWaitTimeMinutes(Number(e.target.value))}
                >
                  {WAIT_TIME_OPTIONS.map((minutes) => (
                    <option key={minutes} value={minutes}>
                      {minutes === 0 ? 'Immediate (0 min)' : `${minutes} minute${minutes === 1 ? '' : 's'}`}
                    </option>
                  ))}
                </select>
                <p className="mt-1.5 font-mono text-[10px] text-text-tertiary">
                  Delay before moving newly downloaded files. Manual scans are not delayed.
                </p>
              </>
            ),
          },
          {
            key: 'quarantine',
            label: 'Quarantine',
            accent: 'accent',
            node: (
              <div className="nothing-input font-mono text-xs text-text-tertiary">
                {settings.quarantineFolder || '—'}
              </div>
            ),
          },
          {
            key: 'taxonomy',
            label: 'Taxonomy',
            accent: 'gemini',
            node: (
              <textarea
                className="nothing-input min-h-20 resize-y font-mono text-xs"
                value={userTaxonomy}
                onChange={(e) => setUserTaxonomy(e.target.value)}
              />
            ),
          },
        ].map((field, index) => (
          <motion.label
            key={field.key}
            className="block"
            variants={fieldVariants}
            transition={{ ...springSnappy, delay: index * 0.04 }}
          >
            <span className="mb-1.5 flex items-center gap-2">
              <span className={`nothing-section-dot ${accentDotClass[field.accent as keyof typeof accentDotClass]}`} aria-hidden />
              <span className="nothing-label">{field.label}</span>
            </span>
            {field.node}
          </motion.label>
        ))}

        <motion.div className="flex flex-col gap-2" variants={fieldVariants}>
          {toggles.map(({ label, value, set, hint, tone }, index) => (
            <motion.div
              key={label}
              className={`flex items-center justify-between rounded border px-3 py-2.5 transition-colors ${
                value ? toggleActiveClass[tone] : 'border-stroke'
              }`}
              initial={{ opacity: 0, x: -10 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.25 + index * 0.05 }}
              whileHover={{ borderColor: 'rgba(42, 42, 42, 1)' }}
            >
              <div>
                <span className="text-sm text-text-secondary">{label}</span>
                {hint && (
                  <p className="mt-0.5 font-mono text-[10px] text-text-tertiary">{hint}</p>
                )}
              </div>
              <motion.button
                type="button"
                className="nothing-toggle shrink-0"
                data-on={value ? 'true' : 'false'}
                data-tone={tone}
                onClick={() => set((v) => !v)}
                aria-label={`Toggle ${label}`}
                whileTap={{ scale: 0.9 }}
              />
            </motion.div>
          ))}
        </motion.div>

        <motion.div className="flex items-center gap-3 pt-1" variants={fieldVariants}>
          <motion.button
            type="button"
            className="nothing-btn nothing-btn-primary"
            onClick={save}
            whileHover={{ scale: 1.02, y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <Save size={13} strokeWidth={1.5} />
            Save
          </motion.button>
          <AnimatePresence>
            {savedFlash && (
              <motion.span
                initial={{ opacity: 0, x: -8, scale: 0.9 }}
                animate={{ opacity: 1, x: 0, scale: 1 }}
                exit={{ opacity: 0, x: 8 }}
                className="font-mono text-xs text-success"
              >
                Saved
              </motion.span>
            )}
          </AnimatePresence>
        </motion.div>
      </motion.section>
    </motion.div>
  )
}
