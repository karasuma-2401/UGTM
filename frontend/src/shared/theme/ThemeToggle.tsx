import type { MouseEvent } from 'react';
import { Moon, Sun } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useTheme } from './ThemeContext';

export interface ThemeToggleProps {
  className?: string;
}

export function ThemeToggle({ className }: ThemeToggleProps) {
  const { theme, toggleTheme } = useTheme();
  const isDark = theme === 'dark';
  const Icon = isDark ? Sun : Moon;
  const label = isDark ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối';

  const handleClick = (event: MouseEvent<HTMLButtonElement>) => {
    toggleTheme({ sourceElement: event.currentTarget });
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      aria-label={label}
      title={label}
      className={cn(
        'inline-flex size-9 shrink-0 items-center justify-center rounded-full border border-line-input/90 bg-surface-muted/70 text-ink-body shadow-2xs transition-[background-color,border-color,color,transform] duration-150 hover:bg-surface-muted hover:border-line-strong hover:text-ink-primary active:scale-95 dark:hover:bg-surface-strong/50 motion-reduce:transition-none motion-reduce:active:scale-100 cursor-pointer select-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary',
        className,
      )}
    >
      <Icon className="size-4 stroke-[1.75]" aria-hidden="true" />
    </button>
  );
}
