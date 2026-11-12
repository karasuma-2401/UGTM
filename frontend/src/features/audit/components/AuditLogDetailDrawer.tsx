import React, { useState } from 'react';
import {
  Server,
  User,
  Clock,
  Shield,
  Layers,
  FileCode,
  Copy,
  Check,
  CheckCircle2,
  XCircle,
  Hash,
} from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { Badge } from '@/components/ui/badge';
import { Avatar } from '@/components/ui/avatar';
import {
  formatAuditFullTime,
  formatAuditAction,
  formatTargetType,
  parseAuditDiff,
} from '../utils/parseAuditDiff';
import type { AuditLogItem } from '../types/auditLog.types';

interface AuditLogDetailDrawerProps {
  log: AuditLogItem | null;
  open: boolean;
  onClose: () => void;
}

export const AuditLogDetailDrawer: React.FC<AuditLogDetailDrawerProps> = ({
  log,
  open,
  onClose,
}) => {
  const [copiedRequestId, setCopiedRequestId] = useState(false);
  const [copiedJson, setCopiedJson] = useState(false);

  if (!log) return null;

  const diff = parseAuditDiff(log);
  const actionTitle = formatAuditAction(log.action);
  const targetLabel = formatTargetType(log.targetType);
  const isSystemActor =
    !log.actorFullName && !log.actorEmail ? true : log.actorFullName === 'Hệ thống';
  const actorName = log.actorFullName || log.actorEmail || 'Hệ thống';

  const handleCopy = async (text: string, type: 'request' | 'json') => {
    try {
      await navigator.clipboard.writeText(text);
      if (type === 'request') {
        setCopiedRequestId(true);
        setTimeout(() => setCopiedRequestId(false), 2000);
      } else {
        setCopiedJson(true);
        setTimeout(() => setCopiedJson(false), 2000);
      }
    } catch {
      // Fallback nếu clipboard API bị hạn chế
    }
  };

  const headerContent = (
    <div className="flex items-start justify-between gap-3 pr-2">
      <div className="space-y-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <h3 className="text-base font-bold text-slate-900 tracking-tight">
            {actionTitle}
          </h3>
          {log.outcome === 'Success' ? (
            <StatusBadge tone="success">Thành công</StatusBadge>
          ) : log.outcome === 'Denied' ? (
            <StatusBadge tone="warning">Bị từ chối</StatusBadge>
          ) : (
            <StatusBadge tone="error">Thất bại</StatusBadge>
          )}
        </div>
        <p className="text-xs text-slate-500 font-mono">
          ID sự kiện #{log.id} • {log.action}
        </p>
      </div>
    </div>
  );

  return (
    <Dialog
      open={open}
      onClose={onClose}
      variant="drawer"
      title={headerContent}
      className="max-w-[560px]"
    >
      <div className="p-5 sm:p-6 space-y-6 overflow-y-auto">
        {/* Khối 1: Tác nhân & Ngữ cảnh */}
        <section className="space-y-3">
          <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
            <User className="size-3.5 text-slate-400" />
            Tác nhân thực hiện
          </h4>

          <div className="bg-slate-50/80 border border-slate-200/80 rounded-xl p-4 flex items-start gap-3.5">
            {isSystemActor ? (
              <span className="size-10 rounded-xl bg-slate-200/80 flex items-center justify-center text-slate-600 shrink-0">
                <Server className="size-5" />
              </span>
            ) : (
              <Avatar name={actorName} className="size-10 rounded-xl text-sm shrink-0" />
            )}

            <div className="min-w-0 flex-1 space-y-1">
              <div className="flex items-center gap-2">
                <span className="font-semibold text-slate-900 text-sm">{actorName}</span>
                {isSystemActor && (
                  <Badge variant="secondary" className="text-[10px] px-1.5 py-0 font-normal">
                    Hệ thống tự động
                  </Badge>
                )}
              </div>

              {log.actorEmail && (
                <div className="text-xs text-slate-500">{log.actorEmail}</div>
              )}

              {log.actorUserId && (
                <div className="text-[11px] font-mono text-slate-400">
                  User ID: {log.actorUserId}
                </div>
              )}

              {log.actorSessionId && (
                <div className="text-[11px] font-mono text-slate-400 truncate" title={log.actorSessionId}>
                  Session: {log.actorSessionId}
                </div>
              )}
            </div>
          </div>
        </section>

        {/* Khối 2: Thời điểm & Đối tượng tác động */}
        <section className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
          <div className="bg-slate-50/70 border border-slate-200/80 rounded-xl p-3.5 space-y-1">
            <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-1">
              <Clock className="size-3 text-slate-400" />
              Thời điểm ghi nhận
            </div>
            <div className="text-xs font-semibold text-slate-800">
              {formatAuditFullTime(log.timestamp)}
            </div>
          </div>

          <div className="bg-slate-50/70 border border-slate-200/80 rounded-xl p-3.5 space-y-1">
            <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-1">
              <Layers className="size-3 text-slate-400" />
              Đối tượng tác động
            </div>
            <div className="flex items-center gap-1.5">
              <span className="text-xs font-semibold text-slate-800">{targetLabel}</span>
              {log.targetId && (
                <span className="text-xs font-mono text-slate-500 truncate" title={log.targetId}>
                  #{log.targetId}
                </span>
              )}
            </div>
          </div>
        </section>

        {/* Khối 3: So sánh thay đổi (Visual Diff) */}
        <section className="space-y-3">
          <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
            <Shield className="size-3.5 text-slate-400" />
            Chi tiết biến động dữ liệu
          </h4>

          {diff.hasDiff ? (
            <div className="bg-white border border-slate-200/90 rounded-xl p-4 shadow-2xs space-y-3">
              <div className="text-xs font-medium text-slate-600">
                {diff.fieldTitle}
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {/* Trước */}
                <div className="bg-rose-50/50 border border-rose-200/60 rounded-lg p-3 space-y-1">
                  <div className="text-[11px] font-semibold text-rose-700 uppercase tracking-wider flex items-center gap-1">
                    <XCircle className="size-3" />
                    Trước thay đổi
                  </div>
                  <div className="text-xs text-rose-950 font-medium break-words">
                    {diff.beforeValue}
                  </div>
                </div>

                {/* Sau */}
                <div className="bg-emerald-50/50 border border-emerald-200/60 rounded-lg p-3 space-y-1">
                  <div className="text-[11px] font-semibold text-emerald-700 uppercase tracking-wider flex items-center gap-1">
                    <CheckCircle2 className="size-3" />
                    Sau thay đổi
                  </div>
                  <div className="text-xs text-emerald-950 font-semibold break-words">
                    {diff.afterValue}
                  </div>
                </div>
              </div>

              {diff.reason && (
                <div className="pt-2 border-t border-slate-100 text-xs text-slate-600 leading-relaxed">
                  <span className="font-semibold text-slate-700">Lý do ghi nhận: </span>
                  <span>{diff.reason}</span>
                </div>
              )}
            </div>
          ) : (
            <div className="bg-slate-50/70 border border-slate-200/80 rounded-xl p-4 space-y-2 text-xs">
              <div className="font-medium text-slate-700">
                Hành vi nghiệp vụ được hệ thống xử lý hợp lệ.
              </div>
              <div className="text-slate-500">
                {diff.reason || 'Không phát sinh thay đổi trường dữ liệu trực tiếp.'}
              </div>
            </div>
          )}
        </section>

        {/* Khối 4: Mã truy vết (Request ID) */}
        <section className="space-y-2">
          <div className="flex items-center justify-between">
            <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
              <Hash className="size-3.5 text-slate-400" />
              Mã truy vết hệ thống (Request ID)
            </h4>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => handleCopy(log.requestId, 'request')}
              leftIcon={copiedRequestId ? <Check className="size-3.5 text-emerald-600" /> : <Copy className="size-3.5" />}
              className="h-7 text-xs text-slate-600 hover:text-slate-900"
            >
              {copiedRequestId ? 'Đã sao chép' : 'Sao chép ID'}
            </Button>
          </div>

          <div className="font-mono text-xs text-slate-700 bg-slate-100/80 p-3 rounded-xl border border-slate-200/80 break-all select-all">
            {log.requestId}
          </div>
        </section>

        {/* Khối 5: Dữ liệu kỹ thuật (Metadata JSON) */}
        {log.metadata && Object.keys(log.metadata).length > 0 && (
          <section className="space-y-2">
            <div className="flex items-center justify-between">
              <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                <FileCode className="size-3.5 text-slate-400" />
                Dữ liệu kỹ thuật (Metadata JSON)
              </h4>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => handleCopy(JSON.stringify(log.metadata, null, 2), 'json')}
                leftIcon={copiedJson ? <Check className="size-3.5 text-emerald-600" /> : <Copy className="size-3.5" />}
                className="h-7 text-xs text-slate-600 hover:text-slate-900"
              >
                {copiedJson ? 'Đã sao chép' : 'Sao chép JSON'}
              </Button>
            </div>

            <pre className="font-mono text-[11px] leading-relaxed bg-slate-900 text-slate-100 p-4 rounded-xl overflow-x-auto max-h-64 border border-slate-800">
              {JSON.stringify(log.metadata, null, 2)}
            </pre>
          </section>
        )}

        {/* Nút đóng */}
        <div className="pt-2 flex justify-end">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            className="w-full sm:w-auto px-6 h-10"
          >
            Đóng
          </Button>
        </div>
      </div>
    </Dialog>
  );
};
