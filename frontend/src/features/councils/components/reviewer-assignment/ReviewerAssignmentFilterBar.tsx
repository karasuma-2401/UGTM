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
    <div className="bg-white border border-slate-200/80 rounded-xl p-3 shadow-xs flex flex-col md:flex-row items-stretch md:items-center gap-3">
      <div className="relative flex-1">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-400" />
        <Input
          type="text"
          placeholder="Tìm mã hồ sơ, tên đề tài hoặc sinh viên"
          value={keyword}
          onChange={(e) => onKeywordChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') onApply();
          }}
          className="pl-9 bg-slate-50/50 border-slate-200 text-sm h-10 w-full rounded-lg focus-visible:bg-white"
        />
      </div>

      <div className="w-full md:w-56 shrink-0">
        <select
          value={statusFilter}
          onChange={(e) => onStatusFilterChange(e.target.value as ReviewerAssignmentFilterStatus)}
          aria-label="Lọc theo trạng thái hồ sơ"
          className="w-full h-10 px-3 py-2 bg-slate-50/50 border border-slate-200 rounded-lg text-sm text-slate-700 focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:bg-white transition-all cursor-pointer"
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
          className="h-10 px-5 text-sm font-medium border-slate-200 text-slate-700 hover:bg-slate-50"
        >
          Áp dụng
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={onReset}
          className="h-10 px-5 text-sm font-medium border-slate-200 text-slate-700 hover:bg-slate-50"
        >
          Đặt lại
        </Button>
      </div>
    </div>
  );
}