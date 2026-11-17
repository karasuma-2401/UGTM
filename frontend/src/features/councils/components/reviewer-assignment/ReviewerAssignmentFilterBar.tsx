import { RotateCcw, Search, X } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import type { ReviewerAssignmentFilterStatus } from '../../types/reviewerAssignment.types';

interface ReviewerAssignmentFilterBarProps {
  keyword: string;
  onKeywordChange: (val: string) => void;
  statusFilter: ReviewerAssignmentFilterStatus;
  onStatusFilterChange: (val: ReviewerAssignmentFilterStatus) => void;
  onReset: () => void;
}

const STATUS_OPTIONS: { value: ReviewerAssignmentFilterStatus; label: string }[] = [
  { value: 'all', label: 'Tất cả trạng thái' },
  { value: 'pending', label: 'Chờ phân công' },
  { value: 'assigned', label: 'Đã phân công' },
];

export function ReviewerAssignmentFilterBar({
  keyword,
  onKeywordChange,
  statusFilter,
  onStatusFilterChange,
  onReset,
}: ReviewerAssignmentFilterBarProps) {
  const hasActiveFilters = Boolean(keyword.trim() || statusFilter !== 'all');

  return (
    <div className="bg-surface-card border border-line/80 rounded-xl p-3 shadow-xs flex flex-col md:flex-row items-stretch md:items-center gap-3">
      <div className="relative flex-1">
        <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 size-4 text-ink-subtle" />
        <Input
          type="text"
          placeholder="Tìm mã hồ sơ, tên đề tài hoặc sinh viên"
          value={keyword}
          onChange={(e) => onKeywordChange(e.target.value)}
          className="pl-9 pr-8 bg-surface-subtle/50 border-line-input text-sm h-10 w-full rounded-lg focus-visible:bg-surface-card"
        />
        {keyword && (
          <button
            type="button"
            onClick={() => onKeywordChange('')}
            aria-label="Xóa từ khóa tìm kiếm"
            className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded p-0.5 text-ink-subtle hover:bg-surface-muted hover:text-ink-primary cursor-pointer"
          >
            <X className="size-3.5" />
          </button>
        )}
      </div>

      <div className="w-full md:w-56 shrink-0">
        <Select<ReviewerAssignmentFilterStatus>
          value={statusFilter}
          onChange={(val) => onStatusFilterChange(val)}
          options={STATUS_OPTIONS}
          aria-label="Lọc theo trạng thái hồ sơ"
          triggerClassName="h-10 rounded-lg border-line-input bg-surface-subtle/50 text-sm"
        />
      </div>

      {hasActiveFilters && (
        <Button
          type="button"
          variant="outline"
          onClick={onReset}
          className="h-10 px-4 text-sm font-medium border-line-input text-ink-secondary hover:bg-surface-hover shrink-0"
        >
          <RotateCcw className="size-3.5 mr-1.5" />
          Đặt lại
        </Button>
      )}
    </div>
  );
}
