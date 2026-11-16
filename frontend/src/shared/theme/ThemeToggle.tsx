import { useEffect, useId, useRef, useState } from 'react';
import { Check, Monitor, Moon, Sun } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useTheme, type ThemePreference } from './ThemeContext';

const THEME_OPTIONS: ReadonlyArray<{
  value: ThemePreference;
  label: string;
  description: string;
  icon: typeof Sun;
}> = [
  { value: 'light', label: 'Sáng', description: 'Nền sáng mặc định', icon: Sun },
  { value: 'dark', label: 'Tối', description: 'Dịu mắt ban đêm', icon: Moon },
  { value: 'system', label: 'Theo hệ thống', description: 'Tự động theo thiết bị', icon: Monitor },
];

export interface ThemeToggleProps {
  variant?: 'header' | 'auth';
  className?: string;
}

export function ThemeToggle({ variant = 'header', className }: ThemeToggleProps) {
  const { preference, resolvedTheme, setPreference } = useTheme();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuId = useId();

  useEffect(() => {
    if (!open) return;

    function handlePointerDown(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
      }
    }

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  const TriggerIcon = preference === 'system' ? Monitor : resolvedTheme === 'dark' ? Moon : Sun;
  const activeOption = THEME_OPTIONS.find((item) => item.value === preference) ?? THEME_OPTIONS[2];

  return (
    <div ref={containerRef} className={cn('relative shrink-0', className)}>
      <button
        ref={triggerRef}
        type="button"
        onClick={() => setOpen((prev) => !prev)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-label={`Giao diện: ${activeOption.label}. Nhấn để đổi chế độ hiển thị`}
        title={`Giao diện: ${activeOption.label}`}
        className={cn(
          'inline-flex h-9 items-center justify-center gap-1.5 rounded-xl border px-2.5 text-xs font-medium transition-colors cursor-pointer select-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary',
          variant === 'auth'
            ? 'border-white/20 bg-surface-card/10 text-white/90 hover:bg-surface-hover/20 hover:text-white backdrop-blur-sm'
            : 'border-line-input/90 bg-surface-muted/70 text-ink-body shadow-2xs hover:bg-surface-hover hover:border-line-strong hover:text-ink-primary',
        )}
      >
        <TriggerIcon className="size-4 shrink-0" aria-hidden="true" />
        <span className="hidden xl:inline">{activeOption.label}</span>
      </button>

      {open && (
        <div
          id={menuId}
          role="menu"
          aria-label="Chọn chế độ giao diện"
          className="absolute right-0 top-full z-50 mt-1.5 w-52 rounded-xl border border-line-input/90 bg-surface-card p-1.5 text-ink-primary shadow-lg animate-in fade-in-0 zoom-in-95 duration-150 dark:shadow-[0_16px_40px_rgba(0,0,0,0.55)]"
        >
          <div className="px-2.5 py-1.5 text-[11px] font-semibold uppercase tracking-wider text-ink-muted">
            Giao diện hiển thị
          </div>
          {THEME_OPTIONS.map((option) => {
            const Icon = option.icon;
            const selected = preference === option.value;
            return (
              <button
                key={option.value}
                type="button"
                role="menuitemradio"
                aria-checked={selected}
                onClick={() => {
                  setPreference(option.value);
                  setOpen(false);
                }}
                className={cn(
                  'flex w-full items-center justify-between gap-2.5 rounded-lg px-2.5 py-2 text-left text-xs transition-colors cursor-pointer focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary',
                  selected
                    ? 'bg-primary/10 font-semibold text-primary dark:bg-primary/15'
                    : 'font-medium text-ink-secondary hover:bg-surface-hover hover:text-ink-primary',
                )}
              >
                <span className="flex min-w-0 items-center gap-2.5">
                  <Icon
                    aria-hidden="true"
                    className={cn(
                      'size-4 shrink-0',
                      selected ? 'text-primary' : 'text-ink-muted',
                    )}
                  />
                  <span className="min-w-0">
                    <span className="block truncate">{option.label}</span>
                    <span className="block truncate text-[11px] font-normal text-ink-muted">
                      {option.description}
                    </span>
                  </span>
                </span>
                {selected && <Check aria-hidden="true" className="size-4 shrink-0 text-primary" />}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
