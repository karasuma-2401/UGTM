import { cn } from '@/lib/utils';

export interface AvatarProps {
  name: string;
  muted?: boolean;
  className?: string;
}

const TONE_CLASSES = [
  'bg-tone-info-bg text-tone-info-text border-tone-info-border',
  'bg-indigo-100 text-indigo-700 border-indigo-200/60 dark:bg-indigo-950/70 dark:text-indigo-300 dark:border-indigo-800/60',
  'bg-tone-success-bg text-tone-success-text border-tone-success-border',
  'bg-tone-warning-bg text-tone-warning-strong border-tone-warning-border',
  'bg-tone-danger-bg text-tone-danger-text border-tone-danger-border',
  'bg-tone-violet-bg text-tone-violet-text border-tone-violet-border',
] as const;

function getToneClass(name: string) {
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = (hash * 31 + name.charCodeAt(i)) | 0;
  }
  return TONE_CLASSES[Math.abs(hash) % TONE_CLASSES.length];
}

export function Avatar({ name, muted = false, className }: AvatarProps) {
  const normalized = name.trim();
  const initials = normalized
    .split(/\s+/)
    .slice(-2)
    .map((part) => part.charAt(0))
    .join('')
    .toUpperCase();
  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex size-10 shrink-0 items-center justify-center rounded-full border text-xs font-semibold ring-2 ring-surface-card',
        muted ? 'border-line-input bg-surface-muted text-ink-subtle' : getToneClass(normalized),
        className,
      )}
    >
      {initials || '?'}
    </span>
  );
}
