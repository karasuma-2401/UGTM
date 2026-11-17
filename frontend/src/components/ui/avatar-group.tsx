import { type HTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

export interface AvatarGroupItem {
  id: string;
  name: string;
  userCode?: string | null;
  subtitle?: string | null;
  avatarUrl?: string | null;
  colorTone?: 'blue' | 'indigo' | 'emerald' | 'amber' | 'rose' | 'violet';
}

export interface AvatarGroupProps extends HTMLAttributes<HTMLDivElement> {
  items: AvatarGroupItem[];
  max?: number;
  size?: 'sm' | 'md' | 'lg';
  ariaLabel?: string;
}

const colorVariants: Record<string, string> = {
  blue: 'bg-tone-info-bg text-tone-info-text ring-surface-card border-tone-info-border',
  indigo: 'bg-indigo-100 text-indigo-700 ring-surface-card border-indigo-200/50 dark:bg-indigo-950/80 dark:text-indigo-300 dark:border-indigo-800/60',
  emerald: 'bg-tone-success-bg text-tone-success-text ring-surface-card border-tone-success-border',
  amber: 'bg-tone-warning-bg text-tone-warning-strong ring-surface-card border-tone-warning-border',
  rose: 'bg-tone-danger-bg text-tone-danger-text ring-surface-card border-tone-danger-border',
  violet: 'bg-tone-violet-bg text-tone-violet-text ring-surface-card border-tone-violet-border',
};

const tones: Array<keyof typeof colorVariants> = [
  'blue',
  'indigo',
  'emerald',
  'amber',
  'rose',
  'violet',
];

function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/);
  if (!parts.length || !parts[0]) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[parts.length - 2][0] + parts[parts.length - 1][0]).toUpperCase();
}

function getTone(name: string, index: number): string {
  let hash = index;
  for (let i = 0; i < name.length; i++) {
    hash = (hash << 5) - hash + name.charCodeAt(i);
  }
  const toneKey = tones[Math.abs(hash) % tones.length];
  return colorVariants[toneKey];
}

const sizeClasses = {
  sm: 'size-6 text-[10px] ring-2',
  md: 'size-7 text-xs ring-2',
  lg: 'size-8 text-sm ring-2',
};

export function AvatarGroup({
  items,
  max = 3,
  size = 'md',
  ariaLabel = 'Nhóm thành viên',
  className,
  ...props
}: AvatarGroupProps) {
  if (!items || items.length === 0) return null;

  const visibleItems = items.slice(0, max);
  const remainingCount = Math.max(0, items.length - max);
  const remainingItems = items.slice(max);
  const remainingTitle = remainingItems
    .map((item) => `${item.name}${item.userCode ? ` (${item.userCode})` : ''}`)
    .join('\n');

  return (
    <div
      role="group"
      aria-label={ariaLabel}
      className={cn('inline-flex shrink-0 items-center -space-x-2 p-0.5', className)}
      {...props}
    >
      {visibleItems.map((item, index) => {
        const itemTitle = `${item.name}${item.userCode ? ` (${item.userCode})` : ''}`;
        const toneClass = item.colorTone ? colorVariants[item.colorTone] : getTone(item.name, index);

        return (
          <div
            key={item.id}
            title={itemTitle}
            aria-label={itemTitle}
            className={cn(
              'relative inline-flex shrink-0 items-center justify-center rounded-full font-bold shadow-2xs transition-transform hover:z-10 hover:scale-110 cursor-default select-none ring-surface-card',
              sizeClasses[size],
              toneClass,
            )}
          >
            {item.avatarUrl ? (
              <img
                src={item.avatarUrl}
                alt={item.name}
                className="size-full rounded-full object-cover"
              />
            ) : (
              <span>{getInitials(item.name)}</span>
            )}
          </div>
        );
      })}

      {remainingCount > 0 && (
        <div
          title={`Còn lại:\n${remainingTitle}`}
          aria-label={`Còn ${remainingCount} thành viên khác`}
          className={cn(
            'relative inline-flex shrink-0 items-center justify-center rounded-full bg-surface-muted font-semibold text-ink-body ring-surface-card shadow-2xs transition-transform hover:z-10 hover:scale-110 cursor-default select-none border border-line-input/60',
            sizeClasses[size],
          )}
        >
          +{remainingCount}
        </div>
      )}
    </div>
  );
}
