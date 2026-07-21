import { motion } from 'framer-motion'
import { Minus, Square, X } from 'lucide-react'
import { pulseDot, slideFromLeft } from '../lib/motion'
import { nativeBridge } from '../services/nativeBridge'

interface TitleBarProps {
  connected: boolean
  isMaximized: boolean
}

export function TitleBar({ connected, isMaximized }: TitleBarProps) {
  const onDrag = () => nativeBridge.windowDrag()

  return (
    <motion.header
      className="relative flex h-8 shrink-0 items-stretch border-b border-stroke bg-surface select-none"
      initial={{ opacity: 0, y: -8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: [0.22, 1, 0.36, 1] }}
    >
      <div
        className="pointer-events-none absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-success/50 via-gemini/40 to-info/50"
        aria-hidden
      />
      <div
        className="flex min-w-0 flex-1 items-center gap-2.5 px-3"
        onMouseDown={(e) => {
          if (e.button === 0) onDrag()
        }}
        onDoubleClick={() => nativeBridge.windowMaximize()}
      >
        <motion.span
          className="h-1.5 w-1.5 shrink-0 rounded-full bg-accent"
          aria-hidden
          variants={connected ? pulseDot : undefined}
          animate={connected ? 'animate' : undefined}
        />
        <motion.span
          className="font-mono text-[11px] font-medium tracking-widest text-text"
          variants={slideFromLeft}
          initial="initial"
          animate="animate"
        >
          D.A.S.A
        </motion.span>
        <motion.span
          className="hidden truncate font-mono text-[10px] uppercase tracking-wider text-text-tertiary sm:inline"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 0.15 }}
        >
          download automation
        </motion.span>
        <motion.span
          className={`ml-auto mr-1 h-1 w-1 rounded-full sm:ml-2 ${connected ? 'bg-success' : 'bg-text-tertiary'}`}
          title={connected ? 'Connected' : 'Preview mode'}
          animate={connected ? { scale: [1, 1.3, 1] } : {}}
          transition={{ duration: 2, repeat: Infinity }}
        />
      </div>

      <div className="flex shrink-0 items-stretch">
        {[
          { label: 'Minimize', icon: <Minus size={14} strokeWidth={1.5} />, action: () => nativeBridge.windowMinimize() },
          {
            label: isMaximized ? 'Restore' : 'Maximize',
            icon: isMaximized ? (
              <span className="relative h-3 w-3">
                <Square size={11} strokeWidth={1.5} className="absolute bottom-0 left-0" />
                <Square size={11} strokeWidth={1.5} className="absolute right-0 top-0 opacity-60" />
              </span>
            ) : (
              <Square size={11} strokeWidth={1.5} />
            ),
            action: () => nativeBridge.windowMaximize(),
          },
          { label: 'Close to tray', icon: <X size={14} strokeWidth={1.5} />, action: () => nativeBridge.windowClose(), close: true },
        ].map(({ label, icon, action, close }) => (
          <motion.button
            key={label}
            type="button"
            className={`win-btn ${close ? 'win-btn-close' : ''}`}
            aria-label={label}
            onClick={action}
            whileHover={{ backgroundColor: close ? '#e81123' : 'rgba(26, 26, 26, 1)' }}
            whileTap={{ scale: 0.94 }}
          >
            {icon}
          </motion.button>
        ))}
      </div>
    </motion.header>
  )
}
