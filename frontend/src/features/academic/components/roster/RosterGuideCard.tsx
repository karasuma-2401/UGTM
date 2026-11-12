import React from 'react';

export const RosterGuideCard: React.FC = () => {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs flex flex-col justify-between h-full transition-all hover:border-slate-300">
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-base font-bold text-slate-900 tracking-tight">
            Quy chuẩn danh sách
          </h2>
          <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-700 shadow-2xs">
            Quy định đào tạo
          </span>
        </div>

        {/* Spec Overview - Fluent Metric Style */}
        <div className="grid grid-cols-2 gap-3">
          <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3 transition-all hover:border-emerald-200 hover:bg-emerald-50/20">
            <span className="text-[11px] font-medium text-slate-500 block mb-0.5">
              Định dạng tệp
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-base font-bold text-emerald-700">Excel</span>
              <span className="text-xs font-semibold text-emerald-600">.xlsx</span>
            </div>
            <span className="text-[11px] text-slate-400 block mt-0.5">
              Dung lượng tối đa 20 MB
            </span>
          </div>

          <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3 transition-all">
            <span className="text-[11px] font-medium text-slate-500 block mb-0.5">
              Cột bắt buộc
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-base font-bold text-slate-900">4</span>
              <span className="text-xs text-slate-500">trường dữ liệu</span>
            </div>
            <span className="text-[11px] text-slate-400 block mt-0.5">
              Theo tệp mẫu chuẩn
            </span>
          </div>
        </div>

        {/* Required Columns Chips */}
        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3.5 space-y-2">
          <span className="text-xs font-semibold text-slate-700 block">
            Cột dữ liệu bắt buộc trong tệp:
          </span>
          <div className="flex flex-wrap gap-1.5">
            <span className="inline-flex items-center rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-800 shadow-2xs">
              Mã số định danh
            </span>
            <span className="inline-flex items-center rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-800 shadow-2xs">
              Họ và tên
            </span>
            <span className="inline-flex items-center rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-800 shadow-2xs">
              Hộp thư @uit.edu.vn
            </span>
            <span className="inline-flex items-center rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-800 shadow-2xs">
              Khoa / Đơn vị
            </span>
          </div>
        </div>

        {/* Policy Rules Key-Value List - Clean single-line layout */}
        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3.5 space-y-2.5 text-xs">
          <div className="flex items-center justify-between gap-2">
            <span className="text-slate-500 shrink-0 font-medium">Sinh viên:</span>
            <span
              className="font-semibold text-slate-800 text-right whitespace-nowrap"
              title="Sinh viên tự kích hoạt tài khoản theo mã số và email trường"
            >
              Tự kích hoạt qua email trường
            </span>
          </div>
          <div className="flex items-center justify-between gap-2 pt-2 border-t border-slate-200/80">
            <span className="text-slate-500 shrink-0 font-medium">Giảng viên:</span>
            <span
              className="font-semibold text-slate-800 text-right whitespace-nowrap"
              title="Giảng viên sử dụng tài khoản công vụ do Nhà trường cấp sẵn"
            >
              Dùng tài khoản công vụ cấp sẵn
            </span>
          </div>
          <div className="flex items-center justify-between gap-2 pt-2 border-t border-slate-200/80">
            <span className="text-slate-500 shrink-0 font-medium">Phân quyền:</span>
            <span
              className="font-semibold text-slate-800 text-right whitespace-nowrap"
              title="Tài khoản đã tồn tại được bảo lưu mật khẩu và vai trò cũ. Phân công Hội đồng khóa luận được quản lý độc lập."
            >
              Bảo lưu vai trò & mật khẩu cũ
            </span>
          </div>
        </div>
      </div>

      {/* Footer Note */}
      <div className="pt-4 border-t border-slate-100 mt-4">
        <p className="text-xs text-slate-400 leading-normal">
          Phòng Đào tạo thẩm định và cập nhật danh sách chính thức cho từng đợt khóa luận.
        </p>
      </div>
    </div>
  );
};
