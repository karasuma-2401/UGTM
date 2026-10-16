import { Checkbox } from '@/components/ui/checkbox';
import { cn } from '@/lib/utils';
import type { PermissionDto } from '../types/rolePermission.types';

export interface PermissionCheckboxItemProps {
  permission: PermissionDto;
  checked: boolean;
  onToggle: (permissionId: number, nextChecked: boolean) => void;
  disabled?: boolean;
}

export function PermissionCheckboxItem({
  permission,
  checked,
  onToggle,
  disabled = false,
}: PermissionCheckboxItemProps) {
  return (
    <div
      onClick={() => {
        if (!disabled) {
          onToggle(permission.id, !checked);
        }
      }}
      className={cn(
        'flex items-center justify-between p-3 rounded-lg border transition-all cursor-pointer',
        'border-[var(--color-border-subtle,#edf1f7)] bg-[var(--color-bg-white,#ffffff)]',
        'hover:border-[var(--color-primary-border,#d4e4ff)] hover:bg-[var(--color-bg-subtle,#f7f9fc)]',
        checked &&
          'border-[var(--color-primary-border,#d4e4ff)] bg-[var(--color-primary-light,#eff6ff)]/50',
        disabled && 'cursor-default opacity-70 hover:bg-white hover:border-[var(--color-border-subtle,#edf1f7)]',
      )}
    >
      <div className="flex items-center gap-3 min-w-0 pr-3">
        <Checkbox
          checked={checked}
          disabled={disabled}
          onCheckedChange={(val) => onToggle(permission.id, val)}
          onClick={(e) => e.stopPropagation()}
        />

        <div className="flex flex-col text-left min-w-0">
          <span
            className={cn(
              'text-xs font-semibold leading-snug truncate',
              checked
                ? 'text-[var(--color-primary-hover,#1d4ed8)]'
                : 'text-[var(--color-text-main,#10182c)]',
            )}
          >
            {permission.permissionName}
          </span>
        </div>
      </div>

      <div className="shrink-0 text-right">
        <span
          className={cn(
            'text-[10px] font-medium px-2 py-0.5 rounded',
            checked
              ? 'bg-[var(--color-primary-muted,#dceaff)] text-[var(--color-primary-hover,#1d4ed8)]'
              : 'bg-[var(--color-bg-muted,#f1f3f6)] text-[var(--color-text-desc,#667b9a)]',
          )}
        >
          {checked ? 'Đã cấp' : 'Chưa cấp'}
        </span>
      </div>
    </div>
  );
}

