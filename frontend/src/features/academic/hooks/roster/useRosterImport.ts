import { useState, useMemo, useCallback } from 'react';
import type { UserSummary } from '@/features/identity/types/user.types';
import { userApi, getUserErrorMessage } from '@/features/identity/services/userApi';
import {
  parseXlsxFile,
  validateRosterRows,
  type RawParsedRow,
  type ValidatedRosterRow,
} from '../../utils/rosterExcel';

export function useRosterImport(existingUsers: UserSummary[], onImportSuccess: () => Promise<void>) {
  const [activeRole, setActiveRole] = useState<'Student' | 'Lecturer'>('Student');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isParsing, setIsParsing] = useState<boolean>(false);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [isDownloading, setIsDownloading] = useState<boolean>(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSingleAddOpen, setIsSingleAddOpen] = useState<boolean>(false);

  const [rawRows, setRawRows] = useState<RawParsedRow[]>([]);

  const handleSelectFile = useCallback(
    async (file: File) => {
      setErrorMessage(null);
      setNotice(null);

      if (!file.name.toLowerCase().endsWith('.xlsx')) {
        setErrorMessage('Vui lòng chọn tệp bảng tính Excel có định dạng .xlsx');
        return;
      }

      if (file.size > 20 * 1024 * 1024) {
        setErrorMessage('Tệp quá lớn. Dung lượng tối đa là 20 MB.');
        return;
      }

      setSelectedFile(file);
      setIsParsing(true);

      try {
        const parsed = await parseXlsxFile(file);
        if (parsed.length === 0) {
          setErrorMessage('Tệp không có dòng dữ liệu hợp lệ nào.');
          setRawRows([]);
        } else {
          setRawRows(parsed);
          setNotice(`Đã đọc ${parsed.length} dòng từ tệp "${file.name}".`);
        }
      } catch {
        setErrorMessage('Không thể đọc tệp Excel. Vui lòng kiểm tra lại cấu trúc file.');
        setRawRows([]);
      } finally {
        setIsParsing(false);
      }
    },
    [],
  );

  const validatedRows: ValidatedRosterRow[] = useMemo(() => {
    if (rawRows.length === 0) return [];
    return validateRosterRows(rawRows, activeRole, existingUsers);
  }, [rawRows, activeRole, existingUsers]);

  const summary = useMemo(() => {
    const total = validatedRows.length;
    const valid = validatedRows.filter((r) => r.status === 'Valid').length;
    const needsFix = validatedRows.filter((r) => r.status === 'NeedsFix').length;
    const duplicate = validatedRows.filter((r) => r.status === 'DuplicateCode').length;
    const canSubmit = total > 0 && needsFix === 0 && duplicate === 0;

    return {
      total,
      valid,
      needsFix,
      duplicate,
      canSubmit,
    };
  }, [validatedRows]);

  const handleSwitchRole = useCallback((role: 'Student' | 'Lecturer') => {
    setActiveRole(role);
    setSelectedFile(null);
    setRawRows([]);
    setErrorMessage(null);
    setNotice(null);
  }, []);

  const handleResetFile = useCallback(() => {
    setSelectedFile(null);
    setRawRows([]);
    setErrorMessage(null);
    setNotice(null);
  }, []);

  const handleDownloadTemplate = useCallback(async () => {
    setIsDownloading(true);
    setErrorMessage(null);
    try {
      const blob = await userApi.downloadUserTemplate(activeRole);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ugtm-${activeRole.toLowerCase()}-import-template.xlsx`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      setNotice(`Đã tải về tệp Excel mẫu cho ${activeRole === 'Student' ? 'Sinh viên' : 'Giảng viên'}.`);
    } catch {
      setErrorMessage('Không thể tải file mẫu. Vui lòng thử lại.');
    } finally {
      setIsDownloading(false);
    }
  }, [activeRole]);

  const handleSubmitImport = useCallback(async () => {
    if (!selectedFile) return;
    if (summary.needsFix > 0 || summary.duplicate > 0) {
      setErrorMessage('Vui lòng sửa các dòng còn lỗi trước khi xác nhận nhập.');
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);
    setNotice(null);

    try {
      const result = await userApi.importUsers(selectedFile, activeRole);
      setNotice(
        `Nhập danh sách thành công! Đã tạo ${result.importedCount.toLocaleString('vi-VN')} tài khoản ${
          activeRole === 'Student' ? 'sinh viên' : 'giảng viên'
        } ở trạng thái Chờ kích hoạt.`,
      );
      setSelectedFile(null);
      setRawRows([]);
      await onImportSuccess();
    } catch (err) {
      setErrorMessage(getUserErrorMessage(err));
    } finally {
      setIsSubmitting(false);
    }
  }, [selectedFile, activeRole, summary, onImportSuccess]);

  const fileInfo = useMemo(() => {
    if (!selectedFile) return null;
    const sizeKb = Math.round(selectedFile.size / 1024);
    return {
      name: selectedFile.name,
      sizeLabel: `${sizeKb} KB`,
      statusText: isParsing ? 'Đang đọc dữ liệu…' : 'Đã kiểm tra định dạng',
    };
  }, [selectedFile, isParsing]);

  return {
    activeRole,
    handleSwitchRole,
    selectedFile,
    fileInfo,
    isParsing,
    isSubmitting,
    isDownloading,
    notice,
    setNotice,
    errorMessage,
    setErrorMessage,
    validatedRows,
    summary,
    handleSelectFile,
    handleResetFile,
    handleDownloadTemplate,
    handleSubmitImport,
    isSingleAddOpen,
    setIsSingleAddOpen,
  };
}
