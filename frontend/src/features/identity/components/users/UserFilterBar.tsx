import type { FormEvent } from 'react';
import { Search, SlidersHorizontal, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { SYSTEM_ROLES } from '../../constants/systemRoles';
import { STATUS_LABELS } from '../../types/user.types';
import type { AccountStatus, SystemRole } from '../../types/user.types';
import type { UserFilters } from '../../hooks/useUserFilters';

interface UserFilterBarProps {
  isStudent: boolean;
  draftFilters: UserFilters;
  filters: UserFilters;
  hasFilters: boolean;
  onDraftChange: (filters: UserFilters) => void;
  onApply: (event: FormEvent<HTMLFormElement>) => void;
  onClear: () => void;
  onRemoveFilter: (key: keyof UserFilters) => void;
}

export function UserFilterBar({
  isStudent,
  draftFilters,
  filters,
  hasFilters,
  onDraftChange,
  onApply,
  onClear,
  onRemoveFilter,
}: UserFilterBarProps) {
  return (
    <div className="space-y-4">
      <form
        onSubmit={onApply}
        className={`grid items-end gap-4 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm md:grid-cols-2 ${isStudent ? 'xl:grid-cols-[2fr_1fr_auto]' : 'xl:grid-cols-[2fr_1fr_1fr_auto]'}`}
      >
        <label className="block space-y-2 text-sm text-slate-700">
          <span>Tìm tài khoản</span>
          <Input
            leftIcon={<Search className="size-4" />}
            className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
            placeholder={isStudent ? 'MSSV, họ tên hoặc email' : 'Mã cán bộ, họ tên hoặc email'}
            value={draftFilters.keyword}
            onChange={(event) =>
              onDraftChange({
                ...draftFilters,
                keyword: event.target.value,
              })
            }
          />
        </label>

        {!isStudent && (
          <div className="space-y-2 text-sm text-slate-700">
            <span>Vai trò</span>
            <Select<SystemRole | ''>
              triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
              aria-label="Lọc theo vai trò"
              placeholder="Tất cả vai trò"
              value={draftFilters.role}
              options={[
                { value: '', label: 'Tất cả vai trò' },
                ...SYSTEM_ROLES.filter((role) => role.roleName !== 'Student').map((role) => ({
                  value: role.roleName,
                  label: role.title,
                })),
              ]}
              onChange={(value: SystemRole | '') =>
                onDraftChange({ ...draftFilters, role: value })
              }
            />
          </div>
        )}

        <div className="space-y-2 text-sm text-slate-700">
          <span>Trạng thái</span>
          <Select<AccountStatus | ''>
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            aria-label="Lọc theo trạng thái"
            placeholder="Tất cả trạng thái"
            value={draftFilters.status}
            options={[
              { value: '', label: 'Tất cả trạng thái' },
              ...Object.entries(STATUS_LABELS).map(([value, label]) => ({
                value: value as AccountStatus,
                label,
              })),
            ]}
            onChange={(value: AccountStatus | '') =>
              onDraftChange({ ...draftFilters, status: value })
            }
          />
        </div>

        <div className="flex flex-wrap gap-2">
          <Button
            type="submit"
            variant="soft"
            leftIcon={<SlidersHorizontal />}
            className="h-10 rounded-lg px-5"
          >
            Áp dụng
          </Button>
          <Button
            type="button"
            variant="outline"
            leftIcon={<X />}
            className="h-10 rounded-lg px-4"
            disabled={!hasFilters}
            onClick={onClear}
          >
            Bỏ lọc
          </Button>
        </div>
      </form>

      {Boolean(filters.keyword || filters.role || filters.status) && (
        <div className="flex flex-wrap items-center gap-2" aria-label="Bộ lọc đang áp dụng">
          <span className="text-xs text-slate-500">Đang lọc:</span>
          {filters.keyword && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => onRemoveFilter('keyword')}
              aria-label={`Bỏ lọc từ khóa ${filters.keyword}`}
            >
              Từ khóa: {filters.keyword}
            </Button>
          )}
          {filters.role && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => onRemoveFilter('role')}
              aria-label="Bỏ lọc vai trò"
            >
              {SYSTEM_ROLES.find((role) => role.roleName === filters.role)?.title ?? filters.role}
            </Button>
          )}
          {filters.status && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => onRemoveFilter('status')}
              aria-label="Bỏ lọc trạng thái"
            >
              {STATUS_LABELS[filters.status]}
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

