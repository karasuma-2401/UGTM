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
    stripe: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921]',
    badgeBg: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921] shadow-[#2054A5]/30',
    titleColor: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921] bg-clip-text text-transparent',
    divider: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921]',
    confirmBtn: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921] hover:opacity-95 shadow-[#2054A5]/25',
    icon: AlertTriangle,
  },
  warning: {
    stripe: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921]',
    badgeBg: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921] shadow-[#2054A5]/30',
    titleColor: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921] bg-clip-text text-transparent',
    divider: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921]',
    confirmBtn: 'bg-gradient-to-r from-[#2054A5] to-[#FA7921] hover:opacity-95 shadow-[#2054A5]/25',
    icon: AlertTriangle,
  },
  error: {
    stripe: 'bg-rose-500',
    badgeBg: 'bg-rose-500 shadow-rose-500/30',
    titleColor: 'text-rose-500',
    divider: 'bg-rose-300',
    confirmBtn: 'bg-rose-500 hover:bg-rose-600 shadow-rose-500/25',
    icon: X,
  },
  success: {
    stripe: 'bg-emerald-500',
    badgeBg: 'bg-emerald-500 shadow-emerald-500/30',
    titleColor: 'text-emerald-600',
    divider: 'bg-emerald-300',
    confirmBtn: 'bg-emerald-500 hover:bg-emerald-600 shadow-emerald-500/25',
    icon: Check,
  },
  info: {
    stripe: 'bg-blue-500',
    badgeBg: 'bg-blue-500 shadow-blue-500/30',
    titleColor: 'text-blue-600',
    divider: 'bg-blue-300',
    confirmBtn: 'bg-blue-500 hover:bg-blue-600 shadow-blue-500/25',
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
      open={open}
      onClose={onClose}
      hideHeader
      className={cn(
        'overflow-visible bg-transparent border-none shadow-none max-w-[420px] w-full p-0 m-auto',
        className,
      )}
    >
      <div className="relative pt-9 px-4 pb-4">
        <div className="relative bg-white rounded-[4px] shadow-2xl pt-9 pb-6 px-6 sm:px-8 text-center">
          <div className={cn('absolute top-0 left-0 right-0 h-1.5 rounded-t-[4px]', theme.stripe)} />

          <div className="absolute -top-7 left-1/2 -translate-x-1/2 z-20">
            <div
              className={cn(
                'size-14 rounded-full flex items-center justify-center text-white shadow-lg ring-4 ring-white',
                theme.badgeBg,
              )}
            >
              <IconComponent className="size-7 stroke-[2.75]" />
            </div>
          </div>

          <h3 className={cn('text-2xl font-bold tracking-tight mt-1', theme.titleColor)}>
            {title}
          </h3>

          <div className={cn('w-9 h-0.5 mx-auto mt-2.5 mb-3.5 rounded-full', theme.divider)} />

          {description && (
            <div className="text-xs sm:text-sm text-slate-500 leading-relaxed max-w-xs mx-auto">
              {description}
            </div>
          )}

          <div className="flex items-center justify-center gap-3 mt-6">
            <button
              type="button"
              onClick={onClose}
              disabled={isConfirming}
              className="rounded-full border border-slate-200 px-5 py-2 text-xs sm:text-sm font-semibold text-slate-400 hover:text-slate-600 hover:bg-slate-50 hover:border-slate-300 transition-colors cursor-pointer"
            >
              {cancelText}
            </button>
            <button
              type="button"
              onClick={onConfirm}
              disabled={isConfirming}
              className={cn(
                'rounded-full px-6 py-2 text-xs sm:text-sm font-semibold text-white shadow-md transition-all active:scale-95 cursor-pointer disabled:pointer-events-none disabled:opacity-50',
                theme.confirmBtn,
              )}
            >
              {isConfirming ? 'Đang xử lý...' : confirmText}
            </button>
          </div>

          <div className={cn('absolute bottom-0 left-0 right-0 h-1.5 rounded-b-[4px]', theme.stripe)} />
        </div>
      </div>
    </Dialog>
  );
};
