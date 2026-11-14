import React from 'react';

export const SynthesisPrinciplesCard: React.FC = () => {
  return (
    <section
      aria-label="Nguyên tắc tổng hợp"
      className="rounded-2xl border border-slate-200/80 bg-white px-5 py-4 sm:px-6 shadow-xs"
    >
      <h2 className="text-sm font-semibold text-slate-900">Nguyên tắc tổng hợp</h2>
      <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6">
        <div className="space-y-1">
          <h3 className="text-xs font-semibold text-slate-800">Thiếu điểm và không áp dụng</h3>
          <p className="text-xs text-slate-500 leading-relaxed">
            Đầu điểm bắt buộc chưa chấm hoặc biên bản Hội đồng chưa khóa sẽ chặn tổng hợp. GVPB
            kiêm ủy viên chỉ chấm phiếu phản biện, không phát sinh thêm đầu điểm Hội đồng.
          </p>
        </div>
        <div className="space-y-1">
          <h3 className="text-xs font-semibold text-slate-800">Không công nhận và công bố</h3>
          <p className="text-xs text-slate-500 leading-relaxed">
            Điểm bị loại theo biên bản được lưu vết nhưng loại đồng thời điểm và hệ số khỏi phép
            tính. Kết quả chính thức chỉ mở cho sinh viên sau khi Khoa nghiệm thu lưu chiểu và công
            bố.
          </p>
        </div>
      </div>
    </section>
  );
};
