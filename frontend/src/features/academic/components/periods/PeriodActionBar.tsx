import React from 'react';
import { Save, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface PeriodActionBarProps {
  canEdit: boolean;
  isSaving: boolean;
  isDisabled?: boolean;
  onSave: () => void;
}

export const PeriodActionBar: React.FC<PeriodActionBarProps> = ({
  canEdit,
  isSaving,
  isDisabled = false,
  onSave,
}) => {
  return (
    <div className="sticky bottom-6 z-30 rounded-2xl border border-slate-200/90 bg-white/95 p-4 shadow-[0_12px_40px_-8px_rgba(15,23,42,0.12)] backdrop-blur-md transition-all">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="text-xs text-slate-500">
          Các thay đổi về thời gian và deadline sẽ được áp dụng cho đợt này.
        </div>

        <div className="flex items-center gap-2.5 ml-auto">
          <Button
            type="button"
            disabled={!canEdit || isSaving || isDisabled}
            onClick={onSave}
            className="h-10 px-6 gap-2 rounded-xl bg-primary hover:bg-primary/90 text-white font-semibold text-xs transition-all active:scale-[0.98] shadow-sm disabled:opacity-50"
          >
            {isSaving ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Save className="size-4" />
            )}
            {isSaving ? 'Đang lưu...' : 'Lưu kế hoạch'}
          </Button>
        </div>
      </div>
    </div>
  );
};
