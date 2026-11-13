import { cn } from '@/lib/utils';

export interface AvatarProps {
  name: string;
  muted?: boolean;
  className?: string;
}

const TONE_CLASSES = [
  'bg-blue-100 text-blue-700 border-blue-200/60',
  'bg-indigo-100 text-indigo-700 border-indigo-200/60',
  'bg-emerald-100 text-emerald-700 border-emerald-200/60',
  'bg-amber-100 text-amber-800 border-amber-200/60',
  'bg-rose-100 text-rose-700 border-rose-200/60',
  'bg-purple-100 text-purple-700 border-purple-200/60',
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
        'flex size-10 shrink-0 items-center justify-center rounded-full border text-xs font-semibold ring-2 ring-white',
        muted ? 'border-slate-200 bg-slate-100 text-slate-400' : getToneClass(normalized),
        className,
      )}
    >
      {initials || '?'}
    </span>
  );
}
