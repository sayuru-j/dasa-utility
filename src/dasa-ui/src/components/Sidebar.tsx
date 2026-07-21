import type { LucideIcon } from 'lucide-react'
import { AnimatePresence, motion } from 'framer-motion'
import { navItem, navStagger, badgePop, pulseDot } from '../lib/motion'
import { tabColors, type TabId } from '../lib/colors'

interface SidebarProps {
  tab: TabId
  onTabChange: (tab: TabId) => void
  tabs: Array<{ id: TabId; label: string; icon: LucideIcon }>
  historyCount: number
  rulesCount: number
  connected: boolean
}

export function Sidebar({
  tab,
  onTabChange,
  tabs,
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
          <span className="relative flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-md border border-stroke bg-surface">
            <img
              src="/icon.ico"
              alt=""
              className="h-5 w-5 object-contain"
              draggable={false}
            />
            <motion.span
              className={`absolute -bottom-0.5 -right-0.5 h-2 w-2 rounded-full border border-surface-alt ${
                connected ? 'bg-accent' : 'bg-text-tertiary'
              }`}
              aria-hidden
              title={connected ? 'Connected' : 'Preview mode'}
              variants={connected ? pulseDot : undefined}
              animate={connected ? 'animate' : undefined}
            />
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
    </motion.nav>
  )
}
