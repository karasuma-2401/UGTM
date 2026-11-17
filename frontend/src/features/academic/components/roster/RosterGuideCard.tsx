import React from 'react';

export const RosterGuideCard: React.FC = () => {
  return (
    <div className="rounded-2xl border border-line bg-surface-card p-6 shadow-xs flex flex-col justify-between h-full transition-all hover:border-line-strong">
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-base font-bold text-ink-primary tracking-tight">
            Quy chuẩn danh sách
          </h2>
          <span className="rounded-full border border-line-input bg-surface-muted px-2.5 py-1 text-xs font-semibold text-ink-secondary shadow-2xs">
            Quy định đào tạo
          </span>
        </div>

        {/* Spec Overview - Fluent Metric Style */}
        <div className="grid grid-cols-2 gap-3">
          <div className="rounded-xl border border-line bg-surface-subtle/70 p-3 transition-all hover:border-tone-success-border hover:bg-tone-success-bg">
            <span className="text-[11px] font-medium text-ink-muted block mb-0.5">
              Định dạng tệp
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-base font-bold text-tone-success-text">Excel</span>
              <span className="text-xs font-semibold text-tone-success-icon">.xlsx</span>
            </div>
            <span className="text-[11px] text-ink-subtle block mt-0.5">
              Dung lượng tối đa 20 MB
            </span>
          </div>

          <div className="rounded-xl border border-line bg-surface-subtle/70 p-3 transition-all">
            <span className="text-[11px] font-medium text-ink-muted block mb-0.5">
              Cột bắt buộc
            </span>
            <div className="flex items-baseline gap-1">
              <span className="text-base font-bold text-ink-primary">4</span>
              <span className="text-xs text-ink-muted">trường dữ liệu</span>
            </div>
            <span className="text-[11px] text-ink-subtle block mt-0.5">
              Theo tệp mẫu chuẩn
            </span>
          </div>
        </div>

        {/* Required Columns Chips */}
        <div className="rounded-xl border border-line bg-surface-subtle/70 p-3.5 space-y-2">
          <span className="text-xs font-semibold text-ink-secondary block">
            Cột dữ liệu bắt buộc trong tệp:
          </span>
          <div className="flex flex-wrap gap-1.5">
            <span className="inline-flex items-center rounded-lg border border-line-input bg-surface-card px-2.5 py-1 text-xs font-medium text-ink-heading shadow-2xs">
              Mã số định danh
            </span>
            <span className="inline-flex items-center rounded-lg border border-line-input bg-surface-card px-2.5 py-1 text-xs font-medium text-ink-heading shadow-2xs">
              Họ và tên
            </span>
            <span className="inline-flex items-center rounded-lg border border-line-input bg-surface-card px-2.5 py-1 text-xs font-medium text-ink-heading shadow-2xs">
              Hộp thư @uit.edu.vn
            </span>
            <span className="inline-flex items-center rounded-lg border border-line-input bg-surface-card px-2.5 py-1 text-xs font-medium text-ink-heading shadow-2xs">
              Khoa / Đơn vị
            </span>
          </div>
        </div>

        {/* Policy Rules Key-Value List - Clean single-line layout */}
        <div className="rounded-xl border border-line bg-surface-subtle/70 p-3.5 space-y-2.5 text-xs">
          <div className="flex items-center justify-between gap-2">
            <span className="text-ink-muted shrink-0 font-medium">Sinh viên:</span>
            <span
              className="font-semibold text-ink-heading text-right whitespace-nowrap"
              title="Sinh viên tự kích hoạt tài khoản theo mã số và email trường"
            >
              Tự kích hoạt qua email trường
            </span>
          </div>
          <div className="flex items-center justify-between gap-2 pt-2 border-t border-line/80">
            <span className="text-ink-muted shrink-0 font-medium">Giảng viên:</span>
            <span
              className="font-semibold text-ink-heading text-right whitespace-nowrap"
              title="Giảng viên sử dụng tài khoản công vụ do Nhà trường cấp sẵn"
            >
              Dùng tài khoản công vụ cấp sẵn
            </span>
          </div>
          <div className="flex items-center justify-between gap-2 pt-2 border-t border-line/80">
            <span className="text-ink-muted shrink-0 font-medium">Phân quyền:</span>
            <span
              className="font-semibold text-ink-heading text-right whitespace-nowrap"
              title="Tài khoản đã tồn tại được bảo lưu mật khẩu và vai trò cũ. Phân công Hội đồng khóa luận được quản lý độc lập."
            >
              Bảo lưu vai trò và mật khẩu cũ
            </span>
          </div>
        </div>
      </div>

      {/* Footer Note */}
      <div className="pt-4 border-t border-line-subtle mt-4">
        <p className="text-xs text-ink-subtle leading-normal">
          Phòng Đào tạo thẩm định và cập nhật danh sách chính thức cho từng đợt khóa luận.
        </p>
      </div>
    </div>
  );
};
