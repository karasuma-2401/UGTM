import { useState, useEffect, useCallback, useMemo } from 'react';
import { AlertCircle, CheckCircle2, UsersRound } from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { IdentityNavigation } from '../components/IdentityNavigation';
import { RoleListCard } from '../components/RoleListCard';
import { RolePermissionEditor } from '../components/RolePermissionEditor';
import { SYSTEM_ROLES } from '../constants/systemRoles';
import { rolePermissionApi } from '../services/rolePermissionApi';
import type { ModulePermissionsDto, SystemRoleInfo } from '../types/rolePermission.types';

export function RolePermissionPage() {
  const [selectedRoleId, setSelectedRoleId] = useState<number>(1);
  const [modulePermissions, setModulePermissions] = useState<ModulePermissionsDto[]>([]);
  const [selectedPermissionIds, setSelectedPermissionIds] = useState<Set<number>>(new Set());
  const [initialPermissionIds, setInitialPermissionIds] = useState<Set<number>>(new Set());
  const [rolePermissionCounts, setRolePermissionCounts] = useState<Record<number, number>>({});

  const [isLoadingAll, setIsLoadingAll] = useState<boolean>(true);
  const [isLoadingRole, setIsLoadingRole] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const [feedback, setFeedback] = useState<{
    type: 'success' | 'error';
    message: string;
  } | null>(null);

  const selectedRole = useMemo(() => {
    return SYSTEM_ROLES.find((r) => r.id === selectedRoleId) || SYSTEM_ROLES[0];
  }, [selectedRoleId]);

  const totalPermissionsCount = useMemo(() => {
    return modulePermissions.reduce((acc, mod) => acc + mod.permissions.length, 0);
  }, [modulePermissions]);

  useEffect(() => {
    let isMounted = true;

    async function loadInitialData() {
      setIsLoadingAll(true);
      try {
        const permsRes = await rolePermissionApi.getPermissions();
        if (!isMounted) return;

        if (permsRes.success) {
          setModulePermissions(permsRes.data);
        }

        const counts: Record<number, number> = {};
        await Promise.all(
          SYSTEM_ROLES.map(async (role) => {
            try {
              const res = await rolePermissionApi.getRolePermissions(role.id);
              if (res.success && isMounted) {
                counts[role.id] = res.data.permissions.length;
              }
            } catch {}
          }),
        );

        if (isMounted) {
          setRolePermissionCounts(counts);
        }
      } catch {
        if (isMounted) {
          setFeedback({
            type: 'error',
            message: 'Không thể tải danh mục quyền hệ thống. Vui lòng thử lại sau.',
          });
        }
      } finally {
        if (isMounted) {
          setIsLoadingAll(false);
        }
      }
    }

    loadInitialData();

    return () => {
      isMounted = false;
    };
  }, []);

  const loadRolePermissions = useCallback(async (roleId: number) => {
    setIsLoadingRole(true);
    setFeedback(null);
    try {
      const res = await rolePermissionApi.getRolePermissions(roleId);
      if (res.success) {
        const ids = new Set(res.data.permissions.map((p) => p.id));
        setSelectedPermissionIds(ids);
        setInitialPermissionIds(new Set(ids));
      } else {
        setFeedback({
          type: 'error',
          message: res.error?.message || 'Không thể tải quyền của vai trò này.',
        });
      }
    } catch {
      setFeedback({
        type: 'error',
        message: 'Đã xảy ra lỗi khi tải danh sách quyền của vai trò.',
      });
    } finally {
      setIsLoadingRole(false);
    }
  }, []);

  useEffect(() => {
    loadRolePermissions(selectedRoleId);
  }, [selectedRoleId, loadRolePermissions]);

  const handleSelectRole = (role: SystemRoleInfo) => {
    if (role.id === selectedRoleId) return;
    setSelectedRoleId(role.id);
  };

  const handleTogglePermission = (permissionId: number, nextChecked: boolean) => {
    setSelectedPermissionIds((prev) => {
      const updated = new Set(prev);
      if (nextChecked) {
        updated.add(permissionId);
      } else {
        updated.delete(permissionId);
      }
      return updated;
    });
  };

  const handleToggleModule = (permissionIds: number[], selectAll: boolean) => {
    setSelectedPermissionIds((prev) => {
      const updated = new Set(prev);
      permissionIds.forEach((id) => {
        if (selectAll) {
          updated.add(id);
        } else {
          updated.delete(id);
        }
      });
      return updated;
    });
  };

  const handleSelectAll = () => {
    const allIds = modulePermissions.flatMap((m) => m.permissions.map((p) => p.id));
    setSelectedPermissionIds(new Set(allIds));
  };

  const handleDeselectAll = () => {
    setSelectedPermissionIds(new Set());
  };

  const handleReset = () => {
    setSelectedPermissionIds(new Set(initialPermissionIds));
    setFeedback(null);
  };

  const handleSave = async () => {
    setIsSaving(true);
    setFeedback(null);
    try {
      const permissionIdsArray = Array.from(selectedPermissionIds);
      const res = await rolePermissionApi.updateRolePermissions(selectedRoleId, permissionIdsArray);

      if (res.success) {
        const newIds = new Set(res.data.permissions.map((p) => p.id));
        setSelectedPermissionIds(newIds);
        setInitialPermissionIds(new Set(newIds));

        setRolePermissionCounts((prev) => ({
          ...prev,
          [selectedRoleId]: newIds.size,
        }));

        setFeedback({
          type: 'success',
          message: `Đã cập nhật thành công phân quyền cho vai trò ${selectedRole.title}!`,
        });
      } else {
        setFeedback({
          type: 'error',
          message: res.error?.message || 'Cập nhật phân quyền thất bại.',
        });
      }
    } catch {
      setFeedback({
        type: 'error',
        message: 'Đã xảy ra lỗi máy chủ trong quá trình lưu phân quyền.',
      });
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="min-w-0 space-y-5">
      <PageHeader
        title="Tài khoản và phân quyền"
        icon={<UsersRound />}
        navigation={<IdentityNavigation />}
      />

      {feedback && (
        <div
          className={`p-3.5 rounded-lg border text-xs flex items-center justify-between gap-3 animate-in fade-in duration-200 ${
            feedback.type === 'success'
              ? 'bg-[var(--color-success-bg,#eefbf5)] border-[var(--color-success-border,#91edc5)] text-[var(--color-success-text,#087c5a)]'
              : 'bg-[var(--color-error-bg,#fff1f0)] border-[var(--color-error-border,#fecdca)] text-[var(--color-error,#b42318)]'
          }`}
        >
          <div className="flex items-center gap-2 min-w-0">
            {feedback.type === 'success' ? (
              <CheckCircle2 className="size-4 shrink-0" />
            ) : (
              <AlertCircle className="size-4 shrink-0" />
            )}
            <span className="font-medium truncate">{feedback.message}</span>
          </div>
          <button
            type="button"
            onClick={() => setFeedback(null)}
            className="text-xs underline hover:opacity-80 shrink-0"
          >
            Đóng
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 min-h-[600px]">
        <div className="lg:col-span-4 h-full">
          <RoleListCard
            selectedRoleId={selectedRoleId}
            onSelectRole={handleSelectRole}
            rolePermissionCounts={rolePermissionCounts}
            totalPermissionsCount={totalPermissionsCount}
          />
        </div>

        <div className="lg:col-span-8 h-full">
          <RolePermissionEditor
            role={selectedRole}
            modulePermissions={modulePermissions}
            selectedPermissionIds={selectedPermissionIds}
            initialPermissionIds={initialPermissionIds}
            isLoading={isLoadingAll || isLoadingRole}
            isSaving={isSaving}
            onTogglePermission={handleTogglePermission}
            onToggleModule={handleToggleModule}
            onSelectAll={handleSelectAll}
            onDeselectAll={handleDeselectAll}
            onReset={handleReset}
            onSave={handleSave}
          />
        </div>
      </div>
    </div>
  );
}
