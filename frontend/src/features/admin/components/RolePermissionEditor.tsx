import { useState, useMemo } from 'react';
import { Search, Save, RotateCcw, ShieldCheck, CheckCheck } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { PermissionModuleCard } from './PermissionModuleCard';
import type { ModulePermissionsDto, SystemRoleInfo } from '../types/rolePermission.types';

export interface RolePermissionEditorProps {
  role: SystemRoleInfo;
  modulePermissions: ModulePermissionsDto[];
  selectedPermissionIds: Set<number>;
  initialPermissionIds: Set<number>;
  isLoading: boolean;
  isSaving: boolean;
  onTogglePermission: (permissionId: number, nextChecked: boolean) => void;
  onToggleModule: (permissionIds: number[], selectAll: boolean) => void;
  onSelectAll: () => void;
  onDeselectAll: () => void;
  onReset: () => void;
  onSave: () => void;
}

export function RolePermissionEditor({
  role,
  modulePermissions,
  selectedPermissionIds,
  initialPermissionIds,
  isLoading,
  isSaving,
  onTogglePermission,
  onToggleModule,
  onSelectAll,
  onDeselectAll,
  onReset,
  onSave,
}: RolePermissionEditorProps) {
  const [searchTerm, setSearchTerm] = useState('');

  const isAdminRole = role.id === 1;

  const isDirty = useMemo(() => {
    if (selectedPermissionIds.size !== initialPermissionIds.size) return true;
    for (const id of selectedPermissionIds) {
      if (!initialPermissionIds.has(id)) return true;
    }
    return false;
  }, [selectedPermissionIds, initialPermissionIds]);

  const allPermissions = useMemo(() => {
    return modulePermissions.flatMap((m) => m.permissions);
  }, [modulePermissions]);

  const totalCount = allPermissions.length;
  const selectedCount = selectedPermissionIds.size;

  const filteredModules = useMemo(() => {
    if (!searchTerm.trim()) return modulePermissions;
    const term = searchTerm.toLowerCase();

    return modulePermissions
      .map((mod) => ({
        ...mod,
        permissions: mod.permissions.filter(
          (p) =>
            (p.permissionName && p.permissionName.toLowerCase().includes(term)) ||
            p.permissionCode.toLowerCase().includes(term),
        ),
      }))
      .filter((mod) => mod.permissions.length > 0);
  }, [modulePermissions, searchTerm]);

  return (
    <Card className="h-full border-[var(--color-border-card,#e0e7f1)] bg-[var(--color-bg-white,#ffffff)] flex flex-col shadow-sm">
      <CardHeader className="p-5 pb-4 border-b border-[var(--color-border-subtle,#edf1f7)] bg-[var(--color-bg-subtle,#f7f9fc)]/40">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="min-w-0">
            <div className="flex items-center gap-2.5">
              <h2 className="text-lg sm:text-xl font-bold text-[var(--color-text-heading,#10182c)]">
                {role.title}
              </h2>
              <Badge variant="secondary" className="font-mono text-xs px-2 py-0.5">
                {role.roleName}
              </Badge>
              {isAdminRole && (
                <Badge variant="info" dot className="text-xs">
                  Toàn quyền hệ thống
                </Badge>
              )}
            </div>
            <p className="text-xs text-[var(--color-text-desc,#667b9a)] mt-1.5 leading-relaxed max-w-2xl">
              {role.description}
            </p>
          </div>

          <div className="flex items-center gap-2.5 shrink-0">
            {isDirty && !isAdminRole && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onReset}
                disabled={isSaving}
                leftIcon={<RotateCcw className="size-3.5" />}
                className="text-xs border-[var(--color-border-input,#cbd7e8)]"
              >
                Đặt lại
              </Button>
            )}

            <Button
              type="button"
              variant="default"
              size="sm"
              onClick={onSave}
              disabled={!isDirty || isSaving || isAdminRole}
              isLoading={isSaving}
              loadingText="Đang lưu..."
              leftIcon={<Save className="size-3.5" />}
              className="text-xs bg-[var(--color-primary,#2563eb)] hover:bg-[var(--color-primary-hover,#1d4ed8)] px-4"
            >
              Lưu thay đổi
            </Button>
          </div>
        </div>

        {isAdminRole ? (
          <div className="mt-4 p-3 rounded-lg bg-[var(--color-primary-light,#eff6ff)] border border-[var(--color-primary-border,#d4e4ff)] flex items-start gap-2.5 text-xs text-[var(--color-primary-link,#0053d8)]">
            <ShieldCheck className="size-4 shrink-0 mt-0.5" />
            <p className="leading-relaxed">
              Tài khoản thuộc vai trò <strong>Quản trị viên (Admin)</strong> được phân quyền tự động
              với toàn bộ đặc quyền hệ thống theo cơ chế Superuser. Danh sách quyền của Admin được
              bảo vệ để phòng tránh nguy cơ khóa hệ thống.
            </p>
          </div>
        ) : (
          <div className="mt-3 text-[11px] text-[var(--color-text-muted,#697e9f)] flex items-center justify-between">
            <span>
              Mọi thay đổi phân quyền sẽ có hiệu lực ngay ở lần làm mới token kế tiếp của người dùng.
            </span>
            {isDirty && (
              <Badge variant="warning" className="text-[10px] font-medium">
                Có thay đổi chưa lưu
              </Badge>
            )}
          </div>
        )}
      </CardHeader>

      <div className="p-4 py-3 border-b border-[var(--color-border-subtle,#edf1f7)] bg-[var(--color-bg-white,#ffffff)] flex flex-wrap items-center justify-between gap-3">
        <div className="w-full sm:w-72">
          <Input
            placeholder="Tìm quyền theo tên, mã quyền..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            leftIcon={<Search className="size-4 text-[var(--color-text-placeholder,#90a3bf)]" />}
            className="h-8.5 text-xs bg-[var(--color-bg-subtle,#f7f9fc)] border-[var(--color-border-input,#cbd7e8)]"
          />
        </div>

        <div className="flex items-center gap-3">
          <span className="text-xs font-medium text-[var(--color-text-muted,#697e9f)]">
            Đã cấp:{' '}
            <strong className="text-[var(--color-text-heading,#10182c)]">
              {isAdminRole ? totalCount : selectedCount}
            </strong>{' '}
            / {totalCount} quyền
          </span>

          {!isAdminRole && (
            <div className="flex items-center gap-1.5">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={onSelectAll}
                disabled={isSaving || selectedCount === totalCount}
                leftIcon={<CheckCheck className="size-3.5" />}
                className="h-8 text-xs text-[var(--color-primary,#2563eb)] hover:bg-[var(--color-primary-light,#eff6ff)] px-2.5"
              >
                Chọn tất cả
              </Button>

              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={onDeselectAll}
                disabled={isSaving || selectedCount === 0}
                className="h-8 text-xs text-[var(--color-text-muted,#697e9f)] hover:bg-slate-100 px-2.5"
              >
                Bỏ chọn tất cả
              </Button>
            </div>
          )}
        </div>
      </div>

      <CardContent className="p-4 flex-1 overflow-y-auto space-y-3.5 bg-[var(--color-bg-subtle,#f7f9fc)]/30">
        {isLoading ? (
          <div className="py-16 flex flex-col items-center justify-center gap-3">
            <div className="size-8 border-3 border-[var(--color-primary,#2563eb)] border-t-transparent rounded-full animate-spin" />
            <p className="text-xs font-medium text-[var(--color-text-muted,#697e9f)]">
              Đang tải danh mục quyền...
            </p>
          </div>
        ) : filteredModules.length === 0 ? (
          <div className="py-16 text-center text-xs text-[var(--color-text-muted,#697e9f)]">
            Không tìm thấy quyền nào phù hợp với từ khóa &ldquo;{searchTerm}&rdquo;
          </div>
        ) : (
          filteredModules.map((mod) => (
            <PermissionModuleCard
              key={mod.module}
              moduleName={mod.module}
              permissions={mod.permissions}
              selectedPermissionIds={selectedPermissionIds}
              onTogglePermission={onTogglePermission}
              onToggleModule={onToggleModule}
              disabled={isAdminRole || isSaving}
            />
          ))
        )}
      </CardContent>
    </Card>
  );
}

