import React from 'react';
import { Card } from '@/components/ui/card';

export const RosterGuideCard: React.FC = () => {
  return (
    <Card className="rounded-2xl border-slate-200/80 bg-white p-6 shadow-sm flex flex-col justify-between h-full">
      <div>
        <h3 className="text-base font-bold text-slate-900 tracking-tight">
          Chuẩn bị danh sách
        </h3>

        <p className="text-xs sm:text-sm text-slate-600 mt-3 leading-relaxed">
          Mã người dùng, họ tên, email trường và Khoa/đơn vị phải khớp danh sách được duyệt.
        </p>

        <hr className="border-slate-100 my-4" />

        <p className="text-xs sm:text-sm text-slate-600 leading-relaxed">
          Sinh viên kích hoạt từ danh sách trường. Giảng viên dùng tài khoản do trường cấp.
        </p>
      </div>

      <div className="pt-4">
        <p className="text-[11px] sm:text-xs text-slate-400 leading-normal">
          Tài khoản đã có được giữ nguyên mật khẩu và quyền. Không tự gán quyền Hội đồng từ tệp nhập.
        </p>
      </div>
    </Card>
  );
};

