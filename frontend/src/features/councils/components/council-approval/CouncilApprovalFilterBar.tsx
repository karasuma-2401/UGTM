import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import type { AcademicBatch } from '@/shared/academic-periods/types';
import type { CouncilApprovalStatus } from '../../types/councilApproval.types';

interface CouncilApprovalFilterBarProps {
  keyword: string;
  onKeywordChange: (val: string) => void;
  batches: AcademicBatch[];
  batchFilter: string;
  onBatchFilterChange: (val: string) => void;
  statusFilter: CouncilApprovalStatus;
  onStatusFilterChange: (val: CouncilApprovalStatus) => void;
  onApply: () => void;
  onReset: () => void;
}

export function CouncilApprovalFilterBar({
  keyword,
  onKeywordChange,
  batches,
  batchFilter,
  onBatchFilterChange,
  statusFilter,
  onStatusFilterChange,
  onApply,
  onReset,
}: CouncilApprovalFilterBarProps) {
  return (
    <div className="bg-surface-card border border-line/80 rounded-xl p-3 shadow-xs flex flex-col lg:flex-row items-stretch lg:items-center gap-3">
      <div className="relative flex-1 min-w-0">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-ink-subtle" />
        <Input
          type="text"
          placeholder="Tìm mã hội đồng, phòng, đề tài hoặc giảng viên"
          value={keyword}
          onChange={(e) => onKeywordChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') onApply();
          }}
          className="pl-9 bg-surface-subtle/50 border-line-input text-sm h-10 w-full rounded-lg focus-visible:bg-surface-card"
        />
      </div>

      <div className="w-full lg:w-56 shrink-0">
        <select
          value={batchFilter}
          onChange={(e) => onBatchFilterChange(e.target.value)}
          aria-label="Lọc theo đợt KLTN"
          className="w-full h-10 px-3 py-2 bg-surface-subtle/50 border border-line-input rounded-lg text-sm text-ink-secondary focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:bg-surface-card transition-all cursor-pointer"
        >
          <option value="all">Tất cả đợt KLTN</option>
          {batches.map((batch) => (
            <option key={batch.id} value={batch.id}>
              {batch.batchName}
            </option>
          ))}
        </select>
      </div>

      <div className="w-full lg:w-48 shrink-0">
        <select
          value={statusFilter}
          onChange={(e) => onStatusFilterChange(e.target.value as CouncilApprovalStatus)}
          aria-label="Lọc theo trạng thái dự thảo"
          className="w-full h-10 px-3 py-2 bg-surface-subtle/50 border border-line-input rounded-lg text-sm text-ink-secondary focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:bg-surface-card transition-all cursor-pointer"
        >
          <option value="all">Tất cả trạng thái</option>
          <option value="draft">Chờ phê duyệt</option>
          <option value="active">Đã phê duyệt</option>
          <option value="completed">Đã hoàn thành</option>
        </select>
      </div>

      <div className="flex items-center gap-2 shrink-0">
        <Button
          type="button"
          variant="outline"
          onClick={onApply}
          className="h-10 px-5 text-sm font-medium border-line-input text-ink-secondary hover:bg-surface-hover"
        >
          Áp dụng
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={onReset}
          className="h-10 px-5 text-sm font-medium border-line-input text-ink-secondary hover:bg-surface-hover"
        >
          Đặt lại
        </Button>
      </div>
    </div>
  );
}