import React from 'react';
import { Dialog } from './dialog';
import { Button } from './button';
import { cn } from '@/lib/utils';

export type ConfirmDialogType = 'brand' | 'warning' | 'error' | 'success' | 'info';

export interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  type?: ConfirmDialogType;
  title?: React.ReactNode;
  description?: React.ReactNode;
  confirmText?: string;
  cancelText?: string;
  isConfirming?: boolean;
  className?: string;
  confirmButtonClassName?: string;
}

interface ThemeItem {
  confirmBtn: string;
}

const THEME_CONFIG: Record<ConfirmDialogType, ThemeItem> = {
  brand: {
    confirmBtn:
      'bg-brand-solid hover:bg-brand-solid-hover text-white shadow-xs focus-visible:ring-primary/30',
  },
  warning: {
    confirmBtn:
      'bg-amber-700 hover:bg-amber-800 text-white shadow-xs focus-visible:ring-amber-700/30',
  },
  error: {
    confirmBtn: 'bg-red-600 hover:bg-red-700 text-white shadow-xs focus-visible:ring-red-600/30',
  },
  success: {
    confirmBtn:
      'bg-emerald-700 hover:bg-emerald-800 text-white shadow-xs focus-visible:ring-emerald-600/30',
  },
  info: {
    confirmBtn: 'bg-brand-solid text-white shadow-xs focus-visible:ring-primary/30 hover:bg-brand-solid-hover',
  },
};

export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  open,
  onClose,
  onConfirm,
  type = 'brand',
  title = 'Cảnh báo!',
  description,
  confirmText = 'Đồng ý',
  cancelText = 'Hủy',
  isConfirming = false,
  className,
  confirmButtonClassName,
}) => {
  const theme = THEME_CONFIG[type];

  return (
    <Dialog
      title={title}
      open={open}
      onClose={onClose}
      busy={isConfirming}
      hideHeader
      className={cn(
        'w-[calc(100%-2rem)] max-w-[420px] overflow-hidden rounded-2xl border border-line-input/90 bg-surface-card p-0 shadow-[0_24px_60px_-15px_rgba(15,23,42,0.18)] dark:shadow-[0_24px_60px_-15px_rgba(0,0,0,0.65)]',
        className,
      )}
    >
      <div className="flex flex-col">
        {/* Header with centered title and clean divider */}
        <div className="border-b border-line-strong px-6 py-4 text-center">
          <h3 className="text-base font-semibold text-ink-heading leading-snug">{title}</h3>
        </div>

        {/* Content area: left-aligned text and right-aligned buttons */}
        <div className="px-6 pt-5 pb-6">
          {description && (
            <div className="text-sm leading-relaxed text-ink-body [overflow-wrap:anywhere]">
              {description}
            </div>
          )}

          <div className="mt-6 flex flex-col-reverse justify-end gap-2.5 sm:flex-row sm:items-center">
            <Button
              type="button"
              variant="outline"
              disabled={isConfirming}
              onClick={onClose}
              className="h-9 w-full rounded-lg border-line-input/90 px-4 text-sm font-medium text-ink-secondary hover:bg-surface-hover sm:w-auto"
            >
              {cancelText}
            </Button>
            <Button
              type="button"
              disabled={isConfirming}
              isLoading={isConfirming}
              loadingText="Đang xử lý..."
              onClick={onConfirm}
              className={cn(
                'h-9 w-full rounded-lg px-4 text-sm font-medium sm:w-auto',
                theme.confirmBtn,
                confirmButtonClassName,
              )}
            >
              {confirmText}
            </Button>
          </div>
        </div>
      </div>
    </Dialog>
  );
};
