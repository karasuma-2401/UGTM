import React from 'react';
import {
  type ScoreCalculationMethod,
  type KltnBatchSummaryDto,
  type ScoreConfigurationStatus,
  CALCULATION_METHOD_LABELS,
  CONFIG_STATUS_LABELS,
} from '../../types/scoreConfig.types';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';

export interface ScoreConfigSummaryCardProps {
  batch?: KltnBatchSummaryDto | null;
  calculationMethod: ScoreCalculationMethod;
  totalWeight: number;
  reviewerMinimumScore: number | null;
  versionNo?: number | null;
  status?: ScoreConfigurationStatus | null;
  isValid?: boolean;
  isLoading?: boolean;
}

const statusToneMap: Record<ScoreConfigurationStatus, StatusTone> = {
  Active: 'success',
  Draft: 'warning',
  Retired: 'neutral',
};

export const ScoreConfigSummaryCard: React.FC<ScoreConfigSummaryCardProps> = ({
  batch,
  calculationMethod,
  totalWeight,
  reviewerMinimumScore,
  versionNo,
  status,
  isValid = true,
  isLoading = false,
}) => {
  const statusInfo = status ? CONFIG_STATUS_LABELS[status] : null;

  return (
    <div className="p-5 sm:p-6 space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-2.5">
        <h2 className="text-base font-semibold tracking-tight text-ink-primary">Tóm tắt cấu hình</h2>
        {status && statusInfo && (
          <StatusBadge tone={statusToneMap[status]}>
            {statusInfo.label} {versionNo ? `(v${versionNo})` : ''}
          </StatusBadge>
        )}
      </div>

      <div className="grid grid-cols-2 gap-2.5">
        <div className="rounded-xl border border-line/80 bg-surface-subtle/70 p-3.5">
          <p className="text-xs font-medium text-ink-muted">Phương thức</p>
          <p className="mt-1.5 text-sm font-semibold text-ink-primary leading-snug">
            {CALCULATION_METHOD_LABELS[calculationMethod]}
          </p>
        </div>

        <div className="rounded-xl border border-line/80 bg-surface-subtle/70 p-3.5">
          <p className="text-xs font-medium text-ink-muted">
            {calculationMethod === 'Percentage' ? 'Tổng tỷ lệ %' : 'Tổng hệ số'}
          </p>
          <p className="mt-1.5 text-lg font-bold tabular-nums text-ink-primary leading-none">
            {calculationMethod === 'Percentage' ? `${totalWeight}%` : totalWeight}
          </p>
          <p
            className={`mt-1.5 text-xs font-medium ${
              isLoading
                ? 'text-ink-muted'
                : isValid
                  ? 'text-tone-success-text'
                  : 'text-tone-warning-text'
            }`}
          >
            {isLoading
              ? 'Đang tải cấu hình'
              : isValid
                ? 'Tổng trọng số hợp lệ'
                : 'Tổng trọng số chưa hợp lệ'}
          </p>
        </div>
      </div>

      <dl className="rounded-xl border border-line/80 bg-surface-subtle/50 px-3.5 py-2.5 divide-y divide-line/70 text-sm">
        <div className="py-2 first:pt-1">
          <dt className="text-xs font-medium text-ink-muted">Đợt áp dụng</dt>
          <dd className="mt-1 font-semibold text-ink-primary break-words">
            {batch ? `${batch.batchName} - ${batch.academicYear}` : 'Chưa chọn đợt'}
          </dd>
        </div>
        <div className="py-2 last:pb-1">
          <dt className="text-xs font-medium text-ink-muted">Điểm sàn phản biện</dt>
          <dd className="mt-1 font-semibold tabular-nums text-ink-primary">
            {reviewerMinimumScore !== null
              ? `${reviewerMinimumScore} / 10`
              : 'Không áp dụng điểm sàn'}
          </dd>
        </div>
      </dl>

      <details className="rounded-xl border border-line/80 bg-surface-subtle/50">
        <summary className="cursor-pointer select-none rounded-xl px-3.5 py-2.5 text-xs font-semibold text-ink-secondary transition-colors hover:bg-surface-muted/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
          Lưu ý xử lý
        </summary>
        <ul className="space-y-1.5 border-t border-line/70 bg-surface-card px-4 py-3 text-xs leading-relaxed text-ink-body list-disc pl-7 rounded-b-xl">
          <li>Phần trăm có tổng 100%</li>
          <li>Hệ số nguyên hợp lệ và tổng dương</li>
          <li>Nguồn không áp dụng không tính vào mẫu số</li>
        </ul>
      </details>
    </div>
  );
};
