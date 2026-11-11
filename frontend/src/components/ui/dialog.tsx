import { useEffect, useId, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { Button } from './button';
import { cn } from '@/lib/utils';

export interface DialogProps {
  title?: ReactNode;
  children: ReactNode;
  onClose: () => void;
  open?: boolean;
  busy?: boolean;
  closeLabel?: string;
  className?: string;
  headerClassName?: string;
  hideHeader?: boolean;
  variant?: 'modal' | 'drawer';
}

export function Dialog({
  title,
  children,
  onClose,
  open = true,
  busy = false,
  closeLabel = 'Đóng cửa sổ',
  className,
  headerClassName,
  hideHeader = false,
  variant = 'modal',
}: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog || !open) return;
    if (!dialog.open) dialog.showModal();
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      dialog.close();
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  if (!open) return null;

  return createPortal(
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-busy={busy}
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onClose();
      }}
      className={cn(
        'fluent-dialog fixed inset-0 m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-2xl overflow-y-auto rounded-2xl border border-slate-200/80 bg-white p-0 text-slate-800 shadow-[0_24px_64px_-12px_rgba(15,23,42,0.2)] backdrop:bg-slate-950/45 backdrop:backdrop-blur-xs',
        variant === 'drawer' &&
          'fluent-drawer inset-y-0 left-auto right-0 m-0 h-dvh max-h-dvh w-full max-w-none rounded-none border-y-0 border-r-0 backdrop:bg-slate-950/20 sm:max-w-[560px]',
        className,
      )}
    >
      {hideHeader && (
        <span id={titleId} className="sr-only">
          {title}
        </span>
      )}
      {!hideHeader && (
        <div
          className={cn(
            'sticky top-0 z-10 flex items-center justify-between border-b border-slate-100 bg-white/95 px-5 py-3.5 backdrop-blur-md',
            variant === 'drawer' && 'min-h-20',
            headerClassName,
          )}
        >
          <div
            id={titleId}
            className="min-w-0 flex-1 text-sm font-semibold tracking-tight text-slate-900"
          >
            {title}
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={closeLabel}
            disabled={busy}
            onClick={onClose}
            className="ml-3 h-8 w-8 shrink-0 rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X />
          </Button>
        </div>
      )}
      {children}
    </dialog>,
    document.body,
  );
}
