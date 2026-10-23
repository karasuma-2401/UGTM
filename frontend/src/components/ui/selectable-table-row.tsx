import type { ComponentPropsWithoutRef } from 'react';
import { cn } from '@/lib/utils';

interface SelectableTableRowProps extends Omit<ComponentPropsWithoutRef<'tr'>, 'onSelect'> {
  selected?: boolean;
  onSelect?: () => void;
  disabled?: boolean;
}

export function SelectableTableRow({
  selected = false,
  onSelect,
  disabled = false,
  className,
  onClick,
  onKeyDown,
  ...props
}: SelectableTableRowProps) {
  const interactive = Boolean(onSelect) && !disabled;
  return (
    <tr
      {...props}
      aria-selected={selected}
      aria-disabled={disabled || undefined}
      tabIndex={interactive ? 0 : undefined}
      data-state={selected ? 'selected' : 'unselected'}
      className={cn(
        'group transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary',
        interactive && 'cursor-pointer',
        selected
          ? "bg-blue-50/70 hover:bg-blue-50/70 [&>td:first-child]:relative [&>td:first-child]:before:pointer-events-none [&>td:first-child]:before:absolute [&>td:first-child]:before:inset-y-1.5 [&>td:first-child]:before:left-0 [&>td:first-child]:before:w-1 [&>td:first-child]:before:rounded-r [&>td:first-child]:before:bg-primary [&>td:first-child]:before:content-['']"
          : 'hover:bg-blue-50/40',
        className,
      )}
      onClick={(event) => {
        if (disabled) return;
        onSelect?.();
        onClick?.(event);
      }}
      onKeyDown={(event) => {
        // Nested buttons and checkboxes keep their own keyboard behavior.
        if (
          interactive &&
          event.target === event.currentTarget &&
          (event.key === 'Enter' || event.key === ' ')
        ) {
          event.preventDefault();
          onSelect?.();
        }
        onKeyDown?.(event);
      }}
    />
  );
}
