import type { Transition, Variants } from 'framer-motion'

export const springSnappy: Transition = {
  type: 'spring',
  stiffness: 460,
  damping: 32,
  mass: 0.82,
}

export const springSoft: Transition = {
  type: 'spring',
  stiffness: 300,
  damping: 28,
  mass: 0.9,
}

export const springBouncy: Transition = {
  type: 'spring',
  stiffness: 520,
  damping: 24,
  mass: 0.75,
}

export const easeSmooth: Transition = {
  duration: 0.48,
  ease: [0.16, 1, 0.3, 1],
}

export const easeOutSharp: Transition = {
  duration: 0.26,
  ease: [0.4, 0, 0.2, 1],
}

export const pageVariants: Variants = {
  initial: { opacity: 0, y: 22, scale: 0.985, filter: 'blur(10px)' },
  animate: {
    opacity: 1,
    y: 0,
    scale: 1,
    filter: 'blur(0px)',
    transition: {
      duration: 0.52,
      ease: [0.16, 1, 0.3, 1],
      staggerChildren: 0.065,
      delayChildren: 0.04,
    },
  },
  exit: {
    opacity: 0,
    y: -14,
    scale: 0.992,
    filter: 'blur(8px)',
    transition: { duration: 0.28, ease: [0.4, 0, 0.2, 1] },
  },
}

export const fadeUp: Variants = {
  initial: { opacity: 0, y: 20 },
  animate: { opacity: 1, y: 0, transition: springSnappy },
  exit: { opacity: 0, y: -12, transition: easeOutSharp },
}

export const fadeIn: Variants = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: easeSmooth },
  exit: { opacity: 0, transition: { duration: 0.15 } },
}

export const scaleIn: Variants = {
  initial: { opacity: 0, scale: 0.94, y: 10 },
  animate: { opacity: 1, scale: 1, y: 0, transition: springBouncy },
  exit: { opacity: 0, scale: 0.97, y: -6, transition: easeOutSharp },
}

export const slideFromLeft: Variants = {
  initial: { opacity: 0, x: -16 },
  animate: { opacity: 1, x: 0, transition: springSnappy },
}

export const confirmSwap: Variants = {
  initial: { opacity: 0, scale: 0.9, x: 14, filter: 'blur(4px)' },
  animate: {
    opacity: 1,
    scale: 1,
    x: 0,
    filter: 'blur(0px)',
    transition: springBouncy,
  },
  exit: {
    opacity: 0,
    scale: 0.94,
    x: -12,
    filter: 'blur(3px)',
    transition: { duration: 0.16 },
  },
}

export const badgePop: Variants = {
  initial: { scale: 0.55, opacity: 0 },
  animate: {
    scale: 1,
    opacity: 1,
    transition: { type: 'spring', stiffness: 540, damping: 22, mass: 0.7 },
  },
  exit: { scale: 0.75, opacity: 0, transition: { duration: 0.12 } },
}

export const listItem: Variants = {
  initial: { opacity: 0, x: -10, height: 0 },
  animate: {
    opacity: 1,
    x: 0,
    height: 'auto',
    transition: springSoft,
  },
  exit: {
    opacity: 0,
    x: 10,
    height: 0,
    transition: easeOutSharp,
  },
}

export const activityItem: Variants = {
  initial: { opacity: 0, y: -14, scale: 0.97, filter: 'blur(2px)' },
  animate: {
    opacity: 1,
    y: 0,
    scale: 1,
    filter: 'blur(0px)',
    transition: springSnappy,
  },
  exit: { opacity: 0, height: 0, scale: 0.98, transition: easeOutSharp },
}

export const expandContent: Variants = {
  initial: { opacity: 0, height: 0 },
  animate: {
    opacity: 1,
    height: 'auto',
    transition: { ...springSoft, opacity: { delay: 0.06, duration: 0.2 } },
  },
  exit: {
    opacity: 0,
    height: 0,
    transition: { duration: 0.24, ease: [0.4, 0, 1, 1] },
  },
}

export const pulseDot: Variants = {
  animate: {
    scale: [1, 1.4, 1],
    opacity: [1, 0.65, 1],
    transition: { duration: 2.2, repeat: Infinity, ease: 'easeInOut' },
  },
}

export const navStagger: Variants = {
  initial: {},
  animate: { transition: { staggerChildren: 0.07, delayChildren: 0.1 } },
}

export const navItem: Variants = {
  initial: { opacity: 0, x: -18 },
  animate: { opacity: 1, x: 0, transition: springSnappy },
}

export const fieldVariants: Variants = {
  initial: { opacity: 0, y: 14 },
  animate: { opacity: 1, y: 0, transition: springSoft },
}

export const listStagger: Variants = {
  animate: { transition: { staggerChildren: 0.045, delayChildren: 0.03 } },
}

export const shimmer: Variants = {
  animate: {
    opacity: [0.4, 1, 0.4],
    transition: { duration: 1.4, repeat: Infinity, ease: 'easeInOut' },
  },
}
