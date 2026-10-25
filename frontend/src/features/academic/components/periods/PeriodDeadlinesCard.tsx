import React from 'react';
import { Sliders } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

interface PeriodDeadlinesCardProps {
  syllabusDeadline: string;
  midtermDeadline: string;
  finalEvaluationDeadline: string;
  defenseDeadline: string;
  syllabusMinDate?: string;
  midtermMinDate?: string;
  finalEvaluationMinDate?: string;
  defenseMinDate?: string;
  canEdit: boolean;
  onSyllabusDeadlineChange: (val: string) => void;
  onMidtermDeadlineChange: (val: string) => void;
  onFinalEvaluationDeadlineChange: (val: string) => void;
  onDefenseDeadlineChange: (val: string) => void;
  onOpenGateDetails?: () => void;
}

export const PeriodDeadlinesCard: React.FC<PeriodDeadlinesCardProps> = ({
  syllabusDeadline,
  midtermDeadline,
  finalEvaluationDeadline,
  defenseDeadline,
  syllabusMinDate,
  midtermMinDate,
  finalEvaluationMinDate,
  defenseMinDate,
  canEdit,
  onSyllabusDeadlineChange,
  onMidtermDeadlineChange,
  onFinalEvaluationDeadlineChange,
  onDefenseDeadlineChange,
  onOpenGateDetails,
}) => {
  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-6 shadow-xs">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-base font-bold text-slate-900">Deadline</h2>
        {onOpenGateDetails && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={onOpenGateDetails}
            className="h-8 gap-1.5 text-xs font-medium text-[#2054A5] hover:bg-blue-50 hover:text-[#1a4485]"
          >
            <Sliders className="size-3.5" />
            Chi tiết cổng thẩm định
          </Button>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <label className="mb-1.5 block text-xs font-semibold text-slate-700">
            Đề cương
          </label>
          <Input
            type="date"
            disabled={!canEdit}
            value={syllabusDeadline}
            min={syllabusMinDate}
            onChange={(e) => onSyllabusDeadlineChange(e.target.value)}
            className="h-10 text-sm border-slate-200"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-xs font-semibold text-slate-700">
            Giữa kỳ
          </label>
          <Input
            type="date"
            disabled={!canEdit}
            value={midtermDeadline}
            min={midtermMinDate}
            onChange={(e) => onMidtermDeadlineChange(e.target.value)}
            className="h-10 text-sm border-slate-200"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-xs font-semibold text-slate-700">
            Cuối kỳ
          </label>
          <Input
            type="date"
            disabled={!canEdit}
            value={finalEvaluationDeadline}
            min={finalEvaluationMinDate}
            onChange={(e) => onFinalEvaluationDeadlineChange(e.target.value)}
            className="h-10 text-sm border-slate-200"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-xs font-semibold text-slate-700">
            Bảo vệ / Lưu chiểu
          </label>
          <Input
            type="date"
            disabled={!canEdit}
            value={defenseDeadline}
            min={defenseMinDate}
            onChange={(e) => onDefenseDeadlineChange(e.target.value)}
            className="h-10 text-sm border-slate-200"
          />
        </div>
      </div>
    </div>
  );
};
