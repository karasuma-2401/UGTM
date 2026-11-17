import {
  Check,
  CheckCircle2,
  Clock,
  Download,
  Edit3,
  Eye,
  FileCheck2,
  History,
  X,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';
import type {
  ReviewDecision,
  ReviewGateHistoryDto,
  ReviewGateItem,
  ReviewGateKind,
} from '../../types/review.types';
import { formatArchivalFileSize } from '../../utils/archival';
import {
  formatReviewGateStudentsInline,
  getReviewGateStatusDisplay,
  isPdfSubmissionFile,
  REVIEW_FEEDBACK_MAX_LENGTH,
} from '../../utils/reviewGate';
import { ThesisFileTypeIcon } from '../shared/ThesisFileTypeIcon';

interface ReviewGateInspectorProps {
  gateKind: ReviewGateKind;
  selectedRow: ReviewGateItem | null;
  selectedBatchLabel: string;
  historyData: ReviewGateHistoryDto | null;
  hasInspectedFile: boolean;
  isAlreadyReviewed: boolean;
  feedback: string;
  feedbackError: string | null;
  isSubmitting: boolean;
  downloadingFileId: string | null;
  previewingFileId: string | null;
  onChangeFeedback: (value: string) => void;
  onOpenDecisionConfirm: (decision: ReviewDecision) => void;
  onOpenHistory: () => void;
  onDownloadFile: (fileId: string, fileName: string, groupId?: string) => void;
  onPreviewPdf: (
    fileId: string,
    fileName: string,
    fileSize?: number | null,
    groupId?: string,
  ) => void;
}

export function ReviewGateInspector({
  gateKind,
  selectedRow,
  selectedBatchLabel,
  historyData,
  hasInspectedFile,
  isAlreadyReviewed,
  feedback,
  feedbackError,
  isSubmitting,
  downloadingFileId,
  previewingFileId,
  onChangeFeedback,
  onOpenDecisionConfirm,
  onOpenHistory,
  onDownloadFile,
  onPreviewPdf,
}: ReviewGateInspectorProps) {
  const gateNoun = gateKind === 'syllabus' ? 'đề cương' : 'báo cáo giữa kỳ';

  if (!selectedRow) {
    return (
      <div className="p-6 text-center space-y-2">
        <div className="mx-auto flex size-10 items-center justify-center rounded-full bg-surface-muted text-ink-subtle">
          <FileCheck2 className="size-5" />
        </div>
        <p className="text-sm font-medium text-ink-heading">Chưa chọn hồ sơ {gateNoun}</p>
        <p className="text-xs text-ink-muted">
          Chọn một hồ sơ ở danh sách bên trái để xem tệp đối chiếu, lịch sử phiên bản và thẩm định.
        </p>
      </div>
    );
  }

  const statusDisplay = getReviewGateStatusDisplay(selectedRow.currentStatus);
  const canPreviewPdf = Boolean(
    selectedRow.fileId &&
      isPdfSubmissionFile(selectedRow.latestFileName, selectedRow.fileExtension),
  );
  const isDownloading = Boolean(selectedRow.fileId && downloadingFileId === selectedRow.fileId);
  const isPreviewing = Boolean(selectedRow.fileId && previewingFileId === selectedRow.fileId);
  const isBusyFileAction = downloadingFileId !== null || previewingFileId !== null;
  const cannotReview = isSubmitting || isAlreadyReviewed || !hasInspectedFile;

  const studentItems =
    selectedRow.students && selectedRow.students.length > 0
      ? selectedRow.students.map((student) => ({
          id: student.studentId,
          name: student.fullName || student.userCode || 'Sinh viên',
          code: student.userCode ?? undefined,
          userCode: student.userCode ?? undefined,
        }))
      : selectedRow.studentNames
          .split(',')
          .map((s) => s.trim())
          .filter(Boolean)
          .map((name, idx) => ({
            id: `${selectedRow.groupId}-s-${idx}`,
            name,
          }));

  const advisorItems =
    selectedRow.advisorName && selectedRow.advisorName !== 'Chưa phân công'
      ? [{ id: selectedRow.advisorId || selectedRow.advisorName, name: selectedRow.advisorName }]
      : [];

  const latestVersionHistory = historyData?.versions?.find(
    (v) => v.versionNo === selectedRow.latestVersionNo,
  );
  const latestReview = latestVersionHistory?.review ?? null;

  return (
    <div className="divide-y divide-line-subtle">
      {/* 1. Header & People */}
      <div className="p-5 space-y-3.5">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            <span className="text-sm font-bold text-primary tabular-nums whitespace-nowrap">
              {selectedRow.topicCode} · {selectedRow.groupCode}
            </span>
            {selectedRow.latestVersionNo > 1 ? (
              <Badge variant="info" className="text-[11px] tabular-nums whitespace-nowrap">
                v{selectedRow.latestVersionNo} · Nộp lại
              </Badge>
            ) : (
              <span className="rounded-md border border-line-input bg-surface-muted px-2 py-0.5 text-xs font-semibold text-ink-secondary tabular-nums whitespace-nowrap">
                v{selectedRow.latestVersionNo}
              </span>
            )}
          </div>
          <StatusBadge tone={statusDisplay.tone} className="shrink-0">
            {statusDisplay.label}
          </StatusBadge>
        </div>

        <div className="space-y-1">
          <h2 className="text-base font-bold text-ink-heading leading-snug">
            {selectedRow.topicTitleVi}
          </h2>
          {selectedRow.topicTitleEn && (
            <p className="text-xs italic text-ink-muted leading-relaxed">
              {selectedRow.topicTitleEn}
            </p>
          )}
        </div>

        <div className="rounded-xl border border-line/75 bg-surface-subtle/60 p-3 space-y-2.5 text-xs">
          <div className="flex items-center gap-2.5 min-w-0">
            <AvatarGroup
              items={studentItems}
              max={3}
              size="sm"
              ariaLabel="Sinh viên thực hiện"
            />
            <div className="min-w-0">
              <p className="text-[11px] text-ink-subtle">Sinh viên thực hiện</p>
              <p className="font-medium text-ink-heading truncate">
                {formatReviewGateStudentsInline(selectedRow)}
              </p>
            </div>
          </div>

          <div className="flex items-center justify-between gap-3 pt-2 border-t border-line/60">
            <div className="flex items-center gap-2 min-w-0">
              <AvatarGroup
                items={advisorItems}
                max={1}
                size="sm"
                ariaLabel="Giảng viên hướng dẫn"
              />
              <div className="min-w-0">
                <p className="text-[11px] text-ink-subtle">GVHD</p>
                <p className="font-medium text-ink-heading truncate">
                  {selectedRow.advisorName || 'Chưa phân công'}
                </p>
              </div>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-7 px-2.5 rounded-lg text-xs shrink-0 bg-surface-card"
              leftIcon={<History className="size-3.5" />}
              onClick={onOpenHistory}
              title={
                selectedRow.latestVersionNo > 1
                  ? `Xem lịch sử các phiên bản (v1–v${selectedRow.latestVersionNo})`
                  : `Xem lịch sử nộp (${selectedBatchLabel})`
              }
            >
              Lịch sử{' '}
              {selectedRow.latestVersionNo > 1 ? `(v1–v${selectedRow.latestVersionNo})` : ''}
            </Button>
          </div>
        </div>
      </div>

      {/* 2. File & Quick Check */}
      <div className="p-5 space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          <div className="rounded-xl border border-line/70 bg-surface-subtle/40 px-3 py-2 flex items-start gap-2">
            <CheckCircle2 className="size-3.5 text-tone-success-icon shrink-0 mt-0.5" />
            <div className="min-w-0">
              <p className="text-xs font-medium text-ink-heading leading-tight">Đợt khóa luận</p>
              <p className="text-[11px] text-ink-muted truncate mt-0.5">
                {selectedRow.batchName || selectedBatchLabel}
              </p>
            </div>
          </div>

          <div className="rounded-xl border border-line/70 bg-surface-subtle/40 px-3 py-2 flex items-start gap-2">
            {hasInspectedFile ? (
              <CheckCircle2 className="size-3.5 text-tone-success-icon shrink-0 mt-0.5" />
            ) : (
              <Clock className="size-3.5 text-tone-warning-icon shrink-0 mt-0.5" />
            )}
            <div className="min-w-0">
              <p className="text-xs font-medium text-ink-heading leading-tight">
                Tài liệu đối chiếu
              </p>
              <p className="text-[11px] text-ink-muted truncate mt-0.5">
                {hasInspectedFile ? 'Đã mở kiểm tra' : 'Chưa mở xem / tải'}
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-xs font-semibold text-ink-secondary">
              Tệp {gateNoun} (v{selectedRow.latestVersionNo})
            </h3>
            {selectedRow.submittedAtUtc && (
              <span className="text-[11px] text-ink-muted tabular-nums">
                Nộp {formatDateTimeVi(selectedRow.submittedAtUtc)}
              </span>
            )}
          </div>

          <div className="rounded-xl border border-line/80 bg-surface-subtle/60 p-3 flex flex-col gap-3">
            <div className="flex items-center gap-2.5 min-w-0">
              <ThesisFileTypeIcon
                fileName={selectedRow.fileId ? selectedRow.latestFileName : 'document.pdf'}
              />
              <div className="min-w-0 flex-1">
                <p
                  className="text-xs font-semibold text-ink-heading truncate"
                  title={selectedRow.latestFileName}
                >
                  {selectedRow.fileId ? selectedRow.latestFileName : 'Chưa có tệp'}
                </p>
                <p className="text-[11px] text-ink-muted tabular-nums truncate">
                  {selectedRow.fileId
                    ? selectedRow.fileSize
                      ? formatArchivalFileSize(selectedRow.fileSize)
                      : `Phiên bản v${selectedRow.latestVersionNo}`
                    : 'Nhóm chưa tải tệp lên'}
                </p>
              </div>
            </div>

            <div className="flex items-center justify-end gap-1.5 pt-2 border-t border-line/60">
              {canPreviewPdf && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  aria-label={`Xem trước bản PDF ${gateNoun}`}
                  className="h-7 rounded-lg bg-surface-card px-2.5 text-[11px]"
                  disabled={!selectedRow.fileId || isBusyFileAction}
                  isLoading={isPreviewing}
                  leftIcon={!isPreviewing ? <Eye className="size-3 text-ink-body" /> : undefined}
                  onClick={() =>
                    selectedRow.fileId &&
                    onPreviewPdf(
                      selectedRow.fileId,
                      selectedRow.latestFileName,
                      selectedRow.fileSize,
                      selectedRow.groupId,
                    )
                  }
                >
                  Xem trước
                </Button>
              )}

              <Button
                type="button"
                variant="outline"
                size="sm"
                aria-label={`Tải tệp ${gateNoun}`}
                className="h-7 rounded-lg bg-surface-card px-2.5 text-[11px]"
                disabled={!selectedRow.fileId || isBusyFileAction}
                isLoading={isDownloading}
                leftIcon={!isDownloading ? <Download className="size-3" /> : undefined}
                onClick={() =>
                  selectedRow.fileId &&
                  onDownloadFile(
                    selectedRow.fileId,
                    selectedRow.latestFileName,
                    selectedRow.groupId,
                  )
                }
              >
                Tải xuống
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* 3. Decision / Status Section */}
      <div className="p-5 bg-surface-subtle/30 space-y-3.5">
        {selectedRow.currentStatus === 'Submitted' ? (
          <div className="space-y-3">
            {!hasInspectedFile && selectedRow.fileId && (
              <InlineAlert
                tone="warning"
                title={`Mở xem tệp ${gateNoun} trước khi thẩm định`}
                action={
                  canPreviewPdf ? (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={isBusyFileAction}
                      isLoading={isPreviewing}
                      onClick={() =>
                        selectedRow.fileId &&
                        onPreviewPdf(
                          selectedRow.fileId,
                          selectedRow.latestFileName,
                          selectedRow.fileSize,
                          selectedRow.groupId,
                        )
                      }
                    >
                      Xem trước PDF
                    </Button>
                  ) : (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={isBusyFileAction}
                      isLoading={isDownloading}
                      onClick={() =>
                        selectedRow.fileId &&
                        onDownloadFile(
                          selectedRow.fileId,
                          selectedRow.latestFileName,
                          selectedRow.groupId,
                        )
                      }
                    >
                      Tải tệp ngay
                    </Button>
                  )
                }
              >
                Cần mở xem trước hoặc tải tệp về đối chiếu để mở khóa phần nhận xét và quyết định.
              </InlineAlert>
            )}

            <div className="flex items-center justify-between gap-2 text-xs">
              <label htmlFor="review-gate-feedback" className="font-semibold text-ink-heading">
                Nhận xét thẩm định{' '}
                <span className="font-normal text-ink-muted">
                  (bắt buộc khi yêu cầu sửa hoặc không đạt)
                </span>
              </label>
              <span className="text-ink-subtle tabular-nums shrink-0">
                {feedback.length}/{REVIEW_FEEDBACK_MAX_LENGTH}
              </span>
            </div>

            <textarea
              id="review-gate-feedback"
              rows={3}
              maxLength={REVIEW_FEEDBACK_MAX_LENGTH}
              disabled={cannotReview}
              value={feedback}
              onChange={(e) => onChangeFeedback(e.target.value)}
              placeholder={
                !hasInspectedFile
                  ? `Mở xem trước hoặc tải tệp ${gateNoun} để mở khóa ô nhận xét...`
                  : 'Nhập nhận xét, góp ý hoặc yêu cầu chỉnh sửa cho nhóm sinh viên...'
              }
              className={cn(
                'w-full rounded-xl border bg-surface-card px-3 py-2 text-xs text-ink-heading placeholder:text-ink-subtle focus:outline-none focus:ring-2 disabled:bg-surface-subtle disabled:text-ink-muted',
                feedbackError
                  ? 'border-tone-danger-border focus:border-tone-danger-icon focus:ring-tone-danger-icon/20'
                  : 'border-line-input focus:border-primary focus:ring-primary/20',
              )}
            />

            {feedbackError && (
              <p role="alert" className="text-xs font-medium text-tone-danger-icon">
                {feedbackError}
              </p>
            )}

            <div className="flex flex-wrap items-center justify-end gap-2 pt-1">
              <Button
                type="button"
                variant="outline"
                disabled={cannotReview}
                leftIcon={<X className="size-3.5" />}
                onClick={() => onOpenDecisionConfirm('Rejected')}
                className="h-9 rounded-xl border-tone-danger-border text-tone-danger-text hover:bg-tone-danger-bg text-xs font-semibold"
              >
                Không đạt
              </Button>

              <Button
                type="button"
                variant="outline"
                disabled={cannotReview}
                leftIcon={<Edit3 className="size-3.5" />}
                onClick={() => onOpenDecisionConfirm('NeedRevision')}
                className="h-9 rounded-xl border-tone-warning-border text-tone-warning-strong hover:bg-tone-warning-bg text-xs font-semibold"
              >
                Yêu cầu chỉnh sửa
              </Button>

              <Button
                type="button"
                disabled={cannotReview}
                leftIcon={<Check className="size-3.5" />}
                onClick={() => onOpenDecisionConfirm('Approved')}
                className="h-9 rounded-xl text-xs font-semibold"
              >
                Đạt
              </Button>
            </div>

            <p className="text-[11px] text-ink-muted leading-relaxed">
              <strong className="text-ink-secondary">Đạt:</strong> mở giai đoạn tiếp theo ·{' '}
              <strong className="text-ink-secondary">Yêu cầu chỉnh sửa:</strong> cho nộp lại ·{' '}
              <strong className="text-ink-secondary">Không đạt:</strong> dừng KLTN.
            </p>
          </div>
        ) : (
          <div className="space-y-3">
            {selectedRow.currentStatus === 'Approved' && (
              <InlineAlert tone="success" title="Hồ sơ đã được thẩm định Đạt">
                {gateKind === 'syllabus'
                  ? 'Hồ sơ đề cương đã được phê duyệt Đạt. Nhóm đủ điều kiện thực hiện các mốc tiếp theo.'
                  : 'Báo cáo giữa kỳ đã được phê duyệt Đạt. Nhóm đủ điều kiện chuẩn bị giai đoạn báo cáo cuối kỳ.'}
              </InlineAlert>
            )}

            {selectedRow.currentStatus === 'NeedRevision' && (
              <InlineAlert tone="warning" title="Đang chờ sinh viên chỉnh sửa và nộp lại">
                Hồ sơ đã được trả về để sinh viên bổ sung theo nhận xét của Khoa.
              </InlineAlert>
            )}

            {selectedRow.currentStatus === 'Rejected' && (
              <InlineAlert tone="error" title="Hồ sơ đã bị đánh giá Không đạt">
                Hồ sơ không đạt yêu cầu tại cổng kiểm soát này và đã khóa các bước tiếp theo.
              </InlineAlert>
            )}

            {latestReview?.feedback && (
              <div className="rounded-xl border border-line/80 bg-surface-card p-3 space-y-1 text-xs">
                <div className="flex items-center justify-between gap-2 text-[11px] text-ink-muted">
                  <span className="font-semibold text-ink-secondary">Nhận xét gần nhất</span>
                  {latestReview.reviewedAtUtc && (
                    <span className="tabular-nums">
                      {formatDateTimeVi(latestReview.reviewedAtUtc)}
                    </span>
                  )}
                </div>
                <p className="text-ink-body whitespace-pre-wrap leading-relaxed">
                  {latestReview.feedback}
                </p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
