import React from 'react';
import { Search, X, Calendar as CalendarIcon, RotateCcw, Filter } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, type SelectOption } from '@/components/ui/select';
import { DatePicker } from '@/components/ui/calendar';
import { ACTION_GROUPS, formatAuditAction } from '../utils/parseAuditDiff';
import type { AuditLogFilterState, AuditTimePreset } from '../types/auditLog.types';
import { cn } from '@/lib/utils';

interface AuditLogFilterBarProps {
  filter: AuditLogFilterState;
  onFilterChange: (next: AuditLogFilterState) => void;
  onApply: () => void;
  onReset: () => void;
  isLoading?: boolean;
  actions?: string[];
}

const ACTION_OPTIONS: SelectOption[] = [
  { value: '', label: 'Tất cả thao tác' },
  ...ACTION_GROUPS.flatMap((group) =>
    group.items.map((item) => ({
      value: item.value,
      label: item.label,
      description: group.group,
    })),
  ),
];

const OUTCOME_OPTIONS: SelectOption[] = [
  { value: '', label: 'Tất cả kết quả' },
  { value: 'Success', label: 'Thành công' },
  { value: 'Denied', label: 'Bị từ chối (Denied)' },
  { value: 'Failed', label: 'Thất bại / Lỗi (Failed)' },
];

function getLocalDateString(date: Date): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(date);
}

function getPastDaysString(days: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - days);
  return getLocalDateString(d);
}

export const AuditLogFilterBar: React.FC<AuditLogFilterBarProps> = ({
  filter,
  onFilterChange,
  onApply,
  onReset,
  isLoading = false,
  actions = [],
}) => {
  const actionOptions = [...ACTION_OPTIONS, ...actions.filter((action) => !ACTION_OPTIONS.some((option) => option.value === action)).map((action) => ({ value: action, label: formatAuditAction(action) }))];
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onApply();
  };

  const handlePresetChange = (preset: AuditTimePreset) => {
    const today = getLocalDateString(new Date());

    if (preset === 'all') {
      onFilterChange({
        ...filter,
        timePreset: 'all',
        fromUtc: '',
        toUtc: '',
      });
    } else if (preset === 'today') {
      onFilterChange({
        ...filter,
        timePreset: 'today',
        fromUtc: today,
        toUtc: today,
      });
    } else if (preset === '7d') {
      onFilterChange({
        ...filter,
        timePreset: '7d',
        fromUtc: getPastDaysString(6),
        toUtc: today,
      });
    } else if (preset === '30d') {
      onFilterChange({
        ...filter,
        timePreset: '30d',
        fromUtc: getPastDaysString(29),
        toUtc: today,
      });
    } else {
      onFilterChange({
        ...filter,
        timePreset: 'custom',
      });
    }
  };

  const hasActiveFilters = Boolean(
    filter.searchTerm ||
      filter.action ||
      filter.outcome ||
      filter.fromUtc ||
      filter.toUtc ||
      filter.requestId || filter.targetId || filter.actorSessionId ||
      filter.timePreset !== 'all',
  );

  return (
    <form
      onSubmit={handleSubmit}
      className="p-4 sm:p-5 border-b border-slate-100 bg-slate-50/40 space-y-3.5"
    >
      {/* Hàng 1: Search, Dropdowns, Action buttons */}
      <div className="flex flex-col lg:flex-row items-stretch lg:items-center gap-3">
        {/* Tìm kiếm */}
        <div className="flex-1 min-w-0">
          <Input
            aria-label="Từ khóa tìm kiếm"
            value={filter.searchTerm}
            onChange={(e) => onFilterChange({ ...filter, searchTerm: e.target.value })}
            placeholder="Tìm theo người thực hiện, mã hồ sơ hoặc nội dung..."
            leftIcon={<Search className="size-4 text-slate-400" />}
            rightIcon={
              filter.searchTerm ? (
                <button
                  type="button"
                  onClick={() => onFilterChange({ ...filter, searchTerm: '' })}
                  className="p-1 text-slate-400 hover:text-slate-600 rounded"
                  aria-label="Xóa từ khóa tìm kiếm"
                >
                  <X className="size-3.5" />
                </button>
              ) : null
            }
            className="h-10 text-sm bg-white border-slate-200/90 focus:bg-white"
          />
        </div>

        {/* Dropdown Thao tác */}
        <div className="w-full sm:w-60 lg:w-64 shrink-0">
          <Select
            options={actionOptions}
            value={filter.action}
            onChange={(val) => onFilterChange({ ...filter, action: String(val || '') })}
            placeholder="Tất cả thao tác"
            searchable
            searchPlaceholder="Tìm thao tác..."
            triggerClassName="h-10 text-sm bg-white border-slate-200/90"
          />
        </div>

        {/* Dropdown Kết quả */}
        <div className="w-full sm:w-48 lg:w-52 shrink-0">
          <Select
            options={OUTCOME_OPTIONS}
            value={filter.outcome}
            onChange={(val) => onFilterChange({ ...filter, outcome: String(val || '') })}
            placeholder="Tất cả kết quả"
            triggerClassName="h-10 text-sm bg-white border-slate-200/90"
          />
        </div>

        {/* Nút bấm Áp dụng & Đặt lại */}
        <div className="flex items-center gap-2 shrink-0">
          <Button
            type="submit"
            disabled={isLoading}
            leftIcon={<Filter className="size-3.5" />}
            className="h-10 px-4 font-medium shadow-2xs"
          >
            Áp dụng
          </Button>

          {hasActiveFilters && (
            <Button
              type="button"
              variant="outline"
              disabled={isLoading}
              onClick={onReset}
              leftIcon={<RotateCcw className="size-3.5" />}
              className="h-10 px-3.5 text-slate-600 hover:text-slate-900"
              title="Đặt lại bộ lọc về mặc định"
            >
              Đặt lại
            </Button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
        <Input aria-label="Request ID chính xác" placeholder="Request ID chính xác" value={filter.requestId || ''} onChange={(event) => onFilterChange({ ...filter, requestId: event.target.value })} />
        <Input aria-label="Mã đối tượng chính xác" placeholder="Mã đối tượng chính xác" value={filter.targetId || ''} onChange={(event) => onFilterChange({ ...filter, targetId: event.target.value })} />
        <Input aria-label="Session ID chính xác" placeholder="Session ID chính xác" value={filter.actorSessionId || ''} onChange={(event) => onFilterChange({ ...filter, actorSessionId: event.target.value })} />
      </div>

      {/* Hàng 2: Mốc thời gian nhanh (Time Presets) & Bộ chọn ngày */}
      <div className="flex flex-wrap items-center justify-between gap-3 pt-0.5">
        <div className="flex flex-wrap items-center gap-1.5 sm:gap-2">
          <span className="text-xs font-medium text-slate-500 mr-1 flex items-center gap-1">
            <CalendarIcon className="size-3.5 text-slate-400" />
            Thời gian:
          </span>

          <button
            type="button"
            onClick={() => handlePresetChange('all')}
            className={cn(
              'px-2.5 py-1 text-xs rounded-lg font-medium transition-colors border',
              filter.timePreset === 'all'
                ? 'bg-primary text-white border-primary shadow-2xs'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-900',
            )}
          >
            Tất cả
          </button>

          <button
            type="button"
            onClick={() => handlePresetChange('today')}
            className={cn(
              'px-2.5 py-1 text-xs rounded-lg font-medium transition-colors border',
              filter.timePreset === 'today'
                ? 'bg-primary text-white border-primary shadow-2xs'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-900',
            )}
          >
            Hôm nay
          </button>

          <button
            type="button"
            onClick={() => handlePresetChange('7d')}
            className={cn(
              'px-2.5 py-1 text-xs rounded-lg font-medium transition-colors border',
              filter.timePreset === '7d'
                ? 'bg-primary text-white border-primary shadow-2xs'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-900',
            )}
          >
            7 ngày qua
          </button>

          <button
            type="button"
            onClick={() => handlePresetChange('30d')}
            className={cn(
              'px-2.5 py-1 text-xs rounded-lg font-medium transition-colors border',
              filter.timePreset === '30d'
                ? 'bg-primary text-white border-primary shadow-2xs'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-900',
            )}
          >
            30 ngày qua
          </button>

          <button
            type="button"
            onClick={() => handlePresetChange('custom')}
            className={cn(
              'px-2.5 py-1 text-xs rounded-lg font-medium transition-colors border',
              filter.timePreset === 'custom'
                ? 'bg-primary text-white border-primary shadow-2xs'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100 hover:text-slate-900',
            )}
          >
            Tùy chọn ngày...
          </button>
        </div>

        {/* Khối chọn ngày Từ - Đến khi chọn Tùy chọn ngày */}
        {filter.timePreset === 'custom' && (
          <div className="flex flex-wrap items-center gap-2 w-full sm:w-auto animate-in fade-in-50 duration-150">
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-slate-500 whitespace-nowrap">Từ:</span>
              <DatePicker
                aria-label="Từ ngày"
                value={filter.fromUtc}
                onValueChange={(val) => onFilterChange({ ...filter, fromUtc: val })}
                className="w-48 h-8 px-2.5 text-xs rounded-md border border-slate-200 bg-white focus:outline-none focus:ring-1 focus:ring-primary text-slate-700"
              />
            </div>
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-slate-500 whitespace-nowrap">Đến:</span>
              <DatePicker
                aria-label="Đến ngày"
                value={filter.toUtc}
                onValueChange={(val) => onFilterChange({ ...filter, toUtc: val })}
                className="w-48 h-8 px-2.5 text-xs rounded-md border border-slate-200 bg-white focus:outline-none focus:ring-1 focus:ring-primary text-slate-700"
              />
            </div>
          </div>
        )}
      </div>
    </form>
  );
};
