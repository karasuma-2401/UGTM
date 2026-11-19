import { RotateCcw, Search, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import type { CouncilReadinessFilter } from '../../types/councilApproval.types';

interface CouncilApprovalFilterBarProps {
  keyword: string;
  onKeywordChange: (val: string) => void;
  readinessFilter: CouncilReadinessFilter;
  onReadinessFilterChange: (val: CouncilReadinessFilter) => void;
  hasActiveFilters: boolean;
  onReset: () => void;
}

const READINESS_OPTIONS: { value: CouncilReadinessFilter; label: string }[] = [
  { value: 'all', label: 'Tất cả trạng thái' },
  { value: 'ready', label: 'Sẵn sàng duyệt' },
  { value: 'needs_action', label: 'Chờ Khoa / Cần xử lý' },
  { value: 'approved', label: 'Đã phê duyệt' },
];

export function CouncilApprovalFilterBar({
  keyword,
  onKeywordChange,
  readinessFilter,
  onReadinessFilterChange,
  hasActiveFilters,
  onReset,
}: CouncilApprovalFilterBarProps) {
  return (
    <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-2.5">
      <div className="relative flex-1 min-w-0">
        <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 size-4 text-ink-subtle" />
        <Input
          type="text"
          value={keyword}
          onChange={(e) => onKeywordChange(e.target.value)}
          placeholder="Tìm mã hội đồng, phòng, số QĐ, đề tài hoặc giảng viên..."
          aria-label="Tìm kiếm hội đồng"
          className="h-9 pl-9 pr-8 text-xs rounded-xl bg-surface-subtle/60 border-line-input focus-visible:bg-surface-card"
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

      <div className="w-full sm:w-48 shrink-0">
        <Select<CouncilReadinessFilter>
          value={readinessFilter}
          onChange={(val) => onReadinessFilterChange(val)}
          options={READINESS_OPTIONS}
          aria-label="Lọc theo tình trạng thẩm định"
          size="default"
        />
      </div>

      {hasActiveFilters && (
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={onReset}
          className="h-9 px-3 text-xs font-medium rounded-xl border-line-input text-ink-secondary hover:bg-surface-hover shrink-0"
        >
          <RotateCcw className="size-3.5 mr-1.5" />
          Đặt lại
        </Button>
      )}
    </div>
  );
}
