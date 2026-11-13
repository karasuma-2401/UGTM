/**
 * Shared Fluent Design semantic palettes for sliding selection indicators
 * across `StatisticsBar` and `StatusTabs`.
 */
export const SELECTION_TONES = {
  primary: {
    background: 'bg-blue-50/55',
    value: 'text-primary',
    label: 'text-slate-600',
    icon: 'text-primary bg-blue-100/70',
    dot: 'bg-blue-600',
    activeBadge: 'bg-blue-100 text-primary',
    inactiveBadge: 'bg-slate-200/80 text-slate-700',
    activeIndicator:
      '[background:linear-gradient(180deg,#f8fbff_0%,#eff6ff_100%)_padding-box,linear-gradient(135deg,#2563eb_0%,#3b82f6_100%)_border-box]',
    hoverCardBorder: 'hover:border-blue-300/80',
    hover: 'hover:bg-blue-100/55',
    focusRing: 'focus-visible:ring-ring',
  },
  success: {
    background: 'bg-emerald-50/65',
    value: 'text-emerald-700',
    label: 'text-emerald-800/80',
    icon: 'text-emerald-600 bg-emerald-100/70',
    dot: 'bg-emerald-500',
    activeBadge: 'bg-emerald-100 text-emerald-800',
    inactiveBadge: 'bg-emerald-50 text-emerald-700 border border-emerald-200/60',
    activeIndicator:
      '[background:linear-gradient(180deg,#f2fdf7_0%,#ecfdf5_100%)_padding-box,linear-gradient(135deg,#059669_0%,#10b981_100%)_border-box]',
    hoverCardBorder: 'hover:border-emerald-300/80',
    hover: 'hover:bg-emerald-100/55',
    focusRing: 'focus-visible:ring-emerald-500',
  },
  warning: {
    background: 'bg-amber-50/65',
    value: 'text-amber-600',
    label: 'text-amber-800/80',
    icon: 'text-amber-600 bg-amber-100/70',
    dot: 'bg-amber-500',
    activeBadge: 'bg-amber-100 text-amber-800',
    inactiveBadge: 'bg-amber-50 text-amber-700 border border-amber-200/60',
    activeIndicator:
      '[background:linear-gradient(180deg,#fffdf5_0%,#fffbeb_100%)_padding-box,linear-gradient(135deg,#d97706_0%,#f59e0b_100%)_border-box]',
    hoverCardBorder: 'hover:border-amber-300/80',
    hover: 'hover:bg-amber-100/55',
    focusRing: 'focus-visible:ring-amber-500',
  },
  muted: {
    background: 'bg-slate-50/80',
    value: 'text-slate-700',
    label: 'text-slate-500',
    icon: 'text-slate-500 bg-slate-200/60',
    dot: 'bg-slate-400',
    activeBadge: 'bg-slate-200 text-slate-900',
    inactiveBadge: 'bg-slate-100 text-slate-600',
    activeIndicator:
      '[background:linear-gradient(180deg,#f8fafc_0%,#f1f5f9_100%)_padding-box,linear-gradient(135deg,#475569_0%,#64748b_100%)_border-box]',
    hoverCardBorder: 'hover:border-slate-300',
    hover: 'hover:bg-slate-200/55',
    focusRing: 'focus-visible:ring-ring',
  },
  danger: {
    background: 'bg-rose-50/80',
    value: 'text-rose-700',
    label: 'text-rose-700',
    icon: 'text-rose-600 bg-rose-100/70',
    dot: 'bg-rose-500',
    activeBadge: 'bg-rose-100 text-rose-800',
    inactiveBadge: 'bg-rose-50 text-rose-700 border border-rose-200/60',
    activeIndicator:
      '[background:linear-gradient(180deg,#fff8f9_0%,#fff1f2_100%)_padding-box,linear-gradient(135deg,#e11d48_0%,#f43f5e_100%)_border-box]',
    hoverCardBorder: 'hover:border-rose-300/80',
    hover: 'hover:bg-rose-100/55',
    focusRing: 'focus-visible:ring-rose-500',
  },
} as const;

export type SelectionTone = keyof typeof SELECTION_TONES;
