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
      'bg-[#2054A5] hover:bg-[#1a458a] text-white shadow-xs focus-visible:ring-[#2054A5]/30',
  },
  warning: {
    confirmBtn:
      'bg-amber-600 hover:bg-amber-700 text-white shadow-xs focus-visible:ring-amber-600/30',
  },
  error: {
    confirmBtn: 'bg-red-600 hover:bg-red-700 text-white shadow-xs focus-visible:ring-red-600/30',
  },
  success: {
    confirmBtn:
      'bg-emerald-700 hover:bg-emerald-800 text-white shadow-xs focus-visible:ring-emerald-600/30',
  },
  info: {
    confirmBtn: 'bg-primary hover:bg-primary/92 text-white shadow-xs focus-visible:ring-primary/30',
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
        'w-[calc(100%-2rem)] max-w-[420px] overflow-hidden rounded-2xl border border-slate-200/90 bg-white p-0 shadow-[0_24px_60px_-15px_rgba(15,23,42,0.18)]',
        className,
      )}
    >
      <div className="flex flex-col">
        {/* Header with centered title and clean divider */}
        <div className="border-b border-slate-300 px-6 py-4 text-center">
          <h3 className="text-base font-semibold text-slate-800 leading-snug">{title}</h3>
        </div>

        {/* Content area: left-aligned text and right-aligned buttons */}
        <div className="px-6 pt-5 pb-6">
          {description && (
            <div className="text-sm leading-relaxed text-slate-600 [overflow-wrap:anywhere]">
              {description}
            </div>
          )}

          <div className="mt-6 flex flex-col-reverse justify-end gap-2.5 sm:flex-row sm:items-center">
            <Button
              type="button"
              variant="outline"
              disabled={isConfirming}
              onClick={onClose}
              className="h-9 w-full rounded-lg border-slate-200/90 px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 sm:w-auto"
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
