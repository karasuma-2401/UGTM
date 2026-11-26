import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Select } from '@/components/ui/select';
import { Avatar } from '@/components/ui/avatar';
import { UserCheck } from 'lucide-react';
import type {
  ReviewerAssignmentEligibilityItemDto,
  ReviewerCandidateContractDto,
} from '../../types/reviewerAssignment.types';

interface ReviewerAssignmentFormCardProps {
  selectedItem: ReviewerAssignmentEligibilityItemDto | null;
  candidates: ReviewerCandidateContractDto[];
  selectedReviewerId: string;
  onSelectReviewerId: (id: string) => void;
  onSubmitAssignment: () => void;
  isSubmitting: boolean;
  canManage: boolean;
  isLoadingCandidates?: boolean;
  candidatesError?: string | null;
  onRetryCandidates?: () => void;
}

export function ReviewerAssignmentFormCard({
  selectedItem,
  candidates,
  selectedReviewerId,
  onSelectReviewerId,
  onSubmitAssignment,
  isSubmitting,
  canManage,
  isLoadingCandidates = false,
  candidatesError,
  onRetryCandidates,
}: ReviewerAssignmentFormCardProps) {
  if (!selectedItem) return null;
  const isAlreadyAssigned = selectedItem.hasActiveAssignment;
  const eligibleCandidates = candidates.filter(
    (candidate) => Boolean(candidate.userId) && candidate.userId !== selectedItem.advisorId,
  );
  const reviewer = selectedReviewerId
    ? eligibleCandidates.find((candidate) => candidate.userId === selectedReviewerId)
    : undefined;
  const canSubmit =
    selectedItem.canAssign &&
    !isAlreadyAssigned &&
    Boolean(reviewer) &&
    canManage &&
    !isSubmitting &&
    !isLoadingCandidates &&
    !candidatesError;

  return (
    <section aria-labelledby="reviewer-form-title" className="space-y-4 p-5">
      <div>
        <h2 id="reviewer-form-title" className="text-base font-semibold text-ink-primary">
          Giảng viên phản biện
        </h2>
        <p className="mt-1 text-xs leading-relaxed text-ink-muted">
          Giảng viên phản biện phải khác giảng viên hướng dẫn của nhóm.
        </p>
      </div>
      {isAlreadyAssigned ? (
        <InlineAlert tone="success" title="Đã phân công">
          Hồ sơ này đã có giảng viên phản biện.
        </InlineAlert>
      ) : !canManage ? (
        <InlineAlert tone="warning">Bạn không có quyền phân công giảng viên phản biện.</InlineAlert>
      ) : candidatesError ? (
        <InlineAlert
          title="Không tải được giảng viên"
          action={
            <Button variant="outline" size="sm" onClick={onRetryCandidates}>
              Thử lại
            </Button>
          }
        >
          {candidatesError}
        </InlineAlert>
      ) : (
        <>
          <Select
            value={selectedReviewerId}
            onChange={onSelectReviewerId}
            searchable
            aria-label="Chọn giảng viên phản biện"
            size="lg"
            placeholder={isLoadingCandidates ? 'Đang tải giảng viên…' : 'Chọn giảng viên phản biện'}
            searchPlaceholder="Tìm tên hoặc mã giảng viên"
            noOptionsMessage="Không tìm thấy giảng viên phù hợp"
            disabled={isLoadingCandidates || isSubmitting || !selectedItem.canAssign}
            options={eligibleCandidates.map((candidate) => ({
              value: candidate.userId,
              label: candidate.fullName,
              description: candidate.userCode || undefined,
              icon: <Avatar name={candidate.fullName} className="size-8 text-[10px]" />,
            }))}
          />
          {!isLoadingCandidates && eligibleCandidates.length === 0 && (
            <p className="text-sm text-ink-muted">Chưa có giảng viên phù hợp để phân công.</p>
          )}
          {reviewer && (
            <p className="text-sm text-ink-body">
              Phân công{' '}
              <strong className="font-semibold text-ink-primary">{reviewer.fullName}</strong> phản
              biện báo cáo v{selectedItem.reportVersionNo} của nhóm này.
            </p>
          )}
          <Button
            type="button"
            onClick={onSubmitAssignment}
            disabled={!canSubmit}
            isLoading={isSubmitting}
            leftIcon={<UserCheck className="size-4" />}
            className="w-full"
          >
            Xác nhận phân công
          </Button>
        </>
      )}
    </section>
  );
}
