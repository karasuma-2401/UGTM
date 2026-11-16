import { useId } from 'react';
import { CheckCircle2, MessageSquareText, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { RichTextEditor } from '@/components/ui/rich-text-editor';

export interface AdvisorMilestoneFeedbackFormProps {
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
  disabledReason = null,
}: AdvisorMilestoneFeedbackFormProps) {
  const labelId = useId();
  const isFormDisabled = disabled || isSubmitting;

  return (
    <section className="overflow-hidden rounded-2xl border border-line/80 bg-surface-card shadow-2xs">
      <div className="flex items-center gap-2 border-b border-line-subtle px-5 py-4">
        <MessageSquareText className="size-4 text-primary" aria-hidden="true" />
        <h2 className="text-base font-semibold text-ink-primary">Đánh giá và Nhận xét</h2>
      </div>

      <div className="space-y-4 p-5">
        {disabled && disabledReason && (
          <InlineAlert tone="warning" title="Trạng thái hiện tại">
            {disabledReason}
          </InlineAlert>
        )}

        <div className="space-y-2">
          <label id={labelId} className="block text-sm font-semibold text-ink-heading">
            Nhận xét của giảng viên
            <span className="ml-1.5 font-normal text-ink-muted">
              (bắt buộc khi yêu cầu chỉnh sửa)
            </span>
          </label>

          <RichTextEditor
            id="standalone-milestone-feedback"
            ariaLabelledBy={labelId}
            value={feedback}
            disabled={isFormDisabled}
            expandable
            expandedTitle="Soạn nhận xét mốc tiến độ"
            placeholder="Nhập nhận xét chi tiết về mã nguồn, tiến độ thực hiện và các điểm cần hoàn thiện..."
            minHeightClassName="min-h-[160px]"
            onChange={onFeedbackChange}
          />
        </div>

        <div className="flex flex-wrap items-center justify-end gap-2.5 border-t border-line-subtle pt-4">
          <Button
            type="button"
            variant="outline"
            disabled={isFormDisabled}
            onClick={onNeedRevision}
            className="gap-1.5 border-tone-warning-border text-tone-warning-strong hover:bg-tone-warning-bg hover:text-tone-warning-strong"
          >
            <RotateCcw className="size-4" aria-hidden="true" />
            <span>Yêu cầu chỉnh sửa</span>
          </Button>

          <Button
            type="button"
            disabled={isFormDisabled}
            onClick={onAccept}
            className="gap-1.5"
          >
            <CheckCircle2 className="size-4" aria-hidden="true" />
            <span>{isSubmitting ? 'Đang gửi...' : 'Xác nhận Đạt'}</span>
          </Button>
        </div>
      </div>
    </section>
  );
}
