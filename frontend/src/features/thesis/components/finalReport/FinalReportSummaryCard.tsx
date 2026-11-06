import React from 'react';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';

interface FinalReportSummaryCardProps {
  groupDetail: StudentGroupDetailDto | null | undefined;
}

export const FinalReportSummaryCard: React.FC<FinalReportSummaryCardProps> = ({
  groupDetail,
}) => {
  const topicCode = groupDetail?.topicCode || 'KLTN-2026-084';
  const groupCode = groupDetail?.groupCode || 'Nhóm N01';
  const dossierLabel = `${topicCode} · ${groupCode}`;

  const advisorName = groupDetail?.advisor?.fullName
    ? `${groupDetail.advisor.academicTitle || 'GV.'} ${groupDetail.advisor.fullName}`.trim()
    : 'TS. Trần Hoàng Long';

  const batchName = groupDetail?.batchName || 'Đợt 1 · HK1 2026–2027';

  const members =
    groupDetail?.members && groupDetail.members.length > 0
      ? groupDetail.members
      : [
          { studentId: '1', fullName: 'Nguyễn Minh Đức', userCode: '24521603' },
          { studentId: '2', fullName: 'Trần Mai Anh', userCode: '24521604' },
        ];

  return (
    <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs space-y-5">
      <h3 className="text-base font-bold text-slate-800">Hồ sơ cuối kỳ</h3>

      <div className="space-y-4 text-sm divide-y divide-slate-100">
        <div className="pt-2 first:pt-0">
          <p className="text-xs font-medium text-slate-400 mb-1">Hồ sơ</p>
          <p className="font-semibold text-slate-800">{dossierLabel}</p>
        </div>

        <div className="pt-3">
          <p className="text-xs font-medium text-slate-400 mb-1">Sinh viên</p>
          <div className="space-y-1">
            {members.map((m) => (
              <p key={m.studentId} className="font-semibold text-slate-800">
                {m.fullName} {m.userCode ? `· ${m.userCode}` : ''}
              </p>
            ))}
          </div>
        </div>

        <div className="pt-3">
          <p className="text-xs font-medium text-slate-400 mb-1">GVHD</p>
          <p className="font-semibold text-slate-800">{advisorName}</p>
        </div>

        <div className="pt-3">
          <p className="text-xs font-medium text-slate-400 mb-1">Đợt</p>
          <p className="font-semibold text-slate-800">{batchName}</p>
        </div>

        <div className="pt-3">
          <p className="text-xs font-medium text-slate-400 mb-1">Trình tự</p>
          <p className="text-xs leading-relaxed text-slate-700">
            GVHD chấm và xác nhận chuyển phản biện → Khoa phân công GVPB → GVPB chấm và kết luận.
          </p>
        </div>

        <div className="pt-3">
          <p className="text-xs font-medium text-slate-400 mb-1">Hạn nộp</p>
          <p className="text-xs font-semibold text-blue-600">
            Theo lịch phản biện · Tuần 11–18
          </p>
        </div>

        <div className="pt-3">
          <p className="text-xs font-medium text-slate-400 mb-1.5">Lưu ý xử lý</p>
          <ul className="text-xs text-slate-600 space-y-1 list-disc pl-4 leading-relaxed">
            <li>Đã được tiếp tục sau giữa kỳ</li>
            <li>Báo cáo toàn văn và sản phẩm đi kèm</li>
            <li>GVHD đánh giá trước khi Khoa phân công phản biện</li>
          </ul>
        </div>
      </div>
    </div>
  );
};