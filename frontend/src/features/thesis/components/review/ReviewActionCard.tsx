import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Check, Edit3, X, AlertCircle, Download, CheckCircle2 } from 'lucide-react';
import type { ReviewDecision, ReviewGroupItemDto } from '../../types/review.types';
import type { ReviewMidtermGroupItemDto } from '../../types/midterm.types';

interface ReviewActionCardProps {
  selectedGroup: ReviewGroupItemDto | ReviewMidtermGroupItemDto | null;
  onSubmitReview: (decision: ReviewDecision, feedback: string) => Promise<void>;
  isSubmitting?: boolean;
  disabled?: boolean;
  hasDownloadedFile?: boolean;
  onDownloadFile?: () => void;
  gateName?: string;
}

export function ReviewActionCard({
  selectedGroup,
  onSubmitReview,
  isSubmitting = false,
  disabled = false,
  hasDownloadedFile = true,
  onDownloadFile,
  gateName = 'đề cương',
}: ReviewActionCardProps) {
  const [feedback, setFeedback] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  const isAlreadyReviewed =
    disabled || (Boolean(selectedGroup) && selectedGroup?.currentStatus !== 'Submitted');

  const cannotReview =
    !selectedGroup || isSubmitting || isAlreadyReviewed || !hasDownloadedFile;

  const handleAction = async (decision: ReviewDecision) => {
    setValidationError(null);

    if (!selectedGroup) {
      setValidationError('Vui lòng chọn một hồ sơ để thẩm định.');
      return;
    }

    if (isAlreadyReviewed) {
      setValidationError('Hồ sơ này đã được thẩm định.');
      return;
    }

    if (!hasDownloadedFile) {
      setValidationError(`Vui lòng tải tệp ${gateName} về đối chiếu trước khi thẩm định.`);
      return;
    }

    if ((decision === 'NeedRevision' || decision === 'Rejected') && !feedback.trim()) {
      setValidationError('Vui lòng nhập nhận xét/lý do khi yêu cầu chỉnh sửa hoặc từ chối.');
      return;
    }

    try {
      await onSubmitReview(decision, feedback.trim());
      setFeedback('');
    } catch {
      // Handled in parent
    }
  };

  return (
    <Card className="p-6 bg-surface-card border-line shadow-xs rounded-xl space-y-5">
      <h3 className="text-base font-bold text-ink-primary border-b border-line-subtle pb-3">
        Nhận xét và thẩm định
      </h3>

      {validationError && (
        <div className="p-3 rounded-lg bg-tone-danger-bg border border-tone-danger-border text-tone-danger-strong text-xs flex items-center gap-2">
          <AlertCircle className="size-4 shrink-0 text-tone-danger-icon" />
          <span>{validationError}</span>
        </div>
      )}

      {selectedGroup && isAlreadyReviewed && (
        <div className="p-3.5 rounded-lg bg-surface-subtle border border-line text-ink-secondary text-xs flex items-start gap-2.5">
          <CheckCircle2 className="size-4 shrink-0 text-ink-muted mt-0.5" />
          <div>
            <p className="font-semibold text-ink-heading">
              Hồ sơ đã được thẩm định
            </p>
            <p className="mt-0.5 text-ink-body">
              {selectedGroup.currentStatus === 'Approved' && 'Hồ sơ đã được phê duyệt Đạt. Giai đoạn thực hiện đã mở.'}
              {selectedGroup.currentStatus === 'NeedRevision' && 'Hồ sơ đã được yêu cầu chỉnh sửa và đang chờ sinh viên nộp phiên bản tiếp theo.'}
              {selectedGroup.currentStatus === 'Rejected' && 'Hồ sơ đã bị đánh giá Không đạt.'}
              {selectedGroup.currentStatus !== 'Approved' &&
                selectedGroup.currentStatus !== 'NeedRevision' &&
                selectedGroup.currentStatus !== 'Rejected' &&
                'Hồ sơ hiện tại không ở trạng thái chờ thẩm định.'}
            </p>
          </div>
        </div>
      )}

      {selectedGroup && !isAlreadyReviewed && !hasDownloadedFile && (
        <div className="p-3.5 rounded-lg bg-tone-warning-bg border border-tone-warning-border text-tone-warning-strong text-xs flex items-start gap-2.5">
          <AlertCircle className="size-4 shrink-0 text-tone-warning-icon mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold text-tone-warning-strong">
              Yêu cầu tải tệp {gateName} trước khi thẩm định
            </p>
            <p className="mt-0.5 text-tone-warning-text">
              Vui lòng tải tệp {gateName} về đối chiếu nội dung trước khi nhập nhận xét và ra quyết định thẩm định.
            </p>
            {onDownloadFile && selectedGroup.fileId && (
              <button
                type="button"
                onClick={onDownloadFile}
                className="mt-2 inline-flex items-center gap-1.5 font-semibold text-brand-ink underline decoration-brand-ink/40 hover:decoration-brand-ink dark:decoration-blue-400/40 dark:hover:decoration-blue-400"
              >
                <Download className="size-3.5" />
                Tải tệp {gateName} ngay
              </button>
            )}
          </div>
        </div>
      )}

      {selectedGroup && !isAlreadyReviewed && hasDownloadedFile && (
        <div className="p-3 rounded-lg bg-tone-success-bg border border-tone-success-border text-tone-success-strong text-xs flex items-center gap-2">
          <Check className="size-4 shrink-0 text-tone-success-icon" />
          <span>Đã tải tệp đối chiếu. Bạn có thể nhập nhận xét và thẩm định hồ sơ bên dưới.</span>
        </div>
      )}

      <div className="space-y-1.5">
        <label className="text-xs font-semibold text-ink-secondary">Nhận xét</label>
        <textarea
          value={feedback}
          onChange={(e) => {
            setFeedback(e.target.value);
            if (validationError) setValidationError(null);
          }}
          disabled={cannotReview}
          rows={4}
          placeholder={
            cannotReview
              ? isAlreadyReviewed
                ? 'Hồ sơ đã được thẩm định.'
                : `Vui lòng tải tệp ${gateName} trước để mở khóa ô nhận xét...`
              : 'Bổ sung nhận xét đánh giá và lưu ý cho nhóm sinh viên...'
          }
          className="w-full text-xs p-3 rounded-lg border border-line-input focus:outline-hidden focus:ring-2 focus:ring-primary placeholder:text-ink-subtle disabled:bg-surface-subtle disabled:text-ink-subtle"
        />
      </div>

      <div className="flex flex-wrap items-center gap-3 pt-2">
        <Button
          type="button"
          onClick={() => handleAction('Approved')}
          disabled={cannotReview}
          className="h-10 px-5 text-xs font-semibold bg-brand-solid text-white hover:bg-brand-solid-hover disabled:bg-surface-strong disabled:text-ink-subtle"
        >
          <Check className="size-4 mr-1.5" />
          Đạt
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={() => handleAction('NeedRevision')}
          disabled={cannotReview}
          className="h-10 px-5 text-xs font-semibold border-tone-warning-border text-tone-warning-text hover:bg-tone-warning-bg disabled:border-line disabled:text-ink-subtle dark:disabled:text-ink-muted"
        >
          <Edit3 className="size-4 mr-1.5" />
          Yêu cầu chỉnh sửa
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={() => handleAction('Rejected')}
          disabled={cannotReview}
          className="h-10 px-5 text-xs font-semibold border-tone-danger-border text-tone-danger-text hover:bg-tone-danger-bg disabled:border-line disabled:text-ink-subtle dark:disabled:text-ink-muted"
        >
          <X className="size-4 mr-1.5" />
          Không đạt
        </Button>
      </div>
    </Card>
  );
}
