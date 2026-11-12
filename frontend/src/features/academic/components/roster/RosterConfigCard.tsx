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
        : 'Giảng viên hướng dẫn & phản biện';
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
    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs space-y-5 transition-all hover:border-slate-300">
      {/* Role Segmented Controls - Fluent Style with Fluid Sliding Animation */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="relative inline-grid grid-cols-2 p-1 rounded-xl border border-slate-200 bg-slate-100/80 select-none">
          {/* Animated Sliding Pill Indicator */}
          <span
            aria-hidden="true"
            className={`absolute top-1 bottom-1 left-1 w-[calc(50%-4px)] rounded-lg bg-white shadow-xs border border-slate-200/60 transition-transform duration-300 ease-out will-change-transform pointer-events-none ${
              activeRole === 'Student' ? 'translate-x-0' : 'translate-x-full'
            }`}
          />

          <button
            type="button"
            onClick={() => onSwitchRole('Student')}
            className={`relative z-10 flex items-center justify-center gap-2 px-4 py-2 text-xs font-semibold rounded-lg transition-colors duration-200 select-none cursor-pointer active:scale-[0.98] ${
              activeRole === 'Student'
                ? 'text-slate-900 font-bold'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <GraduationCap
              className={`size-4 transition-all duration-300 ${
                activeRole === 'Student'
                  ? 'text-primary scale-110'
                  : 'text-slate-400'
              }`}
            />
            <span>Danh sách Sinh viên</span>
          </button>

          <button
            type="button"
            onClick={() => onSwitchRole('Lecturer')}
            className={`relative z-10 flex items-center justify-center gap-2 px-4 py-2 text-xs font-semibold rounded-lg transition-colors duration-200 select-none cursor-pointer active:scale-[0.98] ${
              activeRole === 'Lecturer'
                ? 'text-slate-900 font-bold'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Briefcase
              className={`size-4 transition-all duration-300 ${
                activeRole === 'Lecturer'
                  ? 'text-emerald-600 scale-110'
                  : 'text-slate-400'
              }`}
            />
            <span>Danh sách Giảng viên</span>
          </button>
        </div>

        <span className="text-xs font-medium text-slate-400">
          Chọn đối tượng danh sách cần nhập dữ liệu
        </span>
      </div>

      {/* Selectors Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-slate-700 block">
            Đợt khóa luận tốt nghiệp <span className="text-rose-500">*</span>
          </label>
          <Select
            options={batchOptions}
            value={selectedBatchId}
            onChange={(val) => onSelectBatchId(String(val ?? ''))}
            disabled={isLoadingBatches || batches.length === 0}
            placeholder={isLoadingBatches ? 'Đang tải đợt khóa luận...' : 'Chưa có đợt khóa luận'}
            size="lg"
            triggerClassName="h-10 rounded-xl border-slate-200 bg-white text-xs font-medium text-slate-800 shadow-2xs hover:border-slate-300 transition-all select-none"
            contentClassName="w-max min-w-full sm:min-w-[440px] max-w-[90vw] p-1.5 rounded-xl border-slate-200/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-slate-700 hover:bg-slate-50 transition-colors"
          />
        </div>

        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-slate-700 block">
            Phân loại đối tượng danh sách
          </label>
          <Select
            options={roleTypeOption}
            value={activeRole}
            disabled
            size="lg"
            triggerClassName="h-10 rounded-xl border-slate-200 bg-slate-50 text-xs font-medium text-slate-700 shadow-2xs select-none"
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

      {/* Drag & Drop Upload Zone */}
      {fileInfo ? (
        <div className="rounded-2xl border border-emerald-200 bg-emerald-50/40 p-4 sm:p-5 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3.5 min-w-0">
            <div className="size-11 rounded-xl bg-emerald-100/90 border border-emerald-200 flex items-center justify-center shrink-0 text-emerald-700 shadow-2xs">
              <FileSpreadsheet className="size-5" />
            </div>
            <div className="min-w-0">
              <p className="text-sm font-bold text-slate-900 truncate">
                {fileInfo.name}
              </p>
              <p className="text-xs text-slate-500 mt-0.5">
                Bảng tính Excel (.xlsx) • Dung lượng: {fileInfo.sizeLabel} • {fileInfo.statusText}
              </p>
            </div>
          </div>

          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
            className="rounded-xl border-emerald-300 bg-white text-emerald-800 hover:bg-emerald-50 text-xs font-semibold h-9 px-3.5 shrink-0 shadow-2xs transition-all active:scale-[0.98]"
          >
            Đổi tệp khác
          </Button>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          className="w-full rounded-2xl border-2 border-dashed border-slate-300 hover:border-emerald-500 hover:bg-emerald-50/30 p-8 flex flex-col items-center justify-center gap-3 text-center transition-all cursor-pointer group focus-visible:outline-emerald-600 bg-slate-50/40"
        >
          <div className="size-12 rounded-2xl bg-emerald-50 border border-emerald-200/80 group-hover:scale-105 group-hover:bg-emerald-100 flex items-center justify-center text-emerald-600 transition-all shadow-2xs">
            <Upload className="size-5" />
          </div>
          <div className="space-y-1">
            <span className="text-sm font-bold text-slate-800 block">
              Nhấn để tải lên tệp bảng tính Excel (.xlsx)
            </span>
            <p className="text-xs text-slate-500 max-w-md mx-auto">
              Hệ thống sẽ tự động đọc dữ liệu, kiểm tra cấu trúc cột và đối chiếu điều kiện hợp lệ trước khi nhập
            </p>
          </div>
        </button>
      )}

      {/* Action Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-100 text-xs">
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onDownloadTemplate}
            disabled={isDownloading}
            className="h-8 gap-1.5 rounded-lg border-emerald-200 bg-emerald-50/70 text-emerald-800 hover:bg-emerald-100 font-semibold text-xs shadow-2xs transition-all active:scale-[0.98]"
          >
            <Download className="size-3.5 text-emerald-600" />
            <span>{isDownloading ? 'Đang tải mẫu…' : 'Tải tệp Excel mẫu (.xlsx)'}</span>
          </Button>

          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onOpenSingleAdd}
            className="h-8 gap-1.5 rounded-lg border-slate-200 bg-white text-slate-700 hover:bg-slate-50 font-semibold text-xs shadow-2xs transition-all active:scale-[0.98]"
          >
            <UserPlus className="size-3.5 text-slate-500" />
            <span>Thêm bổ sung đơn lẻ</span>
          </Button>
        </div>

        <span className="text-xs text-slate-400">
          Định dạng hỗ trợ: Microsoft Excel (.xlsx) • Tối đa 20 MB
        </span>
      </div>
    </div>
  );
};
