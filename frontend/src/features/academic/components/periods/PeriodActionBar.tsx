import React from 'react';
import { Save, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface PeriodActionBarProps {
  canEdit: boolean;
  isSaving: boolean;
  onSave: () => void;
  onImportUsers: () => void;
}

export const PeriodActionBar: React.FC<PeriodActionBarProps> = ({
  canEdit,
  isSaving,
  onSave,
  onImportUsers,
}) => {
  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-4 shadow-xs flex flex-wrap items-center gap-3">
      <Button
        type="button"
        disabled={!canEdit || isSaving}
        onClick={onSave}
        className="h-10 px-6 gap-2 bg-[#2054A5] hover:bg-[#1a4485] text-white font-medium text-sm rounded-lg transition-colors shadow-2xs"
      >
        <Save className="size-4" />
        {isSaving ? 'Đang lưu kế hoạch...' : 'Lưu kế hoạch'}
      </Button>

      <Button
        type="button"
        variant="outline"
        onClick={onImportUsers}
        className="h-10 px-5 gap-2 border-slate-200 text-slate-700 hover:bg-slate-50 font-medium text-sm rounded-lg transition-colors"
      >
        <Upload className="size-4" />
        Import SV / GV
      </Button>
    </div>
  );
};