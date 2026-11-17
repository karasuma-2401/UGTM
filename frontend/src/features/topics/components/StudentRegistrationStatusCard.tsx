import {
  ArrowRight,
  Check,
  CheckCircle2,
  Clock,
  GraduationCap,
  Lock,
  Mail,
  ShieldAlert,
} from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { invitationExpired, registrationDate } from '../studentRegistration';
import type { StudentRegistrationStatus, RegistrationReceipt } from '../types/studentTopic.types';

export function StudentRegistrationStatusCard({
  status,
  loading,
  error,
  busy,
  receipt,
  onRetry,
  onConfirm,
  onViewGroup,
}: {
  status?: StudentRegistrationStatus;
  loading: boolean;
  error: string;
  busy: boolean;
  receipt: RegistrationReceipt | null;
  onRetry: () => void;
  onConfirm: () => void;
  onViewGroup?: () => void;
}) {
  const expired = Boolean(status && invitationExpired(status));
  const pending = status?.confirmationStatus === 'Pending';
  const confirmed = Boolean(status?.hasRegistered && status.confirmationStatus === 'Confirmed');
  const blocked = Boolean(!error && status?.isRegistrationBlocked);

  const accentBarClass = blocked
    ? 'bg-gradient-to-r from-rose-500 via-rose-400 to-amber-400'
    : confirmed
      ? 'bg-gradient-to-r from-emerald-500 via-teal-500 to-blue-600'
      : pending && !expired
        ? 'bg-gradient-to-r from-amber-400 via-orange-400 to-amber-500'
        : 'bg-[linear-gradient(90deg,#2054a5_0%,#2563eb_55%,#fa7921_100%)]';

  const headerTintClass = blocked
    ? 'bg-gradient-to-r from-surface-card via-surface-card to-tone-danger-bg/45'
    : confirmed
      ? 'bg-gradient-to-r from-surface-card via-surface-card to-tone-success-bg/45'
      : pending && !expired
        ? 'bg-gradient-to-r from-surface-card via-surface-card to-tone-warning-bg/45'
        : 'bg-gradient-to-r from-surface-card via-surface-card to-tone-info-bg/45';

  const iconTileClass = blocked
    ? 'bg-tone-danger-bg text-tone-danger-icon ring-tone-danger-border/60'
    : confirmed
      ? 'bg-tone-success-bg text-tone-success-icon ring-tone-success-border/60'
      : pending && !expired
        ? 'bg-tone-warning-bg text-tone-warning-icon ring-tone-warning-border/60'
        : 'bg-tone-info-bg text-primary ring-tone-info-border/60';

  const step1State: 'done' | 'active' | 'blocked' = blocked
    ? 'blocked'
    : status?.hasRegistered
      ? 'done'
      : 'active';
  const step2State: 'done' | 'active' | 'expired' | 'upcoming' = confirmed
    ? 'done'
    : pending
      ? expired
        ? 'expired'
        : 'active'
      : 'upcoming';
  const step3State: 'done' | 'upcoming' = confirmed ? 'done' : 'upcoming';

  return (
    <Card
      className="overflow-hidden rounded-2xl border-line/80 shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]"
      aria-busy={busy}
    >
      <div aria-hidden="true" className={`h-1 w-full ${accentBarClass}`} />
      <div className={`flex flex-wrap items-start justify-between gap-4 p-5 sm:p-6 ${headerTintClass}`}>
        <div className="flex min-w-0 flex-1 items-start gap-3.5">
          <span
            className={`flex size-11 shrink-0 items-center justify-center rounded-xl ring-4 ${iconTileClass}`}
            aria-hidden="true"
          >
            {blocked ? <Lock className="size-5" /> : <GraduationCap className="size-5" />}
          </span>
          <div className="min-w-0 flex-1 space-y-1.5">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-xs font-semibold uppercase tracking-wider text-ink-muted">
                Đăng ký của tôi
              </h2>
              {status?.groupId && (
                <span className="inline-flex items-center rounded-md border border-line-input/80 bg-surface-elevated/90 px-2 py-0.5 font-mono text-[11px] font-medium text-ink-muted">
                  Mã nhóm: {status.groupId}
                </span>
              )}
            </div>

            {loading ? (
              <div role="status" className="space-y-2 pt-1">
                <span className="sr-only">Đang tải đăng ký...</span>
                <Skeleton className="h-5 w-64 max-w-full" />
                <Skeleton className="h-3.5 w-44 max-w-full" />
              </div>
            ) : error ? (
              <div role="alert" className="space-y-2 pt-1">
                <p className="text-sm text-tone-danger-text">{error}</p>
                <Button type="button" variant="outline" size="sm" onClick={onRetry} disabled={busy}>
                  Thử lại trạng thái
                </Button>
              </div>
            ) : (
              status && (
                <>
                  <p className="break-words text-base font-semibold leading-snug text-ink-primary">
                    {status.hasRegistered
                      ? status.topicTitleVi || 'Đã có hồ sơ đăng ký đề tài'
                      : 'Bạn chưa có đăng ký đề tài'}
                  </p>
                  {confirmed ? (
                    <p className="text-xs leading-5 text-ink-body">
                      Bạn đã được ghi nhận vào đề tài, không cần đăng ký lại.
                    </p>
                  ) : pending ? (
                    <p className="flex items-center gap-1.5 text-xs leading-5 text-ink-body">
                      <Clock className="size-3.5 shrink-0 text-tone-warning-icon" aria-hidden="true" />
                      <span>
                        {expired
                          ? 'Mã tham gia đã hết hạn.'
                          : `Hạn xác nhận: ${registrationDate(status.confirmationExpiresAtUtc)}`}
                      </span>
                    </p>
                  ) : (
                    !blocked && (
                      <p className="text-xs leading-5 text-ink-muted">
                        Chọn đề tài còn chỗ trống bên dưới để đăng ký cá nhân hoặc mời bạn cùng nhóm.
                      </p>
                    )
                  )}
                </>
              )
            )}
          </div>
        </div>

        {!loading && !error && status && (
          <div className="flex flex-wrap items-center gap-2.5 sm:justify-end">
            <StatusBadge
              tone={confirmed ? 'success' : pending ? (expired ? 'neutral' : 'warning') : 'neutral'}
            >
              {confirmed
                ? 'Đã đăng ký'
                : pending
                  ? expired
                    ? 'Hết hạn xác nhận'
                    : 'Chờ bạn xác nhận'
                  : status.hasRegistered
                    ? 'Đã ghi nhận hồ sơ'
                    : 'Chưa đăng ký'}
            </StatusBadge>
            {pending && !expired && (
              <Button
                type="button"
                size="sm"
                leftIcon={<Mail />}
                onClick={onConfirm}
                className="h-9 rounded-xl shadow-xs"
              >
                Xác nhận tham gia
              </Button>
            )}
            {confirmed && status.groupId && onViewGroup && (
              <Button
                type="button"
                size="sm"
                variant="outline"
                rightIcon={<ArrowRight />}
                onClick={onViewGroup}
                className="h-9 rounded-xl border-line-input bg-surface-elevated shadow-2xs hover:border-line-strong"
              >
                Xem nhóm và tiến độ
              </Button>
            )}
          </div>
        )}
      </div>

      {!loading && !error && status && (
        <ol
          aria-label="Hành trình đăng ký đề tài"
          className="grid gap-2 border-t border-line-subtle bg-surface-subtle/60 px-5 py-3 sm:grid-cols-3 sm:gap-3 sm:px-6"
        >
          {[
            {
              key: 'select',
              step: 1,
              label: 'Chọn và gửi đăng ký',
              state: step1State,
            },
            {
              key: 'confirm',
              step: 2,
              label: 'Xác nhận tham gia nhóm',
              state: step2State,
            },
            {
              key: 'track',
              step: 3,
              label: 'Theo dõi đề cương và tiến độ',
              state: step3State,
            },
          ].map((item) => {
            const isDone = item.state === 'done';
            const isActive = item.state === 'active';
            const isAlert = item.state === 'blocked' || item.state === 'expired';

            const badgeClass = isDone
              ? 'bg-emerald-600 text-white'
              : isActive
                ? 'bg-brand-solid text-white'
                : isAlert
                  ? 'bg-rose-600 text-white'
                  : 'bg-surface-strong text-ink-body';

            const labelClass = isDone
              ? 'font-medium text-tone-success-strong'
              : isActive
                ? 'font-semibold text-ink-primary'
                : isAlert
                  ? 'font-medium text-tone-danger-text'
                  : 'font-medium text-ink-muted';

            return (
              <li key={item.key} className="flex items-center gap-2.5 text-xs">
                <span
                  aria-hidden="true"
                  className={`flex size-5 shrink-0 items-center justify-center rounded-full text-[11px] font-bold tabular-nums ${badgeClass}`}
                >
                  {isDone ? <Check className="size-3" /> : item.step}
                </span>
                <span className={labelClass}>{item.label}</span>
              </li>
            );
          })}
        </ol>
      )}

      {!error && status?.isRegistrationBlocked && (
        <div
          role="alert"
          className="flex items-center gap-2 border-t border-tone-danger-border bg-tone-danger-bg px-5 py-3 text-sm text-tone-danger-text sm:px-6"
        >
          <Lock className="size-4 shrink-0" aria-hidden="true" />
          <p>Quyền đăng ký đang bị khóa. Vui lòng liên hệ Khoa để được hỗ trợ.</p>
        </div>
      )}
      {!error && status && status.violationCount > 0 && (
        <div className="flex items-center gap-2 border-t border-tone-warning-border bg-tone-warning-bg px-5 py-2.5 text-xs text-tone-warning-strong sm:px-6">
          <ShieldAlert className="size-3.5 shrink-0 text-tone-warning-icon" aria-hidden="true" />
          <p>Bạn có {status.violationCount} lần cảnh báo do quá hạn xác nhận.</p>
        </div>
      )}
      {receipt && (
        <div
          role="status"
          className="flex gap-2.5 border-t border-tone-success-border bg-tone-success-bg px-5 py-3.5 text-sm text-tone-success-strong sm:px-6"
        >
          <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-tone-success-icon" />
          <div>
            <p className="font-semibold text-tone-success-strong">
              {receipt.topicTitle}
            </p>
            <p className="text-xs sm:text-sm">{receipt.message}</p>
            {receipt.isPendingConfirmation && (
              <p className="mt-1 text-xs leading-5 text-tone-success-strong">
                Đã gửi lời mời tới MSSV <span className="font-mono font-semibold">{receipt.partnerUserCode}</span>. Hạn xác nhận:{' '}
                {registrationDate(receipt.confirmationExpiresAtUtc)}. Đăng ký của bạn đã có hiệu
                lực.
              </p>
            )}
          </div>
        </div>
      )}
    </Card>
  );
}
