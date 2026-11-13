import { useId, useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { DateTimePicker } from '@/components/ui/calendar';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import type { MilestoneWithReportDto } from '../../types/milestone.types';
import { getDefaultFutureDeadlineInput } from '../../utils/milestoneFormatters';

export interface AdvisorMilestoneFormProps {
  selectedMilestone?: MilestoneWithReportDto | null;
  isSubmitting?: boolean;
  disabled?: boolean;
  disabledReason?: string | null;
  onSubmit: (data: { title: string; description: string; deadlineUtc: string }) => void;
  onNavigateReview?: () => void;
  onResetForm?: () => void;
}

export function AdvisorMilestoneForm({
  isSubmitting = false,
  disabled = false,
  disabledReason = null,
  onSubmit,
  onResetForm,
}: AdvisorMilestoneFormProps) {
  const titleId = useId();
  const deadlineId = useId();
  const descLabelId = useId();

  const [title, setTitle] = useState('');
  const [deadlineInput, setDeadlineInput] = useState(() => getDefaultFutureDeadlineInput(7));
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');

  const formDisabled = disabled || isSubmitting;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (formDisabled) return;

    const cleanTitle = title.trim();
    if (!cleanTitle) {
      setError('Vui lòng nhập tên mốc tiến độ.');
      return;
    }

    const deadlineDate = new Date(deadlineInput);
    if (!deadlineInput || Number.isNaN(deadlineDate.getTime()) || deadlineDate.getTime() <= Date.now()) {
      setError('Hạn chót nộp báo cáo phải nằm ở thời điểm tương lai.');
      return;
    }

    setError('');
    onSubmit({
      title: cleanTitle,
      description: description.trim(),
      deadlineUtc: deadlineDate.toISOString(),
    });
    setTitle('');
    setDescription('');
    setDeadlineInput(getDefaultFutureDeadlineInput(7));
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-4 rounded-2xl border border-slate-200/80 bg-white p-5 shadow-2xs"
    >
      <div className="flex items-center justify-between gap-2 border-b border-slate-100 pb-3">
        <h3 className="text-base font-semibold text-slate-900">Thêm mốc tiến độ mới</h3>
        {onResetForm && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => {
              setTitle('');
              setDescription('');
              setDeadlineInput(getDefaultFutureDeadlineInput(7));
              setError('');
              onResetForm();
            }}
          >
            Làm mới
          </Button>
        )}
      </div>

      {disabled && disabledReason && (
        <InlineAlert tone="warning" title="Không thể tạo mốc">
          {disabledReason}
        </InlineAlert>
      )}

      {error && (
        <InlineAlert tone="error" title="Dữ liệu chưa hợp lệ">
          {error}
        </InlineAlert>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <label htmlFor={titleId} className="block text-sm font-semibold text-slate-800">
            Tên mốc tiến độ <span className="text-rose-600">*</span>
          </label>
          <Input
            id={titleId}
            value={title}
            maxLength={250}
            disabled={formDisabled}
            placeholder="VD: Mốc 1 — Báo cáo khảo sát và kiến trúc"
            onChange={(event) => {
              setTitle(event.target.value);
              if (error) setError('');
            }}
          />
        </div>

        <div className="space-y-1.5">
          <label htmlFor={deadlineId} className="block text-sm font-semibold text-slate-800">
            Hạn chót nộp báo cáo <span className="text-rose-600">*</span>
          </label>
          <DateTimePicker
            id={deadlineId}
            value={deadlineInput}
            required
            disabled={formDisabled}
            onValueChange={(next) => {
              setDeadlineInput(next);
              if (error) setError('');
            }}
          />
        </div>
      </div>

      <div className="space-y-1.5">
        <span id={descLabelId} className="block text-sm font-semibold text-slate-800">
          Sản phẩm & yêu cầu cần nộp
        </span>
        <RichTextEditor
          id="inline-milestone-description"
          ariaLabelledBy={descLabelId}
          value={description}
          disabled={formDisabled}
          expandable
          expandedTitle="Soạn yêu cầu sản phẩm cần nộp"
          minHeightClassName="min-h-[140px]"
          onChange={setDescription}
        />
      </div>

      <div className="flex justify-end border-t border-slate-100 pt-3">
        <Button type="submit" disabled={formDisabled}>
          {isSubmitting ? 'Đang tạo mốc...' : 'Tạo mốc tiến độ'}
        </Button>
      </div>
    </form>
  );
}
