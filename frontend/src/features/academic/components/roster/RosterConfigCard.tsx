import React, { useRef } from 'react';
import { Upload, FileSpreadsheet, Download, UserPlus } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import type { KltnBatchSummaryDto } from '../../types/period.types';

interface RosterConfigCardProps {
  activeRole: 'Student' | 'Lecturer';
  onSwitchRole: (role: 'Student' | 'Lecturer') => void;
  batches: KltnBatchSummaryDto[];
  selectedBatchId: string;
  onSelectBatchId: (id: string) => void;
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
  fileInfo,
  onSelectFile,
  onDownloadTemplate,
  isDownloading,
  onOpenSingleAdd,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);

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
    <Card className="rounded-2xl border-slate-200/80 bg-white p-6 shadow-sm space-y-5">
      <div className="flex items-center gap-2">
        <div className="inline-flex p-1 bg-slate-100 rounded-xl">
          <button
            type="button"
            onClick={() => onSwitchRole('Student')}
            className={`px-4 py-1.5 text-xs font-semibold rounded-lg transition-all cursor-pointer ${
              activeRole === 'Student'
                ? 'bg-[#2054A5] text-white shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Sinh viên
          </button>
          <button
            type="button"
            onClick={() => onSwitchRole('Lecturer')}
            className={`px-4 py-1.5 text-xs font-semibold rounded-lg transition-all cursor-pointer ${
              activeRole === 'Lecturer'
                ? 'bg-[#2054A5] text-white shadow-xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Giảng viên
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <label className="space-y-1.5 text-xs font-semibold text-slate-700 block">
          <span>Đợt khóa luận</span>
          <select
            value={selectedBatchId}
            onChange={(e) => onSelectBatchId(e.target.value)}
            className="w-full h-10 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-800 shadow-2xs focus:outline-hidden focus:ring-2 focus:ring-[#2054A5]"
          >
            {batches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.batchName} · HK{b.semester} {b.academicYear}
              </option>
            ))}
          </select>
        </label>

        <label className="space-y-1.5 text-xs font-semibold text-slate-700 block">
          <span>Loại danh sách</span>
          <select
            className="w-full h-10 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-800 shadow-2xs focus:outline-hidden focus:ring-2 focus:ring-[#2054A5]"
            disabled
          >
            <option>
              {activeRole === 'Student' ? 'Sinh viên đủ điều kiện' : 'Giảng viên hướng dẫn & phản biện'}
            </option>
          </select>
        </label>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={handleFileChange}
      />

      {fileInfo ? (
        <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 flex items-center justify-between gap-3">
          <div className="flex items-center gap-3 min-w-0">
            <div className="size-10 rounded-lg bg-emerald-50 border border-emerald-200 flex items-center justify-center shrink-0">
              <FileSpreadsheet className="size-5 text-emerald-600" />
            </div>
            <div className="min-w-0">
              <p className="text-xs sm:text-sm font-semibold text-slate-900 truncate">
                {fileInfo.name}
              </p>
              <p className="text-[11px] text-slate-500 mt-0.5">
                Excel .xlsx · {fileInfo.sizeLabel} · {fileInfo.statusText}
              </p>
            </div>
          </div>

          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
            className="rounded-lg h-8 text-xs shrink-0 cursor-pointer"
          >
            Thay tệp
          </Button>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          className="w-full rounded-xl border-2 border-dashed border-slate-200 hover:border-[#2054A5]/60 hover:bg-slate-50/50 p-6 flex flex-col items-center justify-center gap-2 text-center transition-all cursor-pointer group"
        >
          <div className="size-10 rounded-full bg-slate-100 group-hover:bg-[#2054A5]/10 flex items-center justify-center text-slate-500 group-hover:text-[#2054A5] transition-colors">
            <Upload className="size-5" />
          </div>
          <div className="space-y-0.5">
            <span className="text-xs sm:text-sm font-semibold text-slate-800">
              Nhấn để tải lên tệp danh sách Excel (.xlsx)
            </span>
            <p className="text-[11px] text-slate-400">
              Hệ thống sẽ tự động đọc và đối chiếu hợp lệ trước khi nhập
            </p>
          </div>
        </button>
      )}

      <div className="flex flex-wrap items-center justify-between gap-2 pt-1 border-t border-slate-100 text-xs">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={onDownloadTemplate}
            disabled={isDownloading}
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#2054A5] hover:text-[#184285] hover:underline cursor-pointer disabled:opacity-50"
          >
            <Download className="size-3.5" />
            <span>{isDownloading ? 'Đang tải mẫu…' : 'Xem mẫu Excel'}</span>
          </button>

          <span className="text-slate-300">|</span>

          <button
            type="button"
            onClick={onOpenSingleAdd}
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-600 hover:text-slate-900 hover:underline cursor-pointer"
          >
            <UserPlus className="size-3.5" />
            <span>Thêm bổ sung đơn lẻ</span>
          </button>
        </div>

        <span className="text-[11px] text-slate-400">
          Tệp tối đa 20 MB
        </span>
      </div>
    </Card>
  );
};