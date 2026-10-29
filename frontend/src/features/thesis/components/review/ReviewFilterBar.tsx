import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

interface ReviewFilterBarProps {
  searchTerm: string;
  onSearchChange: (value: string) => void;
  statusFilter: string;
  onStatusFilterChange: (value: string) => void;
  onApply: () => void;
  onReset: () => void;
}

export function ReviewFilterBar({
  searchTerm,
  onSearchChange,
  statusFilter,
  onStatusFilterChange,
  onApply,
  onReset,
}: ReviewFilterBarProps) {
  return (
    <div className="bg-white p-4 rounded-xl border border-slate-200/90 shadow-xs flex flex-col sm:flex-row items-center justify-between gap-3">
      <div className="relative flex-1 w-full">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-400" />
        <Input
          value={searchTerm}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="Tìm mã hồ sơ, tên đề tài hoặc sinh viên"
          className="pl-9 h-10 text-xs bg-slate-50/50 border-slate-200 focus-visible:bg-white w-full"
        />
      </div>

      <div className="flex items-center gap-2 w-full sm:w-auto">
        <select
          value={statusFilter}
          onChange={(e) => onStatusFilterChange(e.target.value)}
          className="h-10 px-3 text-xs font-medium text-slate-700 bg-white border border-slate-200 rounded-lg shadow-2xs focus:outline-hidden focus:ring-2 focus:ring-[#2054A5]"
        >
          <option value="ALL">Tất cả trạng thái</option>
          <option value="Submitted">Chờ thẩm định</option>
          <option value="NeedRevision">Yêu cầu chỉnh sửa</option>
          <option value="Approved">Đạt</option>
          <option value="Rejected">Không đạt</option>
        </select>

        <Button
          type="button"
          onClick={onApply}
          className="h-10 px-4 text-xs font-medium bg-white text-slate-700 border border-slate-200 hover:bg-slate-50"
        >
          Áp dụng
        </Button>

        <Button
          type="button"
          variant="ghost"
          onClick={onReset}
          className="h-10 px-3 text-xs text-slate-500 hover:text-slate-800"
        >
          Đặt lại
        </Button>
      </div>
    </div>
  );
}