import React, { useRef, useMemo, useState } from 'react';
import {
  Upload,
  FileSpreadsheet,
  Download,
  UserPlus,
  GraduationCap,
  Briefcase,
  Ban,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { notify } from '@/shared/notifications';
import type { KltnBatchSummaryDto } from '../../types/period.types';
import {
  INVALID_EXCEL_FILE_MESSAGE,
  inspectDraggedExcelTransfer,
  isExcelXlsxFile,
  type RosterDragStatus,
} from '../../utils/rosterExcel';

interface RosterConfigCardProps {
  activeRole: 'Student' | 'Lecturer';
  onSwitchRole: (role: 'Student' | 'Lecturer') => void;
  batches: KltnBatchSummaryDto[];
  selectedBatchId: string;
  onSelectBatchId: (id: string) => void;
  isLoadingBatches?: boolean;
  fileInfo: { name: string; sizeLabel: string; statusText: string } | null;
  onSelectFile: (file: File) => void;
  onDownloadTemplate: () => void;
  isDownloading: boolean;
  onOpenSingleAdd: () => void;
}

export const RosterConfigCard: React.FC<RosterConfigCardProps> = ({
  activeRole,
  onSwitchRole,
  batches,
  selectedBatchId,
  onSelectBatchId,
  isLoadingBatches = false,
  fileInfo,
  onSelectFile,
  onDownloadTemplate,
  isDownloading,
  onOpenSingleAdd,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const dragDepthRef = useRef(0);
  const [dragStatus, setDragStatus] = useState<RosterDragStatus>('idle');

  const batchOptions = useMemo(
    () =>
      batches.map((b) => ({
        value: b.id,
        label: `${b.batchName} (Học kỳ ${b.semester} - Năm học ${b.academicYear})`,
      })),
    [batches],
  );

  const roleTypeOption = useMemo(() => {
    const label =
      activeRole === 'Student'
        ? 'Sinh viên đủ điều kiện làm khóa luận'
        : 'Giảng viên hướng dẫn và phản biện';
    return [{ value: activeRole, label }];
  }, [activeRole]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      onSelectFile(file);
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const resetDragState = () => {
    dragDepthRef.current = 0;
    setDragStatus('idle');
  };

  const handleDragEnter = (e: React.DragEvent<HTMLElement>) => {
    e.preventDefault();
    e.stopPropagation();
    dragDepthRef.current += 1;
    const status = inspectDraggedExcelTransfer(e.dataTransfer);
    setDragStatus(status);
  };

  const handleDragOver = (e: React.DragEvent<HTMLElement>) => {
    e.preventDefault();
    e.stopPropagation();
    const status = inspectDraggedExcelTransfer(e.dataTransfer);
    if (e.dataTransfer) {
      e.dataTransfer.dropEffect = status === 'invalid' ? 'none' : 'copy';
    }
    if (status !== dragStatus) {
      setDragStatus(status);
    }
  };

  const handleDragLeave = (e: React.DragEvent<HTMLElement>) => {
    e.preventDefault();
    e.stopPropagation();
    dragDepthRef.current = Math.max(0, dragDepthRef.current - 1);
    if (dragDepthRef.current === 0) {
      setDragStatus('idle');
    }
  };

  const handleDrop = (e: React.DragEvent<HTMLElement>) => {
    e.preventDefault();
    e.stopPropagation();
    resetDragState();

    const droppedFile = e.dataTransfer?.files?.[0];
    if (!droppedFile) {
      notify.error(INVALID_EXCEL_FILE_MESSAGE, {
        title: 'Định dạng tệp không hợp lệ',
        duration: 5000,
      });
      return;
    }

    if (!isExcelXlsxFile(droppedFile)) {
      notify.error(INVALID_EXCEL_FILE_MESSAGE, {
        title: 'Định dạng tệp không hợp lệ',
        duration: 5000,
      });
      return;
    }

    onSelectFile(droppedFile);
  };

  return (
    <div className="rounded-2xl border border-line bg-surface-card p-6 shadow-xs space-y-5 transition-all hover:border-line-strong">
      {/* Role Segmented Controls - Fluent Style with Fluid Sliding Animation */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="relative inline-grid grid-cols-2 p-1 rounded-xl border border-line bg-surface-muted/80 select-none">
          {/* Animated Sliding Pill Indicator */}
          <span
            aria-hidden="true"
            className={`absolute top-1 bottom-1 left-1 w-[calc(50%-4px)] rounded-lg bg-surface-elevated shadow-xs border border-line-input/60 transition-transform duration-300 ease-out will-change-transform pointer-events-none ${
              activeRole === 'Student' ? 'translate-x-0' : 'translate-x-full'
            }`}
          />

          <button
            type="button"
            onClick={() => onSwitchRole('Student')}
            className={`relative z-10 flex items-center justify-center gap-2 px-4 py-2 text-xs font-semibold rounded-lg transition-colors duration-200 select-none cursor-pointer active:scale-[0.98] ${
              activeRole === 'Student'
                ? 'text-ink-primary font-bold'
                : 'text-ink-body hover:text-ink-primary'
            }`}
          >
            <GraduationCap
              className={`size-4 transition-all duration-300 ${
                activeRole === 'Student'
                  ? 'text-primary scale-110'
                  : 'text-ink-subtle'
              }`}
            />
            <span>Danh sách Sinh viên</span>
          </button>

          <button
            type="button"
            onClick={() => onSwitchRole('Lecturer')}
            className={`relative z-10 flex items-center justify-center gap-2 px-4 py-2 text-xs font-semibold rounded-lg transition-colors duration-200 select-none cursor-pointer active:scale-[0.98] ${
              activeRole === 'Lecturer'
                ? 'text-ink-primary font-bold'
                : 'text-ink-body hover:text-ink-primary'
            }`}
          >
            <Briefcase
              className={`size-4 transition-all duration-300 ${
                activeRole === 'Lecturer'
                  ? 'text-tone-success-icon scale-110'
                  : 'text-ink-subtle'
              }`}
            />
            <span>Danh sách Giảng viên</span>
          </button>
        </div>

        <span className="text-xs font-medium text-ink-subtle">
          Chọn đối tượng danh sách cần nhập dữ liệu
        </span>
      </div>

      {/* Selectors Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-ink-secondary block">
            Đợt khóa luận tốt nghiệp <span className="text-rose-500">*</span>
          </label>
          <Select
            options={batchOptions}
            value={selectedBatchId}
            onChange={(val) => onSelectBatchId(String(val ?? ''))}
            disabled={isLoadingBatches || batches.length === 0}
            placeholder={isLoadingBatches ? 'Đang tải đợt khóa luận...' : 'Chưa có đợt khóa luận'}
            size="lg"
            triggerClassName="h-10 rounded-xl border-line-input bg-surface-card text-xs font-medium text-ink-heading shadow-2xs hover:border-line-strong transition-all select-none"
            contentClassName="w-max min-w-full sm:min-w-[440px] max-w-[90vw] p-1.5 rounded-xl border-line/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-ink-secondary hover:bg-surface-hover transition-colors"
          />
        </div>

        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-ink-secondary block">
            Phân loại đối tượng danh sách
          </label>
          <Select
            options={roleTypeOption}
            value={activeRole}
            disabled
            size="lg"
            triggerClassName="h-10 rounded-xl border-line bg-surface-subtle text-xs font-medium text-ink-secondary shadow-2xs select-none"
          />
        </div>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={handleFileChange}
      />

      {/* Drag và Drop Upload Zone */}
      {fileInfo ? (
        <div
          data-drag-status={dragStatus}
          onDragEnter={handleDragEnter}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          className={`rounded-2xl border-2 p-4 sm:p-5 flex items-center justify-between gap-4 transition-all ${
            dragStatus === 'valid'
              ? 'border-dashed border-emerald-600 dark:border-emerald-400 bg-emerald-500/15 dark:bg-emerald-500/20 ring-2 ring-emerald-500/35 shadow-sm'
              : dragStatus === 'invalid'
                ? 'border-dashed border-rose-500 dark:border-rose-400 bg-tone-danger-bg ring-2 ring-rose-500/30 cursor-not-allowed'
                : 'border-tone-success-border bg-tone-success-bg'
          }`}
        >
          <div className="flex items-center gap-3.5 min-w-0">
            <div
              className={`size-11 rounded-xl border flex items-center justify-center shrink-0 transition-all shadow-2xs ${
                dragStatus === 'valid'
                  ? 'bg-emerald-600 border-emerald-600 text-white scale-105'
                  : dragStatus === 'invalid'
                    ? 'bg-rose-600 border-rose-600 text-white'
                    : 'bg-tone-success-bg/90 border-tone-success-border text-tone-success-text'
              }`}
            >
              {dragStatus === 'invalid' ? (
                <Ban className="size-5" />
              ) : (
                <FileSpreadsheet className="size-5" />
              )}
            </div>
            <div className="min-w-0">
              <p
                className={`text-sm font-bold truncate ${
                  dragStatus === 'valid'
                    ? 'text-emerald-800 dark:text-emerald-200'
                    : dragStatus === 'invalid'
                      ? 'text-tone-danger-strong'
                      : 'text-ink-primary'
                }`}
              >
                {dragStatus === 'valid'
                  ? 'Thả tệp Excel (.xlsx) mới để thay thế'
                  : dragStatus === 'invalid'
                    ? 'Định dạng không hỗ trợ — chỉ chấp nhận tệp Excel (.xlsx)'
                    : fileInfo.name}
              </p>
              <p className="text-xs text-ink-muted mt-0.5">
                {dragStatus === 'invalid'
                  ? 'Tệp hiện tại sẽ được giữ nguyên.'
                  : `Bảng tính Excel (.xlsx) • Dung lượng: ${fileInfo.sizeLabel} • ${fileInfo.statusText}`}
              </p>
            </div>
          </div>

          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
            className="rounded-xl border-tone-success-border bg-surface-card text-tone-success-strong hover:bg-tone-success-bg text-xs font-semibold h-9 px-3.5 shrink-0 shadow-2xs transition-all active:scale-[0.98]"
          >
            Đổi tệp khác
          </Button>
        </div>
      ) : (
        <button
          type="button"
          data-drag-status={dragStatus}
          onClick={() => fileInputRef.current?.click()}
          onDragEnter={handleDragEnter}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          className={`w-full rounded-2xl border-2 border-dashed p-8 flex flex-col items-center justify-center gap-3 text-center transition-all group focus-visible:outline-emerald-600 ${
            dragStatus === 'valid'
              ? 'border-emerald-600 dark:border-emerald-400 bg-emerald-500/15 dark:bg-emerald-500/20 ring-2 ring-emerald-500/35 shadow-sm scale-[1.005] cursor-copy'
              : dragStatus === 'invalid'
                ? 'border-rose-500 dark:border-rose-400 bg-tone-danger-bg ring-2 ring-rose-500/30 cursor-not-allowed'
                : 'border-line-strong hover:border-emerald-500 hover:bg-tone-success-bg bg-surface-subtle/40 cursor-pointer'
          }`}
        >
          <div
            className={`size-12 rounded-2xl border flex items-center justify-center transition-all shadow-2xs ${
              dragStatus === 'valid'
                ? 'bg-emerald-600 border-emerald-600 text-white scale-110 shadow-sm'
                : dragStatus === 'invalid'
                  ? 'bg-rose-600 border-rose-600 text-white'
                  : 'bg-tone-success-bg border-tone-success-border group-hover:scale-105 text-tone-success-icon'
            }`}
          >
            {dragStatus === 'invalid' ? <Ban className="size-5" /> : <Upload className="size-5" />}
          </div>
          <div className="space-y-1">
            <span
              className={`text-sm font-bold block ${
                dragStatus === 'valid'
                  ? 'text-emerald-800 dark:text-emerald-200'
                  : dragStatus === 'invalid'
                    ? 'text-tone-danger-strong'
                    : 'text-ink-heading'
              }`}
            >
              {dragStatus === 'valid'
                ? 'Thả tệp Excel (.xlsx) vào đây để tải lên'
                : dragStatus === 'invalid'
                  ? 'Định dạng không hỗ trợ — chỉ chấp nhận tệp Excel (.xlsx)'
                  : 'Kéo thả hoặc nhấn để tải lên tệp bảng tính Excel (.xlsx)'}
            </span>
            <p className="text-xs text-ink-muted max-w-md mx-auto">
              {dragStatus === 'invalid'
                ? 'Vui lòng chọn tệp bảng tính Microsoft Excel có phần mở rộng .xlsx'
                : 'Hệ thống sẽ tự động đọc dữ liệu, kiểm tra cấu trúc cột và đối chiếu điều kiện hợp lệ trước khi nhập'}
            </p>
          </div>
        </button>
      )}

      {/* Action Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-line-subtle text-xs">
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onDownloadTemplate}
            disabled={isDownloading}
            className="h-8 gap-1.5 rounded-lg border-tone-success-border bg-tone-success-bg text-tone-success-strong hover:bg-tone-success-bg font-semibold text-xs shadow-2xs transition-all active:scale-[0.98]"
          >
            <Download className="size-3.5 text-tone-success-icon" />
            <span>{isDownloading ? 'Đang tải mẫu…' : 'Tải tệp Excel mẫu (.xlsx)'}</span>
          </Button>

          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onOpenSingleAdd}
            className="h-8 gap-1.5 rounded-lg border-line-input bg-surface-elevated text-ink-secondary hover:bg-surface-hover font-semibold text-xs shadow-2xs transition-all active:scale-[0.98]"
          >
            <UserPlus className="size-3.5 text-ink-muted" />
            <span>Thêm bổ sung đơn lẻ</span>
          </Button>
        </div>

        <span className="text-xs text-ink-subtle">
          Định dạng hỗ trợ: Microsoft Excel (.xlsx) • Tối đa 20 MB
        </span>
      </div>
    </div>
  );
};
