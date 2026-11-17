import { useState } from 'react';
import { Search } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { SelectionIndicator } from '@/components/ui/selection-indicator';
import { SYSTEM_ROLES } from '../../constants/systemRoles';
import type { SystemRoleInfo } from '../../types/rolePermission.types';

export interface RoleListCardProps {
  selectedRoleId: number;
  disabled?: boolean;
  failedRoleIds?: number[];
  onSelectRole: (role: SystemRoleInfo) => void;
  rolePermissionCounts?: Record<number, number>;
  totalPermissionsCount?: number;
}

export function RoleListCard({
  selectedRoleId,
  disabled = false,
  failedRoleIds = [],
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
    <Card className="h-full border-line/80 bg-surface-card flex flex-col shadow-sm">
      <CardHeader className="p-4 pb-3 border-b border-line-subtle">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-base font-bold text-ink-heading">
              Vai trò hệ thống
            </CardTitle>
            <CardDescription className="text-xs text-ink-muted mt-0.5">
              Chọn vai trò để xem và cấu hình danh mục quyền
            </CardDescription>
          </div>
        </div>

        <div className="mt-3">
          <Input
            placeholder="Tìm kiếm vai trò..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            leftIcon={<Search className="size-4 text-ink-subtle" />}
            className="h-8.5 text-xs bg-surface-subtle border-line-input"
          />
        </div>
      </CardHeader>

      <CardContent className="relative isolate grid grid-cols-2 gap-1.5 p-2.5 lg:grid-cols-1">
        <SelectionIndicator
          activeKey={`${selectedRoleId}:${searchTerm}`}
          selector='button[aria-pressed="true"]'
          className="rounded-lg border border-transparent shadow-xs [background:linear-gradient(180deg,#f8fbff_0%,#eff6ff_100%)_padding-box,var(--gradient-brand)_border-box] dark:[background:linear-gradient(180deg,#162033_0%,#131c2e_100%)_padding-box,var(--gradient-brand)_border-box]"
        />
        {filteredRoles.length === 0 ? (
          <div className="py-8 text-center text-xs text-ink-muted">
            Không tìm thấy vai trò phù hợp
          </div>
        ) : (
          filteredRoles.map((role) => {
            const isSelected = role.id === selectedRoleId;
            const count = rolePermissionCounts[role.id];

            return (
              <button
                key={role.id}
                data-selection-item
                type="button"
                disabled={disabled}
                aria-pressed={isSelected}
                onClick={() => onSelectRole(role)}
                className={cn(
                  'relative z-10 w-full text-left p-3 rounded-lg border border-transparent transition-colors select-none',
                  isSelected
                    ? 'bg-transparent'
                    : 'bg-transparent hover:bg-surface-hover hover:border-line-subtle',
                )}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span
                        className={cn(
                          'text-sm font-semibold truncate',
                          isSelected ? 'text-brand-ink' : 'text-ink-heading',
                        )}
                      >
                        {role.title}
                      </span>
                    </div>

                    <p className="hidden lg:block text-xs text-ink-muted mt-1.5 line-clamp-2 leading-relaxed">
                      {role.description}
                    </p>
                  </div>
                </div>

                <div className="mt-2.5 pt-2 border-t border-line-subtle/70 flex items-center justify-between text-[11px]">
                  <span className="text-ink-muted">Phân quyền:</span>
                  <span
                    className={cn(
                      'font-medium',
                      role.id === 1
                        ? 'text-brand-ink font-semibold'
                        : count !== undefined && count > 0
                          ? 'text-tone-success-text'
                          : 'text-ink-muted',
                    )}
                  >
                    {role.id === 1
                      ? 'Toàn quyền'
                      : count !== undefined
                        ? `${count} / ${totalPermissionsCount} quyền`
                        : failedRoleIds.includes(role.id)
                          ? 'Không tải được'
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
