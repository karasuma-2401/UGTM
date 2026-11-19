import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowUpRight, RefreshCw, RotateCcw, Send } from 'lucide-react';
import { Link, useBlocker } from 'react-router-dom';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { FormErrorSummary, type FormFieldError } from '@/components/ui/form-error-summary';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { PageHeader } from '@/components/ui/page-header';
import { RichTextContent, stripRichText } from '@/components/ui/rich-text-content';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { useAuth } from '@/shared/auth';
import { OperationToast } from '@/shared/notifications';
import { StudentRenameDetailDialog } from '../components/StudentRenameDetailDialog';
import { StudentRenamePipelineDock } from '../components/StudentRenamePipelineDock';
import { TitleDiffText } from '../components/TitleDiffText';
import {
  computeTitleWordDiff,
  formatRenameDate,
  formatRenameDateTime,
} from '../renameReview';
import { renameRequestApi } from '../services/renameRequestApi';
import { studentTopicApi } from '../services/studentTopicApi';
import { getTopicErrorMessage } from '../services/topicApi';
import { GROUP_STATUS_LABELS, type GroupStatus } from '../types/advising.types';
import {
  renameStatusLabels,
  renameStatusTones,
  type RenameRequest,
} from '../types/renameRequest.types';

const ACTIVE_STATUSES = ['Created', 'AdvisorConfirmed', 'FacultyApproved'] as const;
const EMPTY_ERRORS: FormFieldError[] = [];

function getHistoryStepSummary(item: RenameRequest): string {
  switch (item.status) {
    case 'Created':
      return 'Bước 1/3 · Chờ GVHD';
    case 'AdvisorConfirmed':
      return 'Bước 2/3 · Chờ Khoa';
    case 'FacultyApproved':
      return 'Bước 3/3 · Chờ PĐT';
    case 'Completed':
      return 'Hoàn tất 3/3 cấp';
    case 'RejectedByAdvisor':
      return 'Dừng ở cấp 1 (GVHD)';
    case 'RejectedByFaculty':
      return 'Dừng ở cấp 2 (Khoa)';
    case 'RejectedByAA':
      return 'Dừng ở cấp 3 (PĐT)';
    default:
      return 'Đã đóng';
  }
}

export function StudentRenameRequestPage() {
  const { user } = useAuth();
  return <StudentRenameRequestContent key={user?.id ?? 'anonymous'} />;
}

function StudentRenameRequestContent() {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const [newTitleVi, setNewTitleVi] = useState('');
  const [newTitleEn, setNewTitleEn] = useState('');
  const [reason, setReason] = useState('');
  const [submittedOnce, setSubmittedOnce] = useState(false);
  const [submissionKey, setSubmissionKey] = useState(0);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [notice, setNotice] = useState('');
  const [dismissedRejectionId, setDismissedRejectionId] = useState<string | null>(null);
  const [selectedHistoryId, setSelectedHistoryId] = useState<string | null>(null);
  const [detailRequest, setDetailRequest] = useState<RenameRequest | null>(null);
  const [nowMs, setNowMs] = useState(() => Date.now());

  useEffect(() => {
    const id = window.setInterval(() => setNowMs(Date.now()), 60_000);
    return () => window.clearInterval(id);
  }, []);

  const initializedGroup = useRef<string | null>(null);
  const titleViInputRef = useRef<HTMLInputElement>(null);

  const status = useQuery({
    queryKey: ['student-registration', user?.id],
    queryFn: ({ signal }) => studentTopicApi.getMyStatus(signal),
  });

  const groupId = status.data?.confirmationStatus === 'Confirmed' ? status.data.groupId : null;

  const group = useQuery({
    queryKey: ['student-topic-group', user?.id, groupId],
    queryFn: ({ signal }) => studentTopicApi.getGroupDetail(groupId!, signal),
    enabled: Boolean(groupId),
    refetchInterval: 30_000,
  });

  const history = useQuery({
    queryKey: ['rename-requests-group', user?.id, groupId],
    queryFn: ({ signal }) => renameRequestApi.listByGroup(groupId!, signal),
    enabled: Boolean(groupId),
    refetchInterval: 30_000,
  });

  const timeline = useQuery({
    queryKey: ['rename-request-timeline', group.data?.batchId],
    queryFn: ({ signal }) => studentTopicApi.getTimeline(group.data!.batchId, signal),
    enabled: Boolean(group.data?.batchId),
    refetchInterval: 60_000,
  });

  useEffect(() => {
    if (group.data && initializedGroup.current !== group.data.groupId) {
      initializedGroup.current = group.data.groupId;
      setNewTitleEn(group.data.topicTitleEn || '');
    }
  }, [group.data]);

  const create = useMutation({
    mutationFn: () =>
      renameRequestApi.create(groupId!, {
        newTitleVi: newTitleVi.trim(),
        newTitleEn: newTitleEn.trim() || null,
        reason: reason.trim(),
      }),
    onSuccess: () => {
      setConfirmOpen(false);
      setNewTitleVi('');
      setNewTitleEn(group.data?.topicTitleEn || '');
      setReason('');
      setSubmittedOnce(false);
      setNotice('Đã gửi đơn xin đổi tên. Giảng viên hướng dẫn sẽ xem xét ở cấp 1.');
      void queryClient.invalidateQueries({
        queryKey: ['rename-requests-group', user?.id, groupId],
      });
    },
  });

  const activeRequest = useMemo(
    () =>
      history.data?.find((item) =>
        (ACTIVE_STATUSES as readonly string[]).includes(item.status),
      ),
    [history.data],
  );
  const latestRequest = history.data?.[0];
  const hasCompletedRequest = Boolean(
    history.data?.some((item) => item.status === 'Completed'),
  );

  const stage3 = timeline.data?.stages.find(
    (stage) => stage.stageCode === 'PROGRESS_EXECUTION' || stage.orderIndex === 3,
  );
  const stage4 = timeline.data?.stages.find(
    (stage) => stage.stageCode === 'MIDTERM_GATE' || stage.orderIndex === 4,
  );

  const windowReason: 'batch-closed' | 'not-started' | 'expired' | 'missing-stages' | null =
    useMemo(() => {
      if (!timeline.data) return null;
      if (timeline.data.status !== 'Open') return 'batch-closed';
      if (!stage3 || !stage4) return 'missing-stages';
      if (timeline.data.currentLocalDate < stage3.startDate) return 'not-started';
      if (nowMs > Date.parse(stage4.deadlineAtUtc)) return 'expired';
      return null;
    }, [nowMs, stage3, stage4, timeline.data]);

  const windowClosed = windowReason !== null;

  const validGroupStatus =
    group.data?.groupStatus === 'InProgress' ||
    group.data?.groupStatus === 'MidtermSubmitted';

  const canCompose = Boolean(
    group.data && validGroupStatus && !windowClosed && history.isSuccess && !activeRequest,
  );

  const viChanged = useMemo(() => {
    if (!group.data) return false;
    return (
      newTitleVi.trim().length > 0 &&
      newTitleVi.trim().toLocaleLowerCase() !==
        group.data.topicTitleVi.trim().toLocaleLowerCase()
    );
  }, [group.data, newTitleVi]);

  const enChanged = useMemo(() => {
    if (!group.data) return false;
    return (
      newTitleEn.trim().toLocaleLowerCase() !==
      (group.data.topicTitleEn || '').trim().toLocaleLowerCase()
    );
  }, [group.data, newTitleEn]);

  const isTitleChanged = viChanged || enChanged;

  const validationErrors: FormFieldError[] = useMemo(() => {
    if (!group.data || !canCompose) return EMPTY_ERRORS;
    const errors: FormFieldError[] = [];
    if (!newTitleVi.trim()) {
      errors.push({
        fieldId: 'rename-new-title-vi',
        message: 'Vui lòng nhập tên đề tài mới bằng tiếng Việt.',
      });
    } else if (!isTitleChanged) {
      errors.push({
        fieldId: 'rename-new-title-vi',
        message: 'Tên đề tài mới (tiếng Việt hoặc tiếng Anh) cần khác tên hiện tại.',
      });
    }
    if (!stripRichText(reason).trim()) {
      errors.push({
        fieldId: 'rename-reason',
        message: 'Vui lòng nêu rõ lý do điều chỉnh tên đề tài.',
      });
    }
    return errors;
  }, [canCompose, group.data, isTitleChanged, newTitleVi, reason]);

  const displayedErrors = submittedOnce ? validationErrors : EMPTY_ERRORS;
  const fieldErrorMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const err of displayedErrors) {
      if (!map.has(err.fieldId)) map.set(err.fieldId, err.message);
    }
    return map;
  }, [displayedErrors]);

  const viLiveDiff = useMemo(() => {
    if (!group.data || !viChanged) return null;
    return computeTitleWordDiff(group.data.topicTitleVi, newTitleVi.trim());
  }, [group.data, newTitleVi, viChanged]);

  const enLiveDiff = useMemo(() => {
    if (!group.data || !enChanged) return null;
    return computeTitleWordDiff(group.data.topicTitleEn, newTitleEn);
  }, [group.data, enChanged, newTitleEn]);

  const hasFormDraft = Boolean(
    newTitleVi.trim() ||
      stripRichText(reason).trim() ||
      (group.data && newTitleEn.trim() !== (group.data.topicTitleEn || '').trim()),
  );

  // Unsaved draft protection when navigating away or closing tab
  const shouldGuardNavigation = canCompose && hasFormDraft && !create.isPending;
  const navigationBlocker = useBlocker(shouldGuardNavigation);

  useEffect(() => {
    if (!shouldGuardNavigation) return;
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [shouldGuardNavigation]);

  function handleResetForm() {
    setNewTitleVi('');
    setNewTitleEn(group.data?.topicTitleEn || '');
    setReason('');
    setSubmittedOnce(false);
  }

  function handleCopyCurrentVi() {
    if (!group.data || !canCompose) return;
    setNewTitleVi(group.data.topicTitleVi);
    requestAnimationFrame(() => {
      titleViInputRef.current?.focus();
    });
  }

  function handleReuseRequest(item: RenameRequest) {
    if (!canCompose) return;
    setNewTitleVi(item.newTitleVi);
    setNewTitleEn(item.newTitleEn ?? '');
    setReason(item.reason);
    setSubmittedOnce(false);
    setNotice('Đã điền lại nội dung từ đơn trước để bạn chỉnh sửa và gửi lại.');
    requestAnimationFrame(() => {
      titleViInputRef.current?.focus();
      titleViInputRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!canCompose || create.isPending) return;
    setSubmittedOnce(true);
    setSubmissionKey((prev) => prev + 1);
    if (validationErrors.length === 0) {
      create.reset();
      setConfirmOpen(true);
    }
  }

  function handleRefreshAll() {
    void status.refetch();
    if (groupId) {
      void group.refetch();
      void history.refetch();
    }
    if (group.data?.batchId) {
      void timeline.refetch();
    }
  }

  const isLoading = status.isPending || (Boolean(groupId) && group.isPending);
  const loadError = status.error || group.error;
  const isFetchingAny =
    status.isFetching || group.isFetching || history.isFetching || timeline.isFetching;

  const activeViDiff = activeRequest
    ? computeTitleWordDiff(activeRequest.oldTitleVi, activeRequest.newTitleVi)
    : null;
  const activeEnDiff = activeRequest
    ? computeTitleWordDiff(activeRequest.oldTitleEn, activeRequest.newTitleEn)
    : null;
  const activeRequester = activeRequest
    ? group.data?.members.find((m) => m.studentId === activeRequest.requestedByStudentId)
    : null;

  const historyColumns: DataTableColumn<RenameRequest>[] = [
    {
      id: 'titles',
      header: 'Tên đề xuất',
      headerClassName: 'px-5 py-3.5 whitespace-nowrap',
      cellClassName: 'px-5 py-4',
      skeleton: { variant: 'topic' },
      cell: (item) => {
        const requester = group.data?.members.find(
          (m) => m.studentId === item.requestedByStudentId,
        );
        const requesterName = requester?.fullName || requester?.userCode;
        const onlyEnChanged =
          item.newTitleVi.trim().toLocaleLowerCase() ===
            item.oldTitleVi.trim().toLocaleLowerCase() && Boolean(item.newTitleEn);

        return (
          <div className="min-w-0 space-y-1">
            <span
              className="line-clamp-2 block break-words text-sm font-semibold leading-snug text-ink-primary"
              title={item.newTitleVi}
            >
              {item.newTitleVi}
            </span>
            {onlyEnChanged && item.newTitleEn && (
              <span className="block truncate text-xs text-ink-body" title={item.newTitleEn}>
                EN: {item.newTitleEn}
              </span>
            )}
            <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-ink-muted">
              <span className="line-clamp-1 min-w-0 flex-1 break-words" title={item.oldTitleVi}>
                Từ: {item.oldTitleVi}
              </span>
              {requesterName && (
                <span className="inline-flex shrink-0 items-center gap-1.5 text-ink-muted">
                  <Avatar name={requesterName} className="size-4 text-[8px]" />
                  <span>{requesterName}</span>
                </span>
              )}
            </div>
          </div>
        );
      },
    },
    {
      id: 'status',
      header: 'Trạng thái',
      widthClassName: 'w-44',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'badge' },
      cell: (item) => (
        <div className="space-y-1">
          <StatusBadge tone={renameStatusTones[item.status]}>
            {renameStatusLabels[item.status] ?? item.status}
          </StatusBadge>
          <p className="text-[11px] text-ink-muted">{getHistoryStepSummary(item)}</p>
        </div>
      ),
    },
    {
      id: 'createdAt',
      header: 'Ngày gửi',
      widthClassName: 'w-28',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'text' },
      cell: (item) => (
        <span className="text-xs font-medium tabular-nums text-ink-secondary">
          {formatRenameDate(item.createdAt)}
        </span>
      ),
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-20',
      align: 'center',
      headerClassName: 'px-3 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-4',
      skeleton: { variant: 'actions', actionCount: 1 },
      cell: (item) => (
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-label={`Xem chi tiết đơn ${item.newTitleVi}`}
          onClick={(event) => {
            event.stopPropagation();
            setSelectedHistoryId(item.id);
            setDetailRequest(item);
          }}
          className="h-8 rounded-md border-line-input bg-surface-card px-3 text-xs font-medium text-ink-secondary shadow-sm hover:border-line-strong hover:bg-surface-hover hover:text-ink-primary"
        >
          Xem
        </Button>
      ),
    },
  ];

  const headerStatusBadge = !group.data ? null : activeRequest ? (
    <StatusBadge tone="info">Đang có đơn chờ duyệt</StatusBadge>
  ) : !validGroupStatus ? (
    <StatusBadge tone="warning">Chưa đủ điều kiện giai đoạn</StatusBadge>
  ) : windowClosed ? (
    <StatusBadge tone="warning">Ngoài thời hạn gửi đơn</StatusBadge>
  ) : (
    <StatusBadge tone="success">Đang mở gửi đơn</StatusBadge>
  );

  const showRejectionRecovery = Boolean(
    canCompose &&
      latestRequest &&
      latestRequest.status.startsWith('Rejected') &&
      latestRequest.rejectionReason &&
      dismissedRejectionId !== latestRequest.id,
  );

  return (
    <div className="min-w-0 space-y-5 pb-12">
      <PageHeader
        accent
        title="Đơn xin đổi tên đề tài"
        description="Đề nghị điều chỉnh tên đề tài khóa luận từ Tuần 3 đến hạn chót giữa kỳ."
      >
        {headerStatusBadge}
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={isFetchingAny}
          leftIcon={<RefreshCw className={isFetchingAny ? 'animate-spin' : undefined} />}
          onClick={handleRefreshAll}
          className="h-9 rounded-xl border-line-input bg-surface-card shadow-2xs hover:border-line-strong"
        >
          Làm mới
        </Button>
      </PageHeader>

      <OperationToast message={notice} onConsumed={() => setNotice('')} />

      {isLoading ? (
        <div
          role="status"
          className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_340px]"
        >
          <span className="sr-only">Đang tải thông tin đề tài và lịch sử đơn...</span>
          <div className="space-y-5">
            <Card className="space-y-4 rounded-2xl border-line/80 p-6">
              <Skeleton className="h-6 w-56" />
              <Skeleton className="h-16 w-full rounded-xl" />
              <Skeleton className="h-11 w-full rounded-xl" />
              <Skeleton className="h-11 w-full rounded-xl" />
              <Skeleton className="h-28 w-full rounded-xl" />
            </Card>
            <Card className="space-y-3 rounded-2xl border-line/80 p-6">
              <Skeleton className="h-6 w-48" />
              <Skeleton className="h-28 w-full rounded-xl" />
            </Card>
          </div>
          <Card className="space-y-4 rounded-2xl border-line/80 p-5">
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-2 w-full" />
            <Skeleton className="h-44 w-full rounded-xl" />
            <Skeleton className="h-28 w-full rounded-xl" />
          </Card>
        </div>
      ) : loadError ? (
        <InlineAlert
          tone="error"
          title="Không thể tải thông tin đổi tên đề tài"
          action={
            <Button type="button" variant="outline" size="sm" onClick={handleRefreshAll}>
              Thử lại
            </Button>
          }
        >
          {getTopicErrorMessage(loadError)}
        </InlineAlert>
      ) : !groupId || !group.data ? (
        <Card className="rounded-2xl border-line/80 p-6">
          <InlineAlert
            tone="info"
            title="Bạn chưa có nhóm đề tài đã xác nhận"
            action={
              <Link
                to="/topics/catalog"
                className="inline-flex items-center gap-1.5 rounded-lg border border-tone-info-border bg-surface-card px-3.5 py-1.5 text-xs font-semibold text-primary transition-colors hover:bg-tone-info-bg"
              >
                Xem danh mục &amp; đăng ký đề tài
                <ArrowUpRight className="size-3.5" aria-hidden="true" />
              </Link>
            }
          >
            Sinh viên cần hoàn tất xác nhận tham gia nhóm đề tài trước khi gửi đơn đề nghị đổi tên.
          </InlineAlert>
        </Card>
      ) : (
        <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-5">
            {canCompose && hasCompletedRequest && !showRejectionRecovery && (
              <InlineAlert
                tone="success"
                title="Đơn đổi tên trước đã được phê duyệt hoàn tất"
                action={
                  <Link
                    to="/thesis/proposal"
                    className="inline-flex items-center gap-1 rounded-lg border border-tone-success-border bg-surface-card px-3 py-1.5 text-xs font-semibold text-tone-success-text transition-colors hover:bg-tone-success-bg"
                  >
                    Đi tới Nộp đề cương
                    <ArrowUpRight className="size-3.5" aria-hidden="true" />
                  </Link>
                }
              >
                Tên đề tài chính thức đã được cập nhật và hệ thống đã mở khóa nộp lại Đề cương cho
                nhóm.
              </InlineAlert>
            )}

            {showRejectionRecovery && latestRequest && (
              <InlineAlert
                tone="warning"
                title={`Đơn gần nhất chưa được chấp thuận (${renameStatusLabels[latestRequest.status] ?? latestRequest.status})`}
                onDismiss={() => setDismissedRejectionId(latestRequest.id)}
                action={
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    leftIcon={<RotateCcw />}
                    onClick={() => handleReuseRequest(latestRequest)}
                    className="h-8 rounded-lg border-tone-warning-border bg-surface-card text-xs font-semibold text-tone-warning-strong hover:bg-tone-warning-bg"
                  >
                    Điền lại để sửa
                  </Button>
                }
              >
                {latestRequest.rejectionReason}
              </InlineAlert>
            )}

            {/* Trạng thái 1: Đang có đơn chờ duyệt -> Hiển thị trực tiếp phiếu theo dõi thay vì form khóa */}
            {activeRequest && activeViDiff ? (
              <Card className="min-w-0 overflow-hidden rounded-2xl border-line/80 bg-surface-card shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
                <ListCardHeader
                  title="Đơn xin đổi tên đang chờ phê duyệt"
                  description={`Gửi lúc ${formatRenameDateTime(activeRequest.createdAt)}${
                    activeRequester
                      ? ` bởi ${activeRequester.fullName || activeRequester.userCode}`
                      : ''
                  }`}
                  className="min-h-0 xl:grid-cols-[minmax(0,1fr)_auto]"
                >
                  <div className="flex items-center gap-2 xl:justify-end">
                    <StatusBadge tone={renameStatusTones[activeRequest.status]}>
                      {renameStatusLabels[activeRequest.status] ?? activeRequest.status}
                    </StatusBadge>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => setDetailRequest(activeRequest)}
                      className="h-8 rounded-lg border-line-input bg-surface-card text-xs font-medium"
                    >
                      Xem chi tiết
                    </Button>
                  </div>
                </ListCardHeader>

                <div className="divide-y divide-line-subtle">
                  <div className="space-y-3 px-5 py-4 sm:px-6">
                    <div>
                      <p className="text-xs font-medium text-ink-muted">Tên tại thời điểm gửi</p>
                      <p className="mt-1 text-sm leading-relaxed text-ink-body">
                        <TitleDiffText segments={activeViDiff.oldSegments} variant="old" />
                      </p>
                      {activeRequest.oldTitleEn && activeEnDiff && (
                        <p className="mt-1 text-xs leading-relaxed text-ink-muted">
                          EN:{' '}
                          <TitleDiffText segments={activeEnDiff.oldSegments} variant="old" subtle />
                        </p>
                      )}
                    </div>

                    <div className="border-t border-line-subtle/70 pt-3">
                      <p className="text-xs font-semibold text-primary">Tên đề xuất mới</p>
                      <p className="mt-1 text-base font-semibold leading-snug text-ink-primary">
                        <TitleDiffText segments={activeViDiff.newSegments} variant="new" />
                      </p>
                      {activeRequest.newTitleEn && activeEnDiff && (
                        <p className="mt-1 text-xs leading-relaxed text-ink-body">
                          EN:{' '}
                          <TitleDiffText segments={activeEnDiff.newSegments} variant="new" subtle />
                        </p>
                      )}
                    </div>
                  </div>

                  <div className="space-y-1.5 px-5 py-4 sm:px-6">
                    <p className="text-xs font-medium text-ink-muted">Lý do điều chỉnh đã gửi</p>
                    <RichTextContent
                      content={activeRequest.reason}
                      className="text-sm leading-relaxed text-ink-secondary"
                    />
                  </div>

                  <div className="bg-surface-subtle/40 px-5 py-3 text-xs text-ink-muted sm:px-6">
                    Mỗi nhóm chỉ gửi tối đa một đơn đang xử lý. Bạn có thể tạo đơn mới nếu đơn
                    này bị từ chối hoặc sau khi hoàn tất.
                  </div>
                </div>
              </Card>
            ) : !validGroupStatus || windowClosed ? (
              /* Trạng thái 2: Không đủ điều kiện hoặc ngoài hạn -> Hiển thị gọn, không trưng form disable */
              <Card className="min-w-0 overflow-hidden rounded-2xl border-line/80 bg-surface-card shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
                <ListCardHeader
                  title="Thông tin đề tài hiện tại"
                  description="Hiện tại nhóm chưa thể tạo đơn xin đổi tên mới."
                  className="min-h-0 xl:grid-cols-[minmax(0,1fr)_auto]"
                />
                <div className="space-y-4 p-5 sm:p-6">
                  {!validGroupStatus ? (
                    <InlineAlert
                      tone="warning"
                      title="Trạng thái nhóm chưa hỗ trợ gửi đơn"
                      action={
                        <Link
                          to="/thesis/milestones"
                          className="inline-flex items-center gap-1 rounded-lg border border-tone-warning-border bg-surface-card px-3 py-1.5 text-xs font-semibold text-tone-warning-strong transition-colors hover:bg-tone-warning-bg"
                        >
                          Xem tiến độ &amp; hồ sơ
                          <ArrowUpRight className="size-3.5" aria-hidden="true" />
                        </Link>
                      }
                    >
                      Nhóm hiện đang ở trạng thái{' '}
                      <strong>
                        {GROUP_STATUS_LABELS[group.data.groupStatus as GroupStatus] ??
                          group.data.groupStatus}
                      </strong>
                      . Đơn xin đổi tên chỉ mở khi nhóm đang ở giai đoạn{' '}
                      <strong>Đang thực hiện</strong> hoặc <strong>Đã nộp báo cáo giữa kỳ</strong>.
                    </InlineAlert>
                  ) : (
                    <InlineAlert tone="warning" title="Hiện ngoài thời hạn gửi đơn đổi tên">
                      {windowReason === 'batch-closed'
                        ? 'Đợt khóa luận của nhóm hiện chưa ở trạng thái Đang mở.'
                        : windowReason === 'not-started' && stage3
                          ? `Thời hạn đổi tên sẽ mở từ ngày ${formatRenameDate(stage3.startDate)} (bắt đầu Giai đoạn thực hiện).`
                          : stage4
                            ? `Hạn chót gửi đơn đổi tên đã kết thúc vào ${formatRenameDateTime(stage4.deadlineAtUtc)}.`
                            : 'Đợt khóa luận chưa cấu hình mốc Giai đoạn thực hiện và Cổng giữa kỳ.'}
                    </InlineAlert>
                  )}

                  <div className="space-y-1 border-t border-line-subtle pt-4">
                    <p className="text-xs font-medium text-ink-muted">Tên đề tài hiện tại</p>
                    <p className="text-sm font-semibold text-ink-primary">
                      {group.data.topicTitleVi}
                    </p>
                    {group.data.topicTitleEn && (
                      <p className="text-xs text-ink-muted">EN: {group.data.topicTitleEn}</p>
                    )}
                  </div>
                </div>
              </Card>
            ) : (
              /* Trạng thái 3: Được phép soạn đơn -> Fluent Clean Sheet */
              <Card className="min-w-0 overflow-hidden rounded-2xl border-line/80 bg-surface-card shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
                <ListCardHeader
                  title="Phiếu đề nghị đổi tên đề tài"
                  description="Đối chiếu với tên hiện tại trước khi gửi qua GVHD, Khoa và Phòng đào tạo."
                  className="min-h-0 xl:grid-cols-[minmax(0,1fr)_auto]"
                />

                <form
                  id="rename-request-form"
                  noValidate
                  onSubmit={handleSubmit}
                  className="divide-y divide-line-subtle"
                >
                  {/* Tham chiếu tên hiện tại dạng phẳng */}
                  <div className="space-y-1 bg-surface-subtle/35 px-5 py-4 sm:px-6">
                    <p className="text-xs font-medium text-ink-muted">Tên đề tài hiện tại</p>
                    <p className="text-sm font-medium leading-relaxed text-ink-primary">
                      {group.data.topicTitleVi}
                    </p>
                    {group.data.topicTitleEn && (
                      <p className="text-xs leading-relaxed text-ink-muted">
                        EN: {group.data.topicTitleEn}
                      </p>
                    )}
                  </div>

                  {/* Vùng nhập liệu chính */}
                  <div className="space-y-4 px-5 py-5 sm:px-6">
                    {displayedErrors.length > 0 && (
                      <FormErrorSummary errors={displayedErrors} submissionKey={submissionKey} />
                    )}

                    <div className="space-y-1.5">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <label
                          htmlFor="rename-new-title-vi"
                          className="text-sm font-medium text-ink-secondary"
                        >
                          Tên mới tiếng Việt <span className="text-tone-danger-icon">*</span>
                        </label>
                        <div className="flex items-center gap-3 text-xs">
                          {newTitleVi.trim() !== group.data.topicTitleVi.trim() && (
                            <button
                              type="button"
                              onClick={handleCopyCurrentVi}
                              className="font-medium text-primary hover:underline focus-visible:outline-none focus-visible:underline"
                            >
                              Lấy từ tên hiện tại
                            </button>
                          )}
                          <span className="tabular-nums text-ink-muted">
                            {newTitleVi.trim().length}/300
                          </span>
                        </div>
                      </div>
                      <Input
                        ref={titleViInputRef}
                        id="rename-new-title-vi"
                        value={newTitleVi}
                        onChange={(event) => setNewTitleVi(event.target.value)}
                        maxLength={300}
                        disabled={create.isPending}
                        aria-invalid={fieldErrorMap.has('rename-new-title-vi')}
                        placeholder="Nhập tên đề tài mới đầy đủ, chính xác bằng tiếng Việt"
                        className="h-10 rounded-xl"
                      />
                      {fieldErrorMap.has('rename-new-title-vi') && (
                        <p role="alert" className="text-xs font-medium text-tone-danger-text">
                          {fieldErrorMap.get('rename-new-title-vi')}
                        </p>
                      )}
                    </div>

                    <div className="space-y-1.5">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <label
                          htmlFor="rename-new-title-en"
                          className="text-sm font-medium text-ink-secondary"
                        >
                          Tên mới tiếng Anh{' '}
                          <span className="text-xs font-normal text-ink-muted">
                            (Không bắt buộc)
                          </span>
                        </label>
                        <div className="flex items-center gap-3 text-xs">
                          {group.data.topicTitleEn &&
                            newTitleEn !== group.data.topicTitleEn && (
                              <button
                                type="button"
                                onClick={() => setNewTitleEn(group.data?.topicTitleEn || '')}
                                className="font-medium text-primary hover:underline focus-visible:outline-none focus-visible:underline"
                              >
                                Khôi phục tên EN gốc
                              </button>
                            )}
                          <span className="tabular-nums text-ink-muted">
                            {newTitleEn.trim().length}/350
                          </span>
                        </div>
                      </div>
                      <Input
                        id="rename-new-title-en"
                        value={newTitleEn}
                        onChange={(event) => setNewTitleEn(event.target.value)}
                        maxLength={350}
                        disabled={create.isPending}
                        placeholder="Nhập tên tiếng Anh tương ứng (mặc định giữ nguyên bản hiện tại)"
                        className="h-10 rounded-xl"
                      />
                      <p className="text-xs text-ink-muted">
                        Mặc định giữ nguyên tên tiếng Anh hiện tại để tránh bị xóa trắng khi chỉ đổi
                        tên tiếng Việt.
                      </p>
                    </div>

                    {/* Progressive Live Diff: chỉ hiện khi người dùng thực sự tạo thay đổi */}
                    {(viLiveDiff || enLiveDiff) && (
                      <div
                        className="space-y-2 rounded-xl border border-line/80 bg-surface-subtle/50 px-4 py-3"
                        aria-label="Xem trước thay đổi tên đề tài"
                      >
                        <p className="text-xs font-semibold text-ink-secondary">
                          Xem trước thay đổi
                        </p>
                        {viLiveDiff && (
                          <div className="space-y-1 text-[13px] leading-relaxed">
                            <p className="text-ink-muted">
                              <span className="mr-1.5 font-medium">Cũ:</span>
                              <TitleDiffText segments={viLiveDiff.oldSegments} variant="old" />
                            </p>
                            <p className="font-medium text-ink-primary">
                              <span className="mr-1.5 text-primary">Mới:</span>
                              <TitleDiffText segments={viLiveDiff.newSegments} variant="new" />
                            </p>
                          </div>
                        )}
                        {enLiveDiff && (
                          <div className="space-y-1 border-t border-line-subtle/70 pt-2 text-xs leading-relaxed">
                            {group.data.topicTitleEn && (
                              <p className="text-ink-muted">
                                <span className="mr-1.5 font-medium">EN cũ:</span>
                                <TitleDiffText
                                  segments={enLiveDiff.oldSegments}
                                  variant="old"
                                  subtle
                                />
                              </p>
                            )}
                            <p className="text-ink-body">
                              <span className="mr-1.5 font-medium text-primary">EN mới:</span>
                              <TitleDiffText
                                segments={enLiveDiff.newSegments}
                                variant="new"
                                subtle
                              />
                            </p>
                          </div>
                        )}
                      </div>
                    )}

                    <div className="space-y-1.5">
                      <label
                        id="rename-reason-label"
                        htmlFor="rename-reason"
                        className="block text-sm font-medium text-ink-secondary"
                      >
                        Lý do điều chỉnh <span className="text-tone-danger-icon">*</span>
                      </label>
                      <RichTextEditor
                        id="rename-reason"
                        ariaLabelledBy="rename-reason-label"
                        ariaDescribedBy={
                          fieldErrorMap.has('rename-reason') ? 'rename-reason-error' : undefined
                        }
                        value={reason}
                        onChange={setReason}
                        disabled={create.isPending}
                        required
                        invalid={fieldErrorMap.has('rename-reason')}
                        expandedTitle="Lý do điều chỉnh tên đề tài"
                        expandedSubtitle="Trình bày rõ lý do thay đổi phạm vi, phương pháp hoặc thuật ngữ để GVHD, Khoa và Phòng đào tạo xem xét."
                        minHeightClassName="min-h-[104px]"
                        placeholder="Ví dụ: Thu hẹp phạm vi thực nghiệm và cập nhật tên công nghệ theo góp ý của GVHD sau khi hoàn thành đề cương."
                      />
                      {fieldErrorMap.has('rename-reason') && (
                        <p
                          id="rename-reason-error"
                          role="alert"
                          className="text-xs font-medium text-tone-danger-text"
                        >
                          {fieldErrorMap.get('rename-reason')}
                        </p>
                      )}
                      <p className="text-xs text-ink-muted">
                        Nêu rõ lý do học thuật, phạm vi thay đổi và trao đổi với GVHD (nếu có).
                      </p>
                    </div>
                  </div>

                  {/* Chân biểu mẫu tích hợp gọn gàng */}
                  <div className="flex flex-wrap items-center justify-between gap-3 bg-surface-subtle/40 px-5 py-3.5 sm:px-6">
                    <p className="text-xs text-ink-muted">
                      Đơn sẽ chuyển tới GVHD {group.data.advisor.fullName || 'phụ trách'} xem xét
                      ở cấp 1.
                    </p>
                    <div className="flex flex-wrap items-center gap-2">
                      {hasFormDraft && (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={create.isPending}
                          leftIcon={<RotateCcw />}
                          onClick={handleResetForm}
                          className="h-9 rounded-xl border-line-input bg-surface-card"
                        >
                          Đặt lại
                        </Button>
                      )}
                      <Button
                        type="submit"
                        disabled={create.isPending}
                        isLoading={create.isPending}
                        loadingText="Đang gửi đơn..."
                        leftIcon={<Send />}
                        className="h-9 rounded-xl px-4"
                      >
                        Gửi đơn đổi tên
                      </Button>
                    </div>
                  </div>
                </form>
              </Card>
            )}

            {/* Bảng lịch sử đơn 4 cột tinh gọn */}
            <Card className="min-w-0 overflow-hidden rounded-2xl border-line/80 bg-surface-card shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
              <ListCardHeader
                title={`Lịch sử đơn của nhóm (${history.data?.length ?? 0})`}
                description="Nhấp đúp vào dòng hoặc bấm Xem để đối chiếu chi tiết và phản hồi từng cấp."
                className="min-h-0 xl:grid-cols-[minmax(0,1fr)_auto]"
              />

              <DataTable
                data={history.data ?? []}
                columns={historyColumns}
                getRowId={(item) => item.id}
                ariaLabel="Lịch sử đơn xin đổi tên đề tài của nhóm"
                selectedRowId={selectedHistoryId}
                onRowSelect={(id) =>
                  setSelectedHistoryId((prev) => (prev === id ? null : id))
                }
                onRowOpen={(item) => {
                  setSelectedHistoryId(item.id);
                  setDetailRequest(item);
                }}
                loading={history.isPending}
                busy={history.isFetching && !history.isPending}
                error={
                  history.isError
                    ? {
                        message: getTopicErrorMessage(history.error),
                        onRetry: () => void history.refetch(),
                      }
                    : null
                }
                empty={{
                  title: 'Nhóm chưa có đơn xin đổi tên nào',
                  description:
                    'Khi bạn hoặc thành viên trong nhóm gửi đơn, lịch sử xử lý qua 3 cấp sẽ được lưu tại đây.',
                }}
                renderMobileItem={(item) => {
                  const isRejected = item.status.startsWith('Rejected');
                  return (
                    <div className="space-y-3 rounded-xl border border-line/80 bg-surface-card p-4">
                      <div className="flex items-start justify-between gap-2">
                        <StatusBadge tone={renameStatusTones[item.status]}>
                          {renameStatusLabels[item.status] ?? item.status}
                        </StatusBadge>
                        <span className="text-xs font-medium tabular-nums text-ink-muted">
                          {formatRenameDate(item.createdAt)}
                        </span>
                      </div>
                      <div className="space-y-1">
                        <p className="text-sm font-semibold leading-snug text-ink-primary">
                          {item.newTitleVi}
                        </p>
                        <p className="line-clamp-2 text-xs text-ink-body">
                          Từ: {item.oldTitleVi}
                        </p>
                      </div>
                      {item.rejectionReason && (
                        <p className="rounded-lg border border-tone-danger-border bg-tone-danger-bg px-3 py-2 text-xs text-tone-danger-text">
                          Lý do từ chối: {item.rejectionReason}
                        </p>
                      )}
                      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line-subtle pt-2.5">
                        <span className="text-xs text-ink-muted">
                          {getHistoryStepSummary(item)}
                        </span>
                        <div className="flex items-center gap-2">
                          {isRejected && canCompose && (
                            <Button
                              type="button"
                              variant="ghost"
                              size="sm"
                              leftIcon={<RotateCcw />}
                              onClick={() => handleReuseRequest(item)}
                              className="h-8 rounded-md px-2.5 text-xs text-primary"
                            >
                              Dùng lại
                            </Button>
                          )}
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={() => setDetailRequest(item)}
                            className="h-8 rounded-md border-line-input px-3 text-xs font-medium"
                          >
                            Xem
                          </Button>
                        </div>
                      </div>
                    </div>
                  );
                }}
              />
            </Card>
          </div>

          <StudentRenamePipelineDock
            group={group.data}
            timeline={timeline.data}
            activeRequest={activeRequest}
            latestRequest={latestRequest}
            windowClosed={windowClosed}
            windowReason={windowReason}
            nowMs={nowMs}
          />
        </div>
      )}

      {detailRequest && group.data && (
        <StudentRenameDetailDialog
          request={detailRequest}
          group={group.data}
          canReuse={canCompose}
          onClose={() => setDetailRequest(null)}
          onReuse={handleReuseRequest}
        />
      )}

      <ConfirmDialog
        open={confirmOpen}
        onClose={() => {
          if (!create.isPending) setConfirmOpen(false);
        }}
        onConfirm={() => create.mutate()}
        isConfirming={create.isPending}
        type="brand"
        title="Xác nhận gửi đơn đổi tên đề tài"
        confirmText="Xác nhận gửi đơn"
        cancelText="Kiểm tra lại"
        description={
          group.data ? (
            <div className="space-y-3 text-left text-xs text-ink-body">
              <p>
                Đơn sẽ được chuyển tới GVHD{' '}
                <strong className="text-ink-heading">
                  {group.data.advisor.fullName || 'phụ trách'}
                </strong>{' '}
                để xem xét ở cấp 1, sau đó qua Khoa và Phòng đào tạo.
              </p>
              <div className="space-y-1 rounded-xl border border-line/80 bg-surface-subtle/60 p-3">
                <p className="font-medium text-ink-muted">Tên mới tiếng Việt</p>
                <p className="font-semibold text-ink-primary">{newTitleVi.trim()}</p>
                {newTitleEn.trim() && (
                  <p className="text-ink-muted">EN: {newTitleEn.trim()}</p>
                )}
              </div>
              {create.isError && (
                <InlineAlert tone="error" title="Không thể gửi đơn">
                  {getTopicErrorMessage(create.error)}
                </InlineAlert>
              )}
            </div>
          ) : null
        }
      />

      <ConfirmDialog
        open={navigationBlocker.state === 'blocked'}
        onClose={() => navigationBlocker.reset?.()}
        onConfirm={() => navigationBlocker.proceed?.()}
        type="warning"
        title="Rời trang khi chưa gửi đơn?"
        confirmText="Rời trang"
        cancelText="Ở lại tiếp tục"
        description="Nội dung bạn đang soạn cho đơn xin đổi tên đề tài chưa được gửi và sẽ bị mất nếu rời khỏi trang này."
      />
    </div>
  );
}
