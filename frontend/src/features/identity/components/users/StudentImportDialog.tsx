import axios from 'axios';
import { useId, useRef, useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Check, Download, FileSpreadsheet, Info, Upload, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import type { ApiResponse } from '@/shared/api/types';
import { getUserErrorMessage, userApi } from '../../services/userApi';

export type ImportUserRole = 'Student' | 'Lecturer';

const IMPORT_ROLE_CONFIG: Record<
  ImportUserRole,
  {
    title: string;
    templateHint: string;
    templateFilename: string;
    fileInputLabel: string;
    limitsText: string;
    roleDisplay: string;
    subjectLower: string;
    submitLabel: string;
  }
> = {
  Student: {
    title: 'Nhập danh sách sinh viên',
    templateHint: 'MSSV bắt buộc; email và MSSV không được trùng.',
    templateFilename: 'ugtm-student-import-template.xlsx',
    fileInputLabel: 'File danh sách sinh viên (.xlsx)',
    limitsText: 'Định dạng .xlsx · Tối đa 5 MB · 5.000 sinh viên / lần',
    roleDisplay: 'Sinh viên',
    subjectLower: 'sinh viên',
    submitLabel: 'Nhập sinh viên',
  },
  Lecturer: {
    title: 'Nhập danh sách giảng viên',
    templateHint: 'Mã giảng viên có thể để trống; email và mã (nếu có) không được trùng.',
    templateFilename: 'ugtm-lecturer-import-template.xlsx',
    fileInputLabel: 'File danh sách giảng viên (.xlsx)',
    limitsText: 'Định dạng .xlsx · Tối đa 5 MB · 5.000 giảng viên / lần',
    roleDisplay: 'Giảng viên',
    subjectLower: 'giảng viên',
    submitLabel: 'Nhập giảng viên',
  },
};

export interface StudentImportDialogProps {
  role?: ImportUserRole;
  onClose: () => void;
  onImported: (count: number) => Promise<void>;
}

export function StudentImportDialog({
  role = 'Student',
  onClose,
  onImported,
}: StudentImportDialogProps) {
  const copy = IMPORT_ROLE_CONFIG[role];
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState('');
  const [downloadError, setDownloadError] = useState('');
  const [downloading, setDownloading] = useState(false);
  const [dragging, setDragging] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const id = useId();
  const mutation = useMutation({
    mutationFn: (value: File) => userApi.importUsers(value, role),
    onSuccess: (result) => onImported(result.importedCount),
  });
  const fieldErrors = axios.isAxiosError<ApiResponse<unknown>>(mutation.error)
    ? mutation.error.response?.data?.error?.errors
    : null;

  function selectFile(next: File | null) {
    if (mutation.isPending || !next) return;
    mutation.reset();
    if (
      !next.name.toLowerCase().endsWith('.xlsx') ||
      next.size === 0 ||
      next.size > 5 * 1024 * 1024
    ) {
      setFile(null);
      setFileError('Chọn file Excel .xlsx không rỗng, tối đa 5 MB.');
    } else {
      setFile(next);
      setFileError('');
    }
    // Allow selecting the same file again after correcting an error.
    if (inputRef.current) inputRef.current.value = '';
  }

  async function downloadTemplate() {
    setDownloading(true);
    setDownloadError('');
    try {
      const blob = await userApi.downloadUserTemplate(role);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = copy.templateFilename;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch {
      setDownloadError('Không thể tải file mẫu. Vui lòng thử lại.');
    } finally {
      setDownloading(false);
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file || mutation.isPending) return;
    mutation.mutate(file);
  }

  return (
    <Dialog
      headerClassName="border-b border-line/80 bg-surface-card px-5 py-4 sm:px-6"
      title={
        <span className="flex items-center gap-3">
          <span
            aria-hidden="true"
            className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-tone-success-bg text-tone-success-text ring-1 ring-inset ring-tone-success-border/70"
          >
            <FileSpreadsheet className="size-5" />
          </span>
          <span className="min-w-0">
            <span className="block text-base font-semibold text-ink-primary">
              {copy.title}
            </span>
            <span className="mt-0.5 block text-xs font-normal text-ink-muted">
              Tạo nhiều tài khoản từ file Excel
            </span>
          </span>
        </span>
      }
      busy={mutation.isPending}
      onClose={onClose}
    >
      <form onSubmit={handleSubmit}>
        <div className="space-y-6 p-5 sm:p-6">
          <section aria-labelledby={`${id}-template`}>
            <div className="mb-3 flex items-center gap-2.5">
              <span
                aria-hidden="true"
                className="flex size-6 items-center justify-center rounded-full bg-surface-muted text-xs font-semibold text-ink-body"
              >
                1
              </span>
              <h3 id={`${id}-template`} className="text-sm font-semibold text-ink-primary">
                Chuẩn bị danh sách
              </h3>
            </div>
            <div className="flex flex-col gap-3 rounded-xl border border-line/80 bg-surface-subtle/70 p-4 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-sm leading-6 text-ink-body">
                Dùng file mẫu để điền đúng cấu trúc.
                <br />
                <span className="text-xs text-ink-muted">{copy.templateHint}</span>
              </p>
              <Button
                type="button"
                variant="successOutline"
                leftIcon={<Download />}
                isLoading={downloading}
                loadingText="Đang tải mẫu…"
                disabled={mutation.isPending}
                onClick={() => void downloadTemplate()}
                className="shrink-0 focus-visible:ring-emerald-600/30"
              >
                Tải file mẫu
              </Button>
            </div>
            {downloadError && (
              <p role="alert" className="mt-2 text-sm text-tone-danger-text">
                {downloadError}
              </p>
            )}
          </section>

          <section aria-labelledby={`${id}-upload`}>
            <div className="mb-3 flex min-h-7 items-center justify-between gap-3">
              <div className="flex items-center gap-2.5">
                <span
                  aria-hidden="true"
                  className="flex size-6 items-center justify-center rounded-full bg-surface-muted text-xs font-semibold text-ink-body"
                >
                  2
                </span>
                <h3 id={`${id}-upload`} className="text-sm font-semibold text-ink-primary">
                  Chọn file danh sách
                </h3>
              </div>
              {file && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={mutation.isPending}
                  aria-label="Xóa file đã chọn"
                  onClick={() => {
                    setFile(null);
                    setFileError('');
                    mutation.reset();
                  }}
                  className="h-7 px-2 text-ink-muted"
                  leftIcon={<X />}
                >
                  Xóa file
                </Button>
              )}
            </div>
            <label
              onDragOver={(event) => {
                event.preventDefault();
                if (!mutation.isPending) setDragging(true);
              }}
              onDragLeave={(event) => {
                if (!event.currentTarget.contains(event.relatedTarget as Node | null))
                  setDragging(false);
              }}
              onDrop={(event) => {
                event.preventDefault();
                setDragging(false);
                if (mutation.isPending) return;
                if (event.dataTransfer.files.length > 1) {
                  setFile(null);
                  setFileError('Chỉ chọn một file Excel cho mỗi lần nhập.');
                  mutation.reset();
                  return;
                }
                selectFile(event.dataTransfer.files[0] ?? null);
              }}
              className={cn(
                'group flex min-h-44 cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-tone-success-border bg-tone-success-bg px-4 py-5 text-center transition-colors duration-200 hover:border-emerald-500 hover:bg-tone-success-bg focus-within:border-emerald-600 focus-within:ring-2 focus-within:ring-emerald-600/15',
                dragging && 'border-emerald-600 bg-emerald-100/60 dark:bg-emerald-950/60',
                file && 'border-solid border-tone-success-border bg-tone-success-bg',
                fileError && 'border-tone-danger-border bg-tone-danger-bg hover:border-rose-400 hover:bg-tone-danger-bg',
                mutation.isPending && 'pointer-events-none cursor-wait opacity-60',
              )}
            >
              <input
                ref={inputRef}
                type="file"
                accept=".xlsx"
                disabled={mutation.isPending}
                aria-label={copy.fileInputLabel}
                aria-describedby={`${id}-limits${fileError ? ` ${id}-error` : ''}`}
                aria-invalid={Boolean(fileError)}
                className="sr-only"
                onChange={(event) => selectFile(event.target.files?.[0] ?? null)}
              />
              <span
                aria-hidden="true"
                className="mb-3 flex size-11 items-center justify-center rounded-xl bg-surface-elevated text-tone-success-text shadow-sm"
              >
                {file ? <FileSpreadsheet className="size-6" /> : <Upload className="size-5" />}
              </span>
              <span className="max-w-full break-all text-sm font-semibold text-ink-primary">
                {file
                  ? file.name
                  : dragging
                    ? 'Thả file Excel vào đây'
                    : 'Kéo thả file Excel vào đây'}
              </span>
              {file ? (
                <span className="mt-1.5 flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-xs text-tone-success-text">
                  <span className="inline-flex items-center gap-1">
                    <Check className="size-3.5" />
                    Đã chọn ·{' '}
                    {(file.size / 1024).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} KB
                  </span>
                  <span className="font-medium underline underline-offset-4">Bấm để đổi file</span>
                </span>
              ) : (
                <span className="mt-1.5 text-sm text-tone-success-text">
                  hoặc{' '}
                  <span className="font-semibold underline underline-offset-4">
                    bấm để chọn file
                  </span>
                </span>
              )}
              <span id={`${id}-limits`} className="mt-3 text-xs leading-5 text-ink-muted">
                {copy.limitsText}
              </span>
            </label>
            {(fileError || mutation.error) && (
              <div
                id={`${id}-error`}
                role="alert"
                className="mt-3 space-y-2 rounded-lg border border-tone-danger-border bg-tone-danger-bg p-3 text-sm text-tone-danger-text"
              >
                <p className="font-medium">{fileError || getUserErrorMessage(mutation.error)}</p>
                {fieldErrors && (
                  <ul className="max-h-40 list-disc space-y-1 overflow-y-auto break-words pl-5">
                    {Object.entries(fieldErrors).map(([field, messages]) => (
                      <li key={field}>
                        <strong>{field}:</strong> {messages.join(' ')}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
          </section>

          <div className="flex gap-2.5 text-xs leading-5 text-ink-body">
            <Info aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-ink-subtle" />
            <p>
              Tài khoản được gán vai trò{' '}
              <strong className="font-medium text-ink-heading">{copy.roleDisplay}</strong> và ở
              trạng thái{' '}
              <strong className="font-medium text-ink-heading">Chờ kích hoạt</strong>. Hệ thống không
              gửi email hàng loạt; {copy.subjectLower} chủ động yêu cầu link tại trang Kích hoạt tài
              khoản.
            </p>
          </div>
        </div>
        <div className="sticky bottom-0 z-10 flex justify-end gap-2.5 border-t border-line/80 bg-surface-card/95 px-5 py-3.5 backdrop-blur-md sm:px-6">
          <Button
            type="button"
            variant="outline"
            disabled={mutation.isPending}
            onClick={onClose}
            className="h-9 flex-1 rounded-xl px-4 text-xs font-semibold sm:flex-none"
          >
            Đóng
          </Button>
          <Button
            type="submit"
            variant="success"
            leftIcon={<Upload />}
            disabled={!file}
            isLoading={mutation.isPending}
            loadingText="Đang nhập danh sách…"
            className="h-9 flex-1 rounded-xl px-4 text-xs font-semibold sm:flex-none"
          >
            {copy.submitLabel}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
