import { Avatar } from '@/components/ui/avatar';
import { StatusBadge } from '@/components/ui/status-badge';
import type { ReviewerAssignmentEligibilityItemDto } from '../../types/reviewerAssignment.types';
import type { StudentGroupDetailDto } from '@/features/thesis';

interface ReviewerAssignmentDetailCardProps {
  selectedItem: ReviewerAssignmentEligibilityItemDto | null;
  groupDetail?: StudentGroupDetailDto | null;
  batchName?: string;
  isLoading?: boolean;
}

export function ReviewerAssignmentDetailCard({
  selectedItem,
  groupDetail,
  batchName,
  isLoading,
}: ReviewerAssignmentDetailCardProps) {
  if (!selectedItem)
    return (
      <div className="p-6 text-sm leading-relaxed text-ink-muted">
        Chọn một hồ sơ trong danh sách để xem thông tin và phân công giảng viên phản biện.
      </div>
    );
  return (
    <section className="space-y-4 p-5" aria-label="Hồ sơ đang chọn">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-base font-semibold text-ink-primary">Hồ sơ đang chọn</h2>
        <StatusBadge tone={selectedItem.hasActiveAssignment ? 'success' : 'warning'}>
          {selectedItem.hasActiveAssignment ? 'Đã phân công' : 'Chờ phân công'}
        </StatusBadge>
      </div>
      <div>
        <p className="font-semibold leading-relaxed text-ink-primary">
          {selectedItem.topicTitleVi}
        </p>
        <p className="mt-1 text-xs text-ink-muted" title={selectedItem.groupId}>
          {groupDetail?.groupCode || `Nhóm ${selectedItem.groupId.slice(0, 8).toUpperCase()}`}
        </p>
      </div>
      <dl className="space-y-3 text-sm">
        <div className="flex items-start justify-between gap-4">
          <dt className="shrink-0 text-ink-muted">Đợt khóa luận</dt>
          <dd className="text-right font-medium text-ink-body">
            {batchName || groupDetail?.batchName || 'Chưa có thông tin'}
          </dd>
        </div>
        <div className="flex items-start justify-between gap-4">
          <dt className="shrink-0 text-ink-muted">Báo cáo</dt>
          <dd className="font-medium text-ink-body">Phiên bản {selectedItem.reportVersionNo}</dd>
        </div>
        <div>
          <dt className="mb-2 text-ink-muted">Giảng viên hướng dẫn</dt>
          <dd className="flex items-center gap-2.5">
            <Avatar name={selectedItem.advisorFullName || '?'} className="size-8 text-[10px]" />
            <span className="font-medium text-ink-body">
              {selectedItem.advisorFullName || 'Chưa có thông tin'}
            </span>
          </dd>
        </div>
        <div>
          <dt className="mb-2 text-ink-muted">Sinh viên</dt>
          <dd>
            {isLoading ? (
              <p className="text-ink-muted" role="status">
                Đang tải thành viên…
              </p>
            ) : groupDetail?.members?.length ? (
              <ul className="space-y-3">
                {groupDetail.members.map((member) => (
                  <li
                    key={member.studentId || member.userCode}
                    className="flex items-center gap-2.5"
                  >
                    <Avatar name={member.fullName} className="size-8 text-[10px]" />
                    <div>
                      <p className="font-medium text-ink-body">{member.fullName}</p>
                      <p className="text-xs text-ink-muted">{member.userCode || 'Chưa có MSSV'}</p>
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-ink-muted">Chưa có thông tin thành viên.</p>
            )}
          </dd>
        </div>
      </dl>
    </section>
  );
}
