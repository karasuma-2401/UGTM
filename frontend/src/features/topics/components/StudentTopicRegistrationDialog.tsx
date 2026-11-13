import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { AlertCircle, CalendarClock, Send, UserRound, UsersRound } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { StatusBadge } from '@/components/ui/status-badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useAuth } from '@/shared/auth';
import { TopicPreview } from './TopicPreview';
import { TopicCapacityStatus } from './TopicCapacityStatus';
import { studentTopicApi } from '../services/studentTopicApi';
import { getTopicErrorMessage } from '../services/topicApi';
import {
  registrationWindow,
  registrationDate,
  validateStudentRegistration,
} from '../studentRegistration';
import type {
  PublishedTopic,
  RegistrationRequest,
  RegistrationResult,
  StudentRegistrationStatus,
} from '../types/studentTopic.types';

export function StudentTopicRegistrationDialog({
  topic,
  batchName,
  status,
  onClose,
  onRegistered,
  onRefresh,
  availabilityLoading = false,
}: {
  topic: PublishedTopic;
  batchName?: string;
  status?: StudentRegistrationStatus;
  onClose: () => void;
  onRegistered: (result: RegistrationResult, request: RegistrationRequest) => void;
  onRefresh: () => void;
  availabilityLoading?: boolean;
}) {
  const { user, hasRole, hasPermission } = useAuth();
  const student = hasRole('Student');
  const [mode, setMode] = useState<'solo' | 'pair'>('solo');
  const [partnerCode, setPartnerCode] = useState('');
  const [error, setError] = useState('');
  const [confirmation, setConfirmation] = useState<RegistrationRequest | null>(null);
  const submitting = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const canReadSchedule = hasPermission('academic.batches.read');
  const timeline = useQuery({
    queryKey: ['student-registration-timeline', user?.id, topic.batchId],
    queryFn: ({ signal }) => studentTopicApi.getTimeline(topic.batchId, signal),
    enabled: Boolean(user?.id) && student && canReadSchedule,
    refetchInterval: 15_000,
  });
  const window = registrationWindow(timeline.isError ? undefined : timeline.data);
  const mutation = useMutation({
    mutationFn: (request: RegistrationRequest) =>
      studentTopicApi.register(request, user?.userCode, controller.current?.signal),
    retry: false,
    onSuccess: onRegistered,
    onError: () => {
      setConfirmation(null);
      onRefresh();
    },
    onSettled: () => {
      submitting.current = false;
      controller.current = null;
    },
  });
  const eligibility = validateStudentRegistration(topic, '', user?.userCode, status);
  const unavailable = eligibility || (window.state === 'closed' ? window.message : '');
  const readingSchedule = canReadSchedule && timeline.isPending;
  function review() {
    if (!student || mutation.isPending || readingSchedule || availabilityLoading) return;
    const partner = mode === 'pair' ? partnerCode.trim().toUpperCase() : '';
    const validation =
      mode === 'pair' && !partner
        ? 'Vui lòng nhập MSSV của bạn cùng nhóm.'
        : validateStudentRegistration(topic, partner, user?.userCode, status);
    if (validation || unavailable) {
      setError(validation || unavailable);
      return;
    }
    setError('');
    setConfirmation({ topic, partnerUserCode: partner });
  }
  function confirm() {
    if (
      !student ||
      !confirmation ||
      submitting.current ||
      mutation.isPending ||
      readingSchedule ||
      availabilityLoading
    )
      return;
    const validation = validateStudentRegistration(
      topic,
      confirmation.partnerUserCode,
      user?.userCode,
      status,
    );
    const currentWindow = registrationWindow(timeline.isError ? undefined : timeline.data);
    if (validation || currentWindow.state === 'closed') {
      setError(validation || currentWindow.message);
      setConfirmation(null);
      return;
    }
    submitting.current = true;
    controller.current = new AbortController();
    setError('');
    mutation.mutate({ ...confirmation, topic });
  }
  const headerBarClass =
    "sticky top-0 z-10 overflow-hidden border-b border-slate-200/80 bg-white/95 px-5 py-4 sm:px-6 before:content-[''] before:absolute before:inset-x-0 before:top-0 before:h-1 before:bg-[linear-gradient(90deg,#2054a5_0%,#2563eb_55%,#fa7921_100%)]";
  const preview = (
    <TopicPreview
      values={{
        batchId: topic.batchId,
        titleVi: topic.titleVi,
        titleEn: topic.titleEn ?? '',
        description: topic.description ?? '',
        requirements: topic.requirements ?? '',
        studentNumber: topic.studentNumber,
      }}
      advisorName={topic.advisorName || `Mã GVHD: ${topic.advisorId}`}
      batchName={batchName || `Mã đợt: ${topic.batchId}`}
      previewLabel="Đề tài đã công bố"
      showQuota={false}
    />
  );
  if (!student)
    return (
      <Dialog
        title="Chi tiết đề tài"
        onClose={onClose}
        className="max-w-4xl"
        headerClassName={headerBarClass}
      >
        <div className="grid items-stretch lg:grid-cols-[minmax(0,1fr)_288px]">
          {preview}
          <aside
            aria-label="Thông tin chỉ tiêu"
            className="flex flex-col justify-between gap-5 border-t border-slate-100 bg-slate-50/75 p-5 sm:p-6 lg:border-l lg:border-t-0"
          >
            <div className="space-y-4">
              <div className="space-y-3 rounded-xl border border-slate-200/80 bg-white p-4 shadow-2xs">
                <h2 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Tình trạng đăng ký
                </h2>
                <div aria-busy={availabilityLoading}>
                  {availabilityLoading ? (
                    <Skeleton className="h-12 w-full" />
                  ) : (
                    <TopicCapacityStatus topic={topic} />
                  )}
                </div>
              </div>
              <div className="rounded-xl border border-slate-200/80 bg-white/80 p-4 text-xs leading-5 text-slate-600">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-slate-500">Trạng thái đề tài</span>
                  <StatusBadge tone="success">Đã công bố</StatusBadge>
                </div>
                <p className="mt-2.5 border-t border-slate-100 pt-2.5 text-slate-500">
                  Sinh viên tra cứu và gửi đăng ký theo thời hạn mở đăng ký của đợt KLTN.
                </p>
              </div>
            </div>
            <div className="flex justify-end border-t border-slate-200/80 pt-4">
              <Button
                type="button"
                variant="outline"
                onClick={onClose}
                className="h-9 w-full rounded-xl bg-white sm:w-auto"
              >
                Đóng
              </Button>
            </div>
          </aside>
        </div>
      </Dialog>
    );
  const windowCardClass =
    window.state === 'open'
      ? 'border-emerald-200/80 bg-emerald-50/60'
      : window.state === 'closed'
        ? 'border-amber-200/80 bg-amber-50/60'
        : 'border-slate-200/80 bg-white';
  return (
    <>
      <Dialog
        title="Chi tiết & đăng ký đề tài"
        busy={mutation.isPending}
        onClose={() => {
          if (!mutation.isPending) onClose();
        }}
        className="max-w-5xl"
        headerClassName={headerBarClass}
      >
        <div className="grid items-start lg:grid-cols-[minmax(0,1fr)_356px]">
          {preview}
          <section
            className="space-y-4 border-t border-slate-100 bg-gradient-to-b from-slate-50/90 via-slate-50/50 to-white p-5 sm:p-6 lg:min-h-full lg:border-l lg:border-t-0"
            aria-label="Đăng ký đề tài"
          >
            <div className="space-y-3 rounded-xl border border-slate-200/80 bg-white p-4 shadow-2xs">
              <div className="flex items-center justify-between gap-2">
                <h2 className="text-sm font-semibold text-slate-900">Đăng ký đề tài</h2>
                <span className="text-[11px] font-medium text-slate-400">Chỉ tiêu hiện tại</span>
              </div>
              <div aria-busy={availabilityLoading}>
                {availabilityLoading ? (
                  <Skeleton className="h-12 w-full" />
                ) : (
                  <TopicCapacityStatus topic={topic} />
                )}
              </div>
            </div>

            <div
              aria-busy={timeline.isFetching}
              className={`rounded-xl border p-3.5 transition-colors ${windowCardClass}`}
            >
              {readingSchedule ? (
                <Skeleton className="h-9 w-full" />
              ) : (
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <StatusBadge
                      tone={
                        window.state === 'open'
                          ? 'success'
                          : window.state === 'closed'
                            ? 'warning'
                            : 'neutral'
                      }
                    >
                      {window.message}
                    </StatusBadge>
                    {timeline.isError && (
                      <Button
                        type="button"
                        size="sm"
                        variant="link"
                        className="h-auto p-0 text-xs"
                        onClick={() => void timeline.refetch()}
                      >
                        Thử tải lại lịch
                      </Button>
                    )}
                  </div>
                  {window.state === 'open' && (
                    <p className="flex items-center gap-1.5 text-xs font-medium text-emerald-900">
                      <CalendarClock className="size-3.5 shrink-0 text-emerald-600" aria-hidden="true" />
                      <span>Hạn đăng ký: {registrationDate(window.deadline)}</span>
                    </p>
                  )}
                </div>
              )}
            </div>

            <div className="space-y-3 rounded-xl border border-slate-200/80 bg-white p-4 shadow-2xs">
              <p className="text-xs font-semibold text-slate-700">Hình thức đăng ký</p>
              <div className="grid grid-cols-2 gap-2" aria-label="Hình thức đăng ký">
                <Button
                  type="button"
                  variant={mode === 'solo' ? 'soft' : 'outline'}
                  aria-pressed={mode === 'solo'}
                  leftIcon={<UserRound />}
                  disabled={mutation.isPending}
                  className={
                    mode === 'solo'
                      ? 'h-10 rounded-xl ring-1 ring-primary/35 font-semibold'
                      : 'h-10 rounded-xl'
                  }
                  onClick={() => {
                    setMode('solo');
                    setError('');
                  }}
                >
                  Cá nhân
                </Button>
                <Button
                  type="button"
                  variant={mode === 'pair' ? 'soft' : 'outline'}
                  aria-pressed={mode === 'pair'}
                  leftIcon={<UsersRound />}
                  className={
                    mode === 'pair'
                      ? 'h-10 rounded-xl ring-1 ring-primary/35 font-semibold'
                      : 'h-10 rounded-xl'
                  }
                  disabled={
                    topic.studentNumber !== 2 ||
                    (typeof topic.availableSlots === 'number' && topic.availableSlots < 2) ||
                    mutation.isPending
                  }
                  onClick={() => {
                    setMode('pair');
                    setError('');
                  }}
                >
                  2 sinh viên
                </Button>
              </div>
              {mode === 'pair' ? (
                <div className="space-y-2 pt-1">
                  <label htmlFor="registration-partner-code" className="text-xs font-medium text-slate-700">
                    MSSV bạn cùng nhóm
                  </label>
                  <Input
                    id="registration-partner-code"
                    value={partnerCode}
                    maxLength={20}
                    autoComplete="off"
                    spellCheck={false}
                    placeholder="Nhập MSSV được mời"
                    disabled={mutation.isPending}
                    className="h-10 rounded-xl font-mono uppercase shadow-none"
                    onChange={(event) => {
                      setPartnerCode(event.target.value);
                      setError('');
                    }}
                  />
                  <p className="text-xs leading-5 text-slate-500">
                    Chỉ người được mời cần xác nhận tham gia trong 24 giờ.
                  </p>
                </div>
              ) : (
                <p className="text-xs leading-5 text-slate-500">
                  {topic.studentNumber === 2
                    ? 'Đề tài nhận tối đa 2 SV vẫn cho phép đăng ký cá nhân. Nếu còn chỗ, SV khác sẽ được thêm vào cùng nhóm.'
                    : 'Đăng ký cá nhân được ghi nhận ngay khi thành công.'}
                </p>
              )}
            </div>

            {(error || mutation.error || unavailable) && (
              <div
                role="alert"
                className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50/90 p-3 text-xs font-medium leading-5 text-rose-700"
              >
                <AlertCircle className="mt-0.5 size-4 shrink-0 text-rose-600" aria-hidden="true" />
                <p>{error || (mutation.error ? getTopicErrorMessage(mutation.error) : unavailable)}</p>
              </div>
            )}

            <div className="flex flex-wrap items-center justify-end gap-2 border-t border-slate-200/80 pt-4">
              <Button
                type="button"
                variant="outline"
                disabled={mutation.isPending}
                onClick={onClose}
                className="h-10 rounded-xl"
              >
                Đóng
              </Button>
              <Button
                type="button"
                leftIcon={<Send />}
                disabled={
                  mutation.isPending ||
                  Boolean(unavailable) ||
                  readingSchedule ||
                  availabilityLoading
                }
                isLoading={mutation.isPending}
                onClick={review}
                className="h-10 flex-1 rounded-xl sm:flex-initial"
              >
                Đăng ký đề tài
              </Button>
            </div>
          </section>
        </div>
      </Dialog>
      <ConfirmDialog
        open={Boolean(confirmation)}
        onClose={() => setConfirmation(null)}
        onConfirm={confirm}
        type="warning"
        title="Xác nhận đăng ký đề tài"
        description={
          <>
            <p className="font-medium text-slate-700">{topic.titleVi}</p>
            <p className="mt-2">
              Người đăng ký: {user?.fullName} ({user?.userCode})
            </p>
            <p className="mt-2">
              {confirmation?.partnerUserCode
                ? `Mời MSSV ${confirmation.partnerUserCode} xác nhận qua email trong 24 giờ.`
                : 'Đăng ký cá nhân sẽ được ghi nhận ngay.'}
            </p>
          </>
        }
        confirmText="Xác nhận đăng ký"
        isConfirming={mutation.isPending}
      />
    </>
  );
}
