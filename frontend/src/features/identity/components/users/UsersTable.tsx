import { useId, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Avatar } from '@/components/ui/avatar';
import { InfoCell } from '@/components/ui/info-cell';
import { Badge } from '@/components/ui/badge';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { SelectionIndicator } from '@/components/ui/selection-indicator';

import { SYSTEM_ROLES } from '../../constants/systemRoles';
import {
  Eye,
  MoreHorizontal,
  Mail,
  ShieldOff,
  RotateCcw,
  ShieldCheck,
  GraduationCap,
  BookOpen,
  Building2,
  Landmark,
  UsersRound,
  type LucideIcon,
} from 'lucide-react';

import {
  STATUS_LABELS,
  type AccountStatus,
  type AccountGroup,
  type SystemRole,
  type UserSummary,
} from '../../types/user.types';

const roleAppearance: Record<SystemRole, { icon: LucideIcon; tone: string }> = {
  Admin: { icon: ShieldCheck, tone: 'bg-violet-50 text-violet-600 ring-violet-100' },
  Student: { icon: GraduationCap, tone: 'bg-blue-50 text-blue-600 ring-blue-100' },
  Lecturer: { icon: BookOpen, tone: 'bg-teal-50 text-teal-600 ring-teal-100' },
  Faculty: { icon: Building2, tone: 'bg-amber-50 text-amber-600 ring-amber-100' },
  AcademicAffairs: { icon: Landmark, tone: 'bg-indigo-50 text-indigo-600 ring-indigo-100' },
  CouncilMember: { icon: UsersRound, tone: 'bg-rose-50 text-rose-600 ring-rose-100' },
};

interface UsersTableProps {
  accountGroup: AccountGroup;
  users: UserSummary[];
  currentUserId?: string;
  canManage: boolean;
  isChangingStatus: boolean;
  pageNumber: number;
  pageSize: number;
  onEdit: (user: UserSummary) => void;
  onChangeStatus: (user: UserSummary) => void;
  onResendActivation: (user: UserSummary) => void;
  selectedRowId?: string | null;
  onSelectRow?: (id: string) => void;
}

const statusTones: Record<AccountStatus, StatusTone> = {
  Active: 'success',
  PendingActivation: 'warning',
  Suspended: 'orange',
  Disabled: 'error',
};

function UserRowActions({
  user,
  currentUserId,
  disabled,
  onEdit,
  onChangeStatus,
  onResendActivation,
}: {
  user: UserSummary;
  currentUserId?: string;
  disabled: boolean;
  onEdit: (user: UserSummary) => void;
  onChangeStatus: (user: UserSummary) => void;
  onResendActivation: (user: UserSummary) => void;
}) {
  const id = useId();
  const popup = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const closeAndRun = (action: (user: UserSummary) => void) => {
    popup.current?.hidePopover();
    action(user);
  };
  return (
    <div className="flex items-center justify-end gap-1">
      <Button
        type="button"
        variant="ghost"
        size="sm"
        disabled={disabled}
        onClick={() => onEdit(user)}
        leftIcon={<Eye />}
        aria-label={`Xem chi tiết tài khoản ${user.fullName}`}
        className="h-8 px-2 text-primary"
      >
        Chi tiết
      </Button>
      <button
        ref={trigger}
        type="button"
        disabled={disabled}
        popoverTarget={id}
        aria-label={`Thao tác với ${user.fullName}`}
        className="flex size-8 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 focus-visible:outline-primary disabled:opacity-40"
      >
        <MoreHorizontal className="size-4" />
      </button>
      <div
        ref={popup}
        id={id}
        popover="auto"
        aria-label={`Thao tác với ${user.fullName}`}
        className="fixed inset-auto m-0 w-56 rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-lg"
        onToggle={(event) => {
          if (event.newState !== 'open') return;
          const rect = trigger.current?.getBoundingClientRect();
          const el = popup.current;
          if (!rect || !el) return;
          el.style.left = Math.max(8, Math.min(rect.right - 224, window.innerWidth - 232)) + 'px';
          el.style.top =
            Math.max(
              8,
              rect.bottom + el.offsetHeight + 8 > window.innerHeight
                ? rect.top - el.offsetHeight - 4
                : rect.bottom + 4,
            ) + 'px';
          el.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
        }}
      >
        {user.status === 'PendingActivation' ? (
          <button
            type="button"
            disabled={disabled}
            onClick={() => closeAndRun(onResendActivation)}
            className="flex w-full items-center gap-2 rounded-lg px-3 py-2.5 text-sm text-slate-700 hover:bg-slate-50 focus-visible:outline-primary"
          >
            <Mail className="size-4" />
            Gửi email kích hoạt
          </button>
        ) : (
          <button
            type="button"
            disabled={disabled || user.id === currentUserId}
            title={
              user.id === currentUserId
                ? 'Không thể vô hiệu hóa tài khoản đang đăng nhập'
                : undefined
            }
            onClick={() => closeAndRun(onChangeStatus)}
            className={`flex w-full items-center gap-2 rounded-lg px-3 py-2.5 text-sm hover:bg-slate-50 focus-visible:outline-primary disabled:opacity-40 ${user.status === 'Active' ? 'text-rose-600' : 'text-emerald-700'}`}
          >
            {user.status === 'Active' ? (
              <ShieldOff className="size-4" />
            ) : (
              <RotateCcw className="size-4" />
            )}
            {user.status === 'Active' ? 'Vô hiệu hóa tài khoản' : 'Khôi phục tài khoản'}
          </button>
        )}
      </div>
    </div>
  );
}

function formatLastLogin(value: string | null): string {
  if (!value) return 'Chưa đăng nhập';

  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function UsersTable({
  accountGroup,
  users,
  currentUserId,
  canManage,
  isChangingStatus,
  pageNumber,
  pageSize,
  onEdit,
  onChangeStatus,
  onResendActivation,
  selectedRowId,
  onSelectRow,
}: UsersTableProps) {
  return (
    <div className="relative isolate">
      <SelectionIndicator
        activeKey={selectedRowId}
        selector='tr[aria-selected="true"]'
        variant="rail"
        className="z-20 rounded-r bg-primary"
      />
      <table className="block w-full text-left text-sm md:table md:min-w-[900px] md:table-fixed">
        <colgroup className="hidden md:table-column-group">
          <col className="w-14" />
          <col />
          <col className="w-52" />
          <col className="w-36" />
          <col className="w-36" />
          <col className="w-[176px]" />
        </colgroup>
        <thead className="hidden md:table-header-group h-11 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600">
          <tr>
            <th scope="col" className="w-14 px-3 py-4 text-center font-bold">
              STT
            </th>
            {[
              accountGroup === 'Student' ? 'Sinh viên' : 'Giảng viên / Cán bộ',
              accountGroup === 'Student' ? 'MSSV' : 'Vai trò / Mã cán bộ',
              'Lần truy cập',
              'Trạng thái',
              'Thao tác',
            ].map((heading) => (
              <th
                key={heading}
                scope="col"
                className={`whitespace-nowrap px-4 py-4 font-bold ${heading === 'Thao tác' ? 'text-center' : ''}`}
              >
                {heading}
              </th>
            ))}
          </tr>
        </thead>

        <tbody className="block divide-y divide-slate-100 md:table-row-group">
          {users.length === 0 ? (
            <tr>
              <td colSpan={6} className="px-4 py-12 text-center text-slate-500">
                Không tìm thấy tài khoản phù hợp.
              </td>
            </tr>
          ) : (
            users.map((user, index) => (
              <SelectableTableRow
                data-selection-item
                hideSelectionMarker
                key={user.id}
                className="grid grid-cols-[1fr_auto] items-center gap-x-3 px-4 py-3 md:table-row md:h-[72px] md:p-0"
                selected={selectedRowId === user.id}
                onSelect={onSelectRow ? () => onSelectRow(user.id) : undefined}
                onDoubleClick={(event) => {
                  if (
                    !canManage ||
                    isChangingStatus ||
                    (event.target as Element).closest(
                      'button, a, input, select, textarea, [role="button"]',
                    )
                  )
                    return;
                  onEdit(user);
                }}
                title={canManage ? 'Nhấp đúp để xem chi tiết tài khoản' : undefined}
                disabled={isChangingStatus}
              >
                <td className="hidden px-3 py-3 text-center text-xs tabular-nums text-slate-400 md:table-cell">
                  {(pageNumber - 1) * pageSize + index + 1}
                </td>
                <td className="block min-w-0 py-1 md:table-cell md:px-4 md:py-3">
                  <InfoCell
                    leading={<Avatar name={user.fullName} muted={user.status === 'Disabled'} />}
                    title={
                      <span className="min-w-0 truncate" title={user.fullName}>
                        {user.fullName}
                      </span>
                    }
                    titleClassName="flex-nowrap"
                    description={<span title={user.email}>{user.email}</span>}
                    descriptionClassName="truncate"
                    trailing={
                      user.id === currentUserId ? (
                        <Badge variant="info" className="px-1.5 py-0 text-[10px]">
                          Bạn
                        </Badge>
                      ) : undefined
                    }
                  />
                </td>
                <td className="block min-w-0 py-1 md:table-cell md:px-4 md:py-3">
                  {accountGroup === 'Student' ? (
                    <span className="font-medium tabular-nums text-slate-700">
                      {user.userCode || 'Chưa cập nhật'}
                    </span>
                  ) : (
                    <InfoCell
                      className="gap-2.5"
                      titleClassName="block truncate text-xs font-semibold leading-5 text-slate-700"
                      descriptionClassName="mt-0.5 truncate text-[11px] font-normal leading-4"
                      leading={(() => {
                        const { icon: Icon, tone } =
                          roleAppearance[user.roles[0]] ?? roleAppearance.CouncilMember;
                        return (
                          <span
                            aria-hidden="true"
                            className={`flex size-8 items-center justify-center rounded-lg ring-1 ring-inset ${tone}`}
                          >
                            <Icon className="size-4" />
                          </span>
                        );
                      })()}
                      title={
                        <span
                          title={user.roles
                            .map(
                              (name) =>
                                SYSTEM_ROLES.find((role) => role.roleName === name)?.title ?? name,
                            )
                            .join(', ')}
                        >
                          {SYSTEM_ROLES.find((role) => role.roleName === user.roles[0])?.title ??
                            user.roles[0] ??
                            'Chưa phân vai trò'}
                          {user.roles.length > 1 && ` +${user.roles.length - 1}`}
                        </span>
                      }
                      description={
                        <span title={user.userCode || undefined}>
                          {user.userCode || 'Chưa cập nhật'}
                        </span>
                      }
                    />
                  )}
                </td>

                <td className="hidden whitespace-nowrap px-4 py-3 text-xs text-slate-500 md:table-cell">
                  {formatLastLogin(user.lastLoginAt)}
                </td>

                <td className="block min-w-0 py-1 md:table-cell md:px-4 md:py-3">
                  <StatusBadge tone={statusTones[user.status]}>
                    {STATUS_LABELS[user.status]}
                  </StatusBadge>
                </td>

                <td className="block py-1 text-right md:table-cell md:px-3 md:py-3">
                  {canManage ? (
                    <UserRowActions
                      user={user}
                      currentUserId={currentUserId}
                      disabled={isChangingStatus}
                      onEdit={onEdit}
                      onChangeStatus={onChangeStatus}
                      onResendActivation={onResendActivation}
                    />
                  ) : (
                    <span className="text-xs text-slate-400">Chỉ xem</span>
                  )}
                </td>
              </SelectableTableRow>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
