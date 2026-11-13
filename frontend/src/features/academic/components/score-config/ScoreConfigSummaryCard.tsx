import React from 'react';
import {
  type ScoreCalculationMethod,
  type KltnBatchSummaryDto,
  type ScoreConfigurationStatus,
  CALCULATION_METHOD_LABELS,
  CONFIG_STATUS_LABELS,
} from '../../types/scoreConfig.types';
import { Badge } from '@/components/ui/badge';

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
    <div className="p-5 space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-4">
        <h2 className="text-base font-semibold text-slate-900">Tóm tắt cấu hình</h2>
        {statusInfo && (
          <Badge variant={statusInfo.badgeVariant} className="text-xs px-2 py-0.5">
            {statusInfo.label} {versionNo ? `(v${versionNo})` : ''}
          </Badge>
        )}
      </div>

      <div className="space-y-4 text-sm">
        <div>
          <span className="text-slate-500 block mb-1">Đợt áp dụng</span>
          <span className="font-semibold text-slate-800 text-sm">
            {batch ? `${batch.batchName} - ${batch.academicYear}` : 'Chưa chọn đợt'}
          </span>
        </div>

        <div>
          <span className="text-slate-500 block mb-1">Phương thức</span>
          <span className="font-semibold text-slate-800 text-sm">
            {CALCULATION_METHOD_LABELS[calculationMethod]}
          </span>
        </div>

        <div>
          <span className="text-slate-500 block mb-1">
            {calculationMethod === 'Percentage' ? 'Tổng tỷ lệ phần trăm' : 'Tổng hệ số'}
          </span>
          <span className="font-semibold text-slate-800 text-sm tabular-nums">
            {calculationMethod === 'Percentage' ? `${totalWeight}%` : totalWeight}
          </span>
          <p
            className={`mt-1 text-xs font-medium ${isValid ? 'text-emerald-700' : 'text-amber-700'}`}
          >
            {isLoading
              ? 'Đang tải cấu hình'
              : isValid
                ? 'Tổng trọng số hợp lệ'
                : 'Tổng trọng số chưa hợp lệ'}
          </p>
        </div>

        <div>
          <span className="text-slate-500 block mb-1">Điểm sàn phản biện</span>
          <span className="font-semibold text-slate-800 text-sm">
            {reviewerMinimumScore !== null
              ? `${reviewerMinimumScore} / 10`
              : 'Không áp dụng điểm sàn'}
          </span>
        </div>

        <details className="border-t border-slate-100 pt-3">
          <summary className="cursor-pointer font-semibold text-slate-700 text-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary rounded">
            Lưu ý xử lý
          </summary>
          <ul className="mt-3 space-y-2 text-slate-600 list-disc pl-4 leading-relaxed text-xs">
            <li>Phần trăm có tổng 100%</li>
            <li>Hệ số nguyên hợp lệ và tổng dương</li>
            <li>Nguồn không áp dụng không tính vào mẫu số</li>
          </ul>
        </details>
      </div>
    </div>
  );
};
