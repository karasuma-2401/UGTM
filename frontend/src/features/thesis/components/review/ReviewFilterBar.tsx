import { Search, RotateCcw } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import type { KltnBatchSummaryDto } from '@/features/academic/types/period.types';

interface ReviewFilterBarProps {
  searchTerm: string;
  onSearchChange: (value: string) => void;
  statusFilter: string;
  onStatusFilterChange: (value: string) => void;
  batchId?: string;
  onBatchChange?: (value: string) => void;
  batches?: ReadonlyArray<
    Pick<KltnBatchSummaryDto, 'id' | 'batchName' | 'semester' | 'academicYear'>
  >;
  onApply: () => void;
  onReset: () => void;
}

const STATUS_OPTIONS = [
  { value: 'ALL', label: 'Tất cả trạng thái' },
  { value: 'Submitted', label: 'Chờ thẩm định' },
  { value: 'NeedRevision', label: 'Yêu cầu chỉnh sửa' },
  { value: 'Approved', label: 'Đạt' },
  { value: 'Rejected', label: 'Không đạt' },
];

export function ReviewFilterBar({
  searchTerm,
  onSearchChange,
  statusFilter,
  onStatusFilterChange,
  batchId = '',
  onBatchChange,
  batches = [],
  onApply,
  onReset,
}: ReviewFilterBarProps) {
  const batchOptions = [
    { value: '', label: 'Tất cả đợt KLTN' },
    ...batches.map((b) => ({
      value: b.id,
      label: `${b.batchName} - HK${b.semester} ${b.academicYear}`,
    })),
  ];

  return (
    <div className="bg-white p-4 rounded-xl border border-slate-200/90 shadow-xs flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3">
      <div className="relative flex-1 w-full">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-400 pointer-events-none" />
        <Input
          value={searchTerm}
          onChange={(e) => onSearchChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              onApply();
            }
          }}
          placeholder="Tìm mã hồ sơ, tên đề tài hoặc sinh viên..."
          className="pl-9 h-10 text-xs bg-slate-50/50 border-slate-200 focus-visible:bg-white w-full"
        />
      </div>

      <div className="flex flex-wrap items-center gap-2 w-full md:w-auto">
        {onBatchChange && batches.length > 0 && (
          <div className="w-full sm:w-56">
            <Select
              options={batchOptions}
              value={batchId}
              onChange={(val) => onBatchChange(String(val ?? ''))}
              placeholder="Tất cả đợt KLTN"
              size="lg"
              triggerClassName="h-10 text-xs font-medium text-slate-700 bg-white border-slate-200 rounded-lg shadow-2xs"
            />
          </div>
        )}

        <div className="w-full sm:w-48">
          <Select
            options={STATUS_OPTIONS}
            value={statusFilter}
            onChange={(val) => onStatusFilterChange(String(val ?? 'ALL'))}
            placeholder="Tất cả trạng thái"
            size="lg"
            triggerClassName="h-10 text-xs font-medium text-slate-700 bg-white border-slate-200 rounded-lg shadow-2xs"
          />
        </div>

        <Button
          type="button"
          onClick={onApply}
          className="h-10 px-4 text-xs font-medium bg-[#2054A5] text-white hover:bg-[#1a4485]"
        >
          Áp dụng
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={onReset}
          className="h-10 px-3 text-xs border-slate-200 text-slate-600 hover:text-slate-900"
        >
          <RotateCcw className="size-3.5 mr-1.5" />
          Đặt lại
        </Button>
      </div>
    </div>
  );
}