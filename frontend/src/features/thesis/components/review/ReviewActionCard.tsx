import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Check, Edit3, X, AlertCircle } from 'lucide-react';
import type { ReviewDecision, ReviewGroupItemDto } from '../../types/review.types';

interface ReviewActionCardProps {
  selectedGroup: ReviewGroupItemDto | null;
  onSubmitReview: (decision: ReviewDecision, feedback: string) => Promise<void>;
  isSubmitting?: boolean;
}

export function ReviewActionCard({
  selectedGroup,
  onSubmitReview,
  isSubmitting = false,
}: ReviewActionCardProps) {
  const [feedback, setFeedback] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleAction = async (decision: ReviewDecision) => {
    setValidationError(null);

    if (!selectedGroup) {
      setValidationError('Vui lòng chọn một hồ sơ để thẩm định.');
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

  const isAlreadyFinalized =
    selectedGroup?.currentStatus === 'Approved' || selectedGroup?.currentStatus === 'Rejected';

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

      {isAlreadyFinalized && (
        <div className="p-3 rounded-lg bg-slate-50 border border-slate-200 text-slate-600 text-xs">
          Hồ sơ này đã có kết luận thẩm định cuối cùng ({selectedGroup?.currentStatus === 'Approved' ? 'Đạt' : 'Không đạt'}).
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
          disabled={!selectedGroup || isSubmitting || isAlreadyFinalized}
          rows={4}
          placeholder="Bổ sung phương pháp đánh giá và dữ liệu thực nghiệm..."
          className="w-full text-xs p-3 rounded-lg border border-slate-200 focus:outline-hidden focus:ring-2 focus:ring-[#2054A5] placeholder:text-slate-400 disabled:bg-slate-50 disabled:text-slate-400"
        />
      </div>

      <div className="flex flex-wrap items-center gap-3 pt-2">
        <Button
          type="button"
          onClick={() => handleAction('Approved')}
          disabled={!selectedGroup || isSubmitting || isAlreadyFinalized}
          className="h-10 px-5 text-xs font-semibold bg-[#2054A5] text-white hover:bg-[#1a4485]"
        >
          <Check className="size-4 mr-1.5" />
          Đạt
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={() => handleAction('NeedRevision')}
          disabled={!selectedGroup || isSubmitting || isAlreadyFinalized}
          className="h-10 px-5 text-xs font-semibold border-amber-300 text-amber-700 hover:bg-amber-50"
        >
          <Edit3 className="size-4 mr-1.5" />
          Yêu cầu chỉnh sửa
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={() => handleAction('Rejected')}
          disabled={!selectedGroup || isSubmitting || isAlreadyFinalized}
          className="h-10 px-5 text-xs font-semibold border-rose-300 text-rose-700 hover:bg-rose-50"
        >
          <X className="size-4 mr-1.5" />
          Không đạt
        </Button>
      </div>
    </Card>
  );
}