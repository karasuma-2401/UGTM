import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { UserRound, UsersRound, ArrowLeft, Send, CheckCircle2 } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
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
    onError: () => onRefresh(),
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
      return;
    }
    submitting.current = true;
    controller.current = new AbortController();
    setError('');
    mutation.mutate({ ...confirmation, topic });
  }
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
    />
  );
  if (!student)
    return (
      <Dialog title="Chi tiết đề tài" onClose={onClose} className="max-w-3xl">
        <div className="border-b border-slate-100 px-5 py-4">
          <TopicCapacityStatus topic={topic} />
        </div>
        {preview}
      </Dialog>
    );
  return (
    <Dialog
      title={confirmation ? 'Xác nhận đăng ký đề tài' : 'Chi tiết & đăng ký đề tài'}
      busy={mutation.isPending}
      onClose={() => {
        if (!mutation.isPending) onClose();
      }}
      className="max-w-5xl"
    >
      <div className="grid items-start lg:grid-cols-[minmax(0,1fr)_340px]">
        {preview}
        <section
          className="space-y-5 border-t border-slate-100 bg-slate-50/70 p-5 sm:p-6 lg:min-h-full lg:border-l lg:border-t-0"
          aria-label="Đăng ký đề tài"
        >
          <div>
            <h2 className="font-semibold text-slate-900">
              {confirmation ? 'Kiểm tra đăng ký' : 'Đăng ký đề tài'}
            </h2>
            <div className="mt-3" aria-busy={availabilityLoading}>
              {availabilityLoading ? (
                <Skeleton className="h-12 w-36" />
              ) : (
                <TopicCapacityStatus topic={topic} />
              )}
            </div>
          </div>
          <div aria-busy={timeline.isFetching}>
            {readingSchedule ? (
              <Skeleton className="h-7 w-40" />
            ) : (
              <>
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
                {window.state === 'open' && (
                  <p className="mt-2 text-xs text-slate-500">
                    Hạn đăng ký: {registrationDate(window.deadline)}
                  </p>
                )}
                {timeline.isError && (
                  <Button
                    type="button"
                    size="sm"
                    variant="link"
                    onClick={() => void timeline.refetch()}
                  >
                    Thử tải lại lịch
                  </Button>
                )}
              </>
            )}
          </div>
          {confirmation ? (
            <>
              <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 text-sm">
                <div>
                  <p className="text-xs text-slate-500">Người đăng ký</p>
                  <p className="mt-1 font-medium">{user?.fullName}</p>
                  <p className="mt-1 text-xs text-slate-500">{user?.userCode}</p>
                </div>
                <div>
                  <p className="text-xs text-slate-500">Hình thức</p>
                  <p className="mt-1 font-medium">
                    {confirmation.partnerUserCode ? 'Đăng ký kèm lời mời' : 'Đăng ký cá nhân'}
                  </p>
                </div>
                {confirmation.partnerUserCode && (
                  <div>
                    <p className="text-xs text-slate-500">MSSV được mời</p>
                    <p className="mt-1 font-medium">{confirmation.partnerUserCode}</p>
                  </div>
                )}
              </div>
              <p className="text-xs leading-6 text-slate-600">
                {confirmation.partnerUserCode
                  ? 'Bạn được ghi nhận ngay. Hệ thống gửi email cho bạn cùng nhóm xác nhận trong 24 giờ.'
                  : 'Bạn được ghi nhận ngay khi đăng ký thành công, không cần xác nhận qua email.'}
              </p>
            </>
          ) : (
            <>
              <div className="grid grid-cols-2 gap-2" aria-label="Hình thức đăng ký">
                <Button
                  type="button"
                  variant={mode === 'solo' ? 'soft' : 'outline'}
                  aria-pressed={mode === 'solo'}
                  leftIcon={<UserRound />}
                  disabled={mutation.isPending}
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
                <div className="space-y-2">
                  <label htmlFor="registration-partner-code" className="text-sm font-medium">
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
                <p className="text-xs leading-6 text-slate-500">
                  {topic.studentNumber === 2
                    ? 'Đề tài nhận tối đa 2 SV vẫn cho phép đăng ký cá nhân. Nếu còn chỗ, SV khác sẽ được thêm vào cùng nhóm.'
                    : 'Đăng ký cá nhân được ghi nhận ngay khi thành công.'}
                </p>
              )}
            </>
          )}
          {(error || mutation.error || unavailable) && (
            <p role="alert" className="text-sm leading-6 text-rose-700">
              {error || (mutation.error ? getTopicErrorMessage(mutation.error) : unavailable)}
            </p>
          )}
          <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-4">
            <Button
              type="button"
              variant="outline"
              disabled={mutation.isPending}
              leftIcon={confirmation ? <ArrowLeft /> : undefined}
              onClick={() => {
                if (confirmation) {
                  setConfirmation(null);
                  setError('');
                  mutation.reset();
                } else onClose();
              }}
            >
              {confirmation ? 'Quay lại' : 'Đóng'}
            </Button>
            <Button
              type="button"
              leftIcon={confirmation ? <CheckCircle2 /> : <Send />}
              disabled={
                mutation.isPending || Boolean(unavailable) || readingSchedule || availabilityLoading
              }
              isLoading={mutation.isPending}
              onClick={confirmation ? confirm : review}
            >
              {confirmation ? 'Xác nhận đăng ký' : 'Đăng ký đề tài'}
            </Button>
          </div>
        </section>
      </div>
    </Dialog>
  );
}
