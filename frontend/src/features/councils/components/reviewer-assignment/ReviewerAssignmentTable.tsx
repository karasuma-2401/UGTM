import type { ReactNode } from 'react';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { StatusBadge } from '@/components/ui/status-badge';
import type { ReviewerAssignmentEligibilityItemDto } from '../../types/reviewerAssignment.types';

interface ReviewerAssignmentTableProps {
  items: ReviewerAssignmentEligibilityItemDto[];
  selectedGroupId: string | null;
  onSelectGroupId: (groupId: string) => void;
  isLoading?: boolean;
  isBusy?: boolean;
  error?: { message: string; onRetry: () => void } | null;
  filters?: ReactNode;
  pendingCount: number;
}

export function ReviewerAssignmentTable({
  items,
  selectedGroupId,
  onSelectGroupId,
  isLoading,
  isBusy,
  error,
  filters,
  pendingCount,
}: ReviewerAssignmentTableProps) {
  const status = (item: ReviewerAssignmentEligibilityItemDto) => (
    <StatusBadge tone={item.hasActiveAssignment ? 'success' : 'warning'}>
      {item.hasActiveAssignment ? 'Đã phân công' : 'Chờ phân công'}
    </StatusBadge>
  );
  const action = (item: ReviewerAssignmentEligibilityItemDto) => (
    <Button
      variant="outline"
      size="sm"
      className="rounded-md"
      disabled={isBusy}
      aria-label={`Xem hồ sơ nhóm ${item.groupId.slice(0, 8).toUpperCase()}`}
      onClick={() => onSelectGroupId(item.groupId)}
    >
      Xem
    </Button>
  );
  const columns: DataTableColumn<ReviewerAssignmentEligibilityItemDto>[] = [
    {
      id: 'topic',
      header: 'Nhóm / đề tài',
      skeleton: { variant: 'topic' },
      cell: (item) => (
        <div className="space-y-1">
          <p className="line-clamp-2 font-semibold text-ink-primary" title={item.topicTitleVi}>
            {item.topicTitleVi}
          </p>
          <p className="flex flex-wrap gap-x-1 text-xs text-ink-muted" title={item.groupId}>
            <span className="whitespace-nowrap">Nhóm {item.groupId.slice(0, 8).toUpperCase()}</span>
            <span className="whitespace-nowrap">· Báo cáo v{item.reportVersionNo}</span>
          </p>
        </div>
      ),
    },
    {
      id: 'advisor',
      header: 'GV hướng dẫn',
      widthClassName: 'w-[150px] 2xl:w-[180px]',
      skeleton: { variant: 'person' },
      cell: (item) => (
        <div className="flex items-center gap-2.5">
          <Avatar
            name={item.advisorFullName || '?'}
            className="hidden size-8 text-[10px] 2xl:flex"
          />
          <span className="break-words text-ink-body">
            {item.advisorFullName || 'Chưa có thông tin'}
          </span>
        </div>
      ),
    },
    {
      id: 'status',
      header: 'Trạng thái',
      widthClassName: 'w-[146px]',
      headerClassName: 'whitespace-nowrap',
      skeleton: { variant: 'badge' },
      cell: status,
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-[76px]',
      align: 'right',
      headerClassName: 'whitespace-nowrap',
      skeleton: { variant: 'actions' },
      cell: action,
    },
  ];

  return (
    <Card className="min-w-0 overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line-subtle px-5 py-4">
        <h2 className="text-base font-semibold text-ink-primary">Danh sách hồ sơ</h2>
        <StatusBadge tone="warning">{pendingCount} chờ phân công</StatusBadge>
      </div>
      {filters}
      <DataTable
        tableClassName="[&_th]:px-3 [&_td]:px-3"
        data={items}
        columns={columns}
        getRowId={(item) => item.groupId}
        ariaLabel="Hồ sơ phân công phản biện"
        selectedRowId={selectedGroupId}
        onRowSelect={onSelectGroupId}
        loading={isLoading}
        busy={isBusy}
        error={error}
        empty={{
          title: 'Không có hồ sơ phù hợp',
          description: 'Thử đổi bộ lọc hoặc kiểm tra hồ sơ đã được GVHD đồng ý chuyển phản biện.',
        }}
        renderMobileItem={(item) => (
          <div className="space-y-3">
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs font-medium text-ink-muted">
                Nhóm {item.groupId.slice(0, 8).toUpperCase()}
              </span>
              {status(item)}
            </div>
            <p className="font-semibold text-ink-primary">{item.topicTitleVi}</p>
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm text-ink-muted">
                GVHD: {item.advisorFullName || 'Chưa có thông tin'}
              </p>
              {action(item)}
            </div>
            {selectedGroupId === item.groupId && (
              <p className="text-xs font-medium text-brand-ink">Đang chọn · Phân công ở bên dưới</p>
            )}
          </div>
        )}
      />
      {!isLoading && !error && (
        <div
          className="border-t border-line-subtle px-5 py-3 text-xs text-ink-muted"
          aria-live="polite"
        >
          {items.length} hồ sơ hiển thị
        </div>
      )}
    </Card>
  );
}
