import { Card } from '@/components/ui/card';
import type { ReviewerAssignmentEligibilityItemDto } from '../../types/reviewerAssignment.types';
import type { StudentGroupDetailDto } from '@/features/thesis';

interface ReviewerAssignmentDetailCardProps {
  selectedItem: ReviewerAssignmentEligibilityItemDto | null;
  groupDetail?: StudentGroupDetailDto | null;
  batchName?: string;
  groupIndex?: number;
  isLoading?: boolean;
}

export function ReviewerAssignmentDetailCard({
  selectedItem,
  groupDetail,
  batchName,
  groupIndex = 0,
  isLoading = false,
}: ReviewerAssignmentDetailCardProps) {
  const groupLabel =
    groupDetail?.groupCode || `N${String(groupIndex + 1).padStart(2, '0')}`;
  const topicCode =
    groupDetail?.topicCode ||
    (selectedItem?.topicId ? `KLTN-${selectedItem.topicId.slice(0, 8).toUpperCase()}` : 'KLTN');
  const advisorName =
    selectedItem?.advisorFullName ||
    groupDetail?.advisor?.fullName ||
    'Chưa xác định';
  const displayBatch =
    batchName || groupDetail?.batchName || 'Đợt 1 · HK1 2026–2027';

  return (
    <Card className="p-6 bg-surface-card border-line shadow-xs rounded-xl space-y-6">
      <h2 className="text-base font-bold text-ink-primary">
        Hồ sơ {groupLabel}
      </h2>

      {!selectedItem ? (
        <div className="py-8 text-center text-xs text-ink-subtle">
          Chưa chọn hồ sơ nào.
        </div>
      ) : (
        <div className="space-y-5 text-xs">
          <div>
            <span className="block text-ink-subtle font-medium mb-1">Hồ sơ</span>
            <p className="text-ink-heading font-medium">
              {topicCode} · Nhóm {groupLabel}
            </p>
          </div>

          <div>
            <span className="block text-ink-subtle font-medium mb-1">Sinh viên</span>
            {isLoading ? (
              <p className="text-ink-subtle">Đang tải thông tin sinh viên...</p>
            ) : groupDetail?.members && groupDetail.members.length > 0 ? (
              <div className="space-y-1">
                {groupDetail.members.map((member) => (
                  <p
                    key={member.userCode || member.studentId || member.fullName}
                    className="text-ink-heading font-medium"
                  >
                    {member.fullName} · {member.userCode || 'Chưa có MSSV'}
                  </p>
                ))}
              </div>
            ) : (
              <p className="text-ink-muted">Chưa có thông tin thành viên.</p>
            )}
          </div>

          <div>
            <span className="block text-ink-subtle font-medium mb-1">GVHD</span>
            <p className="text-ink-heading font-medium">{advisorName}</p>
          </div>

          <div>
            <span className="block text-ink-subtle font-medium mb-1">Đợt</span>
            <p className="text-ink-heading font-medium">{displayBatch}</p>
          </div>

          <div className="pt-2 border-t border-line-subtle">
            <span className="block text-ink-subtle font-medium mb-2">Lưu ý xử lý</span>
            <ul className="space-y-1.5 text-ink-body">
              <li className="flex items-center gap-1.5">
                <span className="size-1 rounded-full bg-slate-400 shrink-0" />
                <span>GVHD đã đồng ý chuyển</span>
              </li>
              <li className="flex items-center gap-1.5">
                <span className="size-1 rounded-full bg-slate-400 shrink-0" />
                <span>GVPB phải khác GVHD</span>
              </li>
              <li className="flex items-center gap-1.5">
                <span className="size-1 rounded-full bg-slate-400 shrink-0" />
                <span>Chọn người phù hợp lĩnh vực</span>
              </li>
            </ul>
          </div>
        </div>
      )}
    </Card>
  );
}