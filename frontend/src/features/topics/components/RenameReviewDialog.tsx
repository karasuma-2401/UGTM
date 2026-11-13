import { Fragment, useCallback, useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowDown,
  ArrowRight,
  CalendarClock,
  Check,
  GraduationCap,
  MessageSquareQuote,
  UserCheck,
  Users,
  X,
} from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { StatusBadge } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import { studentTopicApi } from '../services/studentTopicApi';
import {
  RENAME_LEVEL_CONFIGS,
  computeTitleWordDiff,
  formatRenameDateTime,
  getPendingLevelForStatus,
  getRenameApprovalSteps,
  type RenameLevelConfig,
  type RenameReviewLevel,
  type TitleDiffSegment,
} from '../renameReview';
import {
  renameStatusLabels,
  renameStatusTones,
  type RenameRequest,
} from '../types/renameRequest.types';

export interface RenameDecisionPayload {
  request: RenameRequest;
  decision: 'Approve' | 'Reject';
  rejectionReason?: string;
}

interface RenameReviewDialogProps {
  request: RenameRequest;
  levelConfig: RenameLevelConfig;
  busy: boolean;
  error?: string;
  onClose: () => void;
  onSubmitDecision: (payload: RenameDecisionPayload) => void;
  onSwitchLevel?: (level: RenameReviewLevel) => void;
}

function DiffTitleText({
  segments,
  variant,
  subtle = false,
}: {
  segments: TitleDiffSegment[];
  variant: 'old' | 'new';
  subtle?: boolean;
}) {
  return (
    <>
      {segments.map((segment, index) => {
        const prefix = index > 0 ? ' ' : '';
        if (segment.kind === 'removed' && variant === 'old') {
          return (
            <Fragment key={`${segment.kind}-${index}`}>
              {prefix}
              <mark
                className={cn(
                  'rounded px-1 py-0.5 line-through decoration-rose-400/75',
                  subtle ? 'bg-rose-50 text-rose-700/85' : 'bg-rose-100/80 text-rose-700',
                )}
              >
                {segment.text}
              </mark>
            </Fragment>
          );
        }
        if (segment.kind === 'added' && variant === 'new') {
          return (
            <Fragment key={`${segment.kind}-${index}`}>
              {prefix}
              <mark
                className={cn(
                  'rounded px-1 py-0.5 font-semibold',
                  subtle ? 'bg-blue-50 text-blue-700' : 'bg-blue-100/85 text-blue-800',
                )}
              >
                {segment.text}
              </mark>
            </Fragment>
          );
        }
        return (
          <Fragment key={`${segment.kind}-${index}`}>
            {prefix}
            {segment.text}
          </Fragment>
        );
      })}
    </>
  );
}

export function RenameReviewDialog({
  request,
  levelConfig,
  busy,
  error,
  onClose,
  onSubmitDecision,
  onSwitchLevel,
}: RenameReviewDialogProps) {
  const [rejectionReason, setRejectionReason] = useState('');
  const [noteError, setNoteError] = useState('');
  const [confirmation, setConfirmation] = useState<RenameDecisionPayload | null>(null);

  const hasEnrichedContext = Boolean(request.members?.length || request.advisorName);
  const groupDetailQuery = useQuery({
    queryKey: ['student-topic-group', request.groupId],
    queryFn: ({ signal }) => studentTopicApi.getGroupDetail(request.groupId, signal),
    enabled: !hasEnrichedContext,
    staleTime: 60_000,
    retry: false,
  });

  const canReview = request.status === levelConfig.status;
  const pendingLevel = getPendingLevelForStatus(request.status);
  const steps = getRenameApprovalSteps(request);
  const approvedCount = steps.filter((step) => step.state === 'approved').length;
  const isAllApproved = approvedCount === steps.length;
  const hasRejected = steps.some((step) => step.state === 'rejected');
  const viTitleDiff = computeTitleWordDiff(request.oldTitleVi, request.newTitleVi);
  const enTitleDiff = computeTitleWordDiff(request.oldTitleEn, request.newTitleEn);

  const members =
    request.members && request.members.length > 0
      ? request.members
      : (groupDetailQuery.data?.members
          ?.filter((m) => m.confirmationStatus === 'Confirmed')
          .map((m) => ({
            studentId: m.studentId,
            userCode: m.userCode,
            fullName: m.fullName,
          })) ?? []);

  const advisorDisplay =
    request.advisorName || groupDetailQuery.data?.advisor?.fullName || 'Chưa cập nhật';
  const batchDisplay = request.batchName || groupDetailQuery.data?.batchName || null;

  const requestDecision = useCallback(
    (decision: 'Approve' | 'Reject') => {
      if (!canReview || busy) return;
      const trimmedReason = rejectionReason.trim();
      if (decision === 'Reject' && !trimmedReason) {
        setNoteError('Vui lòng nhập lý do từ chối trước khi xác nhận.');
        return;
      }
      setNoteError('');
      setConfirmation({
        request,
        decision,
        rejectionReason: decision === 'Reject' ? trimmedReason : undefined,
      });
    },
    [busy, canReview, rejectionReason, request],
  );

  useEffect(() => {
    if (!canReview) return;
    function handleKeyDown(event: KeyboardEvent) {
      if (!event.altKey || busy || confirmation) return;
      if (event.key === '1') {
        event.preventDefault();
        requestDecision('Approve');
      } else if (event.key === '2') {
        event.preventDefault();
        requestDecision('Reject');
      }
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [busy, canReview, confirmation, requestDecision]);

  return (
    <>
      <Dialog
        open
        busy={busy}
        onClose={onClose}
        className="max-w-5xl"
        headerClassName="before:content-[''] before:absolute before:inset-x-0 before:top-0 before:h-1 before:[background-image:var(--gradient-brand)]"
        title={
          <div className="space-y-1.5 pr-2">
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="text-base font-bold text-slate-900">
                Chi tiết yêu cầu đổi tên đề tài
              </span>
              <StatusBadge tone={renameStatusTones[request.status]}>
                {renameStatusLabels[request.status] ?? request.status}
              </StatusBadge>
            </div>
            <p className="flex items-center gap-1.5 text-xs font-normal text-slate-500 tabular-nums">
              <CalendarClock className="size-3.5 text-slate-400" aria-hidden="true" />
              <span>Gửi lúc {formatRenameDateTime(request.createdAt)}</span>
            </p>
          </div>
        }
      >
        <div className="grid grid-cols-1 lg:grid-cols-12">
          {/* Cột trái: Đối chiếu tên cũ/mới, lý do và thông tin nhóm */}
          <div className="space-y-4 border-b border-slate-100 p-5 sm:p-6 lg:col-span-7 lg:border-b-0 lg:border-r">
            <section className="space-y-2.5" aria-label="Đối chiếu tên đề tài">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Đối chiếu tên đề tài
                </h3>
                {batchDisplay && (
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 text-xs font-medium text-slate-600">
                    <GraduationCap className="size-3.5 text-slate-400" aria-hidden="true" />
                    {batchDisplay}
                  </span>
                )}
              </div>

              <div className="overflow-hidden rounded-2xl border border-slate-200/85 bg-white shadow-[0_1px_2px_rgba(15,23,42,0.03)]">
                {/* Tên đề tài hiện tại */}
                <div className="bg-slate-50/75 px-4 py-3.5">
                  <div className="flex items-center justify-between gap-2">
                    <span className="inline-flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                      <span className="size-1.5 rounded-full bg-slate-400" aria-hidden="true" />
                      Hiện tại
                    </span>
                    {!request.oldTitleEn && (
                      <span className="text-[11px] text-slate-400">Chưa có bản EN</span>
                    )}
                  </div>
                  <p className="mt-1.5 text-[13.5px] font-medium leading-relaxed text-slate-600">
                    <DiffTitleText segments={viTitleDiff.oldSegments} variant="old" />
                  </p>
                  {request.oldTitleEn && (
                    <p className="mt-1.5 flex items-baseline gap-1.5 text-xs leading-relaxed text-slate-500">
                      <span className="shrink-0 rounded border border-slate-200 bg-white px-1 text-[10px] font-semibold text-slate-400">
                        EN
                      </span>
                      <span>
                        <DiffTitleText segments={enTitleDiff.oldSegments} variant="old" subtle />
                      </span>
                    </p>
                  )}
                </div>

                {/* Đường chuyển tiếp */}
                <div className="relative border-t border-slate-200/80" aria-hidden="true">
                  <span className="absolute left-1/2 top-0 flex size-6 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border border-blue-200/85 bg-white text-primary shadow-2xs">
                    <ArrowDown className="size-3.5" />
                  </span>
                </div>

                {/* Tên đề xuất mới */}
                <div className="bg-gradient-to-b from-blue-50/40 via-white to-white px-4 py-4">
                  <div className="flex items-center justify-between gap-2">
                    <span className="inline-flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-primary">
                      <span className="size-1.5 rounded-full bg-primary" aria-hidden="true" />
                      Đề xuất mới
                    </span>
                    {!request.newTitleEn && (
                      <span className="text-[11px] text-slate-400">Chưa có bản EN</span>
                    )}
                  </div>
                  <p className="mt-1.5 text-[15px] font-semibold leading-snug text-slate-900">
                    <DiffTitleText segments={viTitleDiff.newSegments} variant="new" />
                  </p>
                  {request.newTitleEn && (
                    <p className="mt-1.5 flex items-baseline gap-1.5 text-xs leading-relaxed text-slate-600">
                      <span className="shrink-0 rounded border border-blue-200/75 bg-blue-50/70 px-1 text-[10px] font-semibold text-primary/80">
                        EN
                      </span>
                      <span>
                        <DiffTitleText segments={enTitleDiff.newSegments} variant="new" subtle />
                      </span>
                    </p>
                  )}
                </div>

                {/* Lý do đổi tên tích hợp gọn */}
                <section
                  className="border-t border-slate-200/75 bg-slate-50/55 px-4 py-3"
                  aria-label="Lý do đề nghị đổi tên"
                >
                  <p className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                    <MessageSquareQuote className="size-3.5 text-slate-400" aria-hidden="true" />
                    Lý do đổi tên
                  </p>
                  <p className="mt-1 text-[13px] leading-relaxed text-slate-700 whitespace-pre-wrap">
                    {request.reason}
                  </p>
                </section>
              </div>
            </section>

            {request.rejectionReason && (
              <InlineAlert tone="error" title="Lý do từ chối đã ghi nhận">
                <p>{request.rejectionReason}</p>
                {request.rejectedAt && (
                  <p className="mt-1 text-xs opacity-80">
                    Thời điểm từ chối: {formatRenameDateTime(request.rejectedAt)}
                  </p>
                )}
              </InlineAlert>
            )}

            <section
              className="grid grid-cols-1 gap-4 rounded-xl border border-slate-200/80 bg-slate-50/50 p-4 sm:grid-cols-2"
              aria-label="Thông tin nhóm và giảng viên hướng dẫn"
            >
              <div className="space-y-2.5">
                <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
                  <Users className="size-3.5 text-slate-400" aria-hidden="true" />
                  Thành viên nhóm ({members.length})
                </p>
                {members.length > 0 ? (
                  <ul className="space-y-2">
                    {members.map((member) => {
                      const displayName = member.fullName || member.userCode || 'Sinh viên';
                      const isRequester = member.studentId === request.requestedByStudentId;
                      return (
                        <li key={member.studentId} className="flex items-center gap-2.5">
                          <Avatar name={displayName} className="size-8 text-[11px]" />
                          <div className="min-w-0 flex-1">
                            <p className="truncate text-xs font-semibold text-slate-800">
                              {displayName}
                              {isRequester && (
                                <span className="ml-1.5 rounded bg-slate-200/70 px-1.5 py-0.5 text-[10px] font-medium text-slate-600">
                                  Người gửi
                                </span>
                              )}
                            </p>
                            {member.userCode && (
                              <p className="text-[11px] text-slate-500 tabular-nums">
                                MSSV: {member.userCode}
                              </p>
                            )}
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                ) : (
                  <p className="text-xs text-slate-500">
                    {groupDetailQuery.isPending
                      ? 'Đang tải danh sách thành viên...'
                      : 'Chưa có thông tin thành viên hiển thị.'}
                  </p>
                )}
              </div>

              <div className="space-y-2.5">
                <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
                  <UserCheck className="size-3.5 text-slate-400" aria-hidden="true" />
                  Giảng viên hướng dẫn
                </p>
                <div className="flex items-center gap-2.5">
                  <Avatar
                    name={advisorDisplay}
                    muted={advisorDisplay === 'Chưa cập nhật'}
                    className="size-8 text-[11px]"
                  />
                  <div className="min-w-0">
                    <p className="truncate text-xs font-semibold text-slate-800">
                      {advisorDisplay}
                    </p>
                    <p className="text-[11px] text-slate-500">
                      {batchDisplay ?? 'Đợt khóa luận tốt nghiệp'}
                    </p>
                  </div>
                </div>
              </div>
            </section>
          </div>

          {/* Cột phải: Tiến trình 3 cấp & Thao tác phê duyệt */}
          <div className="flex flex-col justify-between gap-5 bg-slate-50/60 p-5 sm:p-6 lg:col-span-5">
            <section className="space-y-3.5" aria-label="Tiến trình phê duyệt 3 cấp">
              <div className="space-y-2">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                    Tiến trình phê duyệt 3 cấp
                  </h3>
                  <span
                    className={cn(
                      'inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-semibold tabular-nums',
                      isAllApproved
                        ? 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200/70'
                        : hasRejected
                          ? 'bg-rose-50 text-rose-700 ring-1 ring-rose-200/70'
                          : 'bg-blue-50 text-primary ring-1 ring-blue-200/70',
                    )}
                  >
                    {isAllApproved
                      ? '3/3 bước hoàn tất'
                      : hasRejected
                        ? `Dừng tại ${approvedCount}/3 bước`
                        : `${approvedCount}/3 hoàn tất · Cấp ${levelConfig.number}`}
                  </span>
                </div>

                <div
                  className="flex items-center gap-1.5"
                  role="progressbar"
                  aria-label="Tiến độ phê duyệt 3 cấp"
                  aria-valuenow={approvedCount}
                  aria-valuemin={0}
                  aria-valuemax={3}
                >
                  {steps.map((step) => (
                    <span
                      key={step.level}
                      className={cn(
                        'h-1.5 flex-1 rounded-full transition-colors duration-300',
                        step.state === 'approved' && 'bg-emerald-500',
                        step.state === 'current' && 'bg-primary',
                        step.state === 'rejected' && 'bg-rose-500',
                        step.state === 'pending' && 'bg-slate-200/90',
                      )}
                    />
                  ))}
                </div>
              </div>

              <ol className="space-y-0">
                {steps.map((step, index) => {
                  const isActiveTarget = step.level === levelConfig.level;
                  const isLast = index === steps.length - 1;

                  return (
                    <li key={step.level} className="flex items-stretch gap-3">
                      <div className="flex w-7 shrink-0 flex-col items-center">
                        <span
                          aria-hidden="true"
                          className={cn(
                            'mt-2.5 flex size-7 shrink-0 items-center justify-center rounded-full text-xs font-bold tabular-nums transition-[background-color,border-color,box-shadow] duration-200',
                            step.state === 'approved' &&
                              'bg-emerald-500 text-white shadow-[0_2px_6px_rgba(16,185,129,0.28)]',
                            step.state === 'current' &&
                              'bg-primary text-white shadow-[0_3px_10px_rgba(37,99,235,0.34)] ring-4 ring-blue-100',
                            step.state === 'rejected' &&
                              'bg-rose-500 text-white shadow-[0_2px_6px_rgba(244,63,94,0.28)]',
                            step.state === 'pending' &&
                              'border border-slate-200 bg-white text-slate-400',
                          )}
                        >
                          {step.state === 'approved' ? (
                            <Check className="size-3.5 stroke-[2.5]" />
                          ) : step.state === 'rejected' ? (
                            <X className="size-3.5 stroke-[2.5]" />
                          ) : (
                            step.number
                          )}
                        </span>
                        {!isLast && (
                          <span
                            aria-hidden="true"
                            className={cn(
                              'my-1 w-0.5 flex-1 rounded-full',
                              step.state === 'approved'
                                ? 'bg-emerald-300/90'
                                : step.state === 'rejected'
                                  ? 'bg-rose-200'
                                  : 'bg-slate-200',
                            )}
                          />
                        )}
                      </div>

                      <div className={cn('min-w-0 flex-1', !isLast && 'pb-2.5')}>
                        <div
                          className={cn(
                            'rounded-xl border px-3.5 py-2.5 transition-[background-color,border-color,box-shadow] duration-200',
                            step.state === 'approved' &&
                              'border-slate-200/80 bg-white shadow-[0_1px_2px_rgba(15,23,42,0.03)]',
                            step.state === 'current' &&
                              'border-blue-200/90 bg-white py-3 shadow-[0_6px_16px_-6px_rgba(37,99,235,0.2),0_1px_2px_rgba(15,23,42,0.04)] ring-1 ring-primary/10',
                            step.state === 'rejected' && 'border-rose-200/90 bg-rose-50/75',
                            step.state === 'pending' && 'border-slate-200/55 bg-white/60',
                            isActiveTarget && step.state === 'pending' && 'border-slate-300 bg-white/85',
                          )}
                        >
                          <div className="flex flex-wrap items-center justify-between gap-1.5">
                            <span
                              className={cn(
                                'text-[13px] font-semibold',
                                step.state === 'pending' ? 'text-slate-500' : 'text-slate-900',
                              )}
                            >
                              Cấp {step.number} · {step.actorLabel}
                            </span>
                            <span
                              className={cn(
                                'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium',
                                step.state === 'approved' && 'bg-emerald-50 text-emerald-700',
                                step.state === 'current' && 'bg-blue-50 text-primary',
                                step.state === 'rejected' && 'bg-rose-100/85 text-rose-700',
                                step.state === 'pending' && 'bg-slate-100 text-slate-500',
                              )}
                            >
                              {step.state === 'current' && (
                                <span
                                  className="size-1.5 rounded-full bg-primary motion-safe:animate-pulse"
                                  aria-hidden="true"
                                />
                              )}
                              {step.stateLabel}
                            </span>
                          </div>

                          <div className="mt-1 flex flex-wrap items-center justify-between gap-2 text-[11px]">
                            {step.timestamp ? (
                              <span className="inline-flex items-center gap-1 text-slate-500 tabular-nums">
                                <CalendarClock className="size-3 text-slate-400" aria-hidden="true" />
                                {formatRenameDateTime(step.timestamp)}
                              </span>
                            ) : step.state === 'current' ? (
                              <span className="font-medium text-blue-700/85">
                                {isActiveTarget
                                  ? 'Bước hiện tại cần bạn xử lý'
                                  : 'Đang chờ cấp này xử lý'}
                              </span>
                            ) : (
                              <span className="text-slate-400">Chờ hoàn tất bước trước</span>
                            )}

                            {!isActiveTarget && step.state === 'current' && onSwitchLevel ? (
                              <button
                                type="button"
                                onClick={() => onSwitchLevel(step.level)}
                                className="inline-flex items-center gap-1 font-medium text-primary transition-colors hover:text-blue-700 focus-visible:outline-none focus-visible:underline"
                              >
                                Xem cấp này
                                <ArrowRight className="size-3" aria-hidden="true" />
                              </button>
                            ) : (
                              isActiveTarget &&
                              step.state !== 'current' && (
                                <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-medium text-slate-600">
                                  Đang xem
                                </span>
                              )
                            )}
                          </div>
                        </div>
                      </div>
                    </li>
                  );
                })}
              </ol>
            </section>

            <section className="space-y-3 border-t border-slate-200/80 pt-4" aria-label="Xử lý đơn">
              {error && (
                <InlineAlert tone="error" title="Không thể cập nhật đơn">
                  {error}
                </InlineAlert>
              )}

              {canReview ? (
                <>
                  {levelConfig.level === 'academic-affairs' && (
                    <InlineAlert tone="info">
                      Sau khi Phòng đào tạo phê duyệt, tên đề tài sẽ được cập nhật chính thức và mở
                      khóa nộp lại đề cương cho nhóm.
                    </InlineAlert>
                  )}

                  <div className="space-y-1.5">
                    <label
                      htmlFor={`rename-reject-reason-${request.id}`}
                      className="block text-xs font-semibold text-slate-700"
                    >
                      Lý do từ chối <span className="font-normal text-slate-500">(bắt buộc nếu từ chối)</span>
                    </label>
                    <textarea
                      id={`rename-reject-reason-${request.id}`}
                      value={rejectionReason}
                      disabled={busy}
                      rows={3}
                      placeholder="Nhập nhận xét hoặc lý do chưa thể chấp thuận đề nghị đổi tên..."
                      onChange={(event) => {
                        setRejectionReason(event.target.value);
                        if (noteError) setNoteError('');
                      }}
                      className={cn(
                        'w-full rounded-xl border bg-white px-3 py-2.5 text-sm text-slate-800 placeholder:text-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30',
                        noteError ? 'border-rose-400' : 'border-slate-200',
                      )}
                    />
                    {noteError && (
                      <p role="alert" className="text-xs font-medium text-rose-600">
                        {noteError}
                      </p>
                    )}
                  </div>

                  <div className="flex flex-wrap items-center gap-2.5">
                    <Button
                      type="button"
                      disabled={busy}
                      leftIcon={<Check />}
                      onClick={() => requestDecision('Approve')}
                      className="flex-1 rounded-xl"
                    >
                      Phê duyệt cấp {levelConfig.number}
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      disabled={busy}
                      leftIcon={<X />}
                      onClick={() => requestDecision('Reject')}
                      className="flex-1 rounded-xl border-rose-200 bg-white text-rose-700 hover:border-rose-300 hover:bg-rose-50 hover:text-rose-800"
                    >
                      Từ chối có lý do
                    </Button>
                  </div>
                </>
              ) : (
                <div className="space-y-3 rounded-xl border border-slate-200/80 bg-white p-4">
                  <p className="text-xs leading-relaxed text-slate-600">
                    {request.status === 'Completed'
                      ? 'Yêu cầu đã hoàn tất quy trình phê duyệt 3 cấp và tên đề tài mới đã được áp dụng chính thức.'
                      : pendingLevel && pendingLevel !== levelConfig.level
                        ? `Đơn này đang ở giai đoạn ${RENAME_LEVEL_CONFIGS[pendingLevel].tabLabel}, không thuộc hàng chờ xử lý của cấp ${levelConfig.number}.`
                        : `Đơn đang ở trạng thái "${renameStatusLabels[request.status] ?? request.status}" và không thể thao tác phê duyệt thêm.`}
                  </p>

                  {onSwitchLevel && pendingLevel && pendingLevel !== levelConfig.level && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      rightIcon={<ArrowRight />}
                      onClick={() => onSwitchLevel(pendingLevel)}
                      className="w-full rounded-xl border-slate-200"
                    >
                      Chuyển sang {RENAME_LEVEL_CONFIGS[pendingLevel].tabLabel} để xử lý
                    </Button>
                  )}
                </div>
              )}
            </section>
          </div>
        </div>
      </Dialog>

      <ConfirmDialog
        open={Boolean(confirmation)}
        onClose={() => {
          if (!busy) setConfirmation(null);
        }}
        onConfirm={() => {
          if (!confirmation) return;
          const payload = confirmation;
          setConfirmation(null);
          onSubmitDecision(payload);
        }}
        type={confirmation?.decision === 'Approve' ? 'brand' : 'error'}
        isConfirming={busy}
        title={
          confirmation?.decision === 'Approve'
            ? `Xác nhận phê duyệt cấp ${levelConfig.number}`
            : `Xác nhận từ chối cấp ${levelConfig.number}`
        }
        confirmText={
          confirmation?.decision === 'Approve'
            ? `Phê duyệt cấp ${levelConfig.number}`
            : 'Xác nhận từ chối'
        }
        cancelText="Hủy bỏ"
        description={
          confirmation?.decision === 'Approve' ? (
            <div className="space-y-2 text-left text-xs text-slate-600">
              <p>
                Xác nhận phê duyệt chuyển đổi tên đề tài từ{' '}
                <strong className="text-slate-800">{request.oldTitleVi}</strong> sang{' '}
                <strong className="text-slate-900">{request.newTitleVi}</strong>.
              </p>
              {levelConfig.level === 'academic-affairs' && (
                <p className="font-medium text-primary">
                  Thao tác này sẽ cập nhật tên đề tài chính thức trong danh mục và mở khóa nộp lại
                  đề cương cho nhóm sinh viên.
                </p>
              )}
            </div>
          ) : (
            <div className="space-y-2 text-left text-xs text-slate-600">
              <p>Đơn đổi tên sẽ dừng quy trình phê duyệt với lý do đã ghi nhận:</p>
              <p className="rounded-lg border border-rose-200 bg-rose-50/70 p-2.5 font-medium text-rose-800">
                {confirmation?.rejectionReason}
              </p>
            </div>
          )
        }
      />
    </>
  );
}
