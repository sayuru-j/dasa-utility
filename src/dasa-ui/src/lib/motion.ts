import type { Transition, Variants } from 'framer-motion'

export const springSnappy: Transition = {
  type: 'spring',
  stiffness: 420,
  damping: 34,
  mass: 0.85,
}

export const springSoft: Transition = {
  type: 'spring',
  stiffness: 280,
  damping: 28,
  mass: 0.9,
}

export const easeSmooth: Transition = {
  duration: 0.45,
  ease: [0.22, 1, 0.36, 1],
}

export const pageVariants: Variants = {
  initial: { opacity: 0, y: 14, filter: 'blur(6px)' },
  animate: {
    opacity: 1,
    y: 0,
    filter: 'blur(0px)',
    transition: { ...easeSmooth, staggerChildren: 0.07, delayChildren: 0.05 },
  },
  exit: {
    opacity: 0,
    y: -10,
    filter: 'blur(4px)',
    transition: { duration: 0.22, ease: [0.4, 0, 1, 1] },
  },
}

export const fadeUp: Variants = {
  initial: { opacity: 0, y: 18 },
  animate: { opacity: 1, y: 0, transition: springSnappy },
  exit: { opacity: 0, y: -10, transition: { duration: 0.2 } },
}

export const fadeIn: Variants = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: easeSmooth },
  exit: { opacity: 0, transition: { duration: 0.15 } },
}

export const scaleIn: Variants = {
  initial: { opacity: 0, scale: 0.96, y: 8 },
  animate: { opacity: 1, scale: 1, y: 0, transition: springSnappy },
  exit: { opacity: 0, scale: 0.98, transition: { duration: 0.18 } },
}

export const slideFromLeft: Variants = {
  initial: { opacity: 0, x: -12 },
  animate: { opacity: 1, x: 0, transition: springSnappy },
}

export const listItem: Variants = {
  initial: { opacity: 0, x: -8, height: 0 },
  animate: {
    opacity: 1,
    x: 0,
    height: 'auto',
    transition: springSoft,
  },
  exit: {
    opacity: 0,
    x: 8,
    height: 0,
    transition: { duration: 0.2 },
  },
}

export const activityItem: Variants = {
  initial: { opacity: 0, y: -12, scale: 0.98 },
  animate: { opacity: 1, y: 0, scale: 1, transition: springSnappy },
  exit: { opacity: 0, height: 0, transition: { duration: 0.2 } },
}

export const expandContent: Variants = {
  initial: { opacity: 0, height: 0 },
  animate: {
    opacity: 1,
    height: 'auto',
    transition: { ...springSoft, opacity: { delay: 0.05 } },
  },
  exit: {
    opacity: 0,
    height: 0,
    transition: { duration: 0.22, ease: [0.4, 0, 1, 1] },
  },
}

export const pulseDot: Variants = {
  animate: {
    scale: [1, 1.35, 1],
    opacity: [1, 0.7, 1],
    transition: { duration: 2, repeat: Infinity, ease: 'easeInOut' },
  },
}

export const navStagger: Variants = {
  animate: { transition: { staggerChildren: 0.06, delayChildren: 0.12 } },
}

export const navItem: Variants = {
  initial: { opacity: 0, x: -16 },
  animate: { opacity: 1, x: 0, transition: springSnappy },
}
