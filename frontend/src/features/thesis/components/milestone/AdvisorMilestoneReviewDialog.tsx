import { useId, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowUpRight,
  CalendarClock,
  CheckCircle2,
  Clock3,
  Download,
  FileText,
  GitBranch,
  History,
  MessageSquareText,
  RotateCcw,
  X,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { Select } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import type { AdvisedGroupOption, MilestoneWithReportDto } from '../../types/milestone.types';
import {
  formatFileSize,
  formatMilestoneDateShort,
  formatMilestoneDateTime,
  formatMilestoneTimeShort,
  formatSubmissionDelta,
  getSafeHttpUrl,
  toReportStatus,
  type ProgressReportDecision,
  type ProgressReportReviewRequest,
} from '../../utils/milestoneFormatters';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';

export interface AdvisorMilestoneReviewDialogProps {
  item: MilestoneWithReportDto | null;
  selectedGroup: AdvisedGroupOption | null;
  onClose: () => void;
  onSubmitReview: (milestoneId: string, data: ProgressReportReviewRequest) => Promise<unknown>;
  onDownloadFile: (fileId: string, fileName?: string) => Promise<void>;
  downloadingFileId?: string | null;
  isSubmitting?: boolean;
  serverError?: string | null;
}

const DECISION_OPTIONS: readonly {
  value: ProgressReportDecision;
  label: string;
  hint: string;
  icon: typeof CheckCircle2;
  activeClass: string;
  iconClass: string;
}[] = [
  {
    value: 'Accepted',
    label: 'Xác nhận Đạt',
    hint: 'Báo cáo đáp ứng yêu cầu mốc, nhóm tiếp tục giai đoạn tiếp theo.',
    icon: CheckCircle2,
    activeClass: 'border-emerald-300 bg-emerald-50/75 text-emerald-950 ring-1 ring-emerald-200',
    iconClass: 'text-emerald-600',
  },
  {
    value: 'NeedRevision',
    label: 'Yêu cầu chỉnh sửa',
    hint: 'Nhóm cần bổ sung mã nguồn hoặc báo cáo và nộp lại phiên bản mới.',
    icon: RotateCcw,
    activeClass: 'border-amber-300 bg-amber-50/80 text-amber-950 ring-1 ring-amber-200',
    iconClass: 'text-amber-600',
  },
];

export function AdvisorMilestoneReviewDialog({
  item,
  selectedGroup,
  onClose,
  onSubmitReview,
  onDownloadFile,
  downloadingFileId = null,
  isSubmitting = false,
  serverError = null,
}: AdvisorMilestoneReviewDialogProps) {
  if (!item) return null;

  return (
    <Dialog
      title={item.milestone.title}
      hideHeader
      onClose={onClose}
      busy={isSubmitting}
      className="flex max-h-[92dvh] w-[calc(100%-2rem)] max-w-6xl flex-col overflow-hidden rounded-2xl border border-slate-200/90 p-0 shadow-[0_24px_60px_-15px_rgba(15,23,42,0.18)]"
    >
      <ReviewDialogBody
        key={`${item.milestone.id}-${item.report?.currentVersionNo ?? 0}-${item.report?.currentStatus ?? 'NotSubmitted'}`}
        item={item}
        selectedGroup={selectedGroup}
        onClose={onClose}
        onSubmitReview={onSubmitReview}
        onDownloadFile={onDownloadFile}
        downloadingFileId={downloadingFileId}
        isSubmitting={isSubmitting}
        serverError={serverError}
      />
    </Dialog>
  );
}

function ReviewDialogBody({
  item,
  selectedGroup,
  onClose,
  onSubmitReview,
  onDownloadFile,
  downloadingFileId,
  isSubmitting,
  serverError,
}: {
  item: MilestoneWithReportDto;
  selectedGroup: AdvisedGroupOption | null;
  onClose: () => void;
  onSubmitReview: (milestoneId: string, data: ProgressReportReviewRequest) => Promise<unknown>;
  onDownloadFile: (fileId: string, fileName?: string) => Promise<void>;
  downloadingFileId: string | null;
  isSubmitting: boolean;
  serverError: string | null;
}) {
  const feedbackLabelId = useId();
  const feedbackErrorId = useId();

  const { milestone, report } = item;
  const status = toReportStatus(report?.currentStatus);
  const versions = [...(report?.versions ?? [])].sort((a, b) => b.versionNo - a.versionNo);
  const latestVersionNo = report?.currentVersionNo ?? versions[0]?.versionNo ?? null;

  const [selectedVersionNo, setSelectedVersionNo] = useState<number | null>(latestVersionNo);
  const [decision, setDecision] = useState<ProgressReportDecision>('Accepted');
  const [feedback, setFeedback] = useState('');
  const [feedbackError, setFeedbackError] = useState('');

  const activeVersion =
    versions.find((version) => version.versionNo === selectedVersionNo) ?? versions[0] ?? null;

  const canEvaluate = status === 'Submitted';
  const feedbackRequired = decision === 'NeedRevision';
  const safeGitUrl = getSafeHttpUrl(activeVersion?.gitUrl);
  const submissionDelta = activeVersion
    ? formatSubmissionDelta(milestone.deadlineUtc, activeVersion.submittedAtUtc)
    : null;
  const reviewedVersions = versions.filter((version) => Boolean(version.review));

  function handleCloseModal() {
    if (isSubmitting) return;
    onClose();
  }

  async function handleSubmit() {
    if (!canEvaluate || isSubmitting) return;
    const cleanFeedback = feedback.trim();
    if (feedbackRequired && !cleanFeedback) {
      setFeedbackError('Vui lòng nhập nhận xét chi tiết để nhóm biết điểm cần chỉnh sửa.');
      return;
    }
    setFeedbackError('');
    await onSubmitReview(milestone.id, {
      decision,
      feedback: cleanFeedback || undefined,
    });
  }

  return (
    <>
      <div
        aria-hidden="true"
        className="h-1 shrink-0 bg-[linear-gradient(90deg,#2054a5_0%,#2563eb_55%,#fa7921_100%)]"
      />

      <div className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 bg-white px-5 py-4 sm:px-6">
        <div className="min-w-0 space-y-1.5">
          <div className="flex flex-wrap items-center gap-2.5">
            <h2 className="text-lg font-semibold tracking-tight text-slate-900">
              {milestone.title}
            </h2>
            <MilestoneStatusBadge status={status} />
          </div>
          <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1 text-xs text-slate-500">
            <span className="inline-flex items-center gap-1 tabular-nums">
              <CalendarClock className="size-3.5 text-slate-400" aria-hidden="true" />
              <span>Hạn nộp: {formatMilestoneDateTime(milestone.deadlineUtc)}</span>
            </span>
            {selectedGroup && (
              <>
                <span aria-hidden="true">·</span>
                <span className="inline-flex items-center gap-1.5">
                  <span className="font-mono font-semibold text-slate-700">
                    {selectedGroup.groupCode}
                  </span>
                  <AvatarGroup
                    size="sm"
                    items={selectedGroup.students.map((student) => ({
                      id: student.studentId,
                      name: student.fullName,
                      userCode: student.userCode,
                    }))}
                  />
                </span>
                <span aria-hidden="true">·</span>
                <span className="truncate">{selectedGroup.topicTitleVi}</span>
              </>
            )}
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-1.5">
          <Button
            asChild
            type="button"
            variant="ghost"
            size="sm"
            className="hidden h-8 gap-1 text-xs text-slate-600 hover:text-primary sm:inline-flex"
          >
            <Link to={`/advisor/milestones/${milestone.id}/review`}>
              <span>Mở trang riêng</span>
              <ArrowUpRight className="size-3.5" aria-hidden="true" />
            </Link>
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-label="Đóng hộp thoại"
            disabled={isSubmitting}
            onClick={handleCloseModal}
            className="size-8 shrink-0 rounded-lg p-0 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="size-4" />
          </Button>
        </div>
      </div>

      <div className="grid min-h-0 flex-1 grid-cols-1 overflow-y-auto lg:grid-cols-[minmax(0,1.1fr)_minmax(400px,0.95fr)] lg:grid-rows-[minmax(0,1fr)] lg:divide-x lg:divide-slate-100 lg:overflow-hidden">
        {/* Left pane: Deliverable spec & submitted student artifact (flat surface hierarchy) */}
        <div className="min-h-0 divide-y divide-slate-100 bg-white lg:overflow-y-auto">
          <section className="space-y-2.5 px-5 py-5 sm:px-6">
            <h3 className="text-xs font-semibold text-slate-600">Sản phẩm & yêu cầu cần nộp</h3>
            <div className="rounded-xl bg-slate-50/80 px-4 py-3.5 text-sm leading-relaxed text-slate-700">
              <RichTextContent
                content={milestone.description}
                emptyFallback="Chưa có mô tả chi tiết yêu cầu sản phẩm."
              />
            </div>
          </section>

          <section className="space-y-4 px-5 py-5 sm:px-6">
            <div className="flex flex-wrap items-center justify-between gap-2.5">
              <h3 className="flex items-center gap-1.5 text-xs font-semibold text-slate-600">
                <History className="size-3.5 text-slate-400" aria-hidden="true" />
                <span>
                  {versions.length > 1
                    ? `Bài nộp của nhóm (${versions.length} lần nộp)`
                    : 'Bài nộp của nhóm'}
                </span>
              </h3>

              {versions.length === 1 ? (
                <span className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200/90 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-700">
                  <span>Lần nộp {versions[0].versionNo}</span>
                  {versions[0].isLate && <span className="text-amber-700">· Trễ hạn</span>}
                </span>
              ) : versions.length > 1 ? (
                <Select<string>
                  size="sm"
                  aria-label="Chọn lần nộp bài"
                  value={activeVersion ? String(activeVersion.versionNo) : undefined}
                  onChange={(nextVal) => {
                    const parsed = Number(nextVal);
                    if (Number.isFinite(parsed)) setSelectedVersionNo(parsed);
                  }}
                  containerClassName="w-full sm:w-auto sm:min-w-[235px]"
                  triggerClassName="rounded-lg font-semibold"
                  contentClassName="left-auto right-0"
                  maxMenuHeight={220}
                  options={versions.map((version, index) => ({
                    value: String(version.versionNo),
                    label:
                      index === 0
                        ? `Lần nộp ${version.versionNo} (Mới nhất)`
                        : `Lần nộp ${version.versionNo}`,
                    description: formatMilestoneDateTime(version.submittedAtUtc),
                    badge: version.review ? (
                      <span
                        className={cn(
                          'text-[11px] font-medium',
                          version.review.decision === 'Accepted'
                            ? 'text-emerald-700'
                            : 'text-amber-700',
                        )}
                      >
                        {version.review.decision === 'Accepted' ? 'Đạt' : 'Cần sửa'}
                      </span>
                    ) : version.isLate ? (
                      <span className="text-[11px] font-medium text-amber-700">Trễ hạn</span>
                    ) : undefined,
                  }))}
                />
              ) : null}
            </div>

            {!activeVersion ? (
              <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50/50 p-6 text-center text-sm text-slate-500">
                Nhóm sinh viên chưa nộp báo cáo nào cho mốc tiến độ này.
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
                  <span className="font-medium text-slate-700">
                    Gửi lúc{' '}
                    <span className="tabular-nums">
                      {formatMilestoneDateTime(activeVersion.submittedAtUtc)}
                    </span>
                  </span>
                  {submissionDelta && (
                    <span
                      className={cn(
                        'font-medium',
                        submissionDelta.isLate ? 'text-amber-700' : 'text-emerald-700',
                      )}
                    >
                      {submissionDelta.label}
                    </span>
                  )}
                </div>

                {/* Artifacts pair: File + Git */}
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="flex flex-col justify-between gap-2.5 rounded-xl border border-slate-200/80 bg-white p-3.5">
                    <div className="flex items-start gap-2.5">
                      <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
                        <FileText className="size-4" aria-hidden="true" />
                      </span>
                      <div className="min-w-0">
                        <p className="text-xs font-medium text-slate-500">Tệp báo cáo</p>
                        <p className="truncate text-sm font-semibold text-slate-900">
                          {activeVersion.file ? activeVersion.file.fileName : 'Không có tệp đính kèm'}
                        </p>
                        {activeVersion.file && (
                          <p className="text-xs tabular-nums text-slate-500">
                            {formatFileSize(activeVersion.file.fileSize)}
                          </p>
                        )}
                      </div>
                    </div>

                    {activeVersion.file && (
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={downloadingFileId === activeVersion.file.id}
                        onClick={() =>
                          void onDownloadFile(
                            activeVersion.file!.id,
                            activeVersion.file!.fileName,
                          )
                        }
                        className="h-8 w-full justify-center gap-1.5 rounded-lg text-xs"
                      >
                        <Download className="size-3.5" aria-hidden="true" />
                        <span>
                          {downloadingFileId === activeVersion.file.id
                            ? 'Đang tải xuống...'
                            : 'Tải xuống tệp'}
                        </span>
                      </Button>
                    )}
                  </div>

                  <div className="flex flex-col justify-between gap-2.5 rounded-xl border border-slate-200/80 bg-white p-3.5">
                    <div className="flex items-start gap-2.5">
                      <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
                        <GitBranch className="size-4" aria-hidden="true" />
                      </span>
                      <div className="min-w-0">
                        <p className="text-xs font-medium text-slate-500">Kho mã nguồn (Git)</p>
                        <p
                          className="truncate text-sm font-medium text-slate-800"
                          title={activeVersion.gitUrl ?? undefined}
                        >
                          {activeVersion.gitUrl || 'Không đính kèm liên kết Git'}
                        </p>
                      </div>
                    </div>

                    {safeGitUrl && (
                      <Button
                        asChild
                        type="button"
                        variant="outline"
                        size="sm"
                        className="h-8 w-full justify-center gap-1.5 rounded-lg text-xs"
                      >
                        <a href={safeGitUrl} target="_blank" rel="noopener noreferrer">
                          <span>Mở liên kết Git</span>
                          <ArrowUpRight className="size-3.5" aria-hidden="true" />
                        </a>
                      </Button>
                    )}
                  </div>
                </div>

                {activeVersion.studentNote && (
                  <div className="space-y-1.5 pt-1">
                    <p className="text-xs font-semibold text-slate-600">Ghi chú của sinh viên</p>
                    <div className="rounded-xl bg-slate-50/80 px-4 py-3 text-sm leading-relaxed text-slate-700">
                      <RichTextContent content={activeVersion.studentNote} />
                    </div>
                  </div>
                )}
              </div>
            )}
          </section>
        </div>

        {/* Right pane: Timing comparison + Evaluation form OR Review history timeline */}
        <div className="flex min-h-0 flex-col bg-slate-50/55">
          <div className="min-h-0 flex-1 space-y-5 p-5 sm:p-6 lg:overflow-y-auto">
            {serverError && (
              <InlineAlert tone="error" title="Gửi đánh giá thất bại">
                {serverError}
              </InlineAlert>
            )}

            {/* Schedule & submission contrast bar */}
            <div className="grid grid-cols-2 gap-3 rounded-xl border border-slate-200/80 bg-white p-3.5 text-xs">
              <div>
                <p className="text-slate-500">Hạn nộp quy định</p>
                <p className="mt-0.5 font-semibold tabular-nums text-slate-900">
                  {formatMilestoneDateShort(milestone.deadlineUtc)} ·{' '}
                  {formatMilestoneTimeShort(milestone.deadlineUtc)}
                </p>
              </div>
              <div className="border-l border-slate-100 pl-3">
                <p className="text-slate-500">
                  {activeVersion ? `Thực nộp (lần ${activeVersion.versionNo})` : 'Tình trạng nộp'}
                </p>
                <p
                  className={cn(
                    'mt-0.5 font-semibold tabular-nums',
                    submissionDelta
                      ? submissionDelta.isLate
                        ? 'text-amber-700'
                        : 'text-emerald-700'
                      : 'text-slate-600',
                  )}
                >
                  {activeVersion
                    ? `${formatMilestoneDateShort(activeVersion.submittedAtUtc)} · ${formatMilestoneTimeShort(activeVersion.submittedAtUtc)}`
                    : 'Chưa có bài nộp'}
                </p>
              </div>
            </div>

            {canEvaluate ? (
              <>
                <div className="space-y-2.5">
                  <p className="text-xs font-semibold text-slate-600">Kết luận đánh giá</p>
                  <div className="grid gap-2.5">
                    {DECISION_OPTIONS.map((option) => {
                      const Icon = option.icon;
                      const active = decision === option.value;
                      return (
                        <button
                          key={option.value}
                          type="button"
                          disabled={isSubmitting}
                          aria-pressed={active}
                          onClick={() => {
                            setDecision(option.value);
                            if (feedbackError) setFeedbackError('');
                          }}
                          className={cn(
                            'flex items-start gap-3 rounded-xl border p-3.5 text-left transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30',
                            active
                              ? option.activeClass
                              : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300',
                          )}
                        >
                          <Icon
                            className={cn(
                              'mt-0.5 size-4 shrink-0',
                              active ? option.iconClass : 'text-slate-500',
                            )}
                            aria-hidden="true"
                          />
                          <div className="min-w-0 space-y-0.5">
                            <p className="text-sm font-semibold">{option.label}</p>
                            <p className="text-xs leading-relaxed opacity-85">{option.hint}</p>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>

                <div className="flex min-h-[220px] flex-1 flex-col gap-2">
                  <div className="flex items-center justify-between gap-2">
                    <span id={feedbackLabelId} className="text-xs font-semibold text-slate-600">
                      Nhận xét của giảng viên
                      {feedbackRequired ? (
                        <span className="ml-1 text-rose-600">*</span>
                      ) : (
                        <span className="ml-1.5 font-normal text-slate-500">
                          (không bắt buộc)
                        </span>
                      )}
                    </span>
                  </div>

                  <RichTextEditor
                    id="milestone-review-feedback"
                    ariaLabelledBy={feedbackLabelId}
                    ariaDescribedBy={feedbackError ? feedbackErrorId : undefined}
                    value={feedback}
                    invalid={Boolean(feedbackError)}
                    disabled={isSubmitting}
                    expandable
                    expandedTitle="Soạn nhận xét mốc tiến độ"
                    expandedSubtitle={milestone.title}
                    placeholder={
                      feedbackRequired
                        ? 'Nêu rõ điểm cần chỉnh sửa để nhóm sinh viên hoàn thiện ở phiên bản tiếp theo...'
                        : 'Nhập lời khuyên hoặc đánh giá khích lệ cho nhóm sinh viên (không bắt buộc)...'
                    }
                    minHeightClassName="min-h-[190px] flex-1"
                    onChange={(nextValue) => {
                      setFeedback(nextValue);
                      if (feedbackError) setFeedbackError('');
                    }}
                  />

                  {feedbackError && (
                    <p id={feedbackErrorId} className="text-xs font-medium text-rose-600">
                      {feedbackError}
                    </p>
                  )}
                </div>
              </>
            ) : (
              <div className="space-y-4">
                <InlineAlert
                  tone={
                    status === 'Accepted'
                      ? 'success'
                      : status === 'NeedRevision'
                        ? 'warning'
                        : 'info'
                  }
                  title={
                    status === 'Accepted'
                      ? 'Mốc tiến độ đã đạt yêu cầu'
                      : status === 'NeedRevision'
                        ? 'Đang chờ nhóm chỉnh sửa và nộp lại'
                        : 'Mốc chưa có báo cáo chờ đánh giá'
                  }
                >
                  {status === 'Accepted'
                    ? 'Giảng viên đã xác nhận đạt cho phiên bản hiện tại của mốc này.'
                    : status === 'NeedRevision'
                      ? 'Nhóm sinh viên cần tải lên phiên bản mới trước khi giảng viên tiếp tục đánh giá.'
                      : 'Khi sinh viên nộp báo cáo tiến độ, biểu mẫu đánh giá sẽ mở tại đây.'}
                </InlineAlert>

                <div className="space-y-2.5">
                  <div className="flex items-center justify-between gap-2">
                    <h3 className="flex items-center gap-1.5 text-xs font-semibold text-slate-600">
                      <MessageSquareText className="size-3.5 text-slate-400" aria-hidden="true" />
                      <span>Lịch sử thẩm định & nhận xét</span>
                    </h3>
                    <span className="text-xs tabular-nums text-slate-500">
                      {reviewedVersions.length} lượt chấm
                    </span>
                  </div>

                  {reviewedVersions.length === 0 ? (
                    <div className="flex items-center gap-2.5 rounded-xl border border-dashed border-slate-200 bg-white/80 p-4 text-xs text-slate-500">
                      <Clock3 className="size-4 shrink-0 text-slate-400" aria-hidden="true" />
                      <span>Chưa có phản hồi nào từ giảng viên cho mốc tiến độ này.</span>
                    </div>
                  ) : (
                    <ol className="space-y-2.5">
                      {reviewedVersions.map((version) => {
                        const review = version.review!;
                        const accepted = review.decision === 'Accepted';
                        return (
                          <li
                            key={version.versionNo}
                            className="rounded-xl border border-slate-200/80 bg-white p-3.5"
                          >
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <div className="flex items-center gap-2">
                                <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-slate-700">
                                  Lần nộp {version.versionNo}
                                </span>
                                <span
                                  className={cn(
                                    'text-xs font-semibold',
                                    accepted ? 'text-emerald-700' : 'text-amber-700',
                                  )}
                                >
                                  {accepted ? 'Đạt yêu cầu' : 'Cần chỉnh sửa'}
                                </span>
                              </div>
                              <span className="text-[11px] tabular-nums text-slate-500">
                                {formatMilestoneDateTime(review.reviewedAtUtc)}
                              </span>
                            </div>

                            <div className="mt-2.5 border-t border-slate-100 pt-2.5 text-xs leading-relaxed text-slate-700">
                              <RichTextContent
                                content={review.feedback}
                                emptyFallback="Giảng viên không để lại ghi chú bổ sung."
                              />
                            </div>
                          </li>
                        );
                      })}
                    </ol>
                  )}
                </div>
              </div>
            )}
          </div>

          <div className="flex shrink-0 items-center justify-end gap-2.5 border-t border-slate-200/80 bg-white px-5 py-3.5 sm:px-6">
            <Button type="button" variant="outline" disabled={isSubmitting} onClick={handleCloseModal}>
              {canEvaluate ? 'Hủy' : 'Đóng'}
            </Button>
            {canEvaluate && (
              <Button
                type="button"
                disabled={isSubmitting}
                onClick={() => void handleSubmit()}
                className={cn(
                  decision === 'NeedRevision' &&
                    'bg-amber-600 text-white hover:bg-amber-700 focus-visible:ring-amber-500',
                )}
              >
                {isSubmitting
                  ? 'Đang lưu...'
                  : decision === 'Accepted'
                    ? 'Xác nhận Đạt'
                    : 'Gửi yêu cầu chỉnh sửa'}
              </Button>
            )}
          </div>
        </div>
      </div>
    </>
  );
}
