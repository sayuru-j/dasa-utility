export type SortSource = 'amsi' | 'rule' | 'gemini' | 'default' | string
export type TabId = 'dashboard' | 'rules' | 'settings' | 'about'

export const tabColors: Record<TabId, { accent: string; dim: string; label: string; description: string }> = {
  dashboard: { accent: '#4ade80', dim: '#4ade8026', label: 'Dashboard', description: 'Monitor & activity' },
  rules: { accent: '#fbbf24', dim: '#fbbf2426', label: 'Rules', description: 'Sort automation' },
  settings: { accent: '#38bdf8', dim: '#38bdf826', label: 'Settings', description: 'App preferences' },
  about: { accent: '#d71921', dim: '#d7192133', label: 'About', description: 'App & credits' },
}

export const sourceColors: Record<string, { label: string; tagClass: string }> = {
  amsi: { label: 'AMSI', tagClass: 'nothing-tag-amsi' },
  rule: { label: 'Rule', tagClass: 'nothing-tag-rule' },
  gemini: { label: 'Gemini', tagClass: 'nothing-tag-gemini' },
  default: { label: 'Default', tagClass: 'nothing-tag-default' },
}

export function sourceMeta(source: string) {
  return sourceColors[source] ?? sourceColors.default
}

export function confidenceTone(confidence: number): string {
  if (confidence >= 0.85) return 'text-success'
  if (confidence >= 0.6) return 'text-rule'
  return 'text-text-tertiary'
}

export function confidenceBarClass(confidence: number): string {
  if (confidence >= 0.85) return 'bg-success'
  if (confidence >= 0.6) return 'bg-rule'
  return 'bg-text-tertiary'
}
