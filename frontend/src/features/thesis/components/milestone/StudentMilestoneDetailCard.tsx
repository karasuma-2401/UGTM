import { Card } from '@/components/ui/card';
import type { ProgressMilestoneDto, ProgressReportHistoryDto } from '../../types/milestone.types';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';

interface StudentMilestoneDetailCardProps {
  milestone?: ProgressMilestoneDto | null;
  report?: ProgressReportHistoryDto | null;
  groupDetail?: StudentGroupDetailDto | null;
  milestoneIndex?: number;
}

export function StudentMilestoneDetailCard({
  milestone,
  report,
  groupDetail,
  milestoneIndex = 0,
}: StudentMilestoneDetailCardProps) {
  const code = String(milestoneIndex + 1).padStart(2, '0');
  const groupCode = groupDetail?.groupCode || 'Nhóm KLTN';
  const topicCode = groupDetail?.topicCode || 'KLTN';
  const advisorName = groupDetail?.advisor?.fullName
    ? `${groupDetail.advisor.academicTitle || 'GV.'} ${groupDetail.advisor.fullName}`.trim()
    : 'Chưa cập nhật';
  const batchName = groupDetail?.batchName || 'Đợt KLTN';

  const formatDeadline = (utcString?: string) => {
    if (!utcString) return 'Chưa cập nhật';
    try {
      const date = new Date(utcString);
      return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return utcString;
    }
  };

  const latestReview = report?.versions
    ?.slice()
    ?.reverse()
    ?.find((v) => Boolean(v.review?.feedback))?.review;

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <div>
        <h3 className="text-base font-semibold text-slate-900">
          Mốc {code} - {milestone?.title || 'Chưa chọn mốc'}
        </h3>
        <p className="text-xs text-slate-500 mt-0.5 line-clamp-2">
          {milestone?.description || groupDetail?.topicTitleVi || 'Chưa có dữ liệu'}
        </p>
      </div>

      <div className="space-y-4 text-xs divide-y divide-slate-100">
        <div className="pt-1 first:pt-0">
          <span className="text-slate-400 block mb-0.5">Hồ sơ</span>
          <span className="font-medium text-slate-800">
            {topicCode} - {groupCode}
          </span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-1">Sinh viên thực hiện</span>
          <div className="space-y-1">
            {groupDetail?.members && groupDetail.members.length > 0 ? (
              groupDetail.members.map((m) => (
                <div key={m.studentId} className="font-medium text-slate-800">
                  {m.fullName} {m.userCode ? `- ${m.userCode}` : ''}
                </div>
              ))
            ) : (
              <span className="text-slate-500">Chưa có dữ liệu</span>
            )}
          </div>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-0.5">GVHD</span>
          <span className="font-medium text-slate-800">{advisorName}</span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-0.5">Đợt</span>
          <span className="font-medium text-slate-800">{batchName}</span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-1">Nhận xét GVHD</span>
          <p className="text-slate-700 bg-slate-50 p-2.5 rounded-lg border border-slate-100 leading-relaxed">
            {latestReview?.feedback ||
              'Chưa có nhận xét từ GVHD cho mốc này. Vui lòng nộp báo cáo để GVHD đánh giá.'}
          </p>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-0.5">Hạn nộp</span>
          <span className="font-medium text-slate-800">
            {formatDeadline(milestone?.deadlineUtc)} - Theo mốc GVHD
          </span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-1">Lưu ý xử lý</span>
          <ul className="text-slate-600 space-y-1 list-disc list-inside">
            <li>Đính kèm báo cáo đúng định dạng PDF/DOC</li>
            <li>Cung cấp link kho mã nguồn chính xác</li>
            <li>Nộp phiên bản mới để lưu giữ toàn bộ lịch sử</li>
          </ul>
        </div>
      </div>
    </Card>
  );
}