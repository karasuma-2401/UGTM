import React, { useEffect, useState } from 'react';
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
  ScrollText,
} from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { Avatar } from '@/components/ui/avatar';
import { rolePermissionApi } from '@/features/identity/services/rolePermissionApi';
import { notify } from '@/shared/notifications';
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
  onViewRequest?: (requestId: string) => void;
}

function HighlightedJson({ value }: { value: unknown }) {
  const json = JSON.stringify(value, null, 2);
  const tokens = json.split(/("(?:\\.|[^"\\])*"\s*:|"(?:\\.|[^"\\])*"|\b(?:true|false|null|-?\d+(?:\.\d+)?)\b)/g);
  return <>{tokens.map((token, index) => <span key={index} className={token.startsWith('"') ? (token.trimEnd().endsWith(':') ? 'text-sky-300' : 'text-emerald-300') : /^(true|false|null|-?\d)/.test(token) ? 'text-orange-300' : undefined}>{token}</span>)}</>;
}

export const AuditLogDetailDrawer: React.FC<AuditLogDetailDrawerProps> = ({
  log,
  open,
  onClose,
  onViewRequest,
}) => {
  const [copiedRequestId, setCopiedRequestId] = useState(false);
  const [copiedJson, setCopiedJson] = useState(false);
  const [permissionNames, setPermissionNames] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!open || log?.action !== 'Identity.Role.PermissionsChanged') return;
    let active = true;
    rolePermissionApi.getPermissions().then((response) => {
      if (!active || !response.success) return;
      const entries = response.data.flatMap((group) => group.permissions.map((permission) => [String(permission.id), `${permission.permissionName || permission.permissionCode} (#${permission.id})`]));
      setPermissionNames(Object.fromEntries(entries));
    }).catch(() => { /* Stored IDs remain visible when the current catalog is unavailable. */ });
    return () => { active = false; };
  }, [open, log?.action]);

  if (!log) return null;

  const diff = parseAuditDiff(log);
  const actionTitle = formatAuditAction(log.action);
  const targetLabel = formatTargetType(log.targetType);
  const isChangeSet = log.action === 'Identity.User.RolesChanged' || log.action === 'Identity.Role.PermissionsChanged';
  const added = log.action === 'Identity.User.RolesChanged' ? log.metadata?.AddedRoles ?? log.metadata?.addedRoles : log.action === 'Identity.Role.PermissionsChanged' ? log.metadata?.AddedPermissionIds ?? log.metadata?.addedPermissionIds : null;
  const removed = log.action === 'Identity.User.RolesChanged' ? log.metadata?.RemovedRoles ?? log.metadata?.removedRoles : log.action === 'Identity.Role.PermissionsChanged' ? log.metadata?.RemovedPermissionIds ?? log.metadata?.removedPermissionIds : null;
  const changeLabel = (item: unknown) => log.action === 'Identity.Role.PermissionsChanged' ? permissionNames[String(item)] || `#${String(item)}` : String(item);
  const isSystemActor = !log.actorFullName && !log.actorEmail;
  const actorName = log.actorFullName || log.actorEmail || (log.actorUserId ? 'Không tìm thấy tài khoản' : 'Chưa xác định');

  const handleCopy = async (text: string, type: 'request' | 'json') => {
    try {
      await navigator.clipboard.writeText(text);
      if (type === 'request') {
        setCopiedRequestId(true);
        notify.success('Đã sao chép Request ID.');
        setTimeout(() => setCopiedRequestId(false), 2000);
      } else {
        setCopiedJson(true);
        setTimeout(() => setCopiedJson(false), 2000);
      }
    } catch {
      if (type === 'request') notify.error('Không thể sao chép Request ID. Vui lòng thử lại.');
    }
  };

  const headerContent = (
    <div className="flex min-w-0 items-start gap-3 pr-2">
      <span aria-hidden="true" className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-blue-200/80 bg-white/90 text-blue-700 shadow-sm">
        <ScrollText className="size-5" />
      </span>
      <div className="space-y-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <h3 className="text-base font-bold text-slate-900 tracking-tight">
            {actionTitle}
          </h3>
          {log.outcome === 'Success' ? (
            <StatusBadge tone="success">Thành công</StatusBadge>
          ) : log.outcome === 'Denied' ? (
            <StatusBadge tone="warning">Bị từ chối</StatusBadge>
          ) : log.outcome === 'Failed' ? (
            <StatusBadge tone="error">Thất bại</StatusBadge>
          ) : (
            <StatusBadge tone="warning">{log.outcome || 'Chưa xác định'}</StatusBadge>
          )}
        </div>
        <p className="break-words text-xs text-slate-600 font-mono">
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
      className="max-w-[560px] bg-slate-50"
      headerClassName="border-blue-100 bg-gradient-to-r from-blue-100 via-blue-50 to-orange-50 py-4"
    >
      <div className="flex min-h-[calc(100dvh-5.5rem)] flex-col gap-6 p-5 sm:p-6">
        {/* Khối 1: Tác nhân & Ngữ cảnh */}
        <section className="space-y-3">
          <h4 className="text-xs font-semibold text-slate-600 uppercase tracking-wider flex items-center gap-1.5">
            <User className="size-3.5 text-blue-600" />
            Tác nhân thực hiện
          </h4>

          <div className="bg-white border border-slate-200 rounded-xl p-4 flex items-start gap-3.5 shadow-sm">
            {isSystemActor ? (
              <span className="size-10 rounded-xl bg-blue-100 flex items-center justify-center text-blue-700 shrink-0">
                <Server className="size-5" />
              </span>
            ) : (
              <Avatar name={actorName} className="size-10 rounded-xl text-sm shrink-0" />
            )}

            <div className="min-w-0 flex-1 space-y-1">
              <div className="flex items-center gap-2">
                <span className="font-semibold text-slate-900 text-sm">{actorName}</span>
              </div>

              {log.actorEmail && (
                <div className="text-xs text-slate-500">{log.actorEmail}</div>
              )}

              {(log.actorFullName || log.actorEmail) && <div className="text-xs text-slate-500">Tên và email theo hồ sơ tài khoản hiện tại.</div>}

              {log.actorUserId && (
                <div className="text-[11px] font-mono text-slate-600 break-all">
                  User ID: {log.actorUserId}
                </div>
              )}

              {log.actorSessionId && (
                <div className="text-[11px] font-mono text-slate-600 break-all" title={log.actorSessionId}>
                  Session: {log.actorSessionId}
                </div>
              )}
            </div>
          </div>
        </section>

        {/* Khối 2: Thời điểm & Đối tượng tác động */}
        <section className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
          <div className="bg-white border border-slate-200 rounded-xl p-3.5 space-y-1.5 shadow-sm">
            <div className="text-[11px] font-semibold text-slate-600 uppercase tracking-wider flex items-center gap-1">
              <Clock className="size-3 text-blue-600" />
              Thời điểm ghi nhận
            </div>
            <div className="text-xs font-semibold text-slate-800">
              {formatAuditFullTime(log.timestamp)}
            </div>
          </div>

          <div className="bg-white border border-slate-200 rounded-xl p-3.5 space-y-1.5 shadow-sm">
            <div className="text-[11px] font-semibold text-slate-600 uppercase tracking-wider flex items-center gap-1">
              <Layers className="size-3 text-blue-600" />
              Đối tượng tác động
            </div>
            <div className="flex min-w-0 flex-wrap items-center gap-1.5">
              <span className="text-xs font-semibold text-slate-800">{targetLabel}</span>
              {log.targetId && (
                <span className="text-xs font-mono text-slate-600 break-all" title={log.targetId}>
                  #{log.targetId}
                </span>
              )}
            </div>
          </div>
        </section>

        {/* Khối 3: So sánh thay đổi (Visual Diff) */}
        <section className="space-y-3">
          <h4 className="text-xs font-semibold text-slate-600 uppercase tracking-wider flex items-center gap-1.5">
            <Shield className="size-3.5 text-blue-600" />
            Chi tiết biến động dữ liệu
          </h4>

          {(Array.isArray(added) || Array.isArray(removed)) && <div className="flex flex-wrap gap-1.5">
            {Array.isArray(added) && added.map((item: unknown) => <span key={`added-${String(item)}`} className="rounded-full bg-emerald-50 px-2 py-1 text-xs text-emerald-800">Cấp thêm: {changeLabel(item)}</span>)}
            {Array.isArray(removed) && removed.map((item: unknown) => <span key={`removed-${String(item)}`} className="rounded-full bg-rose-50 px-2 py-1 text-xs text-rose-800">Thu hồi: <span className="line-through">{changeLabel(item)}</span></span>)}
          </div>}

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
                    {isChangeSet ? 'Thu hồi' : 'Trước thay đổi'}
                  </div>
                  <div className="text-xs text-rose-950 font-medium break-words">
                    {diff.beforeValue}
                  </div>
                </div>

                {/* Sau */}
                <div className="bg-emerald-50/50 border border-emerald-200/60 rounded-lg p-3 space-y-1">
                  <div className="text-[11px] font-semibold text-emerald-700 uppercase tracking-wider flex items-center gap-1">
                    <CheckCircle2 className="size-3" />
                    {isChangeSet ? 'Cấp thêm' : 'Sau thay đổi'}
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
            <div className="bg-blue-50/70 border border-blue-100 rounded-xl p-4 space-y-2 text-xs">
              <div className="font-medium text-slate-700">
                Không có dữ liệu trước và sau để so sánh.
              </div>
              <div className="text-slate-500">
                {diff.reason || (Array.isArray(log.metadata?.ChangedFields) ? `Trường đã thay đổi: ${log.metadata.ChangedFields.join(', ')}` : 'Thông tin chi tiết nằm trong metadata nếu được ghi nhận.')}
              </div>
            </div>
          )}
        </section>

        {/* Khối 4: Mã truy vết (Request ID) */}
        <section className="space-y-2">
          <div className="flex items-center justify-between">
            <h4 className="text-xs font-semibold text-slate-600 uppercase tracking-wider flex items-center gap-1.5">
              <Hash className="size-3.5 text-blue-600" />
              Mã truy vết hệ thống (Request ID)
            </h4>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => handleCopy(log.requestId, 'request')}
              leftIcon={copiedRequestId ? <Check className="size-3.5 text-emerald-600" /> : <Copy className="size-3.5" />}
              className="h-7 text-xs text-blue-700 hover:text-blue-900"
            >
              {copiedRequestId ? 'Đã sao chép' : 'Sao chép ID'}
            </Button>
          </div>

          <div className="font-mono text-xs text-blue-950 bg-blue-100/70 p-3 rounded-xl border border-blue-200/80 break-all select-all">
            {log.requestId}
          </div>
          {onViewRequest && <Button type="button" variant="outline" size="sm" onClick={() => onViewRequest(log.requestId)}>Xem các sự kiện cùng request</Button>}
        </section>

        {/* Khối 5: Dữ liệu kỹ thuật (Metadata JSON) */}
        {log.metadata && Object.keys(log.metadata).length > 0 && (
          <section className="space-y-2">
            <div className="flex items-center justify-between">
              <h4 className="text-xs font-semibold text-slate-600 uppercase tracking-wider flex items-center gap-1.5">
                <FileCode className="size-3.5 text-blue-600" />
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
              <HighlightedJson value={log.metadata} />
            </pre>
          </section>
        )}

        {/* Nút đóng */}
        <div className="mt-auto flex justify-end border-t border-slate-200 pt-4">
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
