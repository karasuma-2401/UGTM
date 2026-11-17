/**
 * Shared Fluent Design semantic palettes for sliding selection indicators
 * across `StatisticsBar` and `StatusTabs`.
 */
export const SELECTION_TONES = {
  primary: {
    background: 'bg-surface-card/80 border-line/60',
    value: 'text-brand-ink',
    label: 'text-ink-body',
    icon: 'text-brand-ink bg-blue-500/12',
    dot: 'bg-blue-600 dark:bg-blue-400',
    activeBadge: 'bg-blue-500/15 text-brand-ink',
    inactiveBadge: 'bg-surface-muted/80 text-ink-muted border border-line/70',
    activeIndicator:
      '[background:linear-gradient(180deg,#ffffff_0%,#f5f9ff_100%)_padding-box,linear-gradient(135deg,#2563eb_0%,#3b82f6_100%)_border-box] dark:[background:linear-gradient(180deg,rgba(37,99,235,0.16)_0%,rgba(15,23,42,0.94)_100%)_padding-box,linear-gradient(135deg,rgba(59,130,246,0.65)_0%,rgba(96,165,250,0.55)_100%)_border-box]',
    hoverCardBorder: 'hover:border-tone-info-border',
    hover: 'hover:bg-blue-500/[0.07] hover:border-line-strong/80',
    focusRing: 'focus-visible:ring-ring',
  },
  success: {
    background: 'bg-surface-card/80 border-line/60',
    value: 'text-tone-success-icon',
    label: 'text-ink-body',
    icon: 'text-tone-success-icon bg-emerald-500/12',
    dot: 'bg-emerald-500 dark:bg-emerald-400',
    activeBadge: 'bg-emerald-500/15 text-tone-success-text',
    inactiveBadge: 'bg-surface-muted/80 text-ink-muted border border-line/70',
    activeIndicator:
      '[background:linear-gradient(180deg,#ffffff_0%,#f2fdf7_100%)_padding-box,linear-gradient(135deg,#059669_0%,#10b981_100%)_border-box] dark:[background:linear-gradient(180deg,rgba(16,185,129,0.14)_0%,rgba(15,23,42,0.94)_100%)_padding-box,linear-gradient(135deg,rgba(16,185,129,0.6)_0%,rgba(52,211,153,0.5)_100%)_border-box]',
    hoverCardBorder: 'hover:border-tone-success-border',
    hover: 'hover:bg-emerald-500/[0.07] hover:border-line-strong/80',
    focusRing: 'focus-visible:ring-emerald-500',
  },
  warning: {
    background: 'bg-surface-card/80 border-line/60',
    value: 'text-tone-warning-icon',
    label: 'text-ink-body',
    icon: 'text-tone-warning-icon bg-amber-500/12',
    dot: 'bg-amber-500 dark:bg-amber-400',
    activeBadge: 'bg-amber-500/15 text-tone-warning-text',
    inactiveBadge: 'bg-surface-muted/80 text-ink-muted border border-line/70',
    activeIndicator:
      '[background:linear-gradient(180deg,#ffffff_0%,#fffcf2_100%)_padding-box,linear-gradient(135deg,#d97706_0%,#f59e0b_100%)_border-box] dark:[background:linear-gradient(180deg,rgba(245,158,11,0.14)_0%,rgba(15,23,42,0.94)_100%)_padding-box,linear-gradient(135deg,rgba(245,158,11,0.55)_0%,rgba(251,191,36,0.48)_100%)_border-box]',
    hoverCardBorder: 'hover:border-tone-warning-border',
    hover: 'hover:bg-amber-500/[0.07] hover:border-line-strong/80',
    focusRing: 'focus-visible:ring-amber-500',
  },
  muted: {
    background: 'bg-surface-card/80 border-line/60',
    value: 'text-ink-primary',
    label: 'text-ink-body',
    icon: 'text-ink-muted bg-surface-muted',
    dot: 'bg-slate-400 dark:bg-slate-500',
    activeBadge: 'bg-surface-strong text-ink-primary',
    inactiveBadge: 'bg-surface-muted/80 text-ink-muted border border-line/70',
    activeIndicator:
      '[background:linear-gradient(180deg,#ffffff_0%,#f8fafc_100%)_padding-box,linear-gradient(135deg,#475569_0%,#64748b_100%)_border-box] dark:[background:linear-gradient(180deg,rgba(51,65,85,0.42)_0%,rgba(15,23,42,0.95)_100%)_padding-box,linear-gradient(135deg,rgba(100,116,139,0.65)_0%,rgba(148,163,184,0.5)_100%)_border-box]',
    hoverCardBorder: 'hover:border-line-strong',
    hover: 'hover:bg-surface-hover/80 hover:border-line-strong/80',
    focusRing: 'focus-visible:ring-ring',
  },
  danger: {
    background: 'bg-surface-card/80 border-line/60',
    value: 'text-tone-danger-icon',
    label: 'text-ink-body',
    icon: 'text-tone-danger-icon bg-rose-500/12',
    dot: 'bg-rose-500 dark:bg-rose-400',
    activeBadge: 'bg-rose-500/15 text-tone-danger-text',
    inactiveBadge: 'bg-surface-muted/80 text-ink-muted border border-line/70',
    activeIndicator:
      '[background:linear-gradient(180deg,#ffffff_0%,#fff6f7_100%)_padding-box,linear-gradient(135deg,#e11d48_0%,#f43f5e_100%)_border-box] dark:[background:linear-gradient(180deg,rgba(244,63,94,0.14)_0%,rgba(15,23,42,0.94)_100%)_padding-box,linear-gradient(135deg,rgba(244,63,94,0.58)_0%,rgba(251,113,133,0.48)_100%)_border-box]',
    hoverCardBorder: 'hover:border-tone-danger-border',
    hover: 'hover:bg-rose-500/[0.07] hover:border-line-strong/80',
    focusRing: 'focus-visible:ring-rose-500',
  },
} as const;

export type SelectionTone = keyof typeof SELECTION_TONES;
