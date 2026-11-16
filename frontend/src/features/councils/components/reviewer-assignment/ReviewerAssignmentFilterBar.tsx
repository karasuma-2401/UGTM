import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import type { ReviewerAssignmentFilterStatus } from '../../types/reviewerAssignment.types';

interface ReviewerAssignmentFilterBarProps {
  keyword: string;
  onKeywordChange: (val: string) => void;
  statusFilter: ReviewerAssignmentFilterStatus;
  onStatusFilterChange: (val: ReviewerAssignmentFilterStatus) => void;
  onApply: () => void;
  onReset: () => void;
}

export function ReviewerAssignmentFilterBar({
  keyword,
  onKeywordChange,
  statusFilter,
  onStatusFilterChange,
  onApply,
  onReset,
}: ReviewerAssignmentFilterBarProps) {
  return (
    <div className="bg-surface-card border border-line/80 rounded-xl p-3 shadow-xs flex flex-col md:flex-row items-stretch md:items-center gap-3">
      <div className="relative flex-1">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-ink-subtle" />
        <Input
          type="text"
          placeholder="Tìm mã hồ sơ, tên đề tài hoặc sinh viên"
          value={keyword}
          onChange={(e) => onKeywordChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') onApply();
          }}
          className="pl-9 bg-surface-subtle/50 border-line-input text-sm h-10 w-full rounded-lg focus-visible:bg-surface-card"
        />
      </div>

      <div className="w-full md:w-56 shrink-0">
        <select
          value={statusFilter}
          onChange={(e) => onStatusFilterChange(e.target.value as ReviewerAssignmentFilterStatus)}
          aria-label="Lọc theo trạng thái hồ sơ"
          className="w-full h-10 px-3 py-2 bg-surface-subtle/50 border border-line-input rounded-lg text-sm text-ink-secondary focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:bg-surface-card transition-all cursor-pointer"
        >
          <option value="all">Tất cả trạng thái</option>
          <option value="pending">Chờ phân công</option>
          <option value="assigned">Đã phân công</option>
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