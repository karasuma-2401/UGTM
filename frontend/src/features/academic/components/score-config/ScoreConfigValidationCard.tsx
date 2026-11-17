import React from 'react';
import { CheckCircle2, AlertCircle } from 'lucide-react';
import type { ScoreCalculationMethod } from '../../types/scoreConfig.types';

export interface ScoreConfigValidationCardProps {
  calculationMethod: ScoreCalculationMethod;
  totalWeight: number;
  componentsCount: number;
  customValidationErrors?: string[];
}

export const ScoreConfigValidationCard: React.FC<ScoreConfigValidationCardProps> = ({
  calculationMethod,
  totalWeight,
  componentsCount,
  customValidationErrors = [],
}) => {
  const errors: string[] = [...customValidationErrors];

  if (componentsCount === 0) {
    errors.push('Cấu hình phải có ít nhất một thành phần điểm.');
  }

  if (calculationMethod === 'Percentage') {
    if (totalWeight !== 100) {
      errors.push(
        `Tổng trọng số hiện tại là ${totalWeight}%, phương thức tỷ lệ phần trăm bắt buộc phải bằng chính xác 100%.`,
      );
    }
  } else {
    if (totalWeight <= 0) {
      errors.push('Tổng các hệ số phải lớn hơn 0.');
    }
  }

  const isValid = errors.length === 0;

  return (
    <div
      className={`rounded-xl border p-5 sm:p-6 transition-colors shadow-xs ${
        isValid
          ? 'bg-surface-card border-line'
          : 'bg-tone-warning-bg border-tone-warning-border'
      }`}
    >
      <div className="flex items-start gap-3">
        <div className="mt-0.5 shrink-0">
          {isValid ? (
            <CheckCircle2 className="size-5 text-tone-success-icon" />
          ) : (
            <AlertCircle className="size-5 text-tone-warning-icon" />
          )}
        </div>

        <div className="space-y-1">
          <h4
            className={`text-base font-bold tracking-tight ${
              isValid ? 'text-ink-primary' : 'text-tone-warning-strong'
            }`}
          >
            {isValid ? 'Hợp lệ' : 'Chưa hợp lệ'}
          </h4>

          <p className="text-xs text-ink-body leading-relaxed">
            Phần trăm tổng 100%; hệ số nguyên hợp lệ và tổng dương. Đầu điểm bắt
            buộc/tùy chọn gắn với điều kiện áp dụng.
          </p>

          {!isValid && (
            <ul className="mt-2 space-y-1 list-disc pl-4 text-xs font-medium text-tone-warning-strong">
              {errors.map((err, idx) => (
                <li key={idx}>{err}</li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
};