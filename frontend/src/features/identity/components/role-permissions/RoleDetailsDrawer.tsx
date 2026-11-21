import { useEffect, useState, type FormEvent } from 'react';
import { useBlocker } from 'react-router-dom';
import { Pencil, Save, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Dialog } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { roleDisplay } from '../../hooks/useRoles';
import type { RoleSummaryDto } from '../../types/rolePermission.types';

export function RoleDetailsDrawer({ role, saving, error, onClose, onSave }: {
  role: RoleSummaryDto; saving: boolean; error: string | null;
  onClose: () => void; onSave: (description: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(role.description ?? '');
  const [discard, setDiscard] = useState<'close' | 'cancel' | null>(null);
  const dirty = value.trim() !== (role.description ?? '').trim();
  const blocker = useBlocker(dirty || saving);
  useEffect(() => {
    if (!dirty && !saving) return;
    const handler = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [dirty, saving]);
  function requestClose(action: 'close' | 'cancel') {
    if (saving) return;
    if (dirty) { setDiscard(action); return; }
    if (action === 'close') onClose(); else setEditing(false);
  }
  function submit(event: FormEvent) { event.preventDefault(); if (dirty && !saving) onSave(value); }
  return <>
    <Dialog variant="drawer" busy={saving} onClose={() => requestClose('close')} headerClassName="border-b border-line/80 bg-surface-card px-5 py-4 sm:px-6" title={<span className="flex items-center gap-3"><span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-tone-info-bg text-primary"><ShieldCheck className="size-5" /></span><span><span className="block text-base font-semibold">Chi tiết vai trò</span><span className="block text-xs font-normal text-ink-muted">Thông tin và mô tả vai trò</span></span></span>}>
      <div className="flex min-h-[calc(100dvh-5rem)] flex-col">
        <div className="flex-1 space-y-6 p-5 sm:p-6">
          <div className="rounded-xl border border-line bg-surface-subtle p-4"><div className="flex flex-wrap items-center gap-2"><h2 className="text-lg font-bold text-ink-heading">{roleDisplay(role).title}</h2><Badge variant={role.isHidden ? 'secondary' : 'success'}>{role.isHidden ? 'Đã ẩn' : 'Đang hiển thị'}</Badge></div><p className="mt-1 text-sm text-ink-muted">{role.roleName}</p></div>
          <div className="grid grid-cols-2 gap-4 text-sm"><div><span className="text-ink-muted">Tài khoản</span><p className="mt-1 font-semibold text-ink-heading">{role.userCount}</p></div><div><span className="text-ink-muted">Quyền</span><p className="mt-1 font-semibold text-ink-heading">{role.permissionCount}</p></div></div>
          {editing ? <form id="role-description-form" onSubmit={submit}><label htmlFor="role-description" className="text-sm font-semibold text-ink-heading">Mô tả</label><textarea id="role-description" autoFocus maxLength={255} value={value} onChange={(event) => setValue(event.target.value)} disabled={saving} rows={6} className="mt-2 w-full resize-y rounded-xl border border-line-input bg-surface-card p-3 text-sm text-ink-body outline-none focus-visible:ring-2 focus-visible:ring-ring" /><p className="mt-1 text-right text-xs text-ink-muted">{value.length}/255</p>{error && <p role="alert" className="mt-3 rounded-lg bg-tone-danger-bg p-3 text-sm text-tone-danger-text">{error}</p>}</form> : <div><h3 className="text-sm font-semibold text-ink-heading">Mô tả</h3><p className="mt-2 whitespace-pre-wrap break-words text-sm leading-6 text-ink-body">{role.description?.trim() || 'Chưa có mô tả.'}</p></div>}
        </div>
        <div className="sticky bottom-0 flex justify-end gap-2.5 border-t border-line/80 bg-surface-card/95 px-5 py-3.5 backdrop-blur-md sm:px-6">{editing ? <><Button variant="outline" disabled={saving} onClick={() => requestClose('cancel')}>Hủy</Button><Button type="submit" form="role-description-form" leftIcon={<Save />} disabled={!dirty} isLoading={saving} loadingText="Đang lưu...">Lưu thay đổi</Button></> : <><Button variant="outline" onClick={() => requestClose('close')}>Đóng</Button><Button leftIcon={<Pencil />} onClick={() => setEditing(true)}>Chỉnh sửa</Button></>}</div>
      </div>
    </Dialog>
    <ConfirmDialog open={discard !== null || blocker.state === 'blocked'} title="Bỏ thay đổi chưa lưu?" description="Mô tả vừa chỉnh sửa sẽ không được lưu." confirmText="Bỏ thay đổi" cancelText="Tiếp tục chỉnh sửa" type="warning" isConfirming={saving} onClose={() => { setDiscard(null); if (blocker.state === 'blocked') blocker.reset(); }} onConfirm={() => { if (saving) return; if (blocker.state === 'blocked') blocker.proceed(); else if (discard === 'close') onClose(); else { setValue(role.description ?? ''); setEditing(false); } setDiscard(null); }} />
  </>;
}
