import React from 'react';
import { Download, FileText, History, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import type { FinalSubmissionPackageDto } from '../../types/archival.types';
import {
  formatArchivalFileSize,
  formatReviewActionLabel,
  getArchivalStatusDisplay,
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
          <span className="text-base font-bold text-slate-900">{title}</span>
        </div>
      }
      closeLabel="Đóng lịch sử hồ sơ lưu chiểu"
      className="w-full max-w-[100vw] sm:max-w-[640px] bg-white"
      headerClassName="border-b border-slate-200/80 bg-white/95 px-5 py-4 sm:px-6"
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
          <div className="rounded-2xl border border-slate-200/80 bg-slate-50/70 p-6 text-center text-sm text-slate-500">
            Chưa có phiên bản hồ sơ lưu chiểu nào được ghi nhận.
          </div>
        ) : (
          <div className="space-y-4">
            {packages.map((pkg, index) => (
              <section
                key={pkg.packageId}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 sm:p-5 shadow-2xs space-y-4"
              >
                <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 pb-3">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-bold text-slate-900 tabular-nums">
                      Phiên bản v{pkg.versionNo}
                    </span>
                    {index === 0 && (
                      <Badge variant="info" className="text-[11px]">
                        Mới nhất
                      </Badge>
                    )}
                  </div>
                  <span className="text-xs text-slate-500 tabular-nums">
                    Nộp lúc {formatDateTimeVi(pkg.submittedAt)}
                  </span>
                </div>

                <div className="space-y-2">
                  <p className="text-xs font-semibold text-slate-700">Tệp đính kèm</p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                    {pkg.files.map((file) => {
                      const dlKey = `${pkg.versionNo}-${file.fileRole}`;
                      const isDownloading = downloadingKey === dlKey;
                      return (
                        <div
                          key={file.fileId}
                          className="flex items-center justify-between gap-2 rounded-xl border border-slate-200/80 bg-slate-50/70 px-3 py-2"
                        >
                          <div className="min-w-0 flex items-center gap-2">
                            <FileText className="size-4 shrink-0 text-slate-500" />
                            <div className="min-w-0">
                              <p className="text-xs font-medium text-slate-800 truncate">
                                {file.fileName}
                              </p>
                              <p className="text-[11px] text-slate-500 tabular-nums">
                                {file.fileRole === 'ThesisPdf' ? 'PDF' : 'DOCX'} -{' '}
                                {formatArchivalFileSize(file.fileSize)}
                              </p>
                            </div>
                          </div>
                          <Button
                            type="button"
                            variant="outline"
                            size="icon"
                            className="size-8 shrink-0 rounded-lg"
                            aria-label={`Tải ${file.fileName}`}
                            disabled={isDownloading}
                            onClick={() =>
                              onDownloadFile(pkg.versionNo, file.fileRole, file.fileName)
                            }
                          >
                            <Download
                              className={`size-3.5 ${isDownloading ? 'animate-pulse' : ''}`}
                            />
                          </Button>
                        </div>
                      );
                    })}
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                  <div className="space-y-1">
                    <p className="font-medium text-slate-500">Tóm tắt tiếng Việt</p>
                    <p className="text-slate-700 line-clamp-3 leading-relaxed">
                      {pkg.abstractVi}
                    </p>
                  </div>
                  <div className="space-y-1">
                    <p className="font-medium text-slate-500">Abstract tiếng Anh</p>
                    <p className="text-slate-700 line-clamp-3 leading-relaxed">
                      {pkg.abstractEn}
                    </p>
                  </div>
                  <div className="space-y-1">
                    <p className="font-medium text-slate-500">Từ khóa tiếng Việt</p>
                    <p className="text-slate-700">{pkg.keywordsVi}</p>
                  </div>
                  <div className="space-y-1">
                    <p className="font-medium text-slate-500">Keywords tiếng Anh</p>
                    <p className="text-slate-700">{pkg.keywordsEn}</p>
                  </div>
                </div>

                {pkg.reviews.length > 0 && (
                  <div className="space-y-2 border-t border-slate-100 pt-3">
                    <p className="text-xs font-semibold text-slate-700">Lịch sử xét duyệt</p>
                    <div className="space-y-2">
                      {pkg.reviews.map((review) => {
                        const resultDisplay = getArchivalStatusDisplay(review.resultStatus);
                        return (
                          <div
                            key={review.reviewId}
                            className="rounded-xl border border-slate-200/80 bg-slate-50/70 px-3 py-2 text-xs space-y-1"
                          >
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <span className="font-semibold text-slate-800">
                                {formatReviewActionLabel(review.action, review.stage)}
                              </span>
                              <div className="flex items-center gap-2">
                                <StatusBadge tone={resultDisplay.tone}>
                                  {resultDisplay.label}
                                </StatusBadge>
                                <span className="text-slate-500 tabular-nums">
                                  {formatDateTimeVi(review.decidedAtUtc)}
                                </span>
                              </div>
                            </div>
                            {review.reason && (
                              <p className="text-slate-600 leading-relaxed">
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
