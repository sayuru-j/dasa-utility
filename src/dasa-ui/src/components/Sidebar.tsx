import type { LucideIcon } from 'lucide-react'
import { AnimatePresence, motion } from 'framer-motion'
import { navItem, navStagger, badgePop, pulseDot } from '../lib/motion'
import { tabColors, type TabId } from '../lib/colors'

interface SidebarProps {
  tab: TabId
  onTabChange: (tab: TabId) => void
  tabs: Array<{ id: TabId; label: string; icon: LucideIcon }>
  monitoring: boolean
  historyCount: number
  rulesCount: number
  connected: boolean
}

export function Sidebar({
  tab,
  onTabChange,
  tabs,
  monitoring,
  historyCount,
  rulesCount,
  connected,
}: SidebarProps) {
  return (
    <motion.nav
      className="nothing-sidebar flex w-[13.5rem] shrink-0 flex-col border-r border-stroke bg-surface-alt"
      initial="initial"
      animate="animate"
      variants={navStagger}
    >
      <motion.div className="border-b border-stroke px-4 py-4" variants={navItem}>
        <div className="flex items-center gap-2.5">
          <span className="flex h-7 w-7 items-center justify-center rounded-md border border-accent/30 bg-accent/10">
            <span className="h-2 w-2 rounded-full bg-accent" aria-hidden />
          </span>
          <div className="min-w-0">
            <p className="truncate font-mono text-[11px] font-medium tracking-[0.18em] text-text">D.A.S.A</p>
            <p className="truncate font-mono text-[9px] uppercase tracking-wider text-text-tertiary">
              {connected ? 'Connected' : 'Preview'}
            </p>
          </div>
        </div>
      </motion.div>

      <div className="flex flex-1 flex-col gap-1 p-3">
        <motion.p className="nothing-label mb-1 px-2" variants={navItem}>
          Navigation
        </motion.p>

        {tabs.map(({ id, label, icon: Icon }) => {
          const active = tab === id
          const colors = tabColors[id]

          return (
            <motion.button
              key={id}
              type="button"
              className="nothing-nav-item group relative w-full"
              data-active={active ? 'true' : 'false'}
              data-tab={id}
              onClick={() => onTabChange(id)}
              variants={navItem}
              whileHover={{ x: 2 }}
              whileTap={{ scale: 0.985 }}
            >
              {active && (
                <motion.span
                  layoutId="nav-active-bg"
                  className="absolute inset-0 rounded-md border"
                  transition={{ type: 'spring', stiffness: 380, damping: 32 }}
                  style={{
                    zIndex: 0,
                    backgroundColor: colors.dim,
                    borderColor: `${colors.accent}55`,
                    boxShadow: `inset 3px 0 0 ${colors.accent}`,
                  }}
                />
              )}

              <span
                className="nav-icon-wrap relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-md border border-stroke bg-surface transition-colors group-hover:border-stroke-strong group-hover:bg-surface-elevated"
                style={active ? { borderColor: `${colors.accent}66`, backgroundColor: colors.dim } : undefined}
              >
                <Icon
                  size={15}
                  strokeWidth={1.5}
                  className="nav-icon transition-colors"
                  style={active ? { color: colors.accent } : undefined}
                />
              </span>

              <span className="relative z-10 min-w-0 flex-1 text-left">
                <span className="block truncate text-[13px] font-medium leading-tight">{label}</span>
                <span
                  className="mt-0.5 block truncate font-mono text-[10px] leading-tight text-text-tertiary transition-colors group-hover:text-text-secondary"
                  style={active ? { color: colors.accent } : undefined}
                >
                  {colors.description}
                </span>
              </span>

              {id === 'dashboard' && historyCount > 0 && (
                <AnimatePresence mode="popLayout">
                  <motion.span
                    key={historyCount}
                    variants={badgePop}
                    initial="initial"
                    animate="animate"
                    exit="exit"
                    className="nothing-nav-badge relative z-10"
                    style={{ color: colors.accent, borderColor: `${colors.accent}44`, backgroundColor: colors.dim }}
                  >
                    {historyCount}
                  </motion.span>
                </AnimatePresence>
              )}

              {id === 'rules' && rulesCount > 0 && (
                <AnimatePresence mode="popLayout">
                  <motion.span
                    key={rulesCount}
                    variants={badgePop}
                    initial="initial"
                    animate="animate"
                    exit="exit"
                    className="nothing-nav-badge relative z-10"
                    style={{ color: colors.accent, borderColor: `${colors.accent}44`, backgroundColor: colors.dim }}
                  >
                    {rulesCount}
                  </motion.span>
                </AnimatePresence>
              )}
            </motion.button>
          )
        })}
      </div>

      <motion.div className="mt-auto border-t border-stroke p-3" variants={navItem}>
        <div className="rounded-md border border-stroke bg-surface p-3">
          <div className="mb-2 flex items-center justify-between gap-2">
            <p className="nothing-label">Status</p>
            <motion.span
              className={`h-1.5 w-1.5 rounded-full ${monitoring ? 'bg-success' : 'bg-text-tertiary'}`}
              aria-hidden
              variants={monitoring ? pulseDot : undefined}
              animate={monitoring ? 'animate' : undefined}
            />
          </div>
          <p className={`font-mono text-[11px] font-medium ${monitoring ? 'text-success' : 'text-text-tertiary'}`}>
            {monitoring ? 'Monitoring active' : 'Monitoring paused'}
          </p>
          <p className="mt-1 font-mono text-[10px] text-text-tertiary">
            {historyCount} sorted · {rulesCount} rules
          </p>
        </div>
      </motion.div>
    </motion.nav>
  )
}
