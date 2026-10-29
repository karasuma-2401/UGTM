import React from 'react';
import { Select } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import {
  type ScoreCalculationMethod,
  type KltnBatchSummaryDto,
  CALCULATION_METHOD_LABELS,
} from '../../types/scoreConfig.types';

export interface ScoreConfigGeneralCardProps {
  batches: KltnBatchSummaryDto[];
  selectedBatchId: string;
  onBatchChange: (batchId: string) => void;
  calculationMethod: ScoreCalculationMethod;
  onCalculationMethodChange: (method: ScoreCalculationMethod) => void;
  reviewerMinimumScore: number | null;
  onReviewerMinimumScoreChange: (score: number | null) => void;
  onAddOptionalComponent?: (sourceCode: string) => void;
  isReadOnly?: boolean;
  isLoadingBatches?: boolean;
}

export const ScoreConfigGeneralCard: React.FC<ScoreConfigGeneralCardProps> = ({
  batches,
  selectedBatchId,
  onBatchChange,
  calculationMethod,
  onCalculationMethodChange,
  reviewerMinimumScore,
  onReviewerMinimumScoreChange,
  onAddOptionalComponent: _onAddOptionalComponent,
  isReadOnly = false,
  isLoadingBatches = false,
}) => {
  const batchOptions = batches.map((b) => ({
    value: b.id,
    label: `${b.batchName} (${b.academicYear})`,
  }));

  const methodOptions = (
    Object.keys(CALCULATION_METHOD_LABELS) as ScoreCalculationMethod[]
  ).map((key) => ({
    value: key,
    label: CALCULATION_METHOD_LABELS[key],
  }));

  const handleMinScoreChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value.trim();
    if (val === '') {
      onReviewerMinimumScoreChange(null);
      return;
    }
    const num = parseFloat(val);
    if (!isNaN(num)) {
      onReviewerMinimumScoreChange(num);
    }
  };

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs p-5 sm:p-6 space-y-5">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-slate-100 pb-4">
        <div>
          <h3 className="text-base font-bold text-slate-900 tracking-tight">Cấu hình</h3>
          <p className="text-xs text-slate-500 mt-0.5">
            Chọn đợt khóa luận và thiết lập phương thức tính điểm chung.
          </p>
        </div>

        <div className="w-full sm:w-80 md:w-96">
          <Select
            options={batchOptions}
            value={selectedBatchId}
            onChange={(val) => onBatchChange(val as string)}
            placeholder={
              isLoadingBatches
                ? 'Đang tải đợt khóa luận...'
                : batches.length === 0
                ? 'Chưa có đợt khóa luận'
                : 'Chọn đợt khóa luận...'
            }
            triggerClassName="h-9 w-full text-xs font-medium"
            contentClassName="min-w-full"
            disabled={isLoadingBatches || batches.length === 0}
          />
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 sm:gap-6">
        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-slate-700">
            Phương thức
          </label>
          <Select
            options={methodOptions}
            value={calculationMethod}
            onChange={(val) => onCalculationMethodChange(val as ScoreCalculationMethod)}
            disabled={isReadOnly}
            triggerClassName="h-10 text-sm bg-white"
          />
        </div>

        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-slate-700">
            Điểm sàn phản biện
          </label>
          <Input
            type="number"
            step="0.1"
            min="0"
            max="10"
            value={reviewerMinimumScore ?? ''}
            onChange={handleMinScoreChange}
            placeholder="Ví dụ: 5.0"
            disabled={isReadOnly}
            className="h-10 text-sm bg-white"
          />
          <p className="text-[11px] text-slate-400">
            Giá trị của đợt mẫu, có thể cấu hình (0.0 – 10.0)
          </p>
        </div>
      </div>
    </div>
  );
};