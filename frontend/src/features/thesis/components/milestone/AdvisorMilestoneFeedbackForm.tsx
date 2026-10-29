import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { AlertCircle, CheckCircle2, RotateCcw } from 'lucide-react';

interface AdvisorMilestoneFeedbackFormProps {
  feedback: string;
  onFeedbackChange: (feedback: string) => void;
  onAccept: () => void;
  onNeedRevision: () => void;
  isSubmitting?: boolean;
  disabled?: boolean;
  disabledReason?: string | null;
  currentStatus?: string;
}

export function AdvisorMilestoneFeedbackForm({
  feedback,
  onFeedbackChange,
  onAccept,
  onNeedRevision,
  isSubmitting = false,
  disabled = false,
  disabledReason,
  currentStatus,
}: AdvisorMilestoneFeedbackFormProps) {
  const isAccepted = currentStatus === 'Accepted';
  const isNeedRevision = currentStatus === 'NeedRevision';

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-5">
      <div>
        <h3 className="text-base font-semibold text-slate-900">Phản hồi</h3>
      </div>

      {disabled && disabledReason && (
        <div className="p-3.5 rounded-lg bg-amber-50 border border-amber-200 text-xs text-amber-800 flex items-start gap-2.5">
          <AlertCircle className="size-4 shrink-0 text-amber-600 mt-0.5" />
          <p>{disabledReason}</p>
        </div>
      )}

      {isAccepted && (
        <div className="p-3.5 rounded-lg bg-emerald-50 border border-emerald-200 text-xs text-emerald-800 flex items-start gap-2.5">
          <CheckCircle2 className="size-4 shrink-0 text-emerald-600 mt-0.5" />
          <p>Mốc báo cáo này đã được bạn đánh giá Đạt. Không cần thao tác thêm.</p>
        </div>
      )}

      {isNeedRevision && (
        <div className="p-3.5 rounded-lg bg-blue-50 border border-blue-200 text-xs text-blue-800 flex items-start gap-2.5">
          <RotateCcw className="size-4 shrink-0 text-primary mt-0.5" />
          <p>
            Bạn đã yêu cầu nhóm chỉnh sửa phiên bản này. Vui lòng chờ sinh viên nộp phiên bản tiếp theo.
          </p>
        </div>
      )}

      <div className="space-y-2">
        <label
          htmlFor="advisor-feedback"
          className="text-xs font-semibold text-slate-700 block uppercase tracking-wide"
        >
          Nhận xét
        </label>
        <textarea
          id="advisor-feedback"
          rows={4}
          value={feedback}
          onChange={(e) => onFeedbackChange(e.target.value)}
          placeholder="Nhập nhận xét chi tiết, hướng dẫn chỉnh sửa hoặc đánh giá kết quả..."
          disabled={disabled || isSubmitting}
          className="w-full rounded-lg border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:opacity-60 disabled:bg-slate-50 disabled:cursor-not-allowed resize-none transition-all shadow-xs"
        />
        <p className="text-[11px] text-slate-400">
          Nhận xét là bắt buộc khi chọn &ldquo;Cần chỉnh sửa&rdquo; để sinh viên biết nội dung cần khắc phục.
        </p>
      </div>

      <div className="flex items-center gap-3 pt-2">
        <Button
          type="button"
          onClick={onAccept}
          disabled={disabled || isSubmitting}
          className="bg-primary hover:bg-primary/90 text-white font-medium px-6 h-10 shadow-xs"
        >
          {isSubmitting ? (
            <span className="flex items-center gap-2">
              <span className="size-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              Đang lưu...
            </span>
          ) : (
            'Đạt'
          )}
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={onNeedRevision}
          disabled={disabled || isSubmitting}
          className="border-slate-300 text-slate-700 hover:bg-slate-50 font-medium px-5 h-10 bg-white"
        >
          Cần chỉnh sửa
        </Button>
      </div>
    </Card>
  );
}

