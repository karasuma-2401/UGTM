import { GraduationCap, Mail, CheckCircle2 } from 'lucide-react';
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
}: {
  status?: StudentRegistrationStatus;
  loading: boolean;
  error: string;
  busy: boolean;
  receipt: RegistrationReceipt | null;
  onRetry: () => void;
  onConfirm: () => void;
}) {
  const expired = status && invitationExpired(status);
  const pending = status?.confirmationStatus === 'Pending';
  const confirmed = status?.hasRegistered && status.confirmationStatus === 'Confirmed';
  return (
    <Card className="overflow-hidden rounded-2xl border-slate-200/80 shadow-sm" aria-busy={busy}>
      <div className="flex flex-wrap items-start justify-between gap-4 p-5 sm:p-6">
        <div className="flex min-w-0 flex-1 items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-primary">
            <GraduationCap className="size-5" aria-hidden="true" />
          </span>
          <div className="min-w-0 flex-1 space-y-2">
            <h2 className="text-sm font-semibold text-slate-900">Đăng ký của tôi</h2>
            {loading ? (
              <div role="status" className="space-y-2">
                <span className="sr-only">Đang tải đăng ký...</span>
                <Skeleton className="h-5 w-56 max-w-full" />
                <Skeleton className="h-3 w-40 max-w-full" />
              </div>
            ) : error ? (
              <div role="alert" className="space-y-2">
                <p className="text-sm text-rose-700">{error}</p>
                <Button type="button" variant="outline" size="sm" onClick={onRetry} disabled={busy}>
                  Thử lại trạng thái
                </Button>
              </div>
            ) : (
              status && (
                <>
                  <p className="break-words font-medium text-slate-800">
                    {status.hasRegistered
                      ? status.topicTitleVi || 'Đã có hồ sơ đăng ký đề tài'
                      : 'Bạn chưa có đăng ký đề tài'}
                  </p>
                  {confirmed && (
                    <p className="text-xs leading-5 text-slate-500">
                      Bạn đã được ghi nhận vào đề tài, không cần đăng ký lại.
                    </p>
                  )}
                  {pending && (
                    <div className="flex flex-wrap items-center gap-3 text-xs text-slate-500">
                      <span>
                        {expired
                          ? 'Mã tham gia đã hết hạn.'
                          : `Hạn xác nhận: ${registrationDate(status.confirmationExpiresAtUtc)}`}
                      </span>
                      {!expired && (
                        <Button
                          type="button"
                          size="sm"
                          variant="soft"
                          leftIcon={<Mail />}
                          onClick={onConfirm}
                        >
                          Xác nhận tham gia
                        </Button>
                      )}
                    </div>
                  )}
                  {status.groupId && (
                    <p className="break-all text-xs text-slate-400">Mã nhóm: {status.groupId}</p>
                  )}
                </>
              )
            )}
          </div>
        </div>
        {!loading && !error && status && (
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
        )}
      </div>
      {!error && status?.isRegistrationBlocked && (
        <p
          role="alert"
          className="border-t border-rose-100 bg-rose-50 px-5 py-3 text-sm text-rose-700"
        >
          Quyền đăng ký đang bị khóa. Vui lòng liên hệ Khoa để được hỗ trợ.
        </p>
      )}
      {!error && status && status.violationCount > 0 && (
        <p className="border-t border-amber-100 bg-amber-50/60 px-5 py-3 text-xs text-amber-800">
          Bạn có {status.violationCount} lần cảnh báo do quá hạn xác nhận.
        </p>
      )}
      {receipt && (
        <div
          role="status"
          className="flex gap-2 border-t border-emerald-100 bg-emerald-50/70 px-5 py-3 text-sm text-emerald-800"
        >
          <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
          <div>
            <p className="font-medium">{receipt.topicTitle}</p>
            <p>{receipt.message}</p>
            {receipt.isPendingConfirmation && (
              <p className="mt-1 text-xs leading-5">
                Đã gửi lời mời tới MSSV {receipt.partnerUserCode}. Hạn xác nhận:{' '}
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
