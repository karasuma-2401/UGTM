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
      <span aria-hidden="true" className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-primary ring-1 ring-inset ring-blue-100">
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
        <p className="break-words text-xs text-slate-500 font-mono">
          ID sự kiện #{log.id} · {log.action}
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
      className="max-w-[560px] bg-white"
      headerClassName="border-b border-slate-200/80 bg-white px-5 py-4 sm:px-6"
    >
      <div className="flex min-h-[calc(100dvh-5.5rem)] flex-col">
        <div className="flex-1 space-y-5 p-5 sm:p-6">
          {/* Khối 1: Tác nhân & Ngữ cảnh */}
          <section className="space-y-2.5">
            <h4 className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
              <User className="size-4 text-primary" />
              Tác nhân thực hiện
            </h4>

            <div className="flex items-start gap-3.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-4">
              {isSystemActor ? (
                <span className="flex size-10 shrink-0 items-center justify-center rounded-full border border-slate-200/80 bg-slate-100 text-slate-500 ring-2 ring-white">
                  <Server className="size-4" />
                </span>
              ) : (
                <Avatar name={actorName} className="size-10 shrink-0 text-sm" />
              )}

              <div className="min-w-0 flex-1 space-y-1">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold text-slate-900">{actorName}</span>
                </div>

                {log.actorEmail && (
                  <div className="text-xs text-slate-600">{log.actorEmail}</div>
                )}

                {(log.actorFullName || log.actorEmail) && (
                  <div className="text-xs text-slate-500">Tên và email theo hồ sơ tài khoản hiện tại.</div>
                )}

                {log.actorUserId && (
                  <div className="break-all font-mono text-xs text-slate-500">
                    User ID: {log.actorUserId}
                  </div>
                )}

                {log.actorSessionId && (
                  <div className="break-all font-mono text-xs text-slate-500" title={log.actorSessionId}>
                    Session: {log.actorSessionId}
                  </div>
                )}
              </div>
            </div>
          </section>

          {/* Khối 2: Thời điểm & Đối tượng tác động */}
          <section className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="space-y-1.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5">
              <div className="flex items-center gap-1.5 text-xs font-medium text-slate-500">
                <Clock className="size-3.5 text-slate-400" />
                Thời điểm ghi nhận
              </div>
              <div className="text-xs font-semibold tabular-nums text-slate-900">
                {formatAuditFullTime(log.timestamp)}
              </div>
            </div>

            <div className="space-y-1.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5">
              <div className="flex items-center gap-1.5 text-xs font-medium text-slate-500">
                <Layers className="size-3.5 text-slate-400" />
                Đối tượng tác động
              </div>
              <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                <span className="text-xs font-semibold text-slate-900">{targetLabel}</span>
                {log.targetId && (
                  <span className="break-all font-mono text-xs text-slate-600" title={log.targetId}>
                    #{log.targetId}
                  </span>
                )}
              </div>
            </div>
          </section>

          {/* Khối 3: So sánh thay đổi (Visual Diff) */}
          <section className="space-y-2.5">
            <h4 className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
              <Shield className="size-4 text-primary" />
              Chi tiết biến động dữ liệu
            </h4>

            {(Array.isArray(added) || Array.isArray(removed)) && (
              <div className="flex flex-wrap gap-1.5">
                {Array.isArray(added) && added.map((item: unknown) => <span key={`added-${String(item)}`} className="rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-800">Cấp thêm: {changeLabel(item)}</span>)}
                {Array.isArray(removed) && removed.map((item: unknown) => <span key={`removed-${String(item)}`} className="rounded-full border border-rose-200 bg-rose-50 px-2.5 py-1 text-xs font-medium text-rose-800">Thu hồi: <span className="line-through">{changeLabel(item)}</span></span>)}
              </div>
            )}

            {diff.hasDiff ? (
              <div className="space-y-3 rounded-xl border border-slate-200/80 bg-slate-50/60 p-4">
                <div className="text-xs font-semibold text-slate-700">
                  {diff.fieldTitle}
                </div>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  {/* Trước */}
                  <div className="space-y-1 rounded-lg border border-rose-200/70 bg-rose-50/70 p-3">
                    <div className="flex items-center gap-1 text-xs font-semibold text-rose-700">
                      <XCircle className="size-3.5" />
                      {isChangeSet ? 'Thu hồi' : 'Trước thay đổi'}
                    </div>
                    <div className="break-words text-xs font-medium text-rose-950">
                      {diff.beforeValue}
                    </div>
                  </div>

                  {/* Sau */}
                  <div className="space-y-1 rounded-lg border border-emerald-200/70 bg-emerald-50/70 p-3">
                    <div className="flex items-center gap-1 text-xs font-semibold text-emerald-700">
                      <CheckCircle2 className="size-3.5" />
                      {isChangeSet ? 'Cấp thêm' : 'Sau thay đổi'}
                    </div>
                    <div className="break-words text-xs font-semibold text-emerald-950">
                      {diff.afterValue}
                    </div>
                  </div>
                </div>

                {diff.reason && (
                  <div className="border-t border-slate-200/70 pt-2.5 text-xs leading-relaxed text-slate-600">
                    <span className="font-semibold text-slate-700">Lý do ghi nhận: </span>
                    <span>{diff.reason}</span>
                  </div>
                )}
              </div>
            ) : (
              <div className="space-y-1.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 text-xs">
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
              <h4 className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
                <Hash className="size-4 text-primary" />
                Mã truy vết hệ thống (Request ID)
              </h4>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => handleCopy(log.requestId, 'request')}
                leftIcon={copiedRequestId ? <Check className="size-3.5 text-emerald-600" /> : <Copy className="size-3.5" />}
                className="h-7 text-xs text-primary hover:bg-blue-50"
              >
                {copiedRequestId ? 'Đã sao chép' : 'Sao chép ID'}
              </Button>
            </div>

            <div className="break-all select-all rounded-xl border border-blue-200/80 bg-blue-50/70 p-3 font-mono text-xs text-blue-950">
              {log.requestId}
            </div>
            {onViewRequest && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => onViewRequest(log.requestId)}
                className="h-8 rounded-lg text-xs"
              >
                Xem các sự kiện cùng request
              </Button>
            )}
          </section>

          {/* Khối 5: Dữ liệu kỹ thuật (Metadata JSON) */}
          {log.metadata && Object.keys(log.metadata).length > 0 && (
            <section className="space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">
                  <FileCode className="size-4 text-primary" />
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

              <pre className="max-h-64 overflow-x-auto rounded-xl border border-slate-800 bg-slate-900 p-4 font-mono text-xs leading-relaxed text-slate-100">
                <HighlightedJson value={log.metadata} />
              </pre>
            </section>
          )}
        </div>

        {/* Nút đóng */}
        <div className="sticky bottom-0 z-10 mt-auto flex justify-end border-t border-slate-200/80 bg-white/95 px-5 py-3.5 backdrop-blur-md sm:px-6">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            className="h-9 w-full rounded-xl px-5 text-xs font-semibold sm:w-auto"
          >
            Đóng
          </Button>
        </div>
      </div>
    </Dialog>
  );
};
