import { type ReactNode } from 'react'
import { motion } from 'framer-motion'
import { ExternalLink, Heart, Link2, Shield, Sparkles } from 'lucide-react'
import { fadeUp, pageVariants } from '../lib/motion'

const APP_VERSION = '1.0.0'
const REPO_URL = 'https://github.com/sayuru-j/dasa-utility'
const AUTHOR_URL = 'https://github.com/sayuru-j'

function LinkButton({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noreferrer noopener"
      className="nothing-btn nothing-btn-ghost !justify-start !px-3 !py-2 text-[12px]"
    >
      {children}
      <ExternalLink size={12} strokeWidth={1.5} className="ml-auto opacity-60" />
    </a>
  )
}

export function AboutPanel() {
  return (
    <motion.div
      className="mx-auto flex h-full min-h-0 w-full max-w-2xl flex-col"
      variants={pageVariants}
      initial="initial"
      animate="animate"
    >
      <div className="nothing-scroll nothing-scroll-fade min-h-0 flex-1 overflow-y-auto pr-1">
        <div className="flex flex-col gap-5 pb-6">
          <motion.section
            variants={fadeUp}
            className="nothing-card border-l-[3px] border-l-accent bg-gradient-to-r from-accent/10 to-surface-alt p-5 sm:p-6"
          >
            <div className="flex items-start gap-4">
              <span className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-stroke bg-surface">
                <img src="/icon.ico" alt="" className="h-9 w-9 object-contain" draggable={false} />
              </span>
              <div className="min-w-0">
                <p className="font-mono text-[10px] uppercase tracking-[0.18em] text-text-tertiary">About</p>
                <h1 className="mt-1 text-lg font-medium tracking-tight text-text">D.A.S.A</h1>
                <p className="mt-1 font-mono text-[11px] leading-relaxed text-text-secondary">
                  Download Automation &amp; Security Assistant
                </p>
                <p className="mt-3 font-mono text-[10px] text-text-tertiary">Version {APP_VERSION}</p>
              </div>
            </div>
          </motion.section>

          <motion.section variants={fadeUp} className="nothing-card p-5">
            <div className="mb-3 flex items-center gap-2">
              <span className="nothing-section-dot bg-accent" aria-hidden />
              <p className="nothing-label">What it does</p>
            </div>
            <p className="font-mono text-[12px] leading-relaxed text-text-secondary">
              A Windows system-tray app that watches your Downloads folder, scans risky files with AMSI,
              applies your automation rules, and uses Google Gemini for intelligent sorting — all stored locally
              on your machine.
            </p>
          </motion.section>

          <motion.section variants={fadeUp} className="nothing-card p-5">
            <div className="mb-3 flex items-center gap-2">
              <span className="nothing-section-dot bg-gemini" aria-hidden />
              <p className="nothing-label">Built with</p>
            </div>
            <ul className="flex flex-col gap-2 font-mono text-[11px] text-text-secondary">
              <li className="flex items-center gap-2">
                <Shield size={13} strokeWidth={1.5} className="text-success" />
                .NET 10 WPF + WebView2 host
              </li>
              <li className="flex items-center gap-2">
                <Sparkles size={13} strokeWidth={1.5} className="text-gemini" />
                React, Tailwind, Framer Motion
              </li>
              <li className="flex items-center gap-2">
                <Heart size={13} strokeWidth={1.5} className="text-accent" />
                Open source — MIT license
              </li>
            </ul>
          </motion.section>

          <motion.section variants={fadeUp} className="nothing-card p-5">
            <div className="mb-3 flex items-center gap-2">
              <span className="nothing-section-dot bg-info" aria-hidden />
              <p className="nothing-label">Links</p>
            </div>
            <div className="flex flex-col gap-2">
              <LinkButton href={REPO_URL}>
                <Link2 size={14} strokeWidth={1.5} />
                sayuru-j/dasa-utility
              </LinkButton>
              <LinkButton href={AUTHOR_URL}>
                <Link2 size={14} strokeWidth={1.5} />
                Sayuru .J Silva
              </LinkButton>
            </div>
          </motion.section>

          <motion.p variants={fadeUp} className="px-1 text-center font-mono text-[10px] text-text-tertiary">
            An open-source passion project — use it, fork it, improve it.
          </motion.p>
        </div>
      </div>
    </motion.div>
  )
}
