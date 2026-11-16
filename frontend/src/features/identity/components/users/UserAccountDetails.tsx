import { Avatar } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { StatusBadge } from '@/components/ui/status-badge';
import { SYSTEM_ROLES } from '../../constants/systemRoles';
import { STATUS_LABELS, type AccountGroup, type UserSummary } from '../../types/user.types';

function dateLabel(value: string | null, fallback: string) {
  if (!value) return fallback;
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? fallback
    : new Intl.DateTimeFormat('vi-VN', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(date);
}

export function UserAccountHeader({ user }: { user: UserSummary }) {
  return (
    <div className="flex min-w-0 flex-1 items-center gap-3">
      <Avatar
        name={user.fullName}
        muted={user.status === 'Disabled'}
        className="size-12 text-base"
      />
      <div className="min-w-0 flex-1">
        <h2 className="break-words text-base font-semibold text-ink-primary">
          {user.fullName}
        </h2>
        <div className="mt-1">
          <p className="break-all text-xs font-normal text-ink-muted">
            {user.userCode || 'Chưa cập nhật mã'}
          </p>
        </div>
      </div>
      <StatusBadge
        className="shrink-0"
        tone={
          user.status === 'Active'
            ? 'success'
            : user.status === 'PendingActivation'
              ? 'warning'
              : user.status === 'Suspended'
                ? 'orange'
                : 'error'
        }
      >
        {STATUS_LABELS[user.status]}
      </StatusBadge>
    </div>
  );
}

export function UserAccountDetails({
  user,
  accountGroup,
}: {
  user: UserSummary;
  accountGroup: AccountGroup;
}) {
  return (
    <div className="space-y-4 p-5 sm:p-6">
      <section
        aria-label="Thông tin cá nhân"
        className="space-y-3 rounded-xl border border-line/80 bg-surface-subtle/60 p-4"
      >
        <h3 className="text-sm font-semibold text-ink-primary">
          Thông tin cá nhân
        </h3>
        <dl className="divide-y divide-line/70 text-sm">
          {[
            ['Họ và tên', user.fullName],
            ['Email', user.email],
            [
              accountGroup === 'Student' ? 'Mã số sinh viên' : 'Mã cán bộ',
              user.userCode || 'Chưa cập nhật',
            ],
            ['Số điện thoại', user.phoneNumber || 'Chưa cập nhật'],
          ].map(([label, value]) => (
            <div key={label} className="grid grid-cols-[7.5rem_minmax(0,1fr)] gap-3 py-2.5 first:pt-1 last:pb-0">
              <dt className="text-ink-muted">{label}</dt>
              <dd className="break-words font-medium text-ink-primary [overflow-wrap:anywhere]">
                {value}
              </dd>
            </div>
          ))}
        </dl>
      </section>

      <section
        aria-label="Vai trò hệ thống"
        className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line/80 bg-surface-subtle/60 p-4"
      >
        <h3 className="text-sm font-semibold text-ink-primary">
          Vai trò hệ thống
        </h3>
        <div className="ml-auto flex flex-wrap justify-end gap-2">
          {user.roles.map((role) => (
            <Badge key={role} variant="info" className="px-2.5 py-1">
              {SYSTEM_ROLES.find((item) => item.roleName === role)?.title ?? role}
            </Badge>
          ))}
        </div>
      </section>

      <section
        aria-label="Hoạt động tài khoản"
        className="space-y-3 rounded-xl border border-line/80 bg-surface-subtle/60 p-4"
      >
        <h3 className="text-sm font-semibold text-ink-primary">
          Hoạt động tài khoản
        </h3>
        <dl className="divide-y divide-line/70 text-sm">
          <div className="flex flex-wrap justify-between gap-2 py-2 first:pt-1">
            <dt className="text-ink-muted">Ngày tạo</dt>
            <dd className="font-medium tabular-nums text-ink-heading">
              {dateLabel(user.createdAt, 'Chưa có thông tin')}
            </dd>
          </div>
          <div className="flex flex-wrap justify-between gap-2 py-2 last:pb-0">
            <dt className="text-ink-muted">Truy cập gần nhất</dt>
            <dd className="font-medium tabular-nums text-ink-heading">
              {dateLabel(user.lastLoginAt, 'Chưa đăng nhập')}
            </dd>
          </div>
        </dl>
      </section>
    </div>
  );
}
