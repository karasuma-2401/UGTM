import { useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { Dialog } from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { OperationToast } from '@/shared/notifications';
import { IdentityNavigation } from '../components/users/IdentityNavigation';
import { roleDisplay, useRoles } from '../hooks/useRoles';
import { rolePermissionApi } from '../services/rolePermissionApi';
import type { RoleSummaryDto } from '../types/rolePermission.types';
import { getUserErrorMessage } from '../services/userApi';
import { RoleDetailsDrawer } from '../components/role-permissions/RoleDetailsDrawer';

export function RoleManagementPage() {
  const client = useQueryClient();
  const roles = useRoles(true);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [target, setTarget] = useState<RoleSummaryDto | null>(null);
  const [viewRole, setViewRole] = useState<RoleSummaryDto | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [notice, setNotice] = useState('');
  const refresh = () => client.invalidateQueries({ queryKey: ['identity-roles'] });
  const create = useMutation({
    mutationFn: async () => {
      const result = await rolePermissionApi.createRole(name.trim(), description.trim());
      if (!result.success) throw new Error(result.error?.message || 'Không thể tạo vai trò.');
      return result.data;
    },
    onSuccess: () => { void refresh(); setOpen(false); setName(''); setDescription(''); setNotice('Đã thêm vai trò.'); },
  });
  const visibility = useMutation({
    mutationFn: async (role: RoleSummaryDto) => {
      const result = await rolePermissionApi.setVisibility(role.roleId, !role.isHidden);
      if (!result.success) throw new Error(result.error?.message || 'Không thể cập nhật vai trò.');
      return result.data;
    },
    onSuccess: (role) => { void refresh(); setTarget(null); setNotice(role.isHidden ? 'Đã ẩn vai trò.' : 'Đã hiển thị lại vai trò.'); },
    onError: () => { void refresh(); },
  });
  const updateDescription = useMutation({
    mutationFn: async ({ id, description }: { id: number; description: string }) => {
      const result = await rolePermissionApi.updateDescription(id, description);
      if (!result.success) throw new Error(result.error?.message || 'Không thể lưu mô tả.');
      return result.data;
    },
    onSuccess: (role) => { void refresh(); void client.invalidateQueries({ queryKey: ['identity-role-permissions', role.roleId] }); setViewRole(null); setNotice('Đã cập nhật mô tả vai trò.'); },
  });
  const columns: DataTableColumn<RoleSummaryDto>[] = [
    { id: 'role', header: 'Vai trò', cell: (role) => <div><strong>{roleDisplay(role).title}</strong><p className="text-xs text-ink-muted">{role.roleName}</p></div> },
    { id: 'users', header: 'Tài khoản', cell: (role) => role.userCount },
    { id: 'permissions', header: 'Quyền', cell: (role) => role.permissionCount },
    { id: 'status', header: 'Trạng thái', cell: (role) => <Badge variant={role.isHidden ? 'secondary' : 'success'}>{role.isHidden ? 'Đã ẩn' : 'Đang hiển thị'}</Badge> },
    { id: 'action', header: 'Thao tác', align: 'right', widthClassName: 'w-48', headerClassName: 'whitespace-nowrap', cell: (role) => <div className="flex justify-end gap-2"><Button size="sm" variant="outline" onClick={() => setViewRole(role)} aria-label={`Xem vai trò ${roleDisplay(role).title}`}>Xem</Button><Button size="sm" variant="outline" disabled={!role.isHidden && !role.canHide} title={role.isSystemRole ? 'Vai trò hệ thống không thể ẩn' : role.userCount ? 'Vai trò đang được sử dụng' : undefined} onClick={() => { visibility.reset(); setTarget(role); }}>{role.isHidden ? 'Hiển thị lại' : 'Ẩn'}</Button></div> },
  ];
  function submit(event: FormEvent) { event.preventDefault(); if (!create.isPending) create.mutate(); }
  return <div className="min-w-0 space-y-4">
    <PageHeader compact accent title="Tài khoản và phân quyền" navigation={<IdentityNavigation />} />
    <OperationToast message={notice} onConsumed={() => setNotice('')} />
    <Card className="space-y-4 p-4 sm:p-5">
      <div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="text-lg font-bold text-ink-heading">Quản lý vai trò</h2><p className="text-sm text-ink-muted">Thêm vai trò hoặc ẩn vai trò tùy chỉnh không còn sử dụng.</p></div><Button leftIcon={<Plus />} onClick={() => { create.reset(); setOpen(true); }}>Thêm vai trò</Button></div>
      <DataTable ariaLabel="Danh sách vai trò" data={roles.data ?? []} columns={columns} getRowId={(role) => String(role.roleId)} selectedRowId={selectedRowId} onRowSelect={(id) => setSelectedRowId((previous) => previous === id ? null : id)} onRowOpen={setViewRole} loading={roles.isPending} error={roles.isError ? { message: roles.error.message, onRetry: () => void roles.refetch() } : null} empty={{ title: 'Chưa có vai trò' }} />
    </Card>
    {viewRole && <RoleDetailsDrawer key={viewRole.roleId} role={viewRole} saving={updateDescription.isPending} error={updateDescription.isError ? getUserErrorMessage(updateDescription.error) : null} onClose={() => { updateDescription.reset(); setViewRole(null); }} onSave={(description) => updateDescription.mutate({ id: viewRole.roleId, description })} />}
    <Dialog title="Thêm vai trò" open={open} busy={create.isPending} onClose={() => setOpen(false)}>
      <form onSubmit={submit} className="space-y-4 p-5"><label className="block space-y-1 text-sm font-medium">Tên vai trò<Input value={name} onChange={(e) => setName(e.target.value)} required minLength={2} maxLength={50} placeholder="Ví dụ: Reviewer" /></label><label className="block space-y-1 text-sm font-medium">Mô tả <span className="text-ink-muted">(không bắt buộc)</span><Input value={description} onChange={(e) => setDescription(e.target.value)} maxLength={255} /></label><p className="text-xs text-ink-muted">Sau khi tạo, cấu hình quyền tại tab Phân quyền.</p>{create.isError && <p role="alert" className="text-sm text-tone-danger-text">{getUserErrorMessage(create.error)}</p>}<div className="flex justify-end gap-2"><Button type="button" variant="outline" disabled={create.isPending} onClick={() => setOpen(false)}>Hủy</Button><Button type="submit" isLoading={create.isPending}>Thêm vai trò</Button></div></form>
    </Dialog>
    <ConfirmDialog open={target !== null} title={target?.isHidden ? 'Hiển thị lại vai trò?' : 'Ẩn vai trò?'} description={<><p>{target?.isHidden ? `Vai trò ${target.roleName} sẽ có thể được gán và phân quyền trở lại.` : `Vai trò ${target?.roleName} sẽ không còn xuất hiện khi gán và phân quyền.`}</p>{visibility.isError && <p role="alert" className="mt-3 text-tone-danger-text">{getUserErrorMessage(visibility.error)}</p>}</>} confirmText={target?.isHidden ? 'Hiển thị lại' : 'Ẩn vai trò'} cancelText="Hủy" type="warning" isConfirming={visibility.isPending} onClose={() => setTarget(null)} onConfirm={() => { if (target) visibility.mutate(target); }} />
  </div>;
}
