import React from 'react';
import { Download, Eye, History, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import type { FinalSubmissionPackageDto } from '../../types/archival.types';
import { ThesisFileTypeIcon } from '../shared/ThesisFileTypeIcon';
import {
  formatArchivalFileSize,
  formatReviewActionLabel,
  getArchivalStatusDisplay,
  parseArchivalKeywords,
} from '../../utils/archival';

interface ArchivalHistoryDialogProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  packages: readonly FinalSubmissionPackageDto[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
  onDownloadFile: (packageVersion: number, fileRole: string, fileName: string) => void;
  downloadingKey: string | null;
  onPreviewPdf?: (packageVersion: number, fileName: string, fileSize?: number) => void;
  previewingKey?: string | null;
}

export const ArchivalHistoryDialog: React.FC<ArchivalHistoryDialogProps> = ({
  open,
  onClose,
  title = 'Lịch sử hồ sơ lưu chiểu',
  packages,
  isLoading,
  error,
  onRetry,
  onDownloadFile,
  downloadingKey,
  onPreviewPdf,
  previewingKey = null,
}) => {
  if (!open) {
    return null;
  }

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
      closeLabel="Đóng lịch sử hồ sơ lưu chiểu"
      className="w-full max-w-[100vw] sm:max-w-[640px] bg-surface-card"
      headerClassName="border-b border-line/80 bg-surface-card/95 px-5 py-4 sm:px-6"
    >
      <div className="p-5 sm:p-6 space-y-4">
        {isLoading ? (
          <div className="space-y-4">
            <Skeleton className="h-36 w-full rounded-2xl" />
            <Skeleton className="h-36 w-full rounded-2xl" />
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
        ) : packages.length === 0 ? (
          <div className="rounded-2xl border border-line/80 bg-surface-subtle/70 p-6 text-center text-sm text-ink-muted">
            Chưa có phiên bản hồ sơ lưu chiểu nào được ghi nhận.
          </div>
        ) : (
          <div className="space-y-4">
            {packages.map((pkg, index) => (
              <section
                key={pkg.packageId}
                className="rounded-2xl border border-line/80 bg-surface-card p-4 sm:p-5 shadow-2xs space-y-4"
              >
                <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line-subtle pb-3">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-bold text-ink-primary tabular-nums">
                      Phiên bản v{pkg.versionNo}
                    </span>
                    {index === 0 && (
                      <Badge variant="info" className="text-[11px]">
                        Mới nhất
                      </Badge>
                    )}
                  </div>
                  <span className="text-xs text-ink-muted tabular-nums">
                    Nộp lúc {formatDateTimeVi(pkg.submittedAt)}
                  </span>
                </div>

                <div className="space-y-2">
                  <p className="text-xs font-semibold text-ink-secondary">Tệp đính kèm</p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                    {pkg.files.map((file) => {
                      const dlKey = `${pkg.versionNo}-${file.fileRole}`;
                      const isDownloading = downloadingKey === dlKey;
                      const isPreviewing = file.fileRole === 'ThesisPdf' && previewingKey === dlKey;
                      const isBusyAction = downloadingKey !== null || previewingKey !== null;
                      return (
                        <div
                          key={file.fileId}
                          className="flex items-center justify-between gap-2.5 rounded-xl border border-line/80 bg-surface-subtle/70 px-3 py-2.5"
                        >
                          <div className="min-w-0 flex items-center gap-2.5">
                            <ThesisFileTypeIcon fileName={file.fileName} />
                            <div className="min-w-0">
                              <p
                                className="text-xs font-medium text-ink-heading truncate"
                                title={file.fileName}
                              >
                                {file.fileName}
                              </p>
                              <p className="text-[11px] text-ink-muted tabular-nums">
                                {file.fileRole === 'ThesisPdf' ? 'PDF' : 'DOCX'} -{' '}
                                {formatArchivalFileSize(file.fileSize)}
                              </p>
                            </div>
                          </div>
                          <div className="flex items-center gap-1 shrink-0">
                            {file.fileRole === 'ThesisPdf' && onPreviewPdf && (
                              <Button
                                type="button"
                                variant="outline"
                                size="icon"
                                className="size-8 shrink-0 rounded-lg bg-surface-card"
                                aria-label={`Xem trước ${file.fileName}`}
                                title="Xem trước PDF"
                                disabled={isBusyAction}
                                onClick={() =>
                                  onPreviewPdf(pkg.versionNo, file.fileName, file.fileSize)
                                }
                              >
                                <Eye className={`size-3.5 ${isPreviewing ? 'animate-pulse' : ''}`} />
                              </Button>
                            )}
                            <Button
                              type="button"
                              variant="outline"
                              size="icon"
                              className="size-8 shrink-0 rounded-lg bg-surface-card"
                              aria-label={`Tải ${file.fileName}`}
                              title={`Tải ${file.fileName}`}
                              disabled={isBusyAction}
                              onClick={() =>
                                onDownloadFile(pkg.versionNo, file.fileRole, file.fileName)
                              }
                            >
                              <Download
                                className={`size-3.5 ${isDownloading ? 'animate-pulse' : ''}`}
                              />
                            </Button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                  <div className="rounded-xl border border-line/70 bg-surface-subtle/50 p-3 space-y-1">
                    <p className="font-semibold text-ink-body">Tóm tắt tiếng Việt</p>
                    <p className="text-ink-secondary line-clamp-4 leading-relaxed">
                      {pkg.abstractVi}
                    </p>
                  </div>
                  <div className="rounded-xl border border-line/70 bg-surface-subtle/50 p-3 space-y-1">
                    <p className="font-semibold text-ink-body">Abstract tiếng Anh</p>
                    <p className="text-ink-secondary line-clamp-4 leading-relaxed">
                      {pkg.abstractEn}
                    </p>
                  </div>
                  <div className="space-y-1.5">
                    <p className="font-semibold text-ink-body">Từ khóa tiếng Việt</p>
                    <div className="flex flex-wrap gap-1.5">
                      {parseArchivalKeywords(pkg.keywordsVi).map((kw) => (
                        <span
                          key={kw}
                          className="inline-flex items-center rounded-md border border-line-input/80 bg-surface-muted/80 px-2 py-0.5 text-[11px] font-medium text-ink-secondary"
                        >
                          {kw}
                        </span>
                      ))}
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <p className="font-semibold text-ink-body">Keywords tiếng Anh</p>
                    <div className="flex flex-wrap gap-1.5">
                      {parseArchivalKeywords(pkg.keywordsEn).map((kw) => (
                        <span
                          key={kw}
                          className="inline-flex items-center rounded-md border border-tone-info-border bg-tone-info-bg px-2 py-0.5 text-[11px] font-medium text-tone-info-strong"
                        >
                          {kw}
                        </span>
                      ))}
                    </div>
                  </div>
                </div>

                {pkg.reviews.length > 0 && (
                  <div className="space-y-2 border-t border-line-subtle pt-3">
                    <p className="text-xs font-semibold text-ink-secondary">Lịch sử xét duyệt</p>
                    <div className="space-y-2">
                      {pkg.reviews.map((review) => {
                        const resultDisplay = getArchivalStatusDisplay(review.resultStatus);
                        return (
                          <div
                            key={review.reviewId}
                            className="rounded-xl border border-line/80 bg-surface-subtle/70 px-3 py-2 text-xs space-y-1"
                          >
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <span className="font-semibold text-ink-heading">
                                {formatReviewActionLabel(review.action, review.stage)}
                              </span>
                              <div className="flex items-center gap-2">
                                <StatusBadge tone={resultDisplay.tone}>
                                  {resultDisplay.label}
                                </StatusBadge>
                                <span className="text-ink-muted tabular-nums">
                                  {formatDateTimeVi(review.decidedAtUtc)}
                                </span>
                              </div>
                            </div>
                            {review.reason && (
                              <p className="text-ink-body leading-relaxed">
                                Lý do: {review.reason}
                              </p>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
              </section>
            ))}
          </div>
        )}
      </div>
    </Dialog>
  );
};
