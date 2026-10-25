import React from 'react';
import { Input } from '@/components/ui/input';

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
    <div className="rounded-xl border border-slate-200/90 bg-white p-6 shadow-xs">
      <h2 className="mb-4 text-base font-bold text-slate-900">Thông tin đợt</h2>

      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-xs font-semibold text-slate-700">
              Năm học / Học kỳ
            </label>
            <div className="grid grid-cols-5 gap-2">
              <div className="col-span-3">
                <Input
                  type="text"
                  disabled={!canEdit}
                  placeholder="2026-2027"
                  value={academicYear}
                  onChange={(e) => onAcademicYearChange(e.target.value)}
                  className="h-10 text-sm border-slate-200"
                />
              </div>
              <div className="col-span-2">
                <select
                  disabled={!canEdit}
                  value={semester}
                  onChange={(e) => onSemesterChange(Number(e.target.value) || 1)}
                  className="h-10 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-900 focus:outline-hidden focus:ring-2 focus:ring-[#2054A5] disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                >
                  <option value={1}>HK1</option>
                  <option value={2}>HK2</option>
                  <option value={3}>HK3</option>
                </select>
              </div>
            </div>
          </div>

          <div>
            <label className="mb-1.5 block text-xs font-semibold text-slate-700">
              Tên đợt
            </label>
            <Input
              type="text"
              disabled={!canEdit}
              placeholder="Đợt 1"
              value={batchName}
              onChange={(e) => onBatchNameChange(e.target.value)}
              className="h-10 text-sm border-slate-200"
            />
          </div>
        </div>

        <div>
          <label className="mb-1.5 block text-xs font-semibold text-slate-700">
            Ngày bắt đầu
          </label>
          <div className="relative">
            <Input
              type="date"
              disabled={!canEdit}
              value={startDate}
              onChange={(e) => onStartDateChange(e.target.value)}
              className="h-10 text-sm border-slate-200"
            />
          </div>
        </div>
      </div>
    </div>
  );
};