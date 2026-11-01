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
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <h3 className="text-base font-bold text-slate-900 border-b border-slate-100 pb-3">
        Nhận xét & thẩm định
      </h3>

      {validationError && (
        <div className="p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 text-xs flex items-center gap-2">
          <AlertCircle className="size-4 shrink-0 text-rose-600" />
          <span>{validationError}</span>
        </div>
      )}

      {selectedGroup && isAlreadyReviewed && (
        <div className="p-3.5 rounded-lg bg-slate-50 border border-slate-200 text-slate-700 text-xs flex items-start gap-2.5">
          <CheckCircle2 className="size-4 shrink-0 text-slate-500 mt-0.5" />
          <div>
            <p className="font-semibold text-slate-800">
              Hồ sơ đã được thẩm định
            </p>
            <p className="mt-0.5 text-slate-600">
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
        <div className="p-3.5 rounded-lg bg-amber-50 border border-amber-200 text-amber-900 text-xs flex items-start gap-2.5">
          <AlertCircle className="size-4 shrink-0 text-amber-600 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold text-amber-800">
              Yêu cầu tải tệp {gateName} trước khi thẩm định
            </p>
            <p className="mt-0.5 text-amber-700">
              Vui lòng tải tệp {gateName} về đối chiếu nội dung trước khi nhập nhận xét và ra quyết định thẩm định.
            </p>
            {onDownloadFile && selectedGroup.fileId && (
              <button
                type="button"
                onClick={onDownloadFile}
                className="mt-2 inline-flex items-center gap-1.5 font-semibold text-[#2054A5] underline decoration-[#2054A5]/40 hover:decoration-[#2054A5]"
              >
                <Download className="size-3.5" />
                Tải tệp {gateName} ngay
              </button>
            )}
          </div>
        </div>
      )}

      {selectedGroup && !isAlreadyReviewed && hasDownloadedFile && (
        <div className="p-3 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs flex items-center gap-2">
          <Check className="size-4 shrink-0 text-emerald-600" />
          <span>Đã tải tệp đối chiếu. Bạn có thể nhập nhận xét và thẩm định hồ sơ bên dưới.</span>
        </div>
      )}

      <div className="space-y-1.5">
        <label className="text-xs font-semibold text-slate-700">Nhận xét</label>
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
          className="w-full text-xs p-3 rounded-lg border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-[#2054A5] placeholder:text-slate-400 disabled:bg-slate-50 disabled:text-slate-400"
        />
      </div>

      <div className="flex flex-wrap items-center gap-3 pt-2">
        <Button
          type="button"
          onClick={() => handleAction('Approved')}
          disabled={cannotReview}
          className="h-10 px-5 text-xs font-semibold bg-[#2054A5] text-white hover:bg-[#1a4485] disabled:bg-slate-200 disabled:text-slate-400"
        >
          <Check className="size-4 mr-1.5" />
          Đạt
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={() => handleAction('NeedRevision')}
          disabled={cannotReview}
          className="h-10 px-5 text-xs font-semibold border-amber-300 text-amber-700 hover:bg-amber-50 disabled:border-slate-200 disabled:text-slate-400"
        >
          <Edit3 className="size-4 mr-1.5" />
          Yêu cầu chỉnh sửa
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={() => handleAction('Rejected')}
          disabled={cannotReview}
          className="h-10 px-5 text-xs font-semibold border-rose-300 text-rose-700 hover:bg-rose-50 disabled:border-slate-200 disabled:text-slate-400"
        >
          <X className="size-4 mr-1.5" />
          Không đạt
        </Button>
      </div>
    </Card>
  );
}
