import { useState } from 'react';
import { ChevronDown, Search } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PendingState } from '@/components/ui/pending-state';
import { Select } from '@/components/ui/select';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type { CouncilDraftItemDto } from '../../types/councilApproval.types';
import { councilPhase, phaseLabelFor, PHASE_LABEL, type CouncilPhase } from '../../utils/councilMemberWorkspace';
import { formatViDate } from '../../utils/councilApprovalReadiness';

export type PhaseFilter = 'all' | CouncilPhase;

const PHASE_TONE: Record<CouncilPhase, StatusTone> = {
  preparing: 'warning',
  submitted: 'info',
  approved: 'success',
};

function missingRoles(council: CouncilDraftItemDto): string[] {
  const missing: string[] = [];
  if (!council.chairman) missing.push('Chủ tịch');
  if (!council.secretary) missing.push('Thư ký');
  return missing;
}

interface BatchOption {
  id: string;
  label: string;
}

export function CouncilPickerPanel({
  councils,
  totalCount,
  selectedId,
  onSelect,
  batchId,
  onBatchChange,
  batchOptions,
  keyword,
  onKeywordChange,
  phaseFilter,
  onPhaseFilterChange,
  isLoading,
  error,
  onRetry,
}: {
  councils: CouncilDraftItemDto[];
  totalCount: number;
  selectedId: string | null;
  onSelect: (id: string) => void;
  batchId: string;
  onBatchChange: (id: string) => void;
  batchOptions: BatchOption[];
  keyword: string;
  onKeywordChange: (value: string) => void;
  phaseFilter: PhaseFilter;
  onPhaseFilterChange: (value: PhaseFilter) => void;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  // Trên màn hình nhỏ danh sách dài đẩy vùng làm việc xuống: thu gọn sau khi đã chọn.
  const [expanded, setExpanded] = useState(false);
  const selected = councils.find((item) => item.id === selectedId);
  const filtersActive = Boolean(keyword.trim()) || phaseFilter !== 'all';

  return (
    <Card className="min-w-0 overflow-visible p-4 sm:p-5">
      <div className="flex items-start justify-between gap-3 border-b border-line-subtle pb-4">
        <div>
          <h2 className="text-base font-semibold text-ink-heading">Danh sách hội đồng</h2>
          <p className="mt-0.5 text-sm text-ink-muted" aria-live="polite">
            {isLoading ? 'Đang tải…' : `${councils.length}/${totalCount} hội đồng`}
          </p>
        </div>
        <button
          type="button"
          aria-expanded={expanded}
          aria-controls="council-picker-body"
          onClick={() => setExpanded((value) => !value)}
          className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line-input px-3 text-sm font-medium text-ink-secondary hover:bg-surface-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary lg:hidden"
        >
          {expanded ? 'Thu gọn' : 'Đổi hội đồng'}
          <ChevronDown className={cn('size-4 transition-transform', expanded && 'rotate-180')} aria-hidden="true" />
        </button>
      </div>

      {selected && !expanded && (
        <p className="mt-3 rounded-xl bg-tone-info-bg px-3 py-2 text-sm text-tone-info-text lg:hidden">
          Đang chọn: <strong>{selected.councilCode}</strong> · {phaseLabelFor(selected)}
        </p>
      )}

      <div id="council-picker-body" className={cn('mt-4', !expanded && selected ? 'hidden lg:block' : 'block')}>
        <div className="grid gap-3">
          <div className="space-y-1.5">
            <p id="council-batch-label" className="text-sm font-medium text-ink-secondary">
              Đợt khóa luận
            </p>
            <Select<string>
              aria-label="Đợt khóa luận"
              aria-labelledby="council-batch-label"
              size="lg"
              value={batchId}
              options={[{ value: 'all', label: 'Tất cả đợt khóa luận' }, ...batchOptions.map((batch) => ({ value: batch.id, label: batch.label }))]}
              onChange={(value) => onBatchChange(value)}
              triggerClassName="text-sm"
              contentClassName="max-w-[min(90vw,26rem)]"
            />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="council-keyword" className="text-sm font-medium text-ink-secondary">
              Tìm hội đồng
            </label>
            <Input
              id="council-keyword"
              type="text"
              autoComplete="off"
              leftIcon={<Search />}
              value={keyword}
              onChange={(event) => onKeywordChange(event.target.value)}
              placeholder="Mã hội đồng, phòng hoặc mã nhóm"
              className="h-10 rounded-xl"
            />
          </div>
          <div className="space-y-1.5">
            <p id="council-phase-label" className="text-sm font-medium text-ink-secondary">
              Trạng thái hồ sơ
            </p>
            <Select<PhaseFilter>
              aria-label="Trạng thái hồ sơ"
              aria-labelledby="council-phase-label"
              size="lg"
              value={phaseFilter}
              options={[
                { value: 'all', label: 'Tất cả trạng thái' },
                { value: 'preparing', label: PHASE_LABEL.preparing },
                { value: 'submitted', label: PHASE_LABEL.submitted },
                { value: 'approved', label: 'Đã phê duyệt / hoàn thành' },
              ]}
              onChange={(value) => onPhaseFilterChange(value)}
              triggerClassName="text-sm"
            />
          </div>
        </div>

        {error && (
          <div className="mt-4">
            <InlineAlert
              title="Không tải được danh sách hội đồng"
              action={
                <button type="button" className="font-medium underline" onClick={onRetry}>
                  Thử lại
                </button>
              }
            >
              {error}
            </InlineAlert>
          </div>
        )}
        {isLoading && <PendingState className="mt-4" label="Đang tải danh sách hội đồng…" />}
        {!isLoading && !error && councils.length === 0 && (
          <p className="mt-4 rounded-xl border border-dashed border-line-input p-4 text-sm text-ink-muted">
            {totalCount === 0
              ? 'Đợt này chưa có hội đồng nào. Hãy chọn đợt khác hoặc kiểm tra lại với PĐT.'
              : filtersActive
                ? 'Không có hội đồng khớp bộ lọc hiện tại. Hãy xóa từ khóa hoặc chọn “Tất cả trạng thái”.'
                : 'Không có hội đồng phù hợp.'}
          </p>
        )}
        <ul
          className="mt-4 max-h-[min(60vh,34rem)] space-y-2 overflow-y-auto pr-1"
          aria-label="Danh sách hội đồng"
        >
          {councils.map((item) => {
            const isSelected = item.id === selectedId;
            const missing = item.status === 'Draft' && !item.isSubmittedToAcademicAffairs ? missingRoles(item) : [];
            return (
              <li key={item.id}>
                <button
                  type="button"
                  aria-pressed={isSelected}
                  onClick={() => {
                    onSelect(item.id);
                    setExpanded(false);
                  }}
                  className={cn(
                    'w-full rounded-xl border p-3 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary',
                    isSelected ? 'border-primary bg-tone-info-bg' : 'border-line/80 hover:bg-surface-subtle',
                  )}
                >
                  <span className="flex flex-wrap items-center justify-between gap-2">
                    <strong className="text-sm text-ink-heading">{item.councilCode}</strong>
                    <StatusBadge tone={item.status === 'Completed' ? 'neutral' : PHASE_TONE[councilPhase(item)]}>
                      {phaseLabelFor(item)}
                    </StatusBadge>
                  </span>
                  <span className="mt-1 block text-sm text-ink-muted">
                    {formatViDate(item.defenseDate)} · {item.timeSlot} · {item.room}
                  </span>
                  <span className="mt-0.5 block text-sm text-ink-muted">
                    {item.groupCodes.length} nhóm bảo vệ
                    {missing.length > 0 && (
                      <span className="text-tone-warning-text"> · Thiếu {missing.join(', ')}</span>
                    )}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </Card>
  );
}
