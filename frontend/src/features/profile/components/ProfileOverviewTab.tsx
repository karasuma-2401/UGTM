import { useMemo, useState, type FormEvent } from 'react';
import {
  Check,
  Copy,
  Mail,
  Phone,
  RotateCcw,
  Save,
  Search,
  ShieldCheck,
  User as UserIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { notify } from '@/shared/notifications';
import type { UserProfile } from '@/features/auth/types/auth.types';
import {
  formatVietnamDateTime,
  groupUserPermissions,
  isValidVietnamPhone,
  normalizePhoneValue,
} from '../utils/profileFormatters';

interface ProfileOverviewTabProps {
  user: UserProfile;
  phoneInput: string;
  onPhoneInputChange: (value: string) => void;
  isDirty: boolean;
  isSavingPhone: boolean;
  phoneServerError: string | null;
  onSavePhone: () => void;
  onResetPhone: () => void;
}

export function ProfileOverviewTab({
  user,
  phoneInput,
  onPhoneInputChange,
  isDirty,
  isSavingPhone,
  phoneServerError,
  onSavePhone,
  onResetPhone,
}: ProfileOverviewTabProps) {
  const [copiedKey, setCopiedKey] = useState<'code' | 'email' | null>(null);
  const [permQuery, setPermQuery] = useState('');
  const [phoneValidationError, setPhoneValidationError] = useState<string | null>(null);

  const isAdmin = user.roles.some((role) => role.toLowerCase() === 'admin');
  const groupedPermissions = useMemo(() => groupUserPermissions(user), [user]);

  const filteredModules = useMemo(() => {
    const q = permQuery.trim().toLowerCase();
    if (!q) return groupedPermissions;
    return groupedPermissions
      .map((group) => {
        const matchModule = group.moduleLabel.toLowerCase().includes(q);
        const items = matchModule
          ? group.items
          : group.items.filter(
              (item) =>
                item.permissionCode.toLowerCase().includes(q) ||
                (item.permissionName ?? '').toLowerCase().includes(q),
            );
        return items.length > 0 ? { ...group, items } : null;
      })
      .filter((item): item is NonNullable<typeof item> => item !== null);
  }, [groupedPermissions, permQuery]);

  const totalPermissionCount = user.permissions.length;

  const handleCopy = async (key: 'code' | 'email', value: string, label: string) => {
    try {
      await navigator.clipboard.writeText(value);
      setCopiedKey(key);
      notify.success(`Đã sao chép ${label}.`);
      window.setTimeout(() => {
        setCopiedKey((current) => (current === key ? null : current));
      }, 2000);
    } catch {
      notify.error(`Không thể sao chép ${label}. Vui lòng thử lại.`);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (isSavingPhone || !isDirty) return;

    const normalized = normalizePhoneValue(phoneInput);
    if (!isValidVietnamPhone(normalized)) {
      setPhoneValidationError(
        'Số điện thoại phải gồm 10 chữ số (ví dụ: 0901234567 hoặc +84901234567).',
      );
      return;
    }

    setPhoneValidationError(null);
    onSavePhone();
  };

  const timelineNodes = [
    {
      key: 'created',
      label: 'Ngày tạo tài khoản',
      value: formatVietnamDateTime(user.createdAt),
    },
    {
      key: 'activated',
      label: 'Kích hoạt email',
      value: formatVietnamDateTime(user.activatedAt, 'Chưa kích hoạt'),
    },
    {
      key: 'password',
      label: 'Đổi mật khẩu cuối',
      value: formatVietnamDateTime(user.passwordChangedAt, 'Chưa ghi nhận'),
    },
    {
      key: 'login',
      label: 'Đăng nhập gần nhất',
      value: formatVietnamDateTime(user.lastLoginAt, 'Phiên hiện tại'),
    },
  ];

  const isStudent = user.roles.some((r) => r.toLowerCase() === 'student');
  const codeLabel = isStudent ? 'Mã số sinh viên' : 'Mã cán bộ / giảng viên';

  return (
    <Card className="relative overflow-hidden divide-y divide-line-subtle">
      {/* Section 1: Institutional Identity & Editable Phone */}
      <form onSubmit={handleSubmit} className="relative">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-16 -top-16 size-56 rounded-full bg-blue-500/[0.05] dark:bg-blue-500/[0.1] blur-3xl"
        />
        <div
          aria-hidden="true"
          className="pointer-events-none absolute right-10 top-12 size-36 rounded-full bg-orange-400/[0.04] dark:bg-orange-400/[0.08] blur-2xl"
        />

        <div className="relative z-10 p-5 sm:p-6 space-y-5">
          <div>
            <h2 className="text-base font-semibold text-ink-primary">
              Thông tin định danh và Liên hệ
            </h2>
            <p className="mt-0.5 text-xs text-ink-muted leading-relaxed">
              Thông tin định danh do nhà trường cấp; bạn có thể cập nhật số điện thoại liên hệ.
            </p>
          </div>

          {phoneServerError && (
            <InlineAlert tone="error" title="Không thể cập nhật số điện thoại">
              {phoneServerError}
            </InlineAlert>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* Full Name (Read-only) */}
            <div className="space-y-1.5">
              <label htmlFor="profile-fullname" className="text-xs font-semibold text-ink-body">
                Họ và tên
              </label>
              <Input
                id="profile-fullname"
                value={user.fullName}
                readOnly
                disabled
                leftIcon={<UserIcon />}
                className="bg-surface-subtle/80"
              />
            </div>

            {/* User Code (Read-only + Copy) */}
            <div className="space-y-1.5">
              <label htmlFor="profile-usercode" className="text-xs font-semibold text-ink-body">
                {codeLabel}
              </label>
              <Input
                id="profile-usercode"
                value={user.userCode || 'Chưa cập nhật'}
                readOnly
                disabled
                className="bg-surface-subtle/80 font-medium tabular-nums"
                rightIcon={
                  user.userCode ? (
                    <button
                      type="button"
                      onClick={() => void handleCopy('code', user.userCode!, codeLabel)}
                      aria-label={`Sao chép ${codeLabel}`}
                      title={`Sao chép ${codeLabel}`}
                      className="pointer-events-auto flex size-7 items-center justify-center rounded-md text-ink-muted hover:bg-surface-muted hover:text-primary transition-colors cursor-pointer"
                    >
                      {copiedKey === 'code' ? (
                        <Check className="size-3.5 text-tone-success-icon" />
                      ) : (
                        <Copy className="size-3.5" />
                      )}
                    </button>
                  ) : undefined
                }
              />
            </div>

            {/* Email (Read-only + Copy) */}
            <div className="space-y-1.5">
              <label htmlFor="profile-email" className="text-xs font-semibold text-ink-body">
                Email UIT
              </label>
              <Input
                id="profile-email"
                value={user.email}
                readOnly
                disabled
                leftIcon={<Mail />}
                className="bg-surface-subtle/80"
                rightIcon={
                  <button
                    type="button"
                    onClick={() => void handleCopy('email', user.email, 'Email')}
                    aria-label="Sao chép Email"
                    title="Sao chép Email"
                    className="pointer-events-auto flex size-7 items-center justify-center rounded-md text-ink-muted hover:bg-surface-muted hover:text-primary transition-colors cursor-pointer"
                  >
                    {copiedKey === 'email' ? (
                      <Check className="size-3.5 text-tone-success-icon" />
                    ) : (
                      <Copy className="size-3.5" />
                    )}
                  </button>
                }
              />
            </div>

            {/* Phone Number (Editable) */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between gap-2">
                <label htmlFor="profile-phone" className="text-xs font-semibold text-ink-body">
                  Số điện thoại liên hệ
                </label>
                {isDirty && (
                  <span className="text-[11px] font-medium text-primary">Chưa lưu</span>
                )}
              </div>
              <Input
                id="profile-phone"
                type="tel"
                autoComplete="tel"
                maxLength={20}
                disabled={isSavingPhone || Boolean(user.mustChangePassword)}
                value={phoneInput}
                onChange={(event) => {
                  setPhoneValidationError(null);
                  onPhoneInputChange(event.target.value);
                }}
                leftIcon={<Phone />}
                placeholder="VD: 0901234567"
                error={phoneValidationError ?? undefined}
                helperText={
                  !phoneValidationError
                    ? user.mustChangePassword
                      ? 'Vui lòng đổi mật khẩu ở tab Bảo mật trước khi cập nhật thông tin.'
                      : 'Định dạng 10 chữ số Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).'
                    : undefined
                }
              />
            </div>
          </div>

          {/* Contextual Dirty Action Bar */}
          {isDirty && !user.mustChangePassword && (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-tone-info-border bg-tone-info-bg/50 px-4 py-2.5 animate-in fade-in duration-150">
              <p className="text-xs font-medium text-tone-info-strong">
                Bạn có thay đổi số điện thoại chưa lưu.
              </p>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={isSavingPhone}
                  leftIcon={<RotateCcw className="size-3.5" />}
                  onClick={() => {
                    setPhoneValidationError(null);
                    onResetPhone();
                  }}
                >
                  Hoàn tác
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  isLoading={isSavingPhone}
                  loadingText="Đang lưu..."
                  leftIcon={<Save className="size-3.5" />}
                >
                  Lưu số điện thoại
                </Button>
              </div>
            </div>
          )}
        </div>
      </form>

      {/* Section 2: Hairline 4-Node Account Timeline Strip */}
      <section aria-label="Mốc hoạt động tài khoản" className="bg-surface-subtle/35">
        <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 divide-y sm:divide-y-0 sm:divide-x divide-line-subtle">
          {timelineNodes.map((node) => (
            <div key={node.key} className="px-5 py-4 space-y-1">
              <p className="text-[11px] font-medium text-ink-muted">{node.label}</p>
              <p className="text-xs sm:text-sm font-semibold tabular-nums text-ink-primary">
                {node.value}
              </p>
            </div>
          ))}
        </div>
      </section>

      {/* Section 3: Read-only Permissions List Grouped by Module */}
      <section aria-label="Quyền hạn theo phân hệ" className="p-5 sm:p-6 space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <h2 className="text-base font-semibold text-ink-primary">
              Phân hệ và Quyền truy cập
            </h2>
            <p className="mt-0.5 text-xs text-ink-muted">
              {isAdmin
                ? 'Toàn quyền quản trị hệ thống.'
                : `Tổng hợp ${totalPermissionCount} quyền theo vai trò hiện tại.`}
            </p>
          </div>

          {totalPermissionCount >= 5 && (
            <div className="w-full sm:w-64 shrink-0">
              <Input
                type="search"
                value={permQuery}
                onChange={(event) => setPermQuery(event.target.value)}
                leftIcon={<Search className="size-3.5" />}
                placeholder="Lọc theo phân hệ hoặc mã quyền..."
                aria-label="Tìm kiếm quyền hạn"
                className="h-8 text-xs"
              />
            </div>
          )}
        </div>

        {isAdmin && (
          <InlineAlert tone="info" title="Đặc quyền Quản trị viên (Admin Role Bypass)">
            Tài khoản mang vai trò Quản trị viên được hệ thống tự động cấp toàn quyền truy cập các phân hệ nghiệp vụ bất kể danh sách claim chi tiết.
          </InlineAlert>
        )}

        {groupedPermissions.length === 0 ? (
          <div className="rounded-xl border border-dashed border-line-input bg-surface-subtle/50 p-6 text-center space-y-1.5">
            <ShieldCheck className="size-6 mx-auto text-ink-subtle" aria-hidden="true" />
            <p className="text-sm font-medium text-ink-primary">
              Không có quyền chi tiết bổ sung
            </p>
            <p className="text-xs text-ink-muted">
              Tài khoản của bạn hoạt động theo phạm vi vai trò mặc định ({user.roles.join(', ') || 'Thành viên'}).
            </p>
          </div>
        ) : filteredModules.length === 0 ? (
          <div className="rounded-xl border border-line-subtle bg-surface-subtle/50 p-6 text-center space-y-2">
            <p className="text-sm font-medium text-ink-primary">
              Không tìm thấy quyền phù hợp với &ldquo;{permQuery}&rdquo;
            </p>
            <Button type="button" variant="outline" size="sm" onClick={() => setPermQuery('')}>
              Xóa bộ lọc
            </Button>
          </div>
        ) : (
          <div className="rounded-xl border border-line overflow-hidden divide-y divide-line bg-surface-card">
            {filteredModules.map((group) => (
              <div key={group.moduleKey}>
                <div className="bg-surface-subtle/65 px-4 py-2 flex items-center justify-between gap-2 border-b border-line-subtle">
                  <h3 className="text-xs font-semibold text-ink-secondary truncate">
                    {group.moduleLabel}
                  </h3>
                  <span className="text-[11px] text-ink-subtle tabular-nums shrink-0">
                    {group.items.length} quyền
                  </span>
                </div>

                <ul className="divide-y divide-line-subtle/80">
                  {group.items.map((perm) => (
                    <li
                      key={perm.permissionCode}
                      className="px-4 py-2.5 flex flex-col sm:flex-row sm:items-center justify-between gap-1 sm:gap-4 text-xs hover:bg-surface-subtle/35 transition-colors"
                    >
                      <span className="flex items-center gap-2 font-medium text-ink-primary min-w-0">
                        <Check
                          className="size-3.5 text-tone-success-icon shrink-0"
                          aria-hidden="true"
                        />
                        <span className="truncate">{perm.permissionName || perm.permissionCode}</span>
                      </span>
                      <code className="pl-5.5 sm:pl-0 shrink-0 font-mono text-[11px] text-ink-subtle">
                        {perm.permissionCode}
                      </code>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}
      </section>
    </Card>
  );
}
