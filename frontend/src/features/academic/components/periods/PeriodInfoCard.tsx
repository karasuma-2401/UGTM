import React from 'react';
import { DatePicker } from '@/components/ui/calendar';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';

interface PeriodInfoCardProps {
  academicYear: string;
  semester: number;
  batchName: string;
  startDate: string;
  canEdit: boolean;
  onAcademicYearChange: (value: string) => void;
  onSemesterChange: (value: number) => void;
  onBatchNameChange: (value: string) => void;
  onStartDateChange: (value: string) => void;
}

const SEMESTER_OPTIONS = [
  { value: 1, label: 'Học kỳ 1 (HK1)' },
  { value: 2, label: 'Học kỳ 2 (HK2)' },
  { value: 3, label: 'Học kỳ 3 (HK3)' },
];

export const PeriodInfoCard: React.FC<PeriodInfoCardProps> = ({
  academicYear,
  semester,
  batchName,
  startDate,
  canEdit,
  onAcademicYearChange,
  onSemesterChange,
  onBatchNameChange,
  onStartDateChange,
}) => {
  return (
    <div className="rounded-2xl border border-line bg-surface-card p-6 shadow-xs transition-all hover:border-line-strong">
      <div className="mb-5">
        <h2 className="text-base font-bold text-ink-primary tracking-tight">
          Thông tin đợt khóa luận
        </h2>
        <p className="text-xs text-ink-muted">
          Niên khóa, học kỳ và mốc xuất phát của đợt
        </p>
      </div>

      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-12">
          {/* Năm học */}
          <div className="sm:col-span-4">
            <label className="mb-1.5 block text-xs font-semibold text-ink-secondary">
              Năm học <span className="text-rose-500">*</span>
            </label>
            <Input
              type="text"
              disabled={!canEdit}
              placeholder="VD: 2026-2027"
              value={academicYear}
              onChange={(e) => onAcademicYearChange(e.target.value)}
              className="h-10 text-sm rounded-lg border-line-input bg-surface-card transition-all focus:ring-2 focus:ring-primary/15"
            />
          </div>

          {/* Học kỳ */}
          <div className="sm:col-span-3">
            <label className="mb-1.5 block text-xs font-semibold text-ink-secondary">
              Học kỳ <span className="text-rose-500">*</span>
            </label>
            <Select
              options={SEMESTER_OPTIONS}
              disabled={!canEdit}
              value={semester}
              onChange={(val) => onSemesterChange(Number(val) || 1)}
              triggerClassName="h-10 text-sm rounded-lg border-line-input bg-surface-card"
            />
          </div>

          {/* Tên đợt */}
          <div className="sm:col-span-5">
            <label className="mb-1.5 block text-xs font-semibold text-ink-secondary">
              Tên đợt khóa luận <span className="text-rose-500">*</span>
            </label>
            <Input
              type="text"
              disabled={!canEdit}
              placeholder="VD: Đợt KLTN Học kỳ 1"
              value={batchName}
              onChange={(e) => onBatchNameChange(e.target.value)}
              className="h-10 text-sm rounded-lg border-line-input bg-surface-card transition-all focus:ring-2 focus:ring-primary/15"
            />
          </div>
        </div>

        {/* Ngày bắt đầu */}
        <div className="pt-1">
          <label className="mb-1.5 block text-xs font-semibold text-ink-secondary">
            Ngày bắt đầu đợt (Mốc tuần 1) <span className="text-rose-500">*</span>
          </label>
          <div className="relative max-w-sm">
            <DatePicker
              aria-label="Ngày bắt đầu"
              disabled={!canEdit}
              value={startDate}
              onValueChange={(value) => onStartDateChange(value)}
              className="h-10 text-sm rounded-lg border-line-input bg-surface-card shadow-2xs"
            />
          </div>
          <p className="mt-1.5 text-[11px] text-ink-subtle">
            Các mốc giai đoạn tiếp theo sẽ tự động tịnh tiến dựa theo ngày khởi đầu này.
          </p>
        </div>
      </div>
    </div>
  );
};
