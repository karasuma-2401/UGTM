import { Checkbox } from '@/components/ui/checkbox';
import { cn } from '@/lib/utils';
import type { PermissionDto } from '../../types/rolePermission.types';

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
      onClick={(event) => {
        // The checkbox label activates its input natively; let onCheckedChange
        // handle that click instead of toggling once here and once on the input.
        if ((event.target as Element).closest('label, input')) return;
        if (!disabled) {
          onToggle(permission.id, !checked);
        }
      }}
      className={cn(
        'flex items-center justify-between p-3 rounded-lg border transition-all cursor-pointer',
        'border-line-subtle bg-surface-card',
        'hover:border-tone-info-border hover:bg-surface-subtle',
        checked && 'border-tone-info-border bg-tone-info-bg/55 hover:bg-tone-info-bg/75',
        disabled &&
          'cursor-default opacity-75 hover:bg-surface-card hover:border-line-subtle',
        disabled &&
          checked &&
          'hover:bg-tone-info-bg/55 hover:border-tone-info-border',
      )}
    >
      <div className="flex items-center gap-3 min-w-0 pr-3">
        <Checkbox
          aria-label={permission.permissionName || permission.permissionCode}
          checked={checked}
          disabled={disabled}
          onCheckedChange={(val) => onToggle(permission.id, val)}
          onClick={(e) => e.stopPropagation()}
        />

        <div className="flex flex-col text-left min-w-0">
          <span
            className={cn(
              'text-sm font-medium leading-snug break-words',
              checked ? 'text-brand-ink' : 'text-ink-primary',
            )}
          >
            {permission.permissionName || permission.permissionCode}
          </span>
          <span className="mt-1 break-all text-xs text-ink-muted">
            {permission.permissionCode}
          </span>
        </div>
      </div>

      <div className="shrink-0 text-right">
        <span
          className={cn(
            'text-[10px] font-medium px-2 py-0.5 rounded',
            checked ? 'bg-tone-info-bg text-tone-info-text' : 'bg-surface-muted text-ink-muted',
          )}
        >
          {checked ? 'Đã cấp' : 'Chưa cấp'}
        </span>
      </div>
    </div>
  );
}
