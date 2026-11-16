import { useEffect, useId, useRef, type CSSProperties, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { Button } from './button';
import { cn } from '@/lib/utils';
import { registerNotificationDialog } from '@/shared/notifications/dialog-host';

export interface DialogProps {
  title?: ReactNode;
  children: ReactNode;
  onClose: () => void;
  open?: boolean;
  busy?: boolean;
  closeOnBackdropClick?: boolean;
  closeLabel?: string;
  className?: string;
  headerClassName?: string;
  hideHeader?: boolean;
  variant?: 'modal' | 'drawer';
  style?: CSSProperties;
}

function isOutsideElementRect(
  element: HTMLElement,
  clientX: number,
  clientY: number,
): boolean {
  const rect = element.getBoundingClientRect();
  return (
    clientX < rect.left ||
    clientX > rect.right ||
    clientY < rect.top ||
    clientY > rect.bottom
  );
}

export function Dialog({
  title,
  children,
  onClose,
  open = true,
  busy = false,
  closeOnBackdropClick = false,
  closeLabel = 'Đóng cửa sổ',
  className,
  headerClassName,
  hideHeader = false,
  variant = 'modal',
  style,
}: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const backdropMouseDownRef = useRef(false);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog || !open) return;
    if (!dialog.open) dialog.showModal();
    const unregisterNotifications = registerNotificationDialog(dialog);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      unregisterNotifications();
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
      style={style}
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onClose();
      }}
      onMouseDown={(event) => {
        backdropMouseDownRef.current =
          event.target === event.currentTarget &&
          isOutsideElementRect(event.currentTarget, event.clientX, event.clientY);
      }}
      onClick={(event) => {
        if (
          closeOnBackdropClick &&
          !busy &&
          backdropMouseDownRef.current &&
          event.target === event.currentTarget &&
          isOutsideElementRect(event.currentTarget, event.clientX, event.clientY)
        ) {
          onClose();
        }
        backdropMouseDownRef.current = false;
      }}
      className={cn(
        'fluent-dialog fixed inset-0 m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-2xl overflow-y-auto rounded-2xl border border-line-input/80 bg-surface-card p-0 text-ink-heading shadow-[0_24px_64px_-12px_rgba(15,23,42,0.2)] backdrop:bg-slate-950/45 backdrop:backdrop-blur-xs dark:shadow-[0_24px_64px_-12px_rgba(0,0,0,0.65)]',
        variant === 'drawer' &&
          'fluent-drawer inset-y-0 left-auto right-0 m-0 h-dvh max-h-dvh w-full max-w-none rounded-none border-y-0 border-r-0 backdrop:bg-slate-950/20 dark:backdrop:bg-black/50 sm:max-w-[560px]',
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
            'sticky top-0 z-10 flex items-center justify-between border-b border-line-subtle bg-surface-card/95 px-5 py-3.5 backdrop-blur-md',
            variant === 'drawer' && 'min-h-20',
            headerClassName,
          )}
        >
          <div
            id={titleId}
            className="min-w-0 flex-1 text-sm font-semibold tracking-tight text-ink-primary"
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
            className="ml-3 h-8 w-8 shrink-0 rounded-lg text-ink-subtle hover:bg-surface-muted hover:text-ink-secondary"
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
