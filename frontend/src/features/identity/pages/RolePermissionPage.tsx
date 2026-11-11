import { useEffect, useState } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useBlocker } from 'react-router-dom';
import { PageHeader } from '@/components/ui/page-header';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { IdentityNavigation } from '../components/users/IdentityNavigation';
import { RoleListCard } from '../components/role-permissions/RoleListCard';
import { RolePermissionEditor } from '../components/role-permissions/RolePermissionEditor';
import { SYSTEM_ROLES } from '../constants/systemRoles';
import { rolePermissionApi } from '../services/rolePermissionApi';
import type { RolePermissionsDto } from '../types/rolePermission.types';

export function RolePermissionPage() {
  const queryClient = useQueryClient();
  const [selectedRoleId, setSelectedRoleId] = useState(1);
  const [pendingRoleId, setPendingRoleId] = useState<number | null>(null);
  const [edits, setEdits] = useState<{
    roleId: number;
    ids: Set<number>;
    initial: Set<number>;
  } | null>(null);
  const [notice, setNotice] = useState('');
  const catalog = useQuery({
    queryKey: ['identity-permissions'],
    queryFn: async () => {
      const res = await rolePermissionApi.getPermissions();
      if (!res.success) throw new Error(res.error?.message || 'Không thể tải danh mục quyền.');
      return res.data;
    },
  });
  // Separate cache entries prevent a late response from replacing another role's permissions.
  const roles = useQueries({
    queries: SYSTEM_ROLES.map((role) => ({
      queryKey: ['identity-role-permissions', role.id],
      queryFn: async () => {
        const res = await rolePermissionApi.getRolePermissions(role.id);
        if (!res.success)
          throw new Error(res.error?.message || 'Không thể tải quyền của vai trò này.');
        return res.data;
      },
    })),
  });
  const selectedIndex = SYSTEM_ROLES.findIndex((role) => role.id === selectedRoleId);
  const selectedRole = SYSTEM_ROLES[selectedIndex];
  const roleQuery = roles[selectedIndex];
  const serverIds = new Set(roleQuery.data?.permissions.map((permission) => permission.id) ?? []);
  const activeEdits = edits?.roleId === selectedRoleId ? edits : null;
  const selectedIds = activeEdits?.ids ?? serverIds;
  const initialIds = activeEdits?.initial ?? serverIds;
  const dirty =
    selectedIds.size !== initialIds.size || [...selectedIds].some((id) => !initialIds.has(id));
  const ready = catalog.isSuccess && roleQuery.isSuccess && !roleQuery.isFetching;
  const save = useMutation({
    mutationFn: async ({ roleId, ids }: { roleId: number; ids: number[] }) => {
      const res = await rolePermissionApi.updateRolePermissions(roleId, ids);
      if (!res.success) throw new Error(res.error?.message || 'Không thể lưu phân quyền.');
      return res.data;
    },
    onSuccess: (result: RolePermissionsDto) => {
      queryClient.setQueryData(['identity-role-permissions', result.roleId], result);
      setEdits(null);
      setNotice('Đã lưu phân quyền thành công.');
    },
  });
  const blocker = useBlocker(dirty || save.isPending);
  useEffect(() => {
    if (!dirty && !save.isPending) return;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [dirty, save.isPending]);
  const toggle = (ids: number[], checked: boolean) => {
    if (!ready || save.isPending || selectedRoleId === 1) return;
    setEdits((previous) => {
      const current =
        previous?.roleId === selectedRoleId
          ? previous
          : { roleId: selectedRoleId, ids: serverIds, initial: serverIds };
      const next = new Set(current.ids);
      ids.forEach((id) => (checked ? next.add(id) : next.delete(id)));
      return { ...current, ids: next };
    });
    setNotice('');
    save.reset();
  };
  const allIds =
    catalog.data?.flatMap((module) => module.permissions.map((permission) => permission.id)) ?? [];
  const loadError = catalog.isError || roleQuery.isError;

  return (
    <div className="min-w-0 space-y-4">
      <PageHeader compact title="Tài khoản và phân quyền" navigation={<IdentityNavigation />} />
      {notice && (
        <p role="status" className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700">
          {notice}
        </p>
      )}
      {save.isError && (
        <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
          {save.error.message} Thay đổi của bạn vẫn được giữ lại.
        </p>
      )}
      <div className="grid min-w-0 grid-cols-1 items-start gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
        <RoleListCard
          selectedRoleId={selectedRoleId}
          disabled={save.isPending}
          onSelectRole={(role) => {
            if (save.isPending || role.id === selectedRoleId) return;
            if (dirty) {
              setPendingRoleId(role.id);
              return;
            }
            setSelectedRoleId(role.id);
            setEdits(null);
            setNotice('');
            save.reset();
          }}
          rolePermissionCounts={Object.fromEntries(
            SYSTEM_ROLES.flatMap((role, index) =>
              roles[index].isSuccess ? [[role.id, roles[index].data.permissions.length]] : [],
            ),
          )}
          failedRoleIds={SYSTEM_ROLES.filter((_, index) => roles[index].isError).map(
            (role) => role.id,
          )}
          totalPermissionsCount={allIds.length}
        />
        <div className="min-w-0">
          {loadError ? (
            <div role="alert" className="rounded-xl border border-rose-200 bg-white p-6 text-sm">
              <p className="mb-3 text-rose-700">
                {catalog.error?.message || roleQuery.error?.message} Chưa thể chỉnh sửa phân quyền.
              </p>
              <Button
                variant="outline"
                disabled={catalog.isFetching || roleQuery.isFetching}
                onClick={() => {
                  if (catalog.isError) void catalog.refetch();
                  if (roleQuery.isError) void roleQuery.refetch();
                }}
              >
                Thử lại
              </Button>
            </div>
          ) : (
            <RolePermissionEditor
              key={selectedRoleId}
              role={selectedRole}
              modulePermissions={catalog.data ?? []}
              selectedPermissionIds={selectedIds}
              initialPermissionIds={initialIds}
              isLoading={!ready}
              isSaving={save.isPending}
              onTogglePermission={(id, checked) => toggle([id], checked)}
              onToggleModule={toggle}
              onSelectAll={() => toggle(allIds, true)}
              onDeselectAll={() => toggle(allIds, false)}
              onReset={() => {
                setEdits(null);
                setNotice('');
                save.reset();
              }}
              onSave={() => {
                if (ready && dirty && selectedRoleId !== 1 && !save.isPending)
                  save.mutate({ roleId: selectedRoleId, ids: [...selectedIds] });
              }}
            />
          )}
        </div>
      </div>
      <ConfirmDialog
        open={pendingRoleId !== null || blocker.state === 'blocked'}
        title="Bạn có thay đổi chưa lưu"
        description="Rời phần chỉnh sửa sẽ bỏ các thay đổi phân quyền hiện tại."
        confirmText="Bỏ thay đổi và tiếp tục"
        cancelText="Tiếp tục chỉnh sửa"
        type="warning"
        isConfirming={save.isPending}
        onClose={() => {
          setPendingRoleId(null);
          if (blocker.state === 'blocked') blocker.reset();
        }}
        onConfirm={() => {
          if (save.isPending) return;
          setEdits(null);
          if (pendingRoleId !== null) {
            setSelectedRoleId(pendingRoleId);
            setPendingRoleId(null);
            setNotice('');
            save.reset();
          }
          if (blocker.state === 'blocked') blocker.proceed();
        }}
      />
    </div>
  );
}
