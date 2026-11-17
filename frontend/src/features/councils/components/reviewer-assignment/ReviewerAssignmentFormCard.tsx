import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { UserCheck, AlertCircle } from 'lucide-react';
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
}: ReviewerAssignmentFormCardProps) {
  const isAlreadyAssigned = Boolean(selectedItem?.hasActiveAssignment);
  const advisorName = selectedItem?.advisorFullName || 'Chưa xác định';
  const canSubmit =
    Boolean(selectedItem) &&
    !isAlreadyAssigned &&
    Boolean(selectedReviewerId) &&
    canManage &&
    !isSubmitting;

  return (
    <Card className="p-6 bg-surface-card border-line shadow-xs rounded-xl flex flex-col justify-between space-y-6">
      <div className="space-y-4">
        <h2 className="text-base font-bold text-ink-primary">Giảng viên</h2>

        {!selectedItem ? (
          <div className="py-8 text-center text-xs text-ink-subtle">
            Vui lòng chọn một hồ sơ từ danh sách bên trên để phân công.
          </div>
        ) : (
          <div className="space-y-3">
            <div>
              <label
                htmlFor="reviewer-select"
                className="block text-xs font-semibold text-ink-secondary mb-1.5"
              >
                GVPB
              </label>

              {isLoadingCandidates ? (
                <div className="w-full h-10 px-3 py-2 bg-surface-subtle border border-line rounded-lg text-xs text-ink-subtle flex items-center">
                  Đang tải danh sách giảng viên...
                </div>
              ) : isAlreadyAssigned ? (
                <div className="w-full p-3 bg-surface-subtle border border-line rounded-lg text-xs text-ink-body flex items-center gap-2">
                  <UserCheck className="size-4 text-tone-success-icon shrink-0" />
                  <span>Hồ sơ này đã có Giảng viên phản biện được phân công.</span>
                </div>
              ) : (
                <select
                  id="reviewer-select"
                  value={selectedReviewerId}
                  onChange={(e) => onSelectReviewerId(e.target.value)}
                  disabled={!canManage || isSubmitting}
                  className="w-full h-10 px-3 py-2 bg-surface-card border border-line-input rounded-lg text-sm text-ink-secondary focus:outline-hidden focus:ring-2 focus:ring-blue-500 transition-all cursor-pointer disabled:bg-surface-subtle disabled:cursor-not-allowed"
                >
                  <option value="">-- Chọn Giảng viên phản biện --</option>
                  {candidates.map((candidate) => (
                    <option key={candidate.id} value={candidate.id}>
                      {candidate.fullName} {candidate.userCode ? `(${candidate.userCode})` : ''}
                    </option>
                  ))}
                </select>
              )}

              <p className="mt-1.5 text-xs text-ink-muted">
                Phải khác GVHD: {advisorName}
              </p>
            </div>

            {!canManage && (
              <div className="p-3 rounded-lg bg-tone-warning-bg border border-tone-warning-border text-tone-warning-strong text-xs flex items-center gap-2">
                <AlertCircle className="size-4 text-tone-warning-icon shrink-0" />
                <span>Bạn không có quyền phân công Giảng viên phản biện.</span>
              </div>
            )}
          </div>
        )}
      </div>

      <div>
        <Button
          type="button"
          onClick={onSubmitAssignment}
          disabled={!canSubmit}
          isLoading={isSubmitting}
          className="w-auto px-6 h-10 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg text-sm transition-colors shadow-xs"
        >
          Xác nhận phân công
        </Button>
      </div>
    </Card>
  );
}