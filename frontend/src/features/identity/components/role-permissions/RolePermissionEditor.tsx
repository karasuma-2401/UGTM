import { useState, useMemo } from 'react';
import { Search, Save, RotateCcw, ShieldCheck, CheckCheck } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { PermissionModuleCard } from './PermissionModuleCard';
import type { ModulePermissionsDto, SystemRoleInfo } from '../../types/rolePermission.types';
import { permissionModuleLabel } from '../../utils/permissionLabels';

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
            permissionModuleLabel(mod.module).toLowerCase().includes(term) ||
            (p.permissionName && p.permissionName.toLowerCase().includes(term)) ||
            p.permissionCode.toLowerCase().includes(term),
        ),
      }))
      .filter((mod) => mod.permissions.length > 0);
  }, [modulePermissions, searchTerm]);

  return (
    <Card className="fluent-panel h-full border-line/80 bg-surface-card flex flex-col shadow-sm">
      <CardHeader className="p-5 pb-4 border-b border-line-subtle bg-surface-subtle/50">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2.5">
              <h2 className="text-lg sm:text-xl font-bold text-ink-heading">
                {role.title}
              </h2>
              {isAdminRole && (
                <Badge variant="info" dot className="text-xs">
                  Toàn quyền hệ thống
                </Badge>
              )}
            </div>
            <p className="text-xs text-ink-muted mt-1.5 leading-relaxed max-w-2xl">
              {role.description}
            </p>
          </div>
        </div>

        {isAdminRole ? (
          <div className="mt-4 p-3 rounded-lg bg-tone-info-bg border border-tone-info-border flex items-start gap-2.5 text-xs text-tone-info-text">
            <ShieldCheck className="size-4 shrink-0 mt-0.5 text-tone-info-icon" />
            <p className="leading-relaxed">
              Tài khoản thuộc vai trò <strong>Quản trị viên</strong> có toàn quyền hệ thống. Danh
              sách quyền của Quản trị viên được bảo vệ để phòng tránh nguy cơ khóa hệ thống.
            </p>
          </div>
        ) : (
          <div className="mt-3 text-[11px] text-ink-muted flex items-center justify-between">
            <span>
              Thay đổi phân quyền có hiệu lực khi phiên đăng nhập của người dùng được làm mới.
            </span>
            {isDirty && (
              <Badge variant="warning" className="text-[10px] font-medium">
                Có thay đổi chưa lưu
              </Badge>
            )}
          </div>
        )}
      </CardHeader>

      <div className="p-4 py-3 border-b border-line-subtle bg-surface-card flex flex-wrap items-center justify-between gap-3">
        <div className="w-full sm:w-72">
          <Input
            placeholder="Tìm quyền theo tên, mã quyền..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            leftIcon={<Search className="size-4 text-ink-subtle" />}
            className="h-8.5 text-xs bg-surface-subtle border-line-input"
          />
        </div>

        <div className="flex items-center gap-3">
          <span className="text-xs font-medium text-ink-muted">
            Đã cấp:{' '}
            <strong className="text-ink-heading">
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
                onClick={() =>
                  searchTerm.trim()
                    ? onToggleModule(
                        filteredModules.flatMap((mod) => mod.permissions.map((p) => p.id)),
                        true,
                      )
                    : onSelectAll()
                }
                disabled={
                  isSaving ||
                  isLoading ||
                  filteredModules.length === 0 ||
                  selectedCount === totalCount
                }
                leftIcon={<CheckCheck className="size-3.5" />}
                className="h-8 text-xs text-brand-ink hover:bg-tone-info-bg px-2.5"
              >
                {searchTerm.trim() ? 'Chọn quyền đang hiển thị' : 'Chọn tất cả'}
              </Button>

              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() =>
                  searchTerm.trim()
                    ? onToggleModule(
                        filteredModules.flatMap((mod) => mod.permissions.map((p) => p.id)),
                        false,
                      )
                    : onDeselectAll()
                }
                disabled={isSaving || isLoading || selectedCount === 0}
                className="h-8 text-xs text-ink-muted hover:bg-surface-muted px-2.5"
              >
                {searchTerm.trim() ? 'Bỏ chọn quyền đang hiển thị' : 'Bỏ chọn tất cả'}
              </Button>
            </div>
          )}
        </div>
      </div>

      <CardContent className="p-4 flex-1 space-y-3.5 bg-surface-subtle/30">
        {isLoading ? (
          <div className="py-16 flex flex-col items-center justify-center gap-3">
            <div className="size-8 border-3 border-primary border-t-transparent rounded-full animate-spin" />
            <p className="text-xs font-medium text-ink-muted">
              Đang tải danh mục quyền...
            </p>
          </div>
        ) : filteredModules.length === 0 ? (
          <div className="py-16 text-center text-xs text-ink-muted">
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
              disabled={isAdminRole || isSaving || isLoading}
            />
          ))
        )}
      </CardContent>
      <div className="sticky bottom-3 z-20 flex flex-wrap items-center justify-between gap-3 border-t border-line bg-surface-card/95 px-4 py-3 shadow-sm backdrop-blur-sm">
        <span role="status" className="text-sm text-ink-body">
          {isAdminRole
            ? 'Vai trò Quản trị viên được bảo vệ'
            : isDirty
              ? 'Có thay đổi chưa lưu'
              : 'Chưa có thay đổi'}
        </span>
        <div className="flex items-center gap-2.5 shrink-0">
          {isDirty && !isAdminRole && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onReset}
              disabled={isSaving || isLoading}
              leftIcon={<RotateCcw className="size-3.5" />}
              className="text-xs border-line-input"
            >
              Hủy thay đổi
            </Button>
          )}

          <Button
            type="button"
            variant="default"
            size="sm"
            onClick={onSave}
            disabled={!isDirty || isSaving || isLoading || isAdminRole}
            isLoading={isSaving}
            loadingText="Đang lưu..."
            leftIcon={<Save className="size-3.5" />}
            className="text-xs bg-brand-solid hover:bg-brand-solid-hover px-4"
          >
            Lưu thay đổi
          </Button>
        </div>
      </div>
    </Card>
  );
}
