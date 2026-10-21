import React from 'react';
import { Button } from '@/components/ui/button';
import { Save, Send, Plus, Sliders, Percent } from 'lucide-react';

export interface ScoreConfigActionBarProps {
  onSave: () => void;
  isSaving?: boolean;
  onPublish?: () => void;
  isPublishing?: boolean;
  canPublish?: boolean;
  onApplyIntegerPreset: () => void;
  onApplyPercentagePreset: () => void;
  onOpenAddComponentModal: () => void;
  isReadOnly?: boolean;
  saveButtonText?: string;
}

export const ScoreConfigActionBar: React.FC<ScoreConfigActionBarProps> = ({
  onSave,
  isSaving = false,
  onPublish,
  isPublishing = false,
  canPublish = false,
  onApplyIntegerPreset,
  onApplyPercentagePreset,
  onOpenAddComponentModal,
  isReadOnly = false,
  saveButtonText = 'Lưu cấu hình',
}) => {
  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs p-4 sm:p-5 flex flex-wrap items-center justify-between gap-3">
      <div className="flex items-center gap-2.5 flex-wrap">
        <button
          type="button"
          onClick={onSave}
          disabled={isSaving || isReadOnly}
          className="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-semibold transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary shadow-sm px-5 py-2.5 text-white bg-gradient-to-r from-[#2054A5] to-[#FA7921] hover:opacity-95 active:scale-[0.99] disabled:pointer-events-none disabled:opacity-50 cursor-pointer"
        >
          {isSaving ? (
            <div className="size-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
          ) : (
            <Save className="size-4" />
          )}
          <span>{saveButtonText}</span>
        </button>

        {canPublish && onPublish && (
          <Button
            type="button"
            variant="default"
            className="rounded-lg px-4 h-10 bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm"
            onClick={onPublish}
            disabled={isPublishing || isReadOnly}
            leftIcon={
              isPublishing ? (
                <div className="size-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
              ) : (
                <Send className="size-4" />
              )
            }
          >
            <span>Công bố (Publish)</span>
          </Button>
        )}
      </div>

      <div className="flex items-center gap-2 flex-wrap">
        <Button
          type="button"
          variant="outline"
          className="rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium hover:bg-slate-50 text-slate-700"
          onClick={onApplyIntegerPreset}
          disabled={isReadOnly}
          leftIcon={<Sliders className="size-4 text-slate-500" />}
        >
          Hệ số nguyên
        </Button>

        <Button
          type="button"
          variant="outline"
          className="rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium hover:bg-slate-50 text-slate-700"
          onClick={onApplyPercentagePreset}
          disabled={isReadOnly}
          leftIcon={<Percent className="size-4 text-slate-500" />}
        >
          Tỷ lệ phần trăm
        </Button>

        <Button
          type="button"
          variant="outline"
          className="rounded-lg h-10 px-3.5 text-xs sm:text-sm font-medium hover:bg-slate-50 text-slate-700"
          onClick={onOpenAddComponentModal}
          disabled={isReadOnly}
          leftIcon={<Plus className="size-4 text-slate-500" />}
        >
          Thêm đầu điểm
        </Button>
      </div>
    </div>
  );
};