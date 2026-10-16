import { useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { PermissionCheckboxItem } from './PermissionCheckboxItem';
import type { PermissionDto } from '../types/rolePermission.types';

export interface PermissionModuleCardProps {
  moduleName: string;
  permissions: PermissionDto[];
  selectedPermissionIds: Set<number>;
  onTogglePermission: (permissionId: number, nextChecked: boolean) => void;
  onToggleModule: (permissionIds: number[], selectAll: boolean) => void;
  disabled?: boolean;
}

export function PermissionModuleCard({
  moduleName,
  permissions,
  selectedPermissionIds,
  onTogglePermission,
  onToggleModule,
  disabled = false,
}: PermissionModuleCardProps) {
  const [isExpanded, setIsExpanded] = useState(true);

  const modulePermIds = permissions.map((p) => p.id);
  const selectedCount = modulePermIds.filter((id) => selectedPermissionIds.has(id)).length;
  const totalCount = modulePermIds.length;

  const isAllSelected = totalCount > 0 && selectedCount === totalCount;
  const isIndeterminate = selectedCount > 0 && selectedCount < totalCount;

  const handleModuleCheckboxChange = (checked: boolean) => {
    if (disabled) return;
    onToggleModule(modulePermIds, checked);
  };

  return (
    <div className="rounded-xl border border-[var(--color-border-card,#e0e7f1)] bg-[var(--color-bg-white,#ffffff)] overflow-hidden shadow-xs transition-all">
      <div className="p-3.5 bg-[var(--color-bg-subtle,#f7f9fc)] border-b border-[var(--color-border-subtle,#edf1f7)] flex items-center justify-between gap-3 select-none">
        <div className="flex items-center gap-3 min-w-0">
          <Checkbox
            checked={isAllSelected ? true : isIndeterminate ? 'indeterminate' : false}
            disabled={disabled}
            onCheckedChange={handleModuleCheckboxChange}
          />
          <div className="flex items-center gap-2 min-w-0">
            <h4 className="text-sm font-bold text-[var(--color-text-heading,#10182c)] truncate">
              Phân hệ {moduleName}
            </h4>
            <Badge
              variant={selectedCount > 0 ? 'info' : 'outline'}
              className="text-[11px] px-2 py-0 h-5 font-semibold shrink-0"
            >
              {selectedCount} / {totalCount} quyền
            </Badge>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setIsExpanded(!isExpanded)}
            className="h-7 w-7 p-0 text-[var(--color-text-muted,#697e9f)] hover:text-[var(--color-text-heading,#10182c)]"
            title={isExpanded ? 'Thu gọn' : 'Mở rộng'}
          >
            {isExpanded ? <ChevronUp className="size-4" /> : <ChevronDown className="size-4" />}
          </Button>
        </div>
      </div>

      {isExpanded && (
        <div className="p-3.5 grid grid-cols-1 md:grid-cols-2 gap-2.5 bg-[var(--color-bg-white,#ffffff)]">
          {permissions.map((perm) => (
            <PermissionCheckboxItem
              key={perm.id}
              permission={perm}
              checked={selectedPermissionIds.has(perm.id)}
              onToggle={onTogglePermission}
              disabled={disabled}
            />
          ))}
        </div>
      )}
    </div>
  );
}

