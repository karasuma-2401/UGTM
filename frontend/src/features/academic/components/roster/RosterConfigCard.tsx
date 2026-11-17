import React, { useRef, useMemo } from 'react';
import { Upload, FileSpreadsheet, Download, UserPlus, GraduationCap, Briefcase } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import type { KltnBatchSummaryDto } from '../../types/period.types';

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
        <div className="rounded-2xl border border-tone-success-border bg-tone-success-bg p-4 sm:p-5 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3.5 min-w-0">
            <div className="size-11 rounded-xl bg-tone-success-bg/90 border border-tone-success-border flex items-center justify-center shrink-0 text-tone-success-text shadow-2xs">
              <FileSpreadsheet className="size-5" />
            </div>
            <div className="min-w-0">
              <p className="text-sm font-bold text-ink-primary truncate">
                {fileInfo.name}
              </p>
              <p className="text-xs text-ink-muted mt-0.5">
                Bảng tính Excel (.xlsx) • Dung lượng: {fileInfo.sizeLabel} • {fileInfo.statusText}
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
          onClick={() => fileInputRef.current?.click()}
          className="w-full rounded-2xl border-2 border-dashed border-line-strong hover:border-emerald-500 hover:bg-tone-success-bg p-8 flex flex-col items-center justify-center gap-3 text-center transition-all cursor-pointer group focus-visible:outline-emerald-600 bg-surface-subtle/40"
        >
          <div className="size-12 rounded-2xl bg-tone-success-bg border border-tone-success-border group-hover:scale-105 group-hover:bg-tone-success-bg flex items-center justify-center text-tone-success-icon transition-all shadow-2xs">
            <Upload className="size-5" />
          </div>
          <div className="space-y-1">
            <span className="text-sm font-bold text-ink-heading block">
              Nhấn để tải lên tệp bảng tính Excel (.xlsx)
            </span>
            <p className="text-xs text-ink-muted max-w-md mx-auto">
              Hệ thống sẽ tự động đọc dữ liệu, kiểm tra cấu trúc cột và đối chiếu điều kiện hợp lệ trước khi nhập
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
