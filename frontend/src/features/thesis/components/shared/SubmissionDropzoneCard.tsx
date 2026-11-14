import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Eye,
  FileText,
  UploadCloud,
  RefreshCw,
  X,
  Lock,
  Send,
} from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { formatFileSize } from '../../utils/milestoneFormatters';
import { validateReportFile } from '../../services/submissionUploadHelper';
import { PdfPreviewDialog } from './PdfPreviewDialog';

function getFileExtensionKey(fileName: string): 'pdf' | 'docx' | 'doc' | 'file' {
  const ext = fileName.trim().toLowerCase().split('.').pop();
  if (ext === 'pdf') return 'pdf';
  if (ext === 'docx') return 'docx';
  if (ext === 'doc') return 'doc';
  return 'file';
}

function SelectedFileTypeIcon({ fileName }: { fileName: string }) {
  const fileType = getFileExtensionKey(fileName);

  if (fileType === 'pdf') {
    return (
      <div
        data-file-type="pdf"
        className="size-10 rounded-xl bg-white border border-rose-200/80 flex items-center justify-center shrink-0 shadow-2xs"
        aria-hidden="true"
      >
        <svg viewBox="0 0 28 28" className="size-6" fill="none">
          <path
            d="M7 3.5h9.2L21 8.3V23a1.5 1.5 0 0 1-1.5 1.5H7A1.5 1.5 0 0 1 5.5 23V5A1.5 1.5 0 0 1 7 3.5Z"
            className="fill-rose-50/70 stroke-rose-600/85"
            strokeWidth="1.5"
            strokeLinejoin="round"
          />
          <path
            d="M16 3.7V8.5h4.8"
            className="stroke-rose-600/75"
            strokeWidth="1.4"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <rect
            x="7"
            y="14"
            width="14"
            height="7"
            rx="2"
            className="fill-rose-600"
          />
          <text
            x="14"
            y="19.1"
            textAnchor="middle"
            className="fill-white"
            fontSize="5.2"
            fontWeight="700"
            letterSpacing="0.3"
            fontFamily="ui-sans-serif, system-ui, sans-serif"
          >
            PDF
          </text>
        </svg>
      </div>
    );
  }

  if (fileType === 'docx' || fileType === 'doc') {
    const label = fileType === 'docx' ? 'DOCX' : 'DOC';
    return (
      <div
        data-file-type={fileType}
        className="size-10 rounded-xl bg-white border border-blue-200/80 flex items-center justify-center shrink-0 shadow-2xs"
        aria-hidden="true"
      >
        <svg viewBox="0 0 28 28" className="size-6" fill="none">
          <path
            d="M7 3.5h9.2L21 8.3V23a1.5 1.5 0 0 1-1.5 1.5H7A1.5 1.5 0 0 1 5.5 23V5A1.5 1.5 0 0 1 7 3.5Z"
            className="fill-blue-50/80 stroke-[#2054a5]"
            strokeWidth="1.5"
            strokeLinejoin="round"
          />
          <path
            d="M16 3.7V8.5h4.8"
            className="stroke-[#2054a5]/80"
            strokeWidth="1.4"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <rect
            x="5.8"
            y="14"
            width="16.4"
            height="7"
            rx="2"
            className="fill-[#2054a5]"
          />
          <text
            x="14"
            y="19.1"
            textAnchor="middle"
            className="fill-white"
            fontSize={fileType === 'docx' ? '4.5' : '5.1'}
            fontWeight="700"
            letterSpacing="0.2"
            fontFamily="ui-sans-serif, system-ui, sans-serif"
          >
            {label}
          </text>
        </svg>
      </div>
    );
  }

  return (
    <div
      data-file-type="file"
      className="size-10 rounded-xl bg-white border border-slate-200/90 text-slate-600 flex items-center justify-center shrink-0 shadow-2xs"
      aria-hidden="true"
    >
      <FileText className="size-4.5" />
    </div>
  );
}

export interface SubmissionDropzoneCardProps {
  title: string;
  subtitle?: string;
  dropzoneHint?: string;
  headerBadge?: ReactNode;
  selectedFile: File | null;
  onFileSelect: (file: File | null) => void;
  existingFileName?: string | null;
  disabled?: boolean;
  lockedTitle?: string;
  lockedDescription?: string | null;
  lockedAction?: ReactNode;
  compactWhenLocked?: boolean;
  submitLabel: string;
  submitLoadingLabel?: string;
  canSubmit: boolean;
  isSubmitting?: boolean;
  uploadProgress?: number | null;
  onSubmit: () => void;
  extraFields?: ReactNode;
  secondaryActions?: ReactNode;
}

export function SubmissionDropzoneCard({
  title,
  subtitle = 'PDF, DOC, DOCX · tối đa 20 MB',
  dropzoneHint = 'Định dạng hỗ trợ: PDF, DOC, DOCX · tối đa 20 MB',
  headerBadge,
  selectedFile,
  onFileSelect,
  existingFileName,
  disabled = false,
  lockedTitle = 'Khu vực nộp hồ sơ đang khóa',
  lockedDescription,
  lockedAction,
  compactWhenLocked = true,
  submitLabel,
  submitLoadingLabel = 'Đang nộp…',
  canSubmit,
  isSubmitting = false,
  uploadProgress = null,
  onSubmit,
  extraFields,
  secondaryActions,
}: SubmissionDropzoneCardProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [pdfPreviewUrl, setPdfPreviewUrl] = useState<string | null>(null);

  const isPdfFile = useMemo(
    () => Boolean(selectedFile && selectedFile.name.toLowerCase().endsWith('.pdf')),
    [selectedFile],
  );

  useEffect(() => {
    if (!selectedFile || !isPdfFile) {
      setPdfPreviewUrl(null);
      setIsPreviewOpen(false);
      return;
    }
    const objectUrl = URL.createObjectURL(selectedFile);
    setPdfPreviewUrl(objectUrl);
    return () => {
      URL.revokeObjectURL(objectUrl);
    };
  }, [selectedFile, isPdfFile]);

  const acceptIncomingFile = (file: File | undefined) => {
    if (!file) return;
    const error = validateReportFile(file);
    if (error) {
      setFileError(error);
      return;
    }
    setFileError(null);
    onFileSelect(file);
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    acceptIncomingFile(event.target.files?.[0]);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    if (disabled) return;
    acceptIncomingFile(event.dataTransfer.files?.[0]);
  };

  const handleDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    if (!disabled && !isDragging) {
      setIsDragging(true);
    }
  };

  const handleDragLeave = () => {
    if (isDragging) setIsDragging(false);
  };

  if (disabled && compactWhenLocked) {
    return (
      <Card className="bg-slate-50/70 border-slate-200/80 shadow-xs rounded-2xl p-4 sm:p-5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="flex items-start gap-3 min-w-0">
            <div className="p-2 rounded-xl bg-white border border-slate-200/80 text-slate-500 shrink-0">
              <Lock className="size-4" />
            </div>
            <div className="min-w-0 space-y-0.5">
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="text-sm font-semibold text-slate-900">{lockedTitle}</h3>
                {headerBadge}
              </div>
              {lockedDescription && (
                <p className="text-xs text-slate-600 leading-relaxed">{lockedDescription}</p>
              )}
              {existingFileName && (
                <p className="text-xs text-slate-500 pt-0.5 truncate">
                  Tệp hiện hành: <span className="font-medium text-slate-700">{existingFileName}</span>
                </p>
              )}
            </div>
          </div>
          {(lockedAction || secondaryActions) && (
            <div className="flex flex-wrap items-center gap-2 shrink-0">
              {lockedAction}
              {secondaryActions}
            </div>
          )}
        </div>
      </Card>
    );
  }

  return (
    <Card className="bg-white border-slate-200/80 shadow-xs rounded-2xl overflow-hidden">
      <div className="px-5 sm:px-6 py-4 border-b border-slate-100 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{title}</h3>
          <p className="text-xs text-slate-500 mt-0.5">{subtitle}</p>
        </div>
        {headerBadge}
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept=".pdf,.doc,.docx"
        hidden
        className="hidden"
        onChange={handleFileChange}
        disabled={disabled}
      />

      <div className="p-5 sm:p-6 space-y-5">
        {fileError && (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-xl border border-rose-200/90 bg-rose-50/80 px-3.5 py-2.5 text-xs text-rose-700"
          >
            <AlertCircle className="size-4 shrink-0 mt-0.5" aria-hidden="true" />
            <span>{fileError}</span>
          </div>
        )}

        {selectedFile ? (
          <div
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            className={cn(
              'rounded-xl border px-4 py-3.5 transition-colors space-y-3',
              isDragging
                ? 'border-[#2054a5] bg-blue-50/40'
                : 'border-slate-200/90 bg-slate-50/55 hover:border-slate-300/90',
            )}
          >
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3 min-w-0">
                <SelectedFileTypeIcon fileName={selectedFile.name} />
                <div className="min-w-0 flex-1">
                  <p
                    className="text-sm font-semibold text-slate-900 truncate"
                    title={selectedFile.name}
                  >
                    {selectedFile.name}
                  </p>
                  <div className="flex flex-wrap items-center gap-2 mt-0.5 text-xs text-slate-500">
                    <span className="inline-flex items-center rounded-md bg-slate-200/70 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-slate-700">
                      {selectedFile.name.split('.').pop()?.toUpperCase() || 'FILE'}
                    </span>
                    <span className="tabular-nums font-medium text-slate-600">
                      {formatFileSize(selectedFile.size)}
                    </span>
                    <span className="text-slate-300">·</span>
                    <span className="inline-flex items-center gap-1 text-slate-600 font-medium">
                      <CheckCircle2 className="size-3.5 text-emerald-600 shrink-0" />
                      <span>Sẵn sàng gửi</span>
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-1.5 shrink-0 self-end sm:self-center">
                {isPdfFile && pdfPreviewUrl && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => setIsPreviewOpen(true)}
                    disabled={disabled || isSubmitting}
                    className="h-8 bg-white text-xs px-2.5"
                  >
                    <Eye className="size-3.5 mr-1.5 text-slate-500" />
                    Xem trước
                  </Button>
                )}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={disabled || isSubmitting}
                  className="h-8 bg-white text-xs px-2.5"
                >
                  <RefreshCw className="size-3.5 mr-1.5 text-slate-500" />
                  Đổi tệp
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Gỡ tệp"
                  title="Gỡ tệp đã chọn"
                  className="size-8 text-slate-400 hover:text-rose-600 hover:bg-rose-50"
                  onClick={() => {
                    setFileError(null);
                    onFileSelect(null);
                    if (fileInputRef.current) fileInputRef.current.value = '';
                  }}
                  disabled={disabled || isSubmitting}
                >
                  <X className="size-4" />
                </Button>
              </div>
            </div>

            {isSubmitting && uploadProgress != null && (
              <div className="space-y-1.5 pt-1 border-t border-slate-200/70">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-medium text-[#2054a5]">Đang tải hồ sơ lên hệ thống…</span>
                  <span className="tabular-nums font-semibold text-slate-700">
                    {uploadProgress}%
                  </span>
                </div>
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-200/80">
                  <div
                    className="h-full rounded-full bg-[#2054a5] transition-all duration-200"
                    style={{ width: `${Math.min(100, Math.max(4, uploadProgress))}%` }}
                  />
                </div>
              </div>
            )}
          </div>
        ) : (
          <div
            role="button"
            tabIndex={disabled ? -1 : 0}
            aria-disabled={disabled}
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onClick={() => !disabled && fileInputRef.current?.click()}
            onKeyDown={(event) => {
              if (!disabled && (event.key === 'Enter' || event.key === ' ')) {
                event.preventDefault();
                fileInputRef.current?.click();
              }
            }}
            className={cn(
              'group rounded-xl border-2 border-dashed p-6 text-center transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              disabled
                ? 'border-slate-200 bg-slate-50/70 opacity-75 cursor-not-allowed'
                : isDragging
                  ? 'border-[#2054a5] bg-blue-50/60 scale-[1.005] cursor-copy'
                  : 'border-slate-200/90 bg-slate-50/35 hover:border-[#2054a5]/60 hover:bg-blue-50/25 cursor-pointer',
            )}
          >
            <div className="flex flex-col items-center justify-center gap-2">
              <div
                className={cn(
                  'size-10 rounded-full flex items-center justify-center transition-transform duration-200',
                  disabled
                    ? 'bg-slate-100 text-slate-400'
                    : isDragging
                      ? 'bg-blue-100 text-[#2054a5] scale-110'
                      : 'bg-white border border-slate-200/80 text-slate-600 shadow-2xs group-hover:scale-105 group-hover:text-[#2054a5]',
                )}
              >
                {disabled ? <Lock className="size-4" /> : <UploadCloud className="size-5" />}
              </div>
              <div className="space-y-0.5">
                <p className="text-sm font-medium text-slate-800">
                  {disabled ? (
                    lockedTitle
                  ) : (
                    <>
                      Kéo thả tệp vào đây, hoặc{' '}
                      <span className="text-[#2054a5] font-semibold">chọn từ máy tính</span>
                    </>
                  )}
                </p>
                <p className="text-xs text-slate-500">
                  {disabled && lockedDescription
                    ? lockedDescription
                    : existingFileName
                      ? `Tệp hiện hành: ${existingFileName} · Chọn tệp mới để cập nhật phiên bản`
                      : dropzoneHint}
                </p>
              </div>
            </div>
          </div>
        )}

        {extraFields && (
          <div className="pt-5 border-t border-slate-200/75">{extraFields}</div>
        )}
      </div>

      <div className="px-5 sm:px-6 py-3.5 border-t border-slate-100 bg-slate-50/60 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {secondaryActions}
          {!secondaryActions && (
            <span className="text-xs text-slate-500">
              Hệ thống tự động lưu lịch sử từng phiên bản sau khi nộp.
            </span>
          )}
        </div>
        <Button
          type="button"
          onClick={onSubmit}
          disabled={!canSubmit || !selectedFile || isSubmitting || disabled}
          isLoading={isSubmitting}
          loadingText={submitLoadingLabel}
          leftIcon={<Send className="size-4" />}
        >
          {submitLabel}
        </Button>
      </div>

      {isPdfFile && pdfPreviewUrl && (
        <PdfPreviewDialog
          open={isPreviewOpen}
          onClose={() => setIsPreviewOpen(false)}
          previewUrl={pdfPreviewUrl}
          fileName={selectedFile?.name}
          fileSize={selectedFile?.size}
        />
      )}
    </Card>
  );
}
