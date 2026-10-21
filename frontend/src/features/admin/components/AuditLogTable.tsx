import React from 'react';
import { TablePagination } from '@/components/ui/table-pagination';
import { Badge } from '@/components/ui/badge';
import {
  formatAuditShortTime,
  formatAuditAction,
} from '../utils/parseAuditDiff';
import type { AuditLogItem } from '../types/auditLog.types';
import { cn } from '@/lib/utils';

interface AuditLogTableProps {
  logs: AuditLogItem[];
  selectedId: number | null;
  onSelect: (log: AuditLogItem) => void;
  onViewDetails?: (log: AuditLogItem) => void;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  isLoading?: boolean;
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
  isLoading = false,
}) => {
  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden space-y-0">
      <div className="p-4 sm:p-5 border-b border-slate-100 flex items-center justify-between">
        <h3 className="text-base font-bold text-slate-900 tracking-tight">
          Sự kiện hệ thống
        </h3>
        {totalCount > 0 && (
          <span className="text-xs font-medium text-slate-500">
            {totalCount} sự kiện ghi nhận
          </span>
        )}
      </div>

      <div className="overflow-x-auto min-h-[280px]">
        <table className="w-full text-left text-sm border-collapse">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50/60 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
              <th scope="col" className="px-5 py-3.5 w-36">
                Thời gian
              </th>
              <th scope="col" className="px-5 py-3.5 w-56">
                Người thực hiện
              </th>
              <th scope="col" className="px-5 py-3.5">
                Thao tác / Hồ sơ
              </th>
              <th scope="col" className="px-5 py-3.5 w-32 text-right">
                Chi tiết
              </th>
            </tr>
          </thead>

          <tbody className="divide-y divide-slate-100">
            {isLoading ? (
              <tr>
                <td colSpan={4} className="px-5 py-12 text-center text-slate-400">
                  <div className="flex flex-col items-center gap-2">
                    <div className="size-6 border-2 border-primary border-t-transparent rounded-full animate-spin" />
                    <span className="text-xs">Đang tải nhật ký thao tác...</span>
                  </div>
                </td>
              </tr>
            ) : logs.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-5 py-12 text-center text-slate-400 text-sm">
                  Không tìm thấy bản ghi nhật ký phù hợp với bộ lọc hiện tại.
                </td>
              </tr>
            ) : (
              logs.map((log) => {
                const isSelected = selectedId === log.id;
                const actorName = log.actorFullName || log.actorEmail || 'Hệ thống';
                const actionTitle = formatAuditAction(log.action);

                return (
                  <tr
                    key={log.id}
                    onClick={() => onSelect(log)}
                    className={cn(
                      'transition-colors cursor-pointer group',
                      isSelected
                        ? 'bg-blue-50/70 text-slate-900'
                        : 'hover:bg-slate-50/60 text-slate-700',
                    )}
                  >
                    <td className="relative px-5 py-4 whitespace-nowrap font-medium text-xs sm:text-sm text-slate-700">
                      {isSelected && (
                        <span
                          className="absolute inset-y-1.5 left-0 w-1 bg-primary rounded-r"
                          aria-hidden="true"
                        />
                      )}
                      {formatAuditShortTime(log.timestamp)}
                    </td>

                    <td className="px-5 py-4 whitespace-nowrap">
                      <div className="font-medium text-slate-800 text-xs sm:text-sm">
                        {actorName}
                      </div>
                      {log.actorEmail && log.actorFullName && (
                        <div className="text-[11px] text-slate-400 truncate max-w-[200px]">
                          {log.actorEmail}
                        </div>
                      )}
                    </td>

                    <td className="px-5 py-4">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-medium text-slate-800 text-xs sm:text-sm">
                          {actionTitle}
                        </span>
                        {log.outcome === 'Denied' && (
                          <Badge variant="warning" dot className="text-[10px] px-1.5 py-0">
                            Từ chối
                          </Badge>
                        )}
                        {log.outcome === 'Failed' && (
                          <Badge variant="error" dot className="text-[10px] px-1.5 py-0">
                            Thất bại
                          </Badge>
                        )}
                      </div>
                    </td>

                    <td className="px-5 py-4 whitespace-nowrap text-right">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          onSelect(log);
                          onViewDetails?.(log);
                        }}
                        className={cn(
                          'text-xs sm:text-sm font-semibold transition-colors focus:outline-none',
                          isSelected
                            ? 'text-primary underline'
                            : 'text-primary hover:underline group-hover:text-primary-hover',
                        )}
                      >
                        Xem chi tiết
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <TablePagination
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={onPageChange}
        itemLabel="hồ sơ hiển thị"
        isLoading={isLoading}
      />
    </div>
  );
};
