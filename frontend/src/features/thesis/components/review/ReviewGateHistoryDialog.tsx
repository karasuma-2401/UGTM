import { Download, Eye, History, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import type {
  ReviewGateHistoryDto,
  ReviewGateKind,
} from '../../types/review.types';
import { formatArchivalFileSize } from '../../utils/archival';
import {
  getReviewDecisionDisplay,
  getReviewGateStatusDisplay,
  isPdfSubmissionFile,
} from '../../utils/reviewGate';
import { ThesisFileTypeIcon } from '../shared/ThesisFileTypeIcon';

interface ReviewGateHistoryDialogProps {
  open: boolean;
  onClose: () => void;
  gateKind: ReviewGateKind;
  title: string;
  historyData: ReviewGateHistoryDto | null;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
  downloadingFileId: string | null;
  previewingFileId: string | null;
  onDownloadFile: (fileId: string, fileName: string) => void;
  onPreviewPdf: (fileId: string, fileName: string, fileSize?: number | null) => void;
}

export function ReviewGateHistoryDialog({
  open,
  onClose,
  gateKind,
  title,
  historyData,
  isLoading,
  error,
  onRetry,
  downloadingFileId,
  previewingFileId,
  onDownloadFile,
  onPreviewPdf,
}: ReviewGateHistoryDialogProps) {
  if (!open) return null;

  const gateNoun = gateKind === 'syllabus' ? 'đề cương' : 'báo cáo giữa kỳ';
  const versions = [...(historyData?.versions ?? [])].sort((a, b) => b.versionNo - a.versionNo);
  const highestVersionNo = versions[0]?.versionNo;
  const isBusyFileAction = downloadingFileId !== null || previewingFileId !== null;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      closeOnBackdropClick
      variant="drawer"
      title={
        <div className="flex items-center gap-2.5">
          <History className="size-4 text-primary" />
          <span className="text-base font-bold text-ink-primary">{title}</span>
        </div>
      }
      closeLabel={`Đóng lịch sử hồ sơ ${gateNoun}`}
      className="w-full max-w-[100vw] sm:max-w-[640px] bg-surface-card"
      headerClassName="border-b border-line/80 bg-surface-card/95 px-5 py-4 sm:px-6"
    >
      <div className="p-5 sm:p-6 space-y-4">
        {isLoading ? (
          <div className="space-y-4">
            <Skeleton className="h-32 w-full rounded-2xl" />
            <Skeleton className="h-32 w-full rounded-2xl" />
          </div>
        ) : error ? (
          <InlineAlert
            title="Không tải được lịch sử hồ sơ"
            action={
              <Button
                type="button"
                variant="outline"
                size="sm"
                leftIcon={<RefreshCw className="size-3.5" />}
                onClick={onRetry}
              >
                Thử lại
              </Button>
            }
          >
            {error}
          </InlineAlert>
        ) : versions.length === 0 ? (
          <div className="rounded-2xl border border-line/80 bg-surface-subtle/70 p-6 text-center text-sm text-ink-muted">
            Chưa có phiên bản {gateNoun} nào được nộp.
          </div>
        ) : (
          versions.map((version) => {
            const isLatest = version.versionNo === highestVersionNo;
            const statusDisplay = version.review
              ? getReviewDecisionDisplay(version.review.decision)
              : isLatest && historyData
                ? getReviewGateStatusDisplay(historyData.currentStatus)
                : { label: 'Cần bổ sung', tone: 'warning' as const };
            const canPreview = isPdfSubmissionFile(
              version.file.fileName,
              version.file.fileExtension,
            );
            const isDownloading = downloadingFileId === version.file.id;
            const isPreviewing = previewingFileId === version.file.id;

            return (
              <div
                key={version.versionNo}
                className="rounded-2xl border border-line/80 bg-surface-card p-4 sm:p-5 space-y-3.5 shadow-2xs"
              >
                <div className="flex flex-wrap items-center justify-between gap-2 pb-3 border-b border-line-subtle">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-bold text-ink-primary tabular-nums">
                      Phiên bản v{version.versionNo}
                    </span>
                    {isLatest && (
                      <Badge variant="info" className="text-[11px]">
                        Mới nhất
                      </Badge>
                    )}
                    <StatusBadge tone={statusDisplay.tone}>{statusDisplay.label}</StatusBadge>
                  </div>
                  <span className="text-xs text-ink-muted tabular-nums">
                    Nộp: {formatDateTimeVi(version.file.uploadedAtUtc)}
                  </span>
                </div>

                <div className="rounded-xl border border-line/75 bg-surface-subtle/60 p-3 flex flex-wrap items-center justify-between gap-3">
                  <div className="flex items-center gap-2.5 min-w-0 flex-1">
                    <ThesisFileTypeIcon fileName={version.file.fileName} />
                    <div className="min-w-0">
                      <p
                        className="text-xs font-semibold text-ink-heading truncate"
                        title={version.file.fileName}
                      >
                        {version.file.fileName}
                      </p>
                      <p className="text-[11px] text-ink-muted tabular-nums">
                        {formatArchivalFileSize(version.file.fileSize)}
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center gap-1.5 shrink-0">
                    {canPreview && (
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        className="h-7 rounded-lg bg-surface-card px-2.5 text-[11px]"
                        disabled={isBusyFileAction}
                        isLoading={isPreviewing}
                        leftIcon={!isPreviewing ? <Eye className="size-3" /> : undefined}
                        onClick={() =>
                          onPreviewPdf(
                            version.file.id,
                            version.file.fileName,
                            version.file.fileSize,
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
                      className="h-7 rounded-lg bg-surface-card px-2.5 text-[11px]"
                      disabled={isBusyFileAction}
                      isLoading={isDownloading}
                      leftIcon={!isDownloading ? <Download className="size-3" /> : undefined}
                      onClick={() => onDownloadFile(version.file.id, version.file.fileName)}
                    >
                      Tải
                    </Button>
                  </div>
                </div>

                {version.review && (
                  <div className="rounded-xl border border-line/75 bg-surface-subtle/40 p-3 space-y-1 text-xs">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="font-semibold text-ink-heading">
                        {getReviewDecisionDisplay(version.review.decision).historyLabel}
                      </span>
                      <span className="text-[11px] text-ink-muted tabular-nums">
                        {formatDateTimeVi(version.review.reviewedAtUtc)}
                      </span>
                    </div>
                    {version.review.feedback && (
                      <p className="text-ink-body whitespace-pre-wrap leading-relaxed pt-0.5">
                        {version.review.feedback}
                      </p>
                    )}
                  </div>
                )}
              </div>
            );
          })
        )}
      </div>
    </Dialog>
  );
}
