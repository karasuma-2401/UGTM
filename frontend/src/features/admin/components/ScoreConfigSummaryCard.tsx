import React from 'react';
import {
  type ScoreCalculationMethod,
  type KltnBatchSummaryDto,
  type ScoreConfigurationStatus,
  CALCULATION_METHOD_LABELS,
  CONFIG_STATUS_LABELS,
} from '../types/scoreConfig.types';
import { Badge } from '@/components/ui/badge';

export interface ScoreConfigSummaryCardProps {
  batch?: KltnBatchSummaryDto | null;
  calculationMethod: ScoreCalculationMethod;
  totalWeight: number;
  reviewerMinimumScore: number | null;
  versionNo?: number | null;
  status?: ScoreConfigurationStatus | null;
}

export const ScoreConfigSummaryCard: React.FC<ScoreConfigSummaryCardProps> = ({
  batch,
  calculationMethod,
  totalWeight,
  reviewerMinimumScore,
  versionNo,
  status,
}) => {
  const statusInfo = status ? CONFIG_STATUS_LABELS[status] : null;

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs p-5 sm:p-6 space-y-5">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3.5">
        <h3 className="text-base font-bold text-slate-900 tracking-tight">
          Phạm vi cấu hình
        </h3>
        {statusInfo && (
          <Badge variant={statusInfo.badgeVariant} className="text-xs px-2 py-0.5">
            {statusInfo.label} {versionNo ? `(v${versionNo})` : ''}
          </Badge>
        )}
      </div>

      <div className="space-y-4 text-xs">
        <div>
          <span className="text-slate-400 font-medium block mb-0.5">Phạm vi</span>
          <span className="font-semibold text-slate-800 text-sm">
            Cấu hình hệ thống
          </span>
        </div>

        <div>
          <span className="text-slate-400 font-medium block mb-0.5">Đợt áp dụng</span>
          <span className="font-semibold text-slate-800 text-sm">
            {batch ? `${batch.batchName} · ${batch.academicYear}` : 'Chưa chọn đợt'}
          </span>
        </div>

        <div>
          <span className="text-slate-400 font-medium block mb-0.5">Phương thức</span>
          <span className="font-semibold text-slate-800 text-sm">
            {CALCULATION_METHOD_LABELS[calculationMethod]}
          </span>
        </div>

        <div>
          <span className="text-slate-400 font-medium block mb-0.5">
            {calculationMethod === 'Percentage' ? 'Tổng tỷ lệ phần trăm' : 'Tổng hệ số'}
          </span>
          <span className="font-semibold text-slate-800 text-sm">
            {calculationMethod === 'Percentage'
              ? `${totalWeight}% ${totalWeight === 100 ? '(Đủ 100%)' : '(Chưa đủ 100%)'}`
              : `Tổng ${totalWeight} · Theo các nguồn thực sự áp dụng`}
          </span>
        </div>

        <div>
          <span className="text-slate-400 font-medium block mb-0.5">
            Điểm sàn phản biện
          </span>
          <span className="font-semibold text-slate-800 text-sm">
            {reviewerMinimumScore !== null
              ? `${reviewerMinimumScore} · Giá trị mẫu`
              : 'Không áp dụng điểm sàn'}
          </span>
        </div>

        <div className="border-t border-slate-100 pt-3 space-y-1.5">
          <span className="font-bold text-slate-800 block text-xs">Lưu ý xử lý</span>
          <ul className="space-y-1 text-slate-600 list-disc pl-4 leading-relaxed text-[11px]">
            <li>Phần trăm có tổng 100%</li>
            <li>Hệ số nguyên hợp lệ và tổng dương</li>
            <li>Nguồn không áp dụng không tính vào mẫu số</li>
          </ul>
        </div>
      </div>
    </div>
  );
};