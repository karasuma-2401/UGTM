import { useId, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Avatar } from '@/components/ui/avatar';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { InfoCell } from '@/components/ui/info-cell';
import { Badge } from '@/components/ui/badge';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';

import { SYSTEM_ROLES } from '../../constants/systemRoles';
import {
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
  type UsersSortField,
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
  sort?: { field: UsersSortField; direction: 'asc' | 'desc' };
  onSortChange?: (field: UsersSortField) => void;
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
        variant="outline"
        size="sm"
        disabled={disabled}
        onClick={() => onEdit(user)}
        aria-label={`Xem chi tiết tài khoản ${user.fullName}`}
        className="h-8 rounded-md border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-sm hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900"
      >
        Xem
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
        className="fixed inset-auto m-0 w-max max-w-[calc(100vw-16px)] rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-lg"
        onToggle={(event) => {
          if (event.newState !== 'open') return;
          const rect = trigger.current?.getBoundingClientRect();
          const el = popup.current;
          if (!rect || !el) return;
          const width = el.offsetWidth;
          el.style.left = Math.max(8, Math.min(rect.right - width, window.innerWidth - width - 8)) + 'px';
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
            className="flex w-full items-center gap-2 whitespace-nowrap rounded-lg px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 focus-visible:outline-primary"
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
            className={`flex w-full items-center gap-2 whitespace-nowrap rounded-lg px-3 py-2 text-sm hover:bg-slate-50 focus-visible:outline-primary disabled:opacity-40 ${user.status === 'Active' ? 'text-rose-600' : 'text-emerald-700'}`}
          >
            {user.status === 'Active' ? (
              <ShieldOff className="size-4" />
            ) : (
              <RotateCcw className="size-4" />
            )}
            {user.status === 'Active' ? 'Vô hiệu hóa' : 'Khôi phục tài khoản'}
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
  sort,
  onSortChange,
}: UsersTableProps) {
  const renderRoleOrCode = (user: UserSummary) =>
    accountGroup === 'Student' ? (
      <span className="font-mono text-xs font-medium tabular-nums text-slate-700">
        {user.userCode || 'Chưa cập nhật'}
      </span>
    ) : (
      <InfoCell
        className="gap-2.5"
        titleClassName="block truncate text-xs font-semibold leading-5 text-slate-700"
        descriptionClassName="mt-0.5 truncate text-xs font-normal leading-4 text-slate-500"
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
    );

  const columns: DataTableColumn<UserSummary, UsersSortField>[] = [
    {
      id: 'index',
      header: 'STT',
      widthClassName: 'w-14',
      align: 'center',
      headerClassName: 'whitespace-nowrap px-3 py-3.5',
      cellClassName: 'px-3 py-3 text-xs tabular-nums text-slate-400',
      cell: (_user, index) => (pageNumber - 1) * pageSize + index + 1,
    },
    {
      id: 'account',
      header: accountGroup === 'Student' ? 'Sinh viên' : 'Giảng viên / Cán bộ',
      sortKey: 'fullname',
      headerClassName: 'px-4 py-3.5',
      cellClassName: 'px-4 py-3',
      cell: (user) => (
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
              <Badge variant="info" className="px-1.5 py-0 text-xs">
                Bạn
              </Badge>
            ) : undefined
          }
        />
      ),
    },
    {
      id: 'codeOrRole',
      header: accountGroup === 'Student' ? 'MSSV' : 'Vai trò / Mã cán bộ',
      sortKey: 'usercode',
      widthClassName: 'w-52',
      headerClassName: 'whitespace-nowrap px-4 py-3.5',
      cellClassName: 'px-4 py-3',
      cell: renderRoleOrCode,
    },
    {
      id: 'lastLogin',
      header: 'Lần truy cập',
      sortKey: 'lastloginat',
      widthClassName: 'w-36',
      headerClassName: 'whitespace-nowrap px-4 py-3.5',
      cellClassName: 'px-4 py-3 whitespace-nowrap text-xs text-slate-500',
      cell: (user) => formatLastLogin(user.lastLoginAt),
    },
    {
      id: 'status',
      header: 'Trạng thái',
      sortKey: 'status',
      widthClassName: 'w-36',
      headerClassName: 'whitespace-nowrap px-4 py-3.5',
      cellClassName: 'px-4 py-3',
      cell: (user) => (
        <StatusBadge tone={statusTones[user.status]}>{STATUS_LABELS[user.status]}</StatusBadge>
      ),
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-44',
      align: 'center',
      headerClassName: 'whitespace-nowrap px-4 py-3.5',
      cellClassName: 'px-3 py-3 text-right',
      cell: (user) =>
        canManage ? (
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
        ),
    },
  ];

  return (
    <DataTable<UserSummary, UsersSortField>
      ariaLabel={accountGroup === 'Student' ? 'Danh sách tài khoản sinh viên' : 'Danh sách giảng viên và cán bộ'}
      data={users}
      columns={columns}
      getRowId={(user) => user.id}
      selectedRowId={selectedRowId}
      onRowSelect={onSelectRow ? (id) => onSelectRow(id) : undefined}
      onRowOpen={canManage ? onEdit : undefined}
      sort={sort}
      onSortChange={onSortChange}
      busy={isChangingStatus}
      tableClassName="min-w-[900px]"
      rowClassName="h-[72px]"
      empty={{ title: 'Không tìm thấy tài khoản phù hợp.' }}
      renderMobileItem={(user) => (
        <div className="space-y-3">
          <div className="flex items-start justify-between gap-3">
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
                  <Badge variant="info" className="px-1.5 py-0 text-xs">
                    Bạn
                  </Badge>
                ) : undefined
              }
            />
            <StatusBadge tone={statusTones[user.status]}>{STATUS_LABELS[user.status]}</StatusBadge>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 pt-2.5">
            <div className="min-w-0 text-xs text-slate-600">
              {renderRoleOrCode(user)}
            </div>
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
          </div>
        </div>
      )}
    />
  );
}
