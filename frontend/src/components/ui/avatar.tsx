import { cn } from '@/lib/utils';

export interface AvatarProps {
  name: string;
  muted?: boolean;
  className?: string;
}

export function Avatar({ name, muted = false, className }: AvatarProps) {
  const initials = name
    .trim()
    .split(/\s+/)
    .slice(-2)
    .map((part) => part.charAt(0))
    .join('')
    .toUpperCase();
  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex size-10 shrink-0 items-center justify-center rounded-xl text-xs font-semibold ring-1 ring-inset',
        muted
          ? 'bg-slate-100 text-slate-400 ring-slate-200'
          : 'bg-gradient-to-br from-blue-50 via-blue-100/80 to-blue-200/70 text-[var(--color-primary-hover,#1d4ed8)] ring-blue-200/80 shadow-[inset_0_1px_0_rgba(255,255,255,0.85)]',
        className,
      )}
    >
      {initials || '?'}
    </span>
  );
}
