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

  const methodOptions = (Object.keys(CALCULATION_METHOD_LABELS) as ScoreCalculationMethod[]).map(
    (key) => ({
      value: key,
      label: CALCULATION_METHOD_LABELS[key],
    }),
  );

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
    <section
      aria-label="Thiết lập chung"
      className="bg-surface-card border border-line/80 rounded-2xl shadow-xs p-5 sm:p-6 space-y-4"
    >
      <h2 className="text-base font-semibold tracking-tight text-ink-primary">Thiết lập chung</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 min-[1400px]:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)_minmax(0,1fr)] gap-4">
        <div className="min-w-0 space-y-1.5 sm:col-span-2 min-[1400px]:col-span-1">
          <p className="text-sm font-medium text-ink-secondary">Đợt khóa luận</p>
          <Select
            aria-label="Đợt khóa luận"
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
            triggerClassName="h-10 w-full rounded-xl border-line-input/90 text-sm font-medium bg-surface-subtle/60"
            contentClassName="min-w-full"
            disabled={isLoadingBatches || batches.length === 0}
          />
        </div>
        <div className="space-y-1.5">
          <p className="text-sm font-medium text-ink-secondary">Phương thức tính điểm</p>
          <Select
            aria-label="Phương thức tính điểm"
            options={methodOptions}
            value={calculationMethod}
            onChange={(val) => onCalculationMethodChange(val as ScoreCalculationMethod)}
            disabled={isReadOnly}
            triggerClassName="h-10 rounded-xl border-line-input/90 text-sm bg-surface-card"
          />
        </div>

        <div className="space-y-1.5">
          <label htmlFor="reviewer-minimum-score" className="text-sm font-medium text-ink-secondary">
            Điểm sàn phản biện
          </label>
          <Input
            id="reviewer-minimum-score"
            aria-describedby="reviewer-score-help"
            type="number"
            step="0.1"
            min="0"
            max="10"
            value={reviewerMinimumScore ?? ''}
            onChange={handleMinScoreChange}
            placeholder="Ví dụ: 5.0"
            disabled={isReadOnly}
            className="h-10 rounded-xl border-line-input/90 text-sm bg-surface-card"
          />
          <p id="reviewer-score-help" className="text-xs text-ink-muted leading-relaxed">
            Thang điểm 0–10. Để trống nếu không áp dụng điểm sàn.
          </p>
        </div>
      </div>
    </section>
  );
};
