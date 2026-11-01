import { Card } from '@/components/ui/card';
import type { ProgressMilestoneDto } from '../../types/milestone.types';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';

interface AdvisorMilestoneSidebarCardProps {
  milestone?: ProgressMilestoneDto | null;
  milestoneIndex?: number;
  groupDetail?: StudentGroupDetailDto | null;
  versionNo?: number;
  fileName?: string | null;
  gitUrl?: string | null;
}

export function AdvisorMilestoneSidebarCard({
  milestone,
  milestoneIndex = 0,
  groupDetail,
  versionNo = 1,
  fileName,
  gitUrl,
}: AdvisorMilestoneSidebarCardProps) {
  const code = String(milestoneIndex + 1).padStart(2, '0');
  const groupCode = groupDetail?.groupCode || 'Nhóm KLTN';
  const topicCode = groupDetail?.topicCode || 'KLTN';
  const advisorName = groupDetail?.advisor?.fullName
    ? `${groupDetail.advisor.academicTitle || 'GV.'} ${groupDetail.advisor.fullName}`.trim()
    : 'Chưa cập nhật';
  const batchName = groupDetail?.batchName || 'Đợt KLTN';

  const shortGitUrl = gitUrl
    ? gitUrl.replace(/^https?:\/\//, '').replace(/\/$/, '')
    : 'Chưa cập nhật';

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <div>
        <h3 className="text-base font-semibold text-slate-900">
          Sản phẩm mốc {code}
        </h3>
        {milestone?.title && (
          <p className="text-xs text-slate-500 mt-0.5 line-clamp-1">
            {milestone.title}
          </p>
        )}
      </div>

      <div className="space-y-4 text-xs divide-y divide-slate-100">
        <div className="pt-1 first:pt-0">
          <span className="text-slate-400 block mb-0.5">Hồ sơ</span>
          <span className="font-medium text-slate-800">
            {topicCode} - {groupCode}
          </span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-1">Sinh viên</span>
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
          <span className="text-slate-400 block mb-1">Tài liệu đối chiếu</span>
          <div className="space-y-1.5 text-slate-700 bg-slate-50 p-2.5 rounded-lg border border-slate-100">
            <div>
              <span className="text-slate-400 block text-[11px]">Báo cáo</span>
              <span className="font-medium text-slate-800 truncate block">
                {fileName || 'Chưa có tệp'}
              </span>
            </div>
            <div>
              <span className="text-slate-400 block text-[11px]">Phiên bản</span>
              <span className="font-medium text-slate-800">
                v{versionNo} - Nhóm đã nộp
              </span>
            </div>
            <div>
              <span className="text-slate-400 block text-[11px]">Kho mã nguồn</span>
              <span className="font-medium text-slate-800 truncate block">
                {shortGitUrl}
              </span>
            </div>
          </div>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-1">Lưu ý xử lý</span>
          <ul className="text-slate-600 space-y-1 list-disc list-inside">
            <li>Đối chiếu yêu cầu mốc</li>
            <li>Đánh giá kết quả và mã nguồn</li>
            <li>Nhận xét rõ nội dung cần sửa</li>
          </ul>
        </div>
      </div>
    </Card>
  );
}

