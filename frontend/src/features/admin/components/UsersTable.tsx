import { Button } from '@/components/ui/button';
import { Avatar } from '@/components/ui/avatar';
import { InfoCell } from '@/components/ui/info-cell';
import { Badge, type BadgeProps } from '@/components/ui/badge';

import { SYSTEM_ROLES } from '../constants/systemRoles';
import {
  Eye,
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
} from '../types/user.types';

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
}

const statusVariants: Record<AccountStatus, NonNullable<BadgeProps['variant']>> = {
  Active: 'success',
  PendingActivation: 'warning',
  Suspended: 'warning',
  Disabled: 'secondary',
};

const statusClasses: Record<AccountStatus, string> = {
  Active: 'border-emerald-200/70 bg-emerald-50 text-emerald-700',
  PendingActivation: 'border-amber-200/70 bg-amber-50 text-amber-700',
  Suspended: 'border-orange-200/70 bg-orange-50 text-orange-700',
  Disabled: 'border-slate-200 bg-slate-100 text-slate-500',
};

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
}: UsersTableProps) {
  return (
    <table className="w-full min-w-[1040px] table-fixed text-left text-sm">
      <colgroup>
        <col className="w-14" />
        <col />
        <col className="w-52" />
        <col className="w-36" />
        <col className="w-36" />
        <col className="w-[248px]" />
      </colgroup>
      <thead className="sticky top-0 z-10 h-14 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600">
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
            <th key={heading} scope="col" className="whitespace-nowrap px-4 py-4 font-bold">
              {heading}
            </th>
          ))}
        </tr>
      </thead>

      <tbody className="divide-y divide-slate-100">
        {users.length === 0 ? (
          <tr>
            <td colSpan={6} className="px-4 py-12 text-center text-slate-500">
              Không tìm thấy tài khoản phù hợp.
            </td>
          </tr>
        ) : (
          users.map((user, index) => (
            <tr key={user.id} className="h-[88px] transition-colors hover:bg-blue-50/30">
              <td className="px-3 py-4 text-center text-xs tabular-nums text-slate-400">
                {(pageNumber - 1) * pageSize + index + 1}
              </td>
              <td className="px-4 py-4">
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
              <td className="px-4 py-4">
                {accountGroup === 'Student' ? (
                  <InfoCell
                    className="gap-2.5"
                    title={
                      <span className="truncate tabular-nums" title={user.userCode || undefined}>
                        {user.userCode || 'Chưa cập nhật'}
                      </span>
                    }
                    titleClassName="text-xs leading-5"
                    description="Sinh viên"
                    descriptionClassName="mt-0.5 text-[11px] leading-4"
                    leading={
                      <span
                        aria-hidden="true"
                        className="flex size-8 items-center justify-center rounded-lg bg-blue-50 text-blue-600 ring-1 ring-inset ring-blue-100"
                      >
                        <GraduationCap className="size-4" />
                      </span>
                    }
                  />
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

              <td className="whitespace-nowrap px-4 py-4 text-xs text-slate-500">
                {formatLastLogin(user.lastLoginAt)}
              </td>

              <td className="px-4 py-4">
                <Badge
                  variant={statusVariants[user.status]}
                  dot
                  className={`whitespace-nowrap px-2.5 py-1 text-[11px] font-medium shadow-none ${statusClasses[user.status]}`}
                >
                  {STATUS_LABELS[user.status]}
                </Badge>
              </td>

              <td className="px-4 py-4">
                {canManage ? (
                  <div className="flex items-center gap-2 whitespace-nowrap">
                    <Button
                      type="button"
                      variant="soft"
                      size="sm"
                      disabled={isChangingStatus}
                      onClick={() => onEdit(user)}
                      leftIcon={<Eye />}
                      aria-label={`Xem chi tiết tài khoản ${user.fullName}`}
                      className="h-9 cursor-pointer"
                    >
                      Chi tiết
                    </Button>

                    {user.status !== 'PendingActivation' && (
                      <Button
                        type="button"
                        variant={user.status === 'Active' ? 'dangerOutline' : 'successOutline'}
                        size="sm"
                        disabled={isChangingStatus || user.id === currentUserId}
                        title={
                          user.id === currentUserId
                            ? 'Không thể vô hiệu hóa tài khoản đang đăng nhập'
                            : undefined
                        }
                        onClick={() => onChangeStatus(user)}
                        leftIcon={user.status === 'Active' ? <ShieldOff /> : <RotateCcw />}
                        aria-label={
                          user.status === 'Active'
                            ? `Vô hiệu hóa tài khoản ${user.fullName}`
                            : `Khôi phục tài khoản ${user.fullName}`
                        }
                        className="h-9 cursor-pointer"
                      >
                        {user.status === 'Active' ? 'Vô hiệu hóa' : 'Khôi phục'}
                      </Button>
                    )}
                    {user.status === 'PendingActivation' && (
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={isChangingStatus}
                        leftIcon={<Mail />}
                        onClick={() => onResendActivation(user)}
                        aria-label={`Gửi email kích hoạt cho ${user.fullName}`}
                        className="h-9 cursor-pointer"
                      >
                        Gửi kích hoạt
                      </Button>
                    )}
                  </div>
                ) : (
                  <span className="text-xs text-slate-400">Chỉ xem</span>
                )}
              </td>
            </tr>
          ))
        )}
      </tbody>
    </table>
  );
}
