import React from 'react';
import { parseAuditDiff } from '../utils/parseAuditDiff';
import type { AuditLogItem } from '../types/auditLog.types';

interface AuditLogDiffCardProps {
  log: AuditLogItem | null;
}

export const AuditLogDiffCard: React.FC<AuditLogDiffCardProps> = ({ log }) => {
  if (!log) {
    return (
      <div className="bg-surface-card border border-line rounded-xl p-5 shadow-xs flex flex-col items-center justify-center min-h-[220px] text-ink-subtle text-sm">
        Chọn một sự kiện trong bảng để xem chi tiết thay đổi hồ sơ
      </div>
    );
  }

  const diff = parseAuditDiff(log);

  return (
    <div className="bg-surface-card border border-line rounded-xl p-5 shadow-xs space-y-4">
      <div>
        <h4 className="text-base font-bold text-ink-primary tracking-tight">
          Thay đổi hồ sơ
        </h4>
        <p className="text-xs text-ink-muted font-medium mt-0.5">
          {diff.fieldTitle}
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1">
        <div className="bg-surface-subtle/80 border border-line/80 rounded-lg p-3.5 space-y-1">
          <div className="text-[11px] font-semibold text-ink-subtle uppercase tracking-wider">
            Trước
          </div>
          <div className="text-sm font-medium text-ink-secondary break-words">
            {diff.beforeValue}
          </div>
        </div>

        <div className="bg-tone-info-bg border border-tone-info-border rounded-lg p-3.5 space-y-1">
          <div className="text-[11px] font-semibold text-primary uppercase tracking-wider">
            Sau
          </div>
          <div className="text-sm font-semibold text-ink-primary break-words">
            {diff.afterValue}
          </div>
        </div>
      </div>

      {diff.reason && (
        <div className="pt-2 text-xs text-ink-body leading-relaxed">
          <span className="font-semibold text-ink-secondary">Lý do: </span>
          <span>{diff.reason}</span>
        </div>
      )}
    </div>
  );
};

