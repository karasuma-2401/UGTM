import { Card } from '@/components/ui/card';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import { Separator } from '@/components/ui/separator';

interface SyllabusTopicInfoCardProps {
  detail?: StudentGroupDetailDto;
  isLoading?: boolean;
}

export function SyllabusTopicInfoCard({
  detail,
  isLoading = false,
}: SyllabusTopicInfoCardProps) {
  if (isLoading) {
    return (
      <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4 animate-pulse">
        <div className="h-5 bg-slate-200 rounded w-1/3 mb-4" />
        <div className="space-y-3">
          <div className="h-4 bg-slate-100 rounded w-1/2" />
          <div className="h-4 bg-slate-100 rounded w-3/4" />
          <div className="h-4 bg-slate-100 rounded w-2/3" />
        </div>
      </Card>
    );
  }

  const topicCode = detail?.topicCode || 'KLTN';
  const groupCode = detail?.groupCode || 'Nhóm';
  const batchName = detail?.batchName || 'Học kỳ hiện tại';
  const advisorName = detail?.advisor?.fullName
    ? `${detail.advisor.academicTitle || 'GV.'} ${detail.advisor.fullName}`
    : 'Chưa phân công';

  const members = detail?.members || [];

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <h3 className="text-base font-semibold text-slate-900 border-b border-slate-100 pb-3">
        Hồ sơ đề cương
      </h3>

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
          Hồ sơ
        </p>
        <p className="text-sm font-semibold text-slate-800">
          {topicCode} · {groupCode}
        </p>
        {detail?.topicTitleVi && (
          <p className="text-xs text-slate-500 mt-0.5 line-clamp-2">
            {detail.topicTitleVi}
          </p>
        )}
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1.5">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
          Sinh viên
        </p>
        <div className="space-y-1">
          {members.map((member) => (
            <p key={member.studentId} className="text-sm text-slate-700">
              {member.fullName} - <span className="text-slate-500 font-mono text-xs">{member.userCode}</span>
            </p>
          ))}
        </div>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
          GVHD
        </p>
        <p className="text-sm font-medium text-slate-800">{advisorName}</p>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
          Đợt
        </p>
        <p className="text-sm text-slate-700">{batchName}</p>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
          Thẩm định
        </p>
        <p className="text-xs text-slate-600 leading-relaxed">
          Văn phòng Khoa: Đạt / Yêu cầu chỉnh sửa / Không đạt. GVHD được xem đề cương của nhóm mình.
        </p>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
          Hạn nộp
        </p>
        <p className="text-sm font-semibold text-slate-800">Cuối tuần 2</p>
        <p className="text-xs text-slate-500">
          Theo kế hoạch đợt và mốc của nhóm.
        </p>
      </div>

      <Separator className="bg-slate-100" />

      <div className="space-y-2">
        <p className="text-xs font-medium text-slate-400 uppercase tracking-wider">
          Cần kiểm tra
        </p>
        <ul className="text-xs text-slate-600 space-y-1.5 list-disc list-inside">
          <li>Đúng tên đề tài đã đăng ký</li>
          <li>Có mục tiêu, phương pháp và kế hoạch thực hiện</li>
          <li>Khoa thẩm định sau khi nhóm nộp</li>
        </ul>
      </div>
    </Card>
  );
}