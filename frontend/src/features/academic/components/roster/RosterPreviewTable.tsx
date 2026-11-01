import React, { useRef } from 'react';
import { RefreshCw, CheckCircle2, AlertCircle, AlertTriangle, FileSpreadsheet } from 'lucide-react';
import { Card } from '@/components/ui/card';
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

  if (rows.length === 0) {
    return (
      <Card className="rounded-2xl border-slate-200/80 bg-white p-12 text-center shadow-sm">
        <div className="size-12 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto text-slate-400 mb-3">
          <FileSpreadsheet className="size-6" />
        </div>
        <h3 className="text-sm font-bold text-slate-800">
          Chưa có danh sách đối chiếu
        </h3>
        <p className="text-xs text-slate-500 max-w-md mx-auto mt-1">
          Vui lòng tải lên file Excel (.xlsx) danh sách {roleLabel} ở bước trên để hệ thống tự động kiểm tra định dạng và hiển thị kết quả đối chiếu.
        </p>
      </Card>
    );
  }

  return (
    <Card className="rounded-2xl border-slate-200/80 bg-white overflow-hidden shadow-sm">
      <input
        ref={replaceInputRef}
        type="file"
        accept=".xlsx"
        className="hidden"
        onChange={handleFileChange}
      />

      <div className="p-5 sm:px-6 flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-100">
        <div>
          <h3 className="text-base font-bold text-slate-900 tracking-tight">
            Đối chiếu trước khi nhập
          </h3>
        </div>

        <div className="flex items-center gap-2 text-xs font-medium text-slate-600">
          <span>{summary.total} dòng</span>
          <span>-</span>
          <span className="text-emerald-700 font-semibold">{summary.valid} hợp lệ</span>
          {(summary.needsFix > 0 || summary.duplicate > 0) && (
            <>
              <span>-</span>
              <span className="text-rose-700 font-semibold">
                {summary.needsFix + summary.duplicate} cần sửa
              </span>
            </>
          )}
        </div>
      </div>

      <div className="overflow-x-auto max-h-[500px]">
        <table className="w-full text-left text-xs text-slate-700">
          <thead className="bg-slate-50/80 text-[11px] font-semibold text-slate-500 uppercase tracking-wider sticky top-0 z-10 border-b border-slate-200/60">
            <tr>
              <th className="py-3 px-4 w-16">Dòng</th>
              <th className="py-3 px-4 w-36">Mã SV / GV</th>
              <th className="py-3 px-4">Họ và tên</th>
              <th className="py-3 px-4">Email trường</th>
              <th className="py-3 px-4 w-32">Đối chiếu</th>
              <th className="py-3 px-4">Ghi chú</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {rows.map((row) => (
              <tr
                key={row.rowNumber}
                className={`hover:bg-slate-50/80 transition-colors ${
                  row.status === 'NeedsFix'
                    ? 'bg-rose-50/20'
                    : row.status === 'DuplicateCode'
                    ? 'bg-amber-50/20'
                    : ''
                }`}
              >
                <td className="py-3 px-4 text-slate-400 font-medium">{row.rowNumber}</td>
                <td className="py-3 px-4 font-semibold text-slate-900">{row.userCode}</td>
                <td className="py-3 px-4 font-semibold text-slate-900">{row.fullName}</td>
                <td className="py-3 px-4 text-slate-600 font-mono text-[11px]">{row.email}</td>
                <td className="py-3 px-4">
                  {row.status === 'Valid' ? (
                    <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200/60">
                      <CheckCircle2 className="size-3 text-emerald-600" />
                      {row.statusLabel}
                    </span>
                  ) : row.status === 'NeedsFix' ? (
                    <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-rose-50 text-rose-700 border border-rose-200/60">
                      <AlertCircle className="size-3 text-rose-600" />
                      {row.statusLabel}
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-amber-50 text-amber-700 border border-amber-200/60">
                      <AlertTriangle className="size-3 text-amber-600" />
                      {row.statusLabel}
                    </span>
                  )}
                </td>
                <td className="py-3 px-4 text-slate-500">{row.note}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="p-4 sm:px-6 bg-slate-50/50 border-t border-slate-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => replaceInputRef.current?.click()}
          className="rounded-lg h-9 text-xs cursor-pointer shrink-0"
        >
          Thay tệp đã sửa
        </Button>

        <p className="text-xs text-slate-500 text-center sm:text-left">
          Nhập toàn bộ danh sách sau khi đối chiếu hợp lệ.
        </p>

        <Button
          type="button"
          onClick={onSubmit}
          disabled={!summary.canSubmit || isSubmitting}
          className="rounded-lg h-9 text-xs font-semibold px-5 bg-[#2054A5] hover:bg-[#184285] text-white shadow-xs cursor-pointer disabled:opacity-50 disabled:pointer-events-none shrink-0"
          leftIcon={
            isSubmitting ? (
              <RefreshCw className="size-3.5 animate-spin" />
            ) : undefined
          }
        >
          {isSubmitting ? 'Đang nhập danh sách…' : 'Xác nhận nhập'}
        </Button>
      </div>

      {batchLabel && (
        <div className="px-6 py-2.5 bg-slate-50 text-[11px] text-slate-400 border-t border-slate-100">
          Danh sách chỉ áp dụng cho {batchLabel}. Phân công GVHD/GVPB và Hội đồng được quản lý ở nghiệp vụ tương ứng.
        </div>
      )}
    </Card>
  );
};

