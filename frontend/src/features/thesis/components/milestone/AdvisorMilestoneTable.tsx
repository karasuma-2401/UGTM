import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';
import type { MilestoneWithReportDto } from '../../types/milestone.types';

interface AdvisorMilestoneTableProps {
  milestones: MilestoneWithReportDto[];
  selectedMilestoneId?: string | null;
  onSelectMilestone: (item: MilestoneWithReportDto) => void;
  onReviewMilestone?: (milestoneId: string) => void;
}

export function AdvisorMilestoneTable({
  milestones,
  selectedMilestoneId,
  onSelectMilestone,
  onReviewMilestone,
}: AdvisorMilestoneTableProps) {
  const formatDeadline = (utcString: string) => {
    try {
      const date = new Date(utcString);
      return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      });
    } catch {
      return utcString;
    }
  };

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900">Mốc thực hiện</h3>
      </div>

      <div className="overflow-x-auto border border-slate-200/80 rounded-lg">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-slate-500 font-medium text-xs border-b border-slate-200/80">
            <tr>
              <th className="py-3 px-4">Nội dung</th>
              <th className="py-3 px-4">Thông tin</th>
              <th className="py-3 px-4">Trạng thái</th>
              <th className="py-3 px-4 text-right">Chi tiết</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {milestones.map((item, index) => {
              const code = String(index + 1).padStart(2, '0');
              const isSelected = selectedMilestoneId === item.milestone.id;
              const status = item.report?.currentStatus || 'NotSubmitted';
              const versionNo = item.report?.currentVersionNo;

              return (
                <tr
                  key={item.milestone.id}
                  onClick={() => onSelectMilestone(item)}
                  className={`hover:bg-slate-50/70 transition-colors cursor-pointer ${
                    isSelected ? 'bg-blue-50/40' : ''
                  }`}
                >
                  <td className="py-3.5 px-4 font-medium text-slate-900">
                    <div className="flex items-center gap-2">
                      <span className="text-slate-500 font-semibold">
                        {code} -
                      </span>
                      <span className="truncate max-w-[220px] md:max-w-[300px]">
                        {item.milestone.title}
                      </span>
                    </div>
                    {item.milestone.description && (
                      <p className="text-xs text-slate-400 mt-0.5 line-clamp-1 pl-7">
                        {item.milestone.description}
                      </p>
                    )}
                  </td>
                  <td className="py-3.5 px-4 text-slate-600 text-xs">
                    {formatDeadline(item.milestone.deadlineUtc)}
                  </td>
                  <td className="py-3.5 px-4">
                    <MilestoneStatusBadge status={status} versionNo={versionNo} />
                  </td>
                  <td className="py-3.5 px-4 text-right" onClick={(e) => e.stopPropagation()}>
                    {status === 'Submitted' ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="h-8 text-xs font-semibold text-primary hover:text-primary hover:bg-blue-50"
                        onClick={() => onReviewMilestone?.(item.milestone.id)}
                      >
                        Đánh giá
                      </Button>
                    ) : status === 'Accepted' || status === 'NeedRevision' ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="h-8 text-xs font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-100"
                        onClick={() => onReviewMilestone?.(item.milestone.id)}
                      >
                        Xem
                      </Button>
                    ) : (
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="h-8 text-xs font-medium text-slate-500 hover:text-slate-800 hover:bg-slate-100"
                        onClick={() => onSelectMilestone(item)}
                      >
                        Sửa deadline
                      </Button>
                    )}
                  </td>
                </tr>
              );
            })}

            {milestones.length === 0 && (
              <tr>
                <td colSpan={4} className="py-8 text-center text-slate-400 text-xs">
                  Chưa có mốc tiến độ nào cho nhóm này. Vui lòng thiết lập mốc đầu tiên ở biểu mẫu bên dưới.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="pt-1">
        <span className="text-xs text-slate-500">
          {milestones.length} mục trong danh sách
        </span>
      </div>
    </Card>
  );
}