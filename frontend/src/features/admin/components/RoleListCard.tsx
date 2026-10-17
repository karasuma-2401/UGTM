import { useState } from 'react';
import { Search } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { SYSTEM_ROLES } from '../constants/systemRoles';
import type { SystemRoleInfo } from '../types/rolePermission.types';

export interface RoleListCardProps {
  selectedRoleId: number;
  onSelectRole: (role: SystemRoleInfo) => void;
  rolePermissionCounts?: Record<number, number>;
  totalPermissionsCount?: number;
}

export function RoleListCard({
  selectedRoleId,
  onSelectRole,
  rolePermissionCounts = {},
  totalPermissionsCount = 0,
}: RoleListCardProps) {
  const [searchTerm, setSearchTerm] = useState('');

  const filteredRoles = SYSTEM_ROLES.filter((role) => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      role.title.toLowerCase().includes(term) ||
      role.roleName.toLowerCase().includes(term) ||
      role.description.toLowerCase().includes(term)
    );
  });

  return (
    <Card className="h-full border-[var(--color-border-card,#e0e7f1)] bg-[var(--color-bg-white,#ffffff)] flex flex-col shadow-sm">
      <CardHeader className="p-4 pb-3 border-b border-[var(--color-border-subtle,#edf1f7)]">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-base font-bold text-[var(--color-text-heading,#10182c)]">
              Vai trò hệ thống
            </CardTitle>
            <CardDescription className="text-xs text-[var(--color-text-desc,#667b9a)] mt-0.5">
              Chọn vai trò để xem và cấu hình danh mục quyền
            </CardDescription>
          </div>
          <Badge variant="secondary" className="text-xs font-semibold">
            {SYSTEM_ROLES.length} vai trò
          </Badge>
        </div>

        <div className="mt-3">
          <Input
            placeholder="Tìm kiếm vai trò..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            leftIcon={<Search className="size-4 text-[var(--color-text-placeholder,#90a3bf)]" />}
            className="h-8.5 text-xs bg-[var(--color-bg-subtle,#f7f9fc)] border-[var(--color-border-input,#cbd7e8)]"
          />
        </div>
      </CardHeader>

      <CardContent className="p-2.5 flex-1 overflow-y-auto space-y-1.5">
        {filteredRoles.length === 0 ? (
          <div className="py-8 text-center text-xs text-[var(--color-text-muted,#697e9f)]">
            Không tìm thấy vai trò phù hợp
          </div>
        ) : (
          filteredRoles.map((role) => {
            const isSelected = role.id === selectedRoleId;
            const count = rolePermissionCounts[role.id];

            return (
              <button
                key={role.id}
                type="button"
                onClick={() => onSelectRole(role)}
                className={cn(
                  'w-full text-left p-3 rounded-lg border transition-all select-none',
                  'hover:bg-[var(--color-bg-subtle,#f7f9fc)]',
                  isSelected
                    ? 'border-[var(--color-primary,#2563eb)] bg-[var(--color-primary-light,#eff6ff)] shadow-xs'
                    : 'border-transparent bg-transparent hover:border-[var(--color-border-subtle,#edf1f7)]',
                )}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span
                        className={cn(
                          'text-sm font-semibold truncate',
                          isSelected
                            ? 'text-[var(--color-primary-hover,#1d4ed8)]'
                            : 'text-[var(--color-text-heading,#10182c)]',
                        )}
                      >
                        {role.title}
                      </span>
                    </div>

                    <p className="text-xs text-[var(--color-text-desc,#667b9a)] mt-1.5 line-clamp-2 leading-relaxed">
                      {role.description}
                    </p>
                  </div>
                </div>

                <div className="mt-2.5 pt-2 border-t border-[var(--color-border-subtle,#edf1f7)]/70 flex items-center justify-between text-[11px]">
                  <span className="text-[var(--color-text-muted,#697e9f)]">Phân quyền:</span>
                  <span
                    className={cn(
                      'font-medium',
                      role.id === 1
                        ? 'text-[var(--color-primary-hover,#1d4ed8)] font-semibold'
                        : count !== undefined && count > 0
                          ? 'text-[var(--color-success-text,#087c5a)]'
                          : 'text-[var(--color-text-muted,#697e9f)]',
                    )}
                  >
                    {role.id === 1
                      ? 'Toàn quyền'
                      : count !== undefined
                        ? `${count} / ${totalPermissionsCount} quyền`
                        : 'Đang tải...'}
                  </span>
                </div>
              </button>
            );
          })
        )}
      </CardContent>
    </Card>
  );
}

