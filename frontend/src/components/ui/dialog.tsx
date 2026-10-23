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
  hideHeader?: boolean;
}

export function Dialog({
  title,
  children,
  onClose,
  open = true,
  busy = false,
  closeLabel = 'Đóng cửa sổ',
  className,
  hideHeader = false,
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
        'fixed inset-0 m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-2xl overflow-y-auto rounded-2xl border border-slate-200 bg-white p-0 text-slate-800 shadow-2xl backdrop:bg-slate-900/50 backdrop:backdrop-blur-sm',
        className,
      )}
    >
      {!hideHeader && (
        <div className="flex items-center justify-between border-b border-slate-100 px-5 py-3">
          <span id={titleId} className="text-xs font-semibold uppercase tracking-wide text-slate-500">
            {title}
          </span>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={closeLabel}
            disabled={busy}
            onClick={onClose}
            className="h-8 w-8 rounded-full text-slate-500 hover:bg-slate-100"
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
