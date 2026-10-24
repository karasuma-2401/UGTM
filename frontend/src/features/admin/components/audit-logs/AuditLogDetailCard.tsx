import React from 'react';
import {
  formatAuditFullTime,
  formatAuditAction,
  parseAuditDiff,
} from '../../utils/parseAuditDiff';
import type { AuditLogItem } from '../../types/auditLog.types';

interface AuditLogDetailCardProps {
  log: AuditLogItem | null;
}

export const AuditLogDetailCard: React.FC<AuditLogDetailCardProps> = ({ log }) => {
  if (!log) {
    return (
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs flex flex-col items-center justify-center min-h-[220px] text-slate-400 text-sm">
        Chọn một sự kiện trong bảng để xem chi tiết
      </div>
    );
  }

  const diff = parseAuditDiff(log);
  const actionTitle = formatAuditAction(log.action);
  const actorName = log.actorFullName || 'Hệ thống';
  const actorSubtitle = log.actorEmail ? ` · ${log.actorEmail}` : '';

  return (
    <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4">
      {/* Tiêu đề Card */}
      <div>
        <h4 className="text-base font-bold text-slate-900 tracking-tight">
          Chi tiết sự kiện
        </h4>
      </div>

      <div className="space-y-3 text-xs divide-y divide-slate-100">
        <div className="pt-2 first:pt-0 space-y-0.5">
          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Sự kiện đang chọn
          </div>
          <div className="text-sm font-semibold text-slate-900">
            {actionTitle}
          </div>
        </div>

        <div className="pt-2 space-y-0.5">
          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Tác nhân
          </div>
          <div className="text-slate-800 font-medium">
            <span>{actorName}</span>
            <span className="text-slate-500 font-normal">{actorSubtitle}</span>
          </div>
        </div>

        <div className="pt-2 space-y-0.5">
          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Thời điểm
          </div>
          <div className="text-slate-800 font-medium">
            {formatAuditFullTime(log.timestamp)}
          </div>
        </div>

        <div className="pt-2 space-y-0.5">
          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Hồ sơ / Đối tượng
          </div>
          <div className="text-slate-800 font-medium font-mono text-[11px]">
            {log.targetType}: {log.targetId}
          </div>
        </div>

        <div className="pt-2 space-y-0.5">
          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Trước → Sau
          </div>
          <div className="text-slate-700 font-medium break-words">
            <span className="text-slate-500">{diff.beforeValue}</span>
            <span className="mx-1.5 text-primary font-bold">→</span>
            <span className="text-slate-900 font-semibold">{diff.afterValue}</span>
          </div>
        </div>

        {diff.reason && (
          <div className="pt-2 space-y-0.5">
            <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
              Lý do
            </div>
            <div className="text-slate-700 leading-relaxed">
              {diff.reason}
            </div>
          </div>
        )}

        <div className="pt-2 space-y-0.5">
          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Mã truy vết (Request ID)
          </div>
          <div className="text-slate-500 font-mono text-[11px] truncate" title={log.requestId}>
            {log.requestId}
          </div>
        </div>
      </div>
    </div>
  );
};

