import axios from 'axios';
import { useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Download, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import type { ApiResponse } from '@/shared/api/types';
import { getUserErrorMessage, userApi } from '../../services/userApi';

interface StudentImportDialogProps {
  onClose: () => void;
  onImported: (count: number) => Promise<void>;
}

export function StudentImportDialog({ onClose, onImported }: StudentImportDialogProps) {
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState('');
  const [downloadError, setDownloadError] = useState('');
  const [downloading, setDownloading] = useState(false);
  const mutation = useMutation({
    mutationFn: (value: File) => userApi.importStudents(value),
    onSuccess: (result) => onImported(result.importedCount),
  });
  const fieldErrors = axios.isAxiosError<ApiResponse<unknown>>(mutation.error)
    ? mutation.error.response?.data?.error?.errors
    : null;

  async function downloadTemplate() {
    setDownloading(true);
    setDownloadError('');
    try {
      const blob = await userApi.downloadStudentTemplate();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'ugtm-student-import-template.xlsx';
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
    if (
      !file.name.toLowerCase().endsWith('.xlsx') ||
      file.size === 0 ||
      file.size > 5 * 1024 * 1024
    ) {
      setFileError('Chọn file Excel .xlsx không rỗng, tối đa 5 MB.');
      return;
    }
    setFileError('');
    mutation.mutate(file);
  }

  return (
    <Dialog title="Nhập danh sách sinh viên" busy={mutation.isPending} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-5 p-5 sm:p-6">
        <p className="text-sm text-slate-600">
          Tải file mẫu, điền danh sách rồi chọn file để nhập. Tối đa 5.000 sinh viên mỗi lần, dung
          lượng 5 MB. MSSV là bắt buộc; email và MSSV không được trùng.
        </p>
        <Button
          type="button"
          variant="outline"
          leftIcon={<Download />}
          isLoading={downloading}
          disabled={mutation.isPending}
          onClick={() => void downloadTemplate()}
        >
          Tải file Excel mẫu
        </Button>
        {downloadError && (
          <p role="alert" className="text-sm text-rose-700">
            {downloadError}
          </p>
        )}
        <label className="block space-y-2 text-sm">
          <span>File danh sách sinh viên (.xlsx)</span>
          <input
            type="file"
            accept=".xlsx"
            required
            disabled={mutation.isPending}
            className="block w-full rounded-lg border border-slate-200 p-3"
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null);
              setFileError('');
              mutation.reset();
            }}
          />
        </label>
        <p className="rounded-lg bg-blue-50 p-3 text-sm text-blue-800">
          Tất cả tài khoản được gán vai trò Sinh viên và chờ kích hoạt. Hệ thống không gửi email
          hàng loạt khi nhập. Sinh viên chủ động yêu cầu link tại trang Kích hoạt tài khoản.
        </p>
        {(fileError || mutation.error) && (
          <div role="alert" className="space-y-2 text-sm text-rose-700">
            <p>{fileError || getUserErrorMessage(mutation.error)}</p>
            {fieldErrors && (
              <ul className="max-h-52 list-disc overflow-y-auto pl-5">
                {Object.entries(fieldErrors).map(([field, messages]) => (
                  <li key={field}>
                    <strong>{field}:</strong> {messages.join(' ')}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
        <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
          <Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>
            Đóng
          </Button>
          <Button
            type="submit"
            leftIcon={<Upload />}
            disabled={!file}
            isLoading={mutation.isPending}
            loadingText="Đang nhập danh sách…"
          >
            Nhập sinh viên
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
