import React from 'react';
import { ShieldCheck, Eye } from 'lucide-react';

interface PeriodPermissionsCardProps {
  canEdit: boolean;
}

export const PeriodPermissionsCard: React.FC<PeriodPermissionsCardProps> = ({
  canEdit,
}) => {
  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-6 shadow-xs">
      <div className="flex items-center justify-between mb-2">
        <h2 className="text-base font-bold text-slate-900">Quyền</h2>
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            canEdit
              ? 'bg-blue-50 text-[#2054A5] ring-1 ring-blue-500/20'
              : 'bg-amber-50 text-amber-700 ring-1 ring-amber-500/20'
          }`}
        >
          {canEdit ? (
            <>
              <ShieldCheck className="size-3.5" />
              Toàn quyền quản trị
            </>
          ) : (
            <>
              <Eye className="size-3.5" />
              Chỉ tra cứu
            </>
          )}
        </span>
      </div>
      <p className="text-sm text-slate-600">
        {canEdit
          ? 'Tài khoản của bạn có quyền thiết lập thông tin đợt, điều chỉnh lịch trình 7 giai đoạn và cập nhật các mốc deadline.'
          : 'Khoa chỉ tra cứu kế hoạch đợt và cấu hình điểm, không sửa deadline đợt.'}
      </p>
    </div>
  );
};