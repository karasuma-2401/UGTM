import { Button } from '@/components/ui/button';
import { Badge, type BadgeProps } from '@/components/ui/badge';

import { SYSTEM_ROLES } from '../constants/systemRoles';
import { Eye, ShieldOff, RotateCcw } from 'lucide-react';

import { STATUS_LABELS, type AccountStatus, type UserSummary } from '../types/user.types';

interface UsersTableProps {
  users: UserSummary[];
  currentUserId?: string;
  canManage: boolean;
  isChangingStatus: boolean;
  onEdit: (user: UserSummary) => void;
  onChangeStatus: (user: UserSummary) => void;
}

const statusVariants: Record<AccountStatus, NonNullable<BadgeProps['variant']>> = {
  Active: 'success',
  PendingActivation: 'warning',
  Suspended: 'warning',
  Disabled: 'secondary',
};

function formatLastLogin(value: string | null): string {
  if (!value) return 'Chưa đăng nhập';

  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function UsersTable({
  users,
  currentUserId,
  canManage,
  isChangingStatus,
  onEdit,
  onChangeStatus,
}: UsersTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[760px] text-left text-sm">
        <thead className="bg-slate-50 text-xs text-slate-500">
          <tr>
            {['Người dùng', 'Vai trò / Mã', 'Lần truy cập', 'Trạng thái', 'Thao tác'].map(
              (heading) => (
                <th key={heading} scope="col" className="px-4 py-4 font-medium">
                  {heading}
                </th>
              ),
            )}
          </tr>
        </thead>

        <tbody className="divide-y divide-slate-100">
          {users.length === 0 ? (
            <tr>
              <td colSpan={5} className="px-4 py-12 text-center text-slate-500">
                Không tìm thấy tài khoản phù hợp.
              </td>
            </tr>
          ) : (
            users.map((user) => (
              <tr key={user.id} className="hover:bg-slate-50/60">
                <td className="px-4 py-5">
                  <div className="font-medium text-slate-800">{user.fullName}</div>
                  <div className="mt-1 text-xs text-slate-500">{user.email}</div>
                </td>

                <td className="px-4 py-5">
                  <div>
                    {user.roles
                      .map(
                        (name) =>
                          SYSTEM_ROLES.find((role) => role.roleName === name)?.title ?? name,
                      )
                      .join(', ')}
                  </div>
                  <div className="mt-1 text-xs text-slate-500">{user.userCode ?? '—'}</div>
                </td>

                <td className="px-4 py-5 text-xs text-slate-500">
                  {formatLastLogin(user.lastLoginAt)}
                </td>

                <td className="px-4 py-5">
                  <Badge variant={statusVariants[user.status]} dot>
                    {STATUS_LABELS[user.status]}
                  </Badge>
                </td>

                <td className="px-4 py-5">
                  {canManage ? (
                    <div className="flex flex-wrap items-center gap-2">
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
                            user.id === currentUserId ? 'Không thể vô hiệu hóa tài khoản đang đăng nhập' : undefined
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
    </div>
  );
}
