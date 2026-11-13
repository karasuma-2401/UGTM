import React from 'react';
import { Server, Copy, Clock, Inbox } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { Badge } from '@/components/ui/badge';
import { StatusBadge } from '@/components/ui/status-badge';
import { Avatar } from '@/components/ui/avatar';
import { InfoCell } from '@/components/ui/info-cell';
import { notify } from '@/shared/notifications';
import {
  formatAuditShortTime,
  formatAuditRelativeTime,
  formatAuditAction,
  formatTargetType,
} from '../utils/parseAuditDiff';
import type { AuditLogItem, AuditLogSortField } from '../types/auditLog.types';

interface AuditLogTableProps {
  logs: AuditLogItem[];
  selectedId: number | null;
  onSelect: (log: AuditLogItem) => void;
  onViewDetails?: (log: AuditLogItem) => void;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  sort?: { field: AuditLogSortField; direction: 'asc' | 'desc' };
  onSortChange?: (field: AuditLogSortField) => void;
  isLoading?: boolean;
}

function AuditOutcomeBadge({ outcome }: { outcome?: string | null }) {
  if (outcome === 'Success') return <StatusBadge tone="success">Thành công</StatusBadge>;
  if (outcome === 'Denied') return <StatusBadge tone="warning">Bị từ chối</StatusBadge>;
  if (outcome === 'Failed') return <StatusBadge tone="error">Thất bại</StatusBadge>;
  return <StatusBadge tone="warning">{outcome || 'Chưa xác định'}</StatusBadge>;
}

function AuditLogRowActions({
  log,
  disabled = false,
  onViewDetails,
}: {
  log: AuditLogItem;
  disabled?: boolean;
  onViewDetails?: (log: AuditLogItem) => void;
}) {
  const handleCopyRequestId = async () => {
    try {
      await navigator.clipboard.writeText(log.requestId);
      notify.success('Đã sao chép Request ID.');
    } catch {
      notify.error('Không thể sao chép Request ID. Vui lòng thử lại.');
    }
  };

  return (
    <div className="flex items-center justify-end gap-1">
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled}
        onClick={() => onViewDetails?.(log)}
        aria-label={`Xem chi tiết sự kiện ${formatAuditAction(log.action)}`}
        className="h-8 rounded-md border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-sm hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900"
      >
        Xem
      </Button>

      <button
        type="button"
        disabled={disabled}
        onClick={() => void handleCopyRequestId()}
        aria-label={`Sao chép Request ID của sự kiện #${log.id}`}
        title="Sao chép Request ID"
        className="flex size-8 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800 focus-visible:outline-primary disabled:opacity-40"
      >
        <Copy className="size-4" aria-hidden="true" />
      </button>
    </div>
  );
}

export const AuditLogTable: React.FC<AuditLogTableProps> = ({
  logs,
  selectedId,
  onSelect,
  onViewDetails,
  pageNumber,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  sort,
  onSortChange,
  isLoading = false,
}) => {
  const resolveActor = (log: AuditLogItem) => {
    const isSystemActor = !log.actorFullName && !log.actorEmail;
    const actorName =
      log.actorFullName ||
      log.actorEmail ||
      (log.actorUserId ? 'Không tìm thấy tài khoản' : 'Chưa xác định');
    return { isSystemActor, actorName };
  };

  const columns: DataTableColumn<AuditLogItem, AuditLogSortField>[] = [
    {
      id: 'index',
      header: 'STT',
      widthClassName: 'w-14',
      align: 'center',
      headerClassName: 'whitespace-nowrap px-3 py-3.5',
      cellClassName: 'px-3 py-3 text-xs tabular-nums text-slate-400',
      skeleton: { variant: 'number' },
      cell: (_log, index) => (pageNumber - 1) * pageSize + index + 1,
    },
    {
      id: 'timestamp',
      header: 'Thời gian',
      sortKey: 'timestamp',
      widthClassName: 'w-44',
      headerClassName: 'whitespace-nowrap px-4 py-3.5',
      cellClassName: 'px-4 py-3 whitespace-nowrap',
      skeleton: { variant: 'text' },
      cell: (log) => {
        const relativeTime = formatAuditRelativeTime(log.timestamp);
        return (
          <div>
            <div className="flex items-center gap-1.5 text-xs font-medium text-slate-800 sm:text-sm">
              <Clock className="size-3.5 shrink-0 text-slate-400" />
              <span className="tabular-nums">{formatAuditShortTime(log.timestamp)}</span>
            </div>
            {relativeTime && (
              <div className="mt-0.5 ml-5 text-xs text-slate-400">{relativeTime}</div>
            )}
          </div>
        );
      },
    },
    {
      id: 'actor',
      header: 'Người thực hiện',
      sortKey: 'actor',
      headerClassName: 'px-4 py-3.5',
      cellClassName: 'px-4 py-3',
      skeleton: { variant: 'person' },
      cell: (log) => {
        const { isSystemActor, actorName } = resolveActor(log);
        return (
          <InfoCell
            leading={
              isSystemActor ? (
                <span
                  aria-hidden="true"
                  className="flex size-9 shrink-0 items-center justify-center rounded-full border border-slate-200/80 bg-slate-100 text-slate-500 ring-2 ring-white"
                >
                  <Server className="size-4" />
                </span>
              ) : (
                <Avatar name={actorName} className="size-9 text-xs" />
              )
            }
            title={
              <span className="min-w-0 truncate font-semibold text-slate-900" title={actorName}>
                {actorName}
              </span>
            }
            titleClassName="flex-nowrap"
            description={
              <span
                className="truncate text-xs text-slate-500"
                title={log.actorEmail || undefined}
              >
                {log.actorEmail || (log.actorUserId ? log.actorUserId : 'Không có thông tin tác nhân')}
              </span>
            }
            descriptionClassName="truncate"
          />
        );
      },
    },
    {
      id: 'actionTarget',
      header: 'Thao tác & Đối tượng',
      sortKey: 'action',
      widthClassName: 'w-72',
      headerClassName: 'px-4 py-3.5',
      cellClassName: 'px-4 py-3',
      skeleton: { variant: 'badge' },
      cell: (log) => {
        const actionTitle = formatAuditAction(log.action);
        const targetLabel = formatTargetType(log.targetType);
        return (
          <div className="space-y-1">
            <div className="truncate text-xs font-semibold text-slate-800 sm:text-sm" title={actionTitle}>
              {actionTitle}
            </div>
            {log.targetType && (
              <div className="flex flex-wrap items-center gap-1.5">
                <Badge
                  variant="secondary"
                  className="border border-slate-200/80 bg-slate-100 px-1.5 py-0 text-xs font-normal text-slate-600"
                >
                  {targetLabel}
                </Badge>
                {log.targetId && (
                  <span
                    className="max-w-[130px] truncate font-mono text-xs text-slate-400"
                    title={log.targetId}
                  >
                    #{log.targetId}
                  </span>
                )}
              </div>
            )}
          </div>
        );
      },
    },
    {
      id: 'outcome',
      header: 'Kết quả',
      sortKey: 'outcome',
      widthClassName: 'w-36',
      headerClassName: 'whitespace-nowrap px-4 py-3.5',
      cellClassName: 'px-4 py-3',
      skeleton: { variant: 'badge' },
      cell: (log) => <AuditOutcomeBadge outcome={log.outcome} />,
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-36',
      align: 'center',
      headerClassName: 'whitespace-nowrap px-4 py-3.5',
      cellClassName: 'px-3 py-3 text-right',
      skeleton: { variant: 'actions', actionCount: 2 },
      cell: (log) => (
        <AuditLogRowActions log={log} disabled={isLoading} onViewDetails={onViewDetails} />
      ),
    },
  ];

  return (
    <DataTable<AuditLogItem, AuditLogSortField>
      ariaLabel="Bảng nhật ký thao tác hệ thống"
      data={logs}
      columns={columns}
      getRowId={(log) => String(log.id)}
      selectedRowId={selectedId !== null ? String(selectedId) : null}
      onRowSelect={(_id, log) => onSelect(log)}
      onRowOpen={onViewDetails}
      sort={sort}
      onSortChange={onSortChange}
      loading={isLoading && logs.length === 0}
      busy={isLoading && logs.length > 0}
      tableClassName="min-w-[960px]"
      rowClassName="h-[72px]"
      empty={{
        icon: <Inbox className="size-8" />,
        title: 'Không tìm thấy nhật ký thao tác',
        description:
          'Không có sự kiện nào khớp với tiêu chí tìm kiếm hoặc khoảng thời gian đã chọn. Hãy thử điều chỉnh hoặc đặt lại bộ lọc.',
      }}
      pagination={{
        pageNumber,
        pageSize,
        totalCount,
        onPageChange,
        onPageSizeChange,
        itemLabel: 'sự kiện hiển thị',
      }}
      renderMobileItem={(log) => {
        const { isSystemActor, actorName } = resolveActor(log);
        const actionTitle = formatAuditAction(log.action);
        const targetLabel = formatTargetType(log.targetType);
        return (
          <div className="space-y-3">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0 space-y-1">
                <p className="text-sm font-semibold text-slate-900">{actionTitle}</p>
                <p className="flex items-center gap-1.5 text-xs text-slate-500">
                  <Clock className="size-3.5 shrink-0 text-slate-400" />
                  <span className="tabular-nums">{formatAuditShortTime(log.timestamp)}</span>
                </p>
              </div>
              <AuditOutcomeBadge outcome={log.outcome} />
            </div>
            <div className="flex items-center justify-between gap-2 border-t border-slate-100 pt-2.5">
              <div className="flex min-w-0 items-center gap-2">
                {isSystemActor ? (
                  <Server className="size-4 shrink-0 text-slate-400" />
                ) : (
                  <Avatar name={actorName} className="size-6 text-xs" />
                )}
                <span className="truncate text-xs font-medium text-slate-700">{actorName}</span>
                {log.targetType && (
                  <Badge variant="secondary" className="shrink-0 text-xs font-normal">
                    {targetLabel}
                  </Badge>
                )}
              </div>
              <AuditLogRowActions log={log} disabled={isLoading} onViewDetails={onViewDetails} />
            </div>
          </div>
        );
      }}
    />
  );
};
