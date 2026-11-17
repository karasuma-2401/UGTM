import React, { useRef, useState } from 'react';
import { Download, Eye, RefreshCw, UploadCloud, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';
import type { FinalSubmissionFileDto } from '../../types/archival.types';
import { formatArchivalFileSize } from '../../utils/archival';
import { ThesisFileTypeIcon } from '../shared/ThesisFileTypeIcon';

export interface ArchivalFileDropSlotProps {
  id: string;
  extension: '.pdf' | '.docx';
  title: string;
  subtitle: string;
  selectedFile: File | null;
  existingFile: FinalSubmissionFileDto | null;
  disabled?: boolean;
  onSelectFile: (file: File) => void;
  onRemoveFile: () => void;
  onPreviewLocalPdf?: () => void;
  onPreviewExistingPdf?: (file: FinalSubmissionFileDto) => void;
  onDownloadExistingFile?: (file: FinalSubmissionFileDto) => void;
  isPreviewingExisting?: boolean;
  isDownloadingExisting?: boolean;
}

/**
 * Interactive drag-and-drop file slot for thesis archival PDF / DOCX uploads,
 * synchronized with system ThesisFileTypeIcon and PdfPreviewDialog affordances.
 */
export const ArchivalFileDropSlot: React.FC<ArchivalFileDropSlotProps> = ({
  id,
  extension,
  title,
  subtitle,
  selectedFile,
  existingFile,
  disabled = false,
  onSelectFile,
  onRemoveFile,
  onPreviewLocalPdf,
  onPreviewExistingPdf,
  onDownloadExistingFile,
  isPreviewingExisting = false,
  isDownloadingExisting = false,
}) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);

  const isPdf = extension === '.pdf';
  const fallbackFileName = isPdf ? 'bao-cao-hoan-thien.pdf' : 'ban-chinh-sua.docx';
  const activeFileName = selectedFile?.name ?? existingFile?.fileName ?? fallbackFileName;
  const acceptAttr = isPdf
    ? '.pdf,application/pdf'
    : '.docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document';

  const handleFileInput = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) onSelectFile(file);
    event.target.value = '';
  };

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    if (disabled) return;
    const file = event.dataTransfer.files?.[0];
    if (file) onSelectFile(file);
  };

  const handleDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    if (!disabled && !isDragging) setIsDragging(true);
  };

  const handleDragLeave = () => {
    if (isDragging) setIsDragging(false);
  };

  const triggerPicker = () => {
    if (!disabled) inputRef.current?.click();
  };

  return (
    <div
      onDrop={handleDrop}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      className={cn(
        'rounded-xl border transition-colors p-4 flex flex-col justify-between gap-3.5',
        isDragging
          ? 'border-primary bg-primary/[0.04]'
          : selectedFile
            ? 'border-tone-success-border bg-tone-success-bg/25'
            : 'border-dashed border-line-input bg-surface-subtle/40 hover:border-line-strong/70 hover:bg-surface-subtle/70',
        disabled && 'opacity-70 cursor-not-allowed hover:bg-surface-subtle/40',
      )}
    >
      <input
        id={id}
        ref={inputRef}
        type="file"
        accept={acceptAttr}
        className="sr-only"
        disabled={disabled}
        onChange={handleFileInput}
      />

      <div className="flex items-start gap-3 min-w-0">
        <ThesisFileTypeIcon fileName={activeFileName} />

        <div className="min-w-0 flex-1 space-y-0.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <label
              htmlFor={id}
              className="text-sm font-semibold text-ink-primary cursor-pointer"
            >
              {title}
            </label>
            {selectedFile ? (
              <span className="text-[11px] font-medium text-tone-success-strong tabular-nums">
                Đã chọn tệp mới
              </span>
            ) : existingFile ? (
              <span className="text-[11px] font-medium text-ink-muted tabular-nums">
                Đang lưu trên hệ thống
              </span>
            ) : (
              <span className="text-[11px] font-medium text-ink-subtle tabular-nums">
                Bắt buộc
              </span>
            )}
          </div>

          {selectedFile ? (
            <>
              <p
                className="text-xs font-medium text-ink-heading truncate"
                title={selectedFile.name}
              >
                {selectedFile.name}
              </p>
              <p className="text-[11px] text-ink-muted tabular-nums">
                {formatArchivalFileSize(selectedFile.size)} · Sẵn sàng tải lên cùng hồ sơ
              </p>
            </>
          ) : existingFile ? (
            <>
              <p
                className="text-xs font-medium text-ink-heading truncate"
                title={existingFile.fileName}
              >
                {existingFile.fileName}
              </p>
              <p className="text-[11px] text-ink-muted tabular-nums">
                {formatArchivalFileSize(existingFile.fileSize)} · Nộp lúc{' '}
                {formatDateTimeVi(existingFile.uploadedAt)}
              </p>
            </>
          ) : (
            <p className="text-xs text-ink-muted leading-relaxed">
              {subtitle} · Kéo thả tệp vào đây hoặc bấm chọn từ máy
            </p>
          )}
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-1.5 pt-2.5 border-t border-line/60">
        <div className="flex flex-wrap items-center gap-1.5">
          {isPdf && selectedFile && onPreviewLocalPdf && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 rounded-lg bg-surface-card px-2.5 text-xs"
              leftIcon={<Eye className="size-3.5 text-ink-body" />}
              onClick={onPreviewLocalPdf}
            >
              Xem trước
            </Button>
          )}

          {!selectedFile && existingFile && isPdf && onPreviewExistingPdf && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 rounded-lg bg-surface-card px-2.5 text-xs"
              isLoading={isPreviewingExisting}
              disabled={isPreviewingExisting || isDownloadingExisting}
              leftIcon={!isPreviewingExisting ? <Eye className="size-3.5 text-ink-body" /> : undefined}
              onClick={() => onPreviewExistingPdf(existingFile)}
            >
              Xem bản lưu
            </Button>
          )}

          {!selectedFile && existingFile && onDownloadExistingFile && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 rounded-lg bg-surface-card px-2.5 text-xs"
              isLoading={isDownloadingExisting}
              disabled={isPreviewingExisting || isDownloadingExisting}
              leftIcon={!isDownloadingExisting ? <Download className="size-3.5" /> : undefined}
              onClick={() => onDownloadExistingFile(existingFile)}
            >
              Tải
            </Button>
          )}
        </div>

        <div className="flex items-center gap-1.5 ml-auto">
          {selectedFile && !disabled && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              aria-label="Bỏ chọn tệp"
              title="Bỏ chọn tệp"
              className="size-8 shrink-0 rounded-lg p-0 text-ink-muted hover:text-ink-primary"
              onClick={onRemoveFile}
            >
              <X className="size-3.5" />
            </Button>
          )}

          <Button
            type="button"
            variant={selectedFile ? 'outline' : 'secondary'}
            size="sm"
            disabled={disabled}
            className="h-8 rounded-lg px-2.5 text-xs whitespace-nowrap"
            leftIcon={
              selectedFile || existingFile ? (
                <RefreshCw className="size-3.5" />
              ) : (
                <UploadCloud className="size-3.5" />
              )
            }
            onClick={triggerPicker}
          >
            {selectedFile
              ? 'Đổi tệp'
              : existingFile
                ? `Chọn ${extension} mới`
                : `Chọn tệp ${extension.toUpperCase().slice(1)}`}
          </Button>
        </div>
      </div>
    </div>
  );
};
