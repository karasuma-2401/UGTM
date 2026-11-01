import { Card } from '@/components/ui/card';
import type { AdvisedGroupOption, MilestoneWithReportDto } from '../../types/milestone.types';

interface AdvisorGroupPlanCardProps {
  group?: AdvisedGroupOption | null;
  milestones: MilestoneWithReportDto[];
  selectedMilestone?: MilestoneWithReportDto | null;
}

export function AdvisorGroupPlanCard({
  group,
  milestones,
  selectedMilestone,
}: AdvisorGroupPlanCardProps) {
  const groupCode = group?.groupCode || 'Nhóm KLTN';
  const topicCode = group?.topicCode || 'KLTN';

  const acceptedCount = milestones.filter(
    (m) => m.report?.currentStatus === 'Accepted'
  ).length;
  const revisionCount = milestones.filter(
    (m) => m.report?.currentStatus === 'NeedRevision'
  ).length;
  const submittedCount = milestones.filter(
    (m) => m.report?.currentStatus === 'Submitted'
  ).length;

  const currentActiveMilestone =
    selectedMilestone?.milestone.title ||
    milestones.find((m) => m.report?.currentStatus === 'NeedRevision')?.milestone.title ||
    milestones.find((m) => m.report?.currentStatus === 'Submitted')?.milestone.title ||
    milestones[0]?.milestone.title ||
    'Chưa có mốc';

  let progressSummary = 'Chưa thiết lập mốc';
  if (milestones.length > 0) {
    const parts: string[] = [];
    if (acceptedCount > 0) parts.push(`${acceptedCount} đã đạt`);
    if (revisionCount > 0) parts.push(`${revisionCount} cần sửa`);
    if (submittedCount > 0) parts.push(`${submittedCount} chờ đánh giá`);
    if (parts.length === 0) {
      progressSummary = `${milestones.length} mốc đang chờ nộp`;
    } else {
      progressSummary = parts.join(' - ');
    }
  }

  const latestFile =
    milestones
      .flatMap((m) => m.report?.versions || [])
      .sort(
        (a, b) =>
          new Date(b.submittedAtUtc).getTime() -
          new Date(a.submittedAtUtc).getTime()
      )[0]?.file?.fileName || 'Chưa có tệp nộp';

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <div>
        <h3 className="text-base font-semibold text-slate-900">
          Kế hoạch {groupCode}
        </h3>
        <p className="text-xs text-slate-500 mt-0.5 line-clamp-1">
          {group?.topicTitleVi || 'Chưa có dữ liệu'}
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
          <span className="text-slate-400 block mb-0.5">Mốc đang chọn</span>
          <span className="font-medium text-slate-800">
            {currentActiveMilestone}
          </span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-0.5">Tiến độ nhóm</span>
          <span className="font-medium text-slate-800">
            {progressSummary}
          </span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-0.5">Báo cáo gần nhất</span>
          <span className="font-medium text-slate-800 truncate block">
            {latestFile}
          </span>
        </div>

        <div className="pt-3">
          <span className="text-slate-400 block mb-1">Lưu ý xử lý</span>
          <ul className="text-slate-600 space-y-1 list-disc list-inside">
            <li>Tên mốc và sản phẩm rõ ràng</li>
            <li>Hạn nộp theo kế hoạch nhóm</li>
            <li>Thay đổi mốc được lưu trong lịch sử</li>
          </ul>
        </div>
      </div>
    </Card>
  );
}