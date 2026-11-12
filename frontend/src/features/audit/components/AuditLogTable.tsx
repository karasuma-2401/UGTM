import React from 'react';
import {
  Server,
  Eye,
  Copy,
  Clock,
  Inbox,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { TablePagination } from '@/components/ui/table-pagination';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { Badge } from '@/components/ui/badge';
import { StatusBadge } from '@/components/ui/status-badge';
import { Avatar } from '@/components/ui/avatar';
import { InfoCell } from '@/components/ui/info-cell';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { SelectionIndicator } from '@/components/ui/selection-indicator';
import { notify } from '@/shared/notifications';
import {
  formatAuditShortTime,
  formatAuditRelativeTime,
  formatAuditAction,
  formatTargetType,
} from '../utils/parseAuditDiff';
import type { AuditLogItem } from '../types/auditLog.types';

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
  isLoading?: boolean;
}

function AuditLogRowActions({
  log,
  onViewDetails,
}: {
  log: AuditLogItem;
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
        variant="ghost"
        size="sm"
        onClick={() => onViewDetails?.(log)}
        leftIcon={<Eye className="size-3.5" />}
        aria-label={`Xem chi tiết sự kiện ${formatAuditAction(log.action)}`}
        className="h-8 px-2.5 text-xs text-primary font-medium hover:bg-blue-50/80"
      >
        Chi tiết
      </Button>

      <button
        type="button"
        onClick={() => void handleCopyRequestId()}
        aria-label={`Sao chép Request ID của sự kiện #${log.id}`}
        title="Sao chép Request ID"
        className="flex size-8 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-800 focus-visible:outline-primary"
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
  isLoading = false,
}) => {
  return (
    <div className="relative isolate overflow-hidden space-y-0">
      {/* Vạch chỉ báo hàng đang chọn trượt mượt mà tương tự UsersTable */}
      <SelectionIndicator
        activeKey={selectedId}
        selector='tr[aria-selected="true"]'
        variant="rail"
        className="z-20 rounded-r bg-primary"
      />

      <div className="overflow-x-auto min-h-[380px]">
        {isLoading ? (
          <TableSkeleton
            rows={pageSize > 15 ? 12 : pageSize}
            columns={[
              { label: 'STT', width: '48px', variant: 'number' },
              { label: 'Thời gian', width: '160px', variant: 'text' },
              { label: 'Người thực hiện', width: '280px', variant: 'person' },
              { label: 'Thao tác & Đối tượng', width: '320px', variant: 'badge' },
              { label: 'Kết quả', width: '130px', variant: 'badge' },
              { label: 'Thao tác', width: '140px', variant: 'actions' },
            ]}
          />
        ) : logs.length === 0 ? (
          <div className="flex flex-col items-center justify-center p-12 text-center">
            <div className="size-12 rounded-2xl bg-slate-100 flex items-center justify-center text-slate-400 mb-3">
              <Inbox className="size-6" />
            </div>
            <h4 className="text-sm font-semibold text-slate-800">
              Không tìm thấy nhật ký thao tác
            </h4>
            <p className="text-xs text-slate-500 max-w-sm mt-1">
              Không có sự kiện nào khớp với tiêu chí tìm kiếm hoặc khoảng thời gian đã chọn. Hãy thử điều chỉnh hoặc đặt lại bộ lọc.
            </p>
          </div>
        ) : (
          <table className="block w-full text-left text-sm md:table md:min-w-[960px] md:table-fixed">
            <colgroup className="hidden md:table-column-group">
              <col className="w-14" />
              <col className="w-44" />
              <col />
              <col className="w-72" />
              <col className="w-36" />
              <col className="w-[140px]" />
            </colgroup>

            <thead className="hidden md:table-header-group h-11 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600 font-bold">
              <tr>
                <th scope="col" className="w-14 px-3 py-3 text-center">
                  STT
                </th>
                <th scope="col" className="px-4 py-3">
                  Thời gian
                </th>
                <th scope="col" className="px-4 py-3">
                  Người thực hiện
                </th>
                <th scope="col" className="px-4 py-3">
                  Thao tác & Đối tượng
                </th>
                <th scope="col" className="px-4 py-3">
                  Kết quả
                </th>
                <th scope="col" className="px-4 py-3 text-center">
                  Thao tác
                </th>
              </tr>
            </thead>

            <tbody className="block divide-y divide-slate-100 md:table-row-group">
              {logs.map((log, index) => {
                const isSelected = selectedId === log.id;
                const isSystemActor = !log.actorFullName && !log.actorEmail;
                const actorName = log.actorFullName || log.actorEmail || (log.actorUserId ? 'Không tìm thấy tài khoản' : 'Chưa xác định');
                const actionTitle = formatAuditAction(log.action);
                const targetLabel = formatTargetType(log.targetType);
                const relativeTime = formatAuditRelativeTime(log.timestamp);

                return (
                  <SelectableTableRow
                    key={log.id}
                    data-selection-item
                    hideSelectionMarker
                    selected={isSelected}
                    onSelect={() => onSelect(log)}
                    onDoubleClick={(event) => {
                      if (
                        (event.target as Element).closest(
                          'button, a, input, select, textarea, [role="button"]',
                        )
                      ) {
                        return;
                      }
                      onViewDetails?.(log);
                    }}
                    title="Nhấp đúp để xem chi tiết sự kiện"
                    className="grid grid-cols-[1fr_auto] items-center gap-x-3 px-4 py-3 md:table-row md:h-[72px] md:p-0 transition-colors cursor-pointer"
                  >
                    {/* Cột 1: STT (Desktop only) */}
                    <td className="hidden px-3 py-3 text-center text-xs tabular-nums text-slate-400 md:table-cell">
                      {(pageNumber - 1) * pageSize + index + 1}
                    </td>

                    {/* Cột 2: Thời gian */}
                    <td className="hidden px-4 py-3 whitespace-nowrap md:table-cell">
                      <div className="font-medium text-xs sm:text-sm text-slate-800 flex items-center gap-1.5">
                        <Clock className="size-3.5 text-slate-400 shrink-0" />
                        <span>{formatAuditShortTime(log.timestamp)}</span>
                      </div>
                      {relativeTime && (
                        <div className="text-[11px] text-slate-400 mt-0.5 ml-5">
                          {relativeTime}
                        </div>
                      )}
                    </td>

                    {/* Cột 3: Người thực hiện (InfoCell) */}
                    <td className="block min-w-0 py-1 md:table-cell md:px-4 md:py-3">
                      <InfoCell
                        leading={
                          isSystemActor ? (
                            <span
                              aria-hidden="true"
                              className="size-9 rounded-xl bg-slate-100 border border-slate-200/80 flex items-center justify-center text-slate-500 shrink-0"
                            >
                              <Server className="size-4" />
                            </span>
                          ) : (
                            <Avatar name={actorName} className="size-9 rounded-xl text-xs" />
                          )
                        }
                        title={
                          <span className="min-w-0 truncate text-slate-900 font-semibold" title={actorName}>
                            {actorName}
                          </span>
                        }
                        titleClassName="flex-nowrap"
                        description={
                          <span className="truncate text-slate-400 text-xs" title={log.actorEmail || undefined}>
                            {log.actorEmail || (log.actorUserId ? log.actorUserId : 'Không có thông tin tác nhân')}
                          </span>
                        }
                        descriptionClassName="truncate"
                      />
                    </td>

                    {/* Cột 4: Thao tác & Đối tượng */}
                    <td className="block min-w-0 py-1 md:table-cell md:px-4 md:py-3">
                      <div className="space-y-1">
                        <div className="font-semibold text-slate-800 text-xs sm:text-sm truncate" title={actionTitle}>
                          {actionTitle}
                        </div>
                        {log.targetType && (
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <Badge
                              variant="secondary"
                              className="text-[10px] px-1.5 py-0 font-normal bg-slate-100 text-slate-600 border border-slate-200/80"
                            >
                              {targetLabel}
                            </Badge>
                            {log.targetId && (
                              <span
                                className="text-[11px] font-mono text-slate-400 truncate max-w-[130px]"
                                title={log.targetId}
                              >
                                #{log.targetId}
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                    </td>

                    {/* Cột 5: Kết quả */}
                    <td className="block min-w-0 py-1 md:table-cell md:px-4 md:py-3">
                      {log.outcome === 'Success' ? (
                        <StatusBadge tone="success">Thành công</StatusBadge>
                      ) : log.outcome === 'Denied' ? (
                        <StatusBadge tone="warning">Bị từ chối</StatusBadge>
                      ) : log.outcome === 'Failed' ? (
                        <StatusBadge tone="error">Thất bại</StatusBadge>
                      ) : (
                        <StatusBadge tone="warning">{log.outcome || 'Chưa xác định'}</StatusBadge>
                      )}
                    </td>

                    {/* Cột 6: Thao tác */}
                    <td className="block py-1 text-right md:table-cell md:px-3 md:py-3">
                      <AuditLogRowActions log={log} onViewDetails={onViewDetails} />
                    </td>
                  </SelectableTableRow>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      <TablePagination
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
        itemLabel="sự kiện hiển thị"
        isLoading={isLoading}
      />
    </div>
  );
};
