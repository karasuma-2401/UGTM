import React from 'react';
import { Dialog } from './dialog';
import { AlertTriangle, Check, X, AlertCircle } from 'lucide-react';
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
}

const THEME_CONFIG = {
  brand: {
    badgeBg: 'bg-blue-50 text-[#2054A5] ring-8 ring-blue-50/60',
    titleColor: 'text-slate-900',
    confirmBtn: 'bg-[#2054A5] hover:bg-[#1a458a] text-white shadow-sm shadow-[#2054A5]/20',
    icon: AlertTriangle,
  },
  warning: {
    badgeBg: 'bg-amber-50 text-amber-600 ring-8 ring-amber-50/60',
    titleColor: 'text-slate-900',
    confirmBtn: 'bg-amber-600 hover:bg-amber-700 text-white shadow-sm shadow-amber-600/20',
    icon: AlertTriangle,
  },
  error: {
    badgeBg: 'bg-rose-50 text-rose-600 ring-8 ring-rose-50/60',
    titleColor: 'text-slate-900',
    confirmBtn: 'bg-rose-600 hover:bg-rose-700 text-white shadow-sm shadow-rose-600/20',
    icon: X,
  },
  success: {
    badgeBg: 'bg-emerald-50 text-emerald-600 ring-8 ring-emerald-50/60',
    titleColor: 'text-slate-900',
    confirmBtn: 'bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm shadow-emerald-600/20',
    icon: Check,
  },
  info: {
    badgeBg: 'bg-blue-50 text-blue-600 ring-8 ring-blue-50/60',
    titleColor: 'text-slate-900',
    confirmBtn: 'bg-blue-600 hover:bg-blue-700 text-white shadow-sm shadow-blue-600/20',
    icon: AlertCircle,
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
}) => {
  const theme = THEME_CONFIG[type];
  const IconComponent = theme.icon;

  return (
    <Dialog
      title={title}
      open={open}
      onClose={onClose}
      hideHeader
      className={cn(
        'overflow-visible bg-transparent border-none shadow-none max-w-[420px] w-full p-0 m-auto',
        className,
      )}
    >
      <div className="p-2">
        <div className="relative rounded-2xl border border-slate-200/80 bg-white p-6 sm:p-7 text-center shadow-[0_24px_60px_-15px_rgba(15,23,42,0.18)]">
          <div className="mx-auto mb-4 flex justify-center">
            <div
              className={cn(
                'size-12 rounded-2xl flex items-center justify-center transition-transform',
                theme.badgeBg,
              )}
            >
              <IconComponent className="size-6 stroke-[2.25]" />
            </div>
          </div>

          <h3 className={cn('text-lg font-semibold tracking-tight', theme.titleColor)}>
            {title}
          </h3>

          {description && (
            <div className="mt-2 text-sm text-slate-600 leading-relaxed max-w-xs mx-auto">
              {description}
            </div>
          )}

          <div className="mt-6 flex items-center justify-center gap-2.5">
            <button
              type="button"
              onClick={onClose}
              disabled={isConfirming}
              className="flex-1 rounded-xl border border-slate-200/90 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 hover:border-slate-300 transition-all cursor-pointer disabled:pointer-events-none disabled:opacity-50"
            >
              {cancelText}
            </button>
            <button
              type="button"
              onClick={onConfirm}
              disabled={isConfirming}
              className={cn(
                'flex-1 rounded-xl px-5 py-2.5 text-sm font-medium transition-all active:scale-[0.98] cursor-pointer disabled:pointer-events-none disabled:opacity-50',
                theme.confirmBtn,
              )}
            >
              {isConfirming ? 'Đang xử lý...' : confirmText}
            </button>
          </div>
        </div>
      </div>
    </Dialog>
  );
};
