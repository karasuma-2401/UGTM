import { useMemo } from 'react';
import { AlertTriangle, CalendarDays, MapPin } from 'lucide-react';
import { AvatarGroup, type AvatarGroupItem } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type { CouncilApprovalSortField } from '../../hooks/useCouncilApproval';
import type { CouncilDraftItemDto } from '../../types/councilApproval.types';
import {
  formatViDate,
  type CouncilReadinessResult,
} from '../../utils/councilApprovalReadiness';
import { CouncilApprovalFilterBar } from './CouncilApprovalFilterBar';
import type { CouncilReadinessFilter } from '../../types/councilApproval.types';

interface CouncilApprovalTableProps {
  items: CouncilDraftItemDto[];
  totalCount: number;
  readinessMap: Map<string, CouncilReadinessResult>;
  selectedCouncilId: string | null;
  onSelectCouncilId: (id: string) => void;
  sort: { field: CouncilApprovalSortField; direction: 'asc' | 'desc' };
  onSortChange: (field: CouncilApprovalSortField) => void;
  pageNumber: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  keyword: string;
  onKeywordChange: (val: string) => void;
  readinessFilter: CouncilReadinessFilter;
  onReadinessFilterChange: (val: CouncilReadinessFilter) => void;
  hasActiveFilters: boolean;
  onResetFilters: () => void;
  isLoading?: boolean;
  isFetching?: boolean;
  error?: { message: string; onRetry?: () => void } | null;
}

function buildAvatarItems(row: CouncilDraftItemDto): AvatarGroupItem[] {
  const items: AvatarGroupItem[] = [];
  if (row.chairman) {
    items.push({
      id: `chair-${row.chairman.userId}`,
      name: row.chairman.fullName,
      userCode: 'Chủ tịch',
    });
  }
  if (row.secretary) {
    items.push({
      id: `sec-${row.secretary.userId}`,
      name: row.secretary.fullName,
      userCode: 'Thư ký',
    });
  }
  for (const member of row.members) {
    items.push({
      id: `mem-${member.userId}`,
      name: member.fullName,
      userCode: member.isReviewer ? 'GVPB kiêm UV' : 'Ủy viên',
    });
  }
  return items;
}

function getStatusBadgeProps(
  row: CouncilDraftItemDto,
  readiness?: CouncilReadinessResult,
): { tone: StatusTone; label: string } {
  if (row.status === 'Completed') {
    return { tone: 'neutral', label: 'Đã hoàn thành' };
  }
  if (row.status === 'Active') {
    return { tone: 'success', label: 'Đã phê duyệt' };
  }
  if (readiness?.category === 'ready') {
    return { tone: 'info', label: 'Sẵn sàng duyệt' };
  }
  if (!row.isSubmittedToAcademicAffairs) {
    return { tone: 'warning', label: 'Chờ Khoa nộp' };
  }
  return { tone: 'error', label: 'Chưa đủ điều kiện' };
}

export function CouncilApprovalTable({
  items,
  totalCount,
  readinessMap,
  selectedCouncilId,
  onSelectCouncilId,
  sort,
  onSortChange,
  pageNumber,
  pageSize,
  onPageChange,
  onPageSizeChange,
  keyword,
  onKeywordChange,
  readinessFilter,
  onReadinessFilterChange,
  hasActiveFilters,
  onResetFilters,
  isLoading = false,
  isFetching = false,
  error = null,
}: CouncilApprovalTableProps) {
  const columns = useMemo<DataTableColumn<CouncilDraftItemDto, CouncilApprovalSortField>[]>(
    () => [
      {
        id: 'councilCode',
        header: 'Hội đồng',
        sortKey: 'councilCode',
        headerClassName: 'px-3 sm:px-4',
        cellClassName: 'px-3 sm:px-4',
        skeleton: { variant: 'topic' },
        cell: (row) => (
          <div className="min-w-0 space-y-0.5">
            <div className="flex items-center gap-1.5 min-w-0">
              <span className="font-semibold text-ink-heading truncate tabular-nums">
                {row.councilCode}
              </span>
              {row.decisionNumber && (
                <span className="inline-flex items-center rounded-md bg-tone-success-bg px-1.5 py-0.5 text-[11px] font-medium text-tone-success-text border border-tone-success-border tabular-nums truncate max-w-[110px]">
                  {row.decisionNumber}
                </span>
              )}
            </div>
            <p className="text-xs text-ink-muted truncate">
              {row.groupCodes.length > 0 ? `${row.groupCodes.length} nhóm bảo vệ` : 'Chưa gán nhóm'}
            </p>
          </div>
        ),
      },
      {
        id: 'defenseDate',
        header: 'Lịch & Phòng',
        sortKey: 'defenseDate',
        headerClassName: 'px-2.5 sm:px-3',
        cellClassName: 'px-2.5 sm:px-3',
        skeleton: { variant: 'text' },
        cell: (row) => (
          <div className="min-w-0 space-y-0.5">
            <div className="flex items-center gap-1.5 text-xs font-medium text-ink-heading tabular-nums">
              <span className="whitespace-nowrap">{formatViDate(row.defenseDate)}</span>
              <span className="text-ink-subtle">·</span>
              <span className="truncate">{row.room}</span>
              {row.hasRoomConflict && (
                <span title="Phòng thi đang bị trùng lịch với hội đồng khác">
                  <AlertTriangle className="size-3.5 text-tone-warning-icon shrink-0" />
                </span>
              )}
            </div>
            <p className="text-xs text-ink-muted truncate">{row.timeSlot}</p>
          </div>
        ),
      },
      {
        id: 'members',
        header: 'Thành viên',
        widthClassName: 'w-[108px]',
        headerClassName: 'px-2.5 sm:px-2.5 whitespace-nowrap',
        cellClassName: 'px-2.5 sm:px-2.5',
        skeleton: { variant: 'person' },
        cell: (row) => {
          const avatarItems = buildAvatarItems(row);
          if (avatarItems.length === 0) {
            return <span className="text-xs text-ink-subtle">—</span>;
          }
          return (
            <div className="flex items-center">
              <AvatarGroup items={avatarItems} max={3} size="sm" ariaLabel={`Thành viên ${row.councilCode}`} />
            </div>
          );
        },
      },
      {
        id: 'status',
        header: 'Tình trạng',
        sortKey: 'status',
        align: 'right',
        widthClassName: 'w-[164px]',
        headerClassName: 'px-3 sm:px-3.5 whitespace-nowrap',
        cellClassName: 'px-3 sm:px-3.5',
        skeleton: { variant: 'badge' },
        cell: (row) => {
          const badge = getStatusBadgeProps(row, readinessMap.get(row.id));
          return (
            <div className="flex justify-end">
              <StatusBadge tone={badge.tone}>{badge.label}</StatusBadge>
            </div>
          );
        },
      },
    ],
    [readinessMap],
  );

  return (
    <Card className="rounded-2xl border border-line/80 bg-surface-card shadow-xs overflow-hidden">
      <div className="p-4 sm:p-5 border-b border-line-subtle space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-semibold text-ink-heading">
              Hồ sơ đã nộp &amp; quyết định
            </h2>
            <span className="inline-flex items-center rounded-full bg-surface-muted px-2 py-0.5 text-xs font-medium text-ink-secondary tabular-nums">
              {totalCount}
            </span>
          </div>
          <span className="text-xs text-ink-subtle hidden sm:inline">
            Nhấp một dòng để kiểm tra điều kiện và phê duyệt
          </span>
        </div>

        <CouncilApprovalFilterBar
          keyword={keyword}
          onKeywordChange={onKeywordChange}
          readinessFilter={readinessFilter}
          onReadinessFilterChange={onReadinessFilterChange}
          hasActiveFilters={hasActiveFilters}
          onReset={onResetFilters}
        />
      </div>

      <DataTable
        data={items}
        columns={columns}
        getRowId={(row) => row.id}
        ariaLabel="Danh sách hội đồng chờ phê duyệt và đã duyệt"
        selectedRowId={selectedCouncilId}
        onRowSelect={(id) => onSelectCouncilId(id)}
        sort={sort}
        onSortChange={onSortChange}
        loading={isLoading}
        busy={isFetching}
        error={error}
        empty={{
          title: hasActiveFilters
            ? 'Không tìm thấy hội đồng phù hợp'
            : 'Chưa có hồ sơ hội đồng nào được gửi',
          description: hasActiveFilters
            ? 'Thử thay đổi từ khóa tìm kiếm, đợt khóa luận hoặc trạng thái thẩm định.'
            : 'Hội đồng sẽ hiển thị khi Khoa gửi hồ sơ lên Phòng Đào tạo.',
          action: hasActiveFilters ? (
            <Button variant="outline" size="sm" onClick={onResetFilters}>
              Đặt lại bộ lọc
            </Button>
          ) : undefined,
        }}
        pagination={{
          pageNumber,
          pageSize,
          totalCount,
          onPageChange,
          onPageSizeChange,
          itemLabel: 'hội đồng',
        }}
        renderMobileItem={(row) => {
          const isSelected = row.id === selectedCouncilId;
          const badge = getStatusBadgeProps(row, readinessMap.get(row.id));
          const avatarItems = buildAvatarItems(row);

          return (
            <button
              type="button"
              onClick={() => onSelectCouncilId(row.id)}
              className={cn(
                'w-full text-left p-4 transition-colors space-y-2.5 cursor-pointer',
                isSelected ? 'bg-tone-info-bg/70' : 'hover:bg-surface-hover/60',
              )}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold text-ink-heading tabular-nums">
                      {row.councilCode}
                    </span>
                    {row.decisionNumber && (
                      <span className="text-xs font-medium text-tone-success-text tabular-nums">
                        {row.decisionNumber}
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-ink-muted mt-0.5">
                    {row.facultyName} ·{' '}
                    {row.groupCodes.length > 0 ? `${row.groupCodes.length} nhóm` : 'Chưa gán nhóm'}
                  </p>
                </div>
                <StatusBadge tone={badge.tone}>{badge.label}</StatusBadge>
              </div>

              <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-ink-body">
                <span className="inline-flex items-center gap-1.5 tabular-nums">
                  <CalendarDays className="size-3.5 text-ink-subtle" />
                  {formatViDate(row.defenseDate)} · {row.timeSlot}
                </span>
                <span className="inline-flex items-center gap-1">
                  <MapPin className="size-3.5 text-ink-subtle" />
                  {row.room}
                </span>
              </div>

              {avatarItems.length > 0 && (
                <div className="flex items-center justify-between pt-0.5">
                  <AvatarGroup items={avatarItems} max={4} size="sm" />
                  <span className="text-xs font-medium text-primary">
                    {isSelected ? 'Đang chọn' : 'Xem chi tiết'}
                  </span>
                </div>
              )}
            </button>
          );
        }}
      />
    </Card>
  );
}
