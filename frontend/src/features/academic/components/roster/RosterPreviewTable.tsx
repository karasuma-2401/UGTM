import React, { useRef } from 'react';
import { RefreshCw, CheckCircle2, AlertCircle, AlertTriangle, FileSpreadsheet, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { ValidatedRosterRow } from '../../utils/rosterExcel';

interface RosterPreviewTableProps {
  rows: ValidatedRosterRow[];
  summary: {
    total: number;
    valid: number;
    needsFix: number;
    duplicate: number;
    canSubmit: boolean;
  };
  isSubmitting: boolean;
  onSelectNewFile: (file: File) => void;
  onSubmit: () => void;
  batchLabel?: string;
  roleLabel: string;
}

export const RosterPreviewTable: React.FC<RosterPreviewTableProps> = ({
  rows,
  summary,
  isSubmitting,
  onSelectNewFile,
  onSubmit,
  batchLabel,
  roleLabel,
}) => {
  const replaceInputRef = useRef<HTMLInputElement>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      onSelectNewFile(file);
    }
    if (replaceInputRef.current) {
      replaceInputRef.current.value = '';
    }
  };

  const isStudentRole = roleLabel.toLowerCase().includes('sinh viên');

  if (rows.length === 0) {
    return (
      <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-xs transition-all hover:border-slate-300">
        <div className="size-14 rounded-2xl bg-emerald-50 border border-emerald-200/80 flex items-center justify-center mx-auto text-emerald-600 mb-4 shadow-2xs">
          <FileSpreadsheet className="size-7" />
        </div>
        <h3 className="text-base font-bold text-slate-900 tracking-tight">
          Chưa có danh sách đối chiếu
        </h3>
        <p className="text-xs text-slate-500 max-w-lg mx-auto mt-1.5 leading-relaxed">
          Vui lòng chọn đợt khóa luận và tải lên tệp bảng tính Excel (.xlsx) danh sách {roleLabel} ở khung trên để hệ thống tự động kiểm tra định dạng và hiển thị kết quả đối chiếu chi tiết.
        </p>

        {/* 3 Step Visual Guidance */}
        <div className="mt-6 flex flex-wrap items-center justify-center gap-2 text-xs text-slate-600">
          <span className="rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1 font-medium">
            1. Tải tệp Excel mẫu
          </span>
          <ArrowRight className="size-3 text-slate-400" />
          <span className="rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1 font-medium">
            2. Điền thông tin danh sách
          </span>
          <ArrowRight className="size-3 text-slate-400" />
          <span className="rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1 font-medium">
            3. Tải lên đối chiếu
          </span>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-slate-200 bg-white overflow-hidden shadow-xs transition-all hover:border-slate-300">
      <input
        ref={replaceInputRef}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={handleFileChange}
      />

      {/* Header bar with summary badges */}
      <div className="p-5 sm:px-6 flex flex-wrap items-center justify-between gap-3 border-b border-slate-100">
        <div>
          <h3 className="text-base font-bold text-slate-900 tracking-tight">
            Kết quả đối chiếu trước khi nhập ({roleLabel})
          </h3>
          <p className="text-xs text-slate-500">
            Rà soát danh sách và tình trạng hợp lệ trước khi xác nhận lưu vào hệ thống
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2 text-xs font-semibold">
          <span className="rounded-full border border-slate-200 bg-slate-50 px-3 py-1 text-slate-700 shadow-2xs">
            Tổng số: {summary.total} dòng
          </span>
          <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-300 bg-emerald-50 px-3 py-1 text-emerald-800 shadow-2xs">
            <CheckCircle2 className="size-3.5 text-emerald-600" />
            {summary.valid} hợp lệ
          </span>
          {(summary.needsFix > 0 || summary.duplicate > 0) && (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-rose-300 bg-rose-50 px-3 py-1 text-rose-800 shadow-2xs">
              <AlertCircle className="size-3.5 text-rose-600" />
              {summary.needsFix + summary.duplicate} cần điều chỉnh
            </span>
          )}
        </div>
      </div>

      {/* Table Container */}
      <div className="overflow-x-auto max-h-[520px]">
        <table className="w-full text-left text-xs text-slate-700">
          <thead className="bg-slate-50/90 text-[11px] font-semibold text-slate-600 uppercase tracking-wider sticky top-0 z-10 border-b border-slate-200">
            <tr>
              <th className="py-3 px-4 w-16 text-center">Thứ tự</th>
              <th className="py-3 px-4 w-40">
                {isStudentRole ? 'Mã số sinh viên' : 'Mã số giảng viên'}
              </th>
              <th className="py-3 px-4">Họ và tên</th>
              <th className="py-3 px-4">Hộp thư điện tử trường</th>
              <th className="py-3 px-4 w-40">Trạng thái đối chiếu</th>
              <th className="py-3 px-4">Ghi chú chi tiết</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {rows.map((row) => (
              <tr
                key={row.rowNumber}
                className={`hover:bg-slate-50/80 transition-colors ${
                  row.status === 'NeedsFix'
                    ? 'bg-rose-50/30'
                    : row.status === 'DuplicateCode'
                    ? 'bg-amber-50/30'
                    : ''
                }`}
              >
                <td className="py-3.5 px-4 text-slate-400 font-medium text-center">
                  {row.rowNumber}
                </td>
                <td className="py-3.5 px-4 font-semibold text-slate-900">
                  {row.userCode}
                </td>
                <td className="py-3.5 px-4 font-semibold text-slate-900">
                  {row.fullName}
                </td>
                <td className="py-3.5 px-4 text-slate-600 font-mono text-[11px]">
                  {row.email}
                </td>
                <td className="py-3.5 px-4">
                  {row.status === 'Valid' ? (
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-50 text-emerald-800 border border-emerald-300 shadow-2xs">
                      <CheckCircle2 className="size-3 text-emerald-600" />
                      {row.statusLabel}
                    </span>
                  ) : row.status === 'NeedsFix' ? (
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-rose-50 text-rose-800 border border-rose-300 shadow-2xs">
                      <AlertCircle className="size-3 text-rose-600" />
                      {row.statusLabel}
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-amber-50 text-amber-800 border border-amber-300 shadow-2xs">
                      <AlertTriangle className="size-3 text-amber-600" />
                      {row.statusLabel}
                    </span>
                  )}
                </td>
                <td className="py-3.5 px-4 text-slate-600 text-xs">
                  {row.note}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Footer action bar */}
      <div className="p-4 sm:px-6 bg-slate-50/70 border-t border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => replaceInputRef.current?.click()}
          className="rounded-xl border-slate-200 bg-white hover:bg-slate-50 text-xs font-semibold h-9 px-4 shadow-2xs transition-all active:scale-[0.98] shrink-0"
        >
          <FileSpreadsheet className="size-3.5 mr-1.5 text-emerald-600" />
          Tải tệp đã chỉnh sửa
        </Button>

        <p className="text-xs text-slate-500 text-center sm:text-left">
          Vui lòng rà soát kỹ các dòng cảnh báo trước khi xác nhận lưu danh sách vào đợt.
        </p>

        <Button
          type="button"
          variant="success"
          size="sm"
          onClick={onSubmit}
          disabled={!summary.canSubmit || isSubmitting}
          className="rounded-xl h-9 text-xs font-semibold px-5 shadow-sm active:scale-[0.98] transition-all shrink-0"
          leftIcon={
            isSubmitting ? (
              <RefreshCw className="size-3.5 animate-spin" />
            ) : undefined
          }
        >
          {isSubmitting ? 'Đang nhập danh sách…' : `Xác nhận nhập danh sách (${summary.valid} dòng)`}
        </Button>
      </div>

      {batchLabel && (
        <div className="px-6 py-2.5 bg-slate-100/70 text-[11px] text-slate-500 border-t border-slate-200">
          Danh sách áp dụng cho {batchLabel}. Phân công Giảng viên hướng dẫn, Giảng viên phản biện và Hội đồng được quản lý ở các phân hệ nghiệp vụ tương ứng.
        </div>
      )}
    </div>
  );
};
