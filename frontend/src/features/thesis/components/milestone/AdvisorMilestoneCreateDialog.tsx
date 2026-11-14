import { useId, useMemo, useState, type FormEvent } from 'react';
import { CalendarClock, CheckCircle2, Flag, X } from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { DateTimePicker } from '@/components/ui/calendar';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { cn } from '@/lib/utils';
import type {
  AdvisedGroupOption,
  CreateProgressMilestoneRequest,
  MilestoneWithReportDto,
} from '../../types/milestone.types';
import {
  formatDeadlineSummary,
  formatMilestoneDateShort,
  formatMilestoneDateTime,
  formatMilestoneTimeShort,
  getDefaultFutureDeadlineInput,
} from '../../utils/milestoneFormatters';

export interface AdvisorMilestoneCreateDialogProps {
  open: boolean;
  onClose: () => void;
  selectedGroup: AdvisedGroupOption | null;
  existingMilestones?: readonly MilestoneWithReportDto[];
  onSubmit: (data: CreateProgressMilestoneRequest) => Promise<unknown>;
  isSubmitting?: boolean;
  serverError?: string | null;
}

const QUICK_DEADLINE_OFFSETS = [
  { label: '+7 ngày', days: 7 },
  { label: '+14 ngày', days: 14 },
  { label: '+21 ngày', days: 21 },
] as const;

export function AdvisorMilestoneCreateDialog({
  open,
  onClose,
  selectedGroup,
  existingMilestones = [],
  onSubmit,
  isSubmitting = false,
  serverError = null,
}: AdvisorMilestoneCreateDialogProps) {
  if (!open || !selectedGroup) return null;

  return (
    <Dialog
      title="Thêm mốc tiến độ"
      hideHeader
      onClose={onClose}
      busy={isSubmitting}
      className="flex max-h-[92dvh] w-[calc(100%-2rem)] max-w-5xl flex-col overflow-hidden rounded-2xl border border-slate-200/90 p-0 shadow-[0_24px_60px_-15px_rgba(15,23,42,0.18)]"
    >
      <CreateMilestoneFormBody
        key={`${selectedGroup.groupId}-${String(open)}`}
        selectedGroup={selectedGroup}
        existingMilestones={existingMilestones}
        onClose={onClose}
        onSubmit={onSubmit}
        isSubmitting={isSubmitting}
        serverError={serverError}
      />
    </Dialog>
  );
}

function CreateMilestoneFormBody({
  selectedGroup,
  existingMilestones,
  onClose,
  onSubmit,
  isSubmitting,
  serverError,
}: {
  selectedGroup: AdvisedGroupOption;
  existingMilestones: readonly MilestoneWithReportDto[];
  onClose: () => void;
  onSubmit: (data: CreateProgressMilestoneRequest) => Promise<unknown>;
  isSubmitting: boolean;
  serverError: string | null;
}) {
  const titleId = useId();
  const deadlineId = useId();
  const descLabelId = useId();

  const nextMilestoneIndex = existingMilestones.length + 1;
  const [mountNowMs] = useState(() => Date.now());
  const [title, setTitle] = useState('');
  const [deadlineInput, setDeadlineInput] = useState(() => getDefaultFutureDeadlineInput(7));
  const [description, setDescription] = useState('');
  const [titleError, setTitleError] = useState('');
  const [deadlineError, setDeadlineError] = useState('');

  const sortedExisting = useMemo(
    () =>
      [...existingMilestones].sort(
        (a, b) =>
          new Date(a.milestone.deadlineUtc).getTime() - new Date(b.milestone.deadlineUtc).getTime(),
      ),
    [existingMilestones],
  );

  const deadlineSummary = useMemo(
    () => formatDeadlineSummary(deadlineInput, mountNowMs),
    [deadlineInput, mountNowMs],
  );

  const previewDeadlineIso = useMemo(() => {
    if (!deadlineInput) return null;
    const parsed = new Date(deadlineInput);
    return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
  }, [deadlineInput]);

  function handleCloseModal() {
    if (isSubmitting) return;
    onClose();
  }

  async function handleFormSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitting) return;

    const cleanTitle = title.trim();
    if (!cleanTitle) {
      setTitleError('Vui lòng nhập tên mốc tiến độ.');
      return;
    }
    if (cleanTitle.length > 250) {
      setTitleError('Tên mốc tiến độ không được vượt quá 250 ký tự.');
      return;
    }

    const deadlineDate = new Date(deadlineInput);
    if (!deadlineInput || Number.isNaN(deadlineDate.getTime())) {
      setDeadlineError('Vui lòng chọn ngày giờ hạn chót hợp lệ.');
      return;
    }
    if (deadlineDate.getTime() <= Date.now()) {
      setDeadlineError('Hạn chót nộp báo cáo phải nằm ở thời điểm tương lai.');
      return;
    }

    setTitleError('');
    setDeadlineError('');

    await onSubmit({
      title: cleanTitle,
      deadlineUtc: deadlineDate.toISOString(),
      description: description.trim() || undefined,
    });
  }

  return (
    <form onSubmit={handleFormSubmit} className="flex min-h-0 flex-1 flex-col">
      <div
        aria-hidden="true"
        className="h-1 shrink-0 bg-[linear-gradient(90deg,#2054a5_0%,#2563eb_55%,#fa7921_100%)]"
      />

      <div className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 bg-white px-5 py-4 sm:px-6">
        <div className="min-w-0 space-y-1.5">
          <div className="flex flex-wrap items-center gap-2.5">
            <h2 className="text-lg font-semibold tracking-tight text-slate-900">
              Thêm mốc tiến độ
            </h2>
            <span className="inline-flex items-center rounded-md border border-slate-200/90 bg-slate-50 px-2 py-0.5 font-mono text-xs font-semibold text-slate-700">
              {selectedGroup.groupCode}
            </span>
            <AvatarGroup
              size="sm"
              items={selectedGroup.students.map((student) => ({
                id: student.studentId,
                name: student.fullName,
                userCode: student.userCode,
              }))}
            />
          </div>
          <p className="line-clamp-1 text-xs text-slate-500" title={selectedGroup.topicTitleVi}>
            {selectedGroup.topicTitleVi}
          </p>
        </div>

        <Button
          type="button"
          variant="ghost"
          size="sm"
          aria-label="Đóng hộp thoại"
          disabled={isSubmitting}
          onClick={handleCloseModal}
          className="size-8 shrink-0 rounded-lg p-0 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
        >
          <X className="size-4" />
        </Button>
      </div>

      <div className="grid min-h-0 flex-1 grid-cols-1 overflow-y-auto lg:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.9fr)] lg:grid-rows-[minmax(0,1fr)] lg:divide-x lg:divide-slate-100 lg:overflow-hidden">
        {/* Left pane: Editor studio */}
        <div className="min-h-0 space-y-5 bg-white p-5 sm:p-6 lg:overflow-y-auto">
          {serverError && (
            <InlineAlert tone="error" title="Không thể tạo mốc tiến độ">
              {serverError}
            </InlineAlert>
          )}

          <div className="space-y-2">
            <div className="flex items-center justify-between gap-2">
              <label htmlFor={titleId} className="text-sm font-semibold text-slate-800">
                Tên mốc tiến độ <span className="text-rose-600">*</span>
              </label>
              <span className="text-xs tabular-nums text-slate-500">{title.length}/250</span>
            </div>
            <Input
              id={titleId}
              value={title}
              maxLength={250}
              disabled={isSubmitting}
              aria-invalid={Boolean(titleError)}
              placeholder={`VD: Mốc ${nextMilestoneIndex} — Hoàn thiện API quản lý và kiểm thử tích hợp`}
              onChange={(event) => {
                setTitle(event.target.value);
                if (titleError) setTitleError('');
              }}
            />

            {titleError && <p className="text-xs font-medium text-rose-600">{titleError}</p>}
          </div>

          <div className="space-y-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <label htmlFor={deadlineId} className="text-sm font-semibold text-slate-800">
                Hạn chót nộp báo cáo <span className="text-rose-600">*</span>
              </label>
              <div className="flex flex-wrap items-center gap-1.5">
                {QUICK_DEADLINE_OFFSETS.map((preset) => (
                  <button
                    key={preset.days}
                    type="button"
                    disabled={isSubmitting}
                    onClick={() => {
                      setDeadlineInput(getDefaultFutureDeadlineInput(preset.days));
                      if (deadlineError) setDeadlineError('');
                    }}
                    className="inline-flex h-6 items-center gap-1 rounded-md border border-slate-200 bg-slate-50 px-2 text-xs font-medium text-slate-600 transition-colors hover:border-blue-200 hover:bg-blue-50 hover:text-primary disabled:opacity-50"
                  >
                    <CalendarClock className="size-3" aria-hidden="true" />
                    <span>{preset.label}</span>
                  </button>
                ))}
              </div>
            </div>

            <DateTimePicker
              id={deadlineId}
              value={deadlineInput}
              required
              disabled={isSubmitting}
              error={deadlineError || undefined}
              placeholder="dd/mm/yyyy HH:mm"
              onValueChange={(nextValue) => {
                setDeadlineInput(nextValue);
                if (deadlineError) setDeadlineError('');
              }}
            />

            {deadlineSummary && !deadlineError && (
              <p
                className={cn(
                  'text-xs capitalize',
                  deadlineSummary.validFuture ? 'text-slate-500' : 'font-medium text-amber-700',
                )}
              >
                {deadlineSummary.summary}
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <div className="flex items-center justify-between gap-2">
              <span id={descLabelId} className="text-sm font-semibold text-slate-800">
                Sản phẩm & yêu cầu cần nộp
                <span className="ml-1.5 font-normal text-slate-500">(không bắt buộc)</span>
              </span>
            </div>

            <RichTextEditor
              id="milestone-create-description"
              ariaLabelledBy={descLabelId}
              value={description}
              disabled={isSubmitting}
              expandable
              expandedTitle="Soạn yêu cầu sản phẩm cần nộp"
              expandedSubtitle={`${selectedGroup.groupCode} · ${selectedGroup.topicTitleVi}`}
              placeholder="Mô tả rõ tài liệu, liên kết Git hoặc đầu mục kiểm thử sinh viên cần nộp..."
              minHeightClassName="min-h-[160px]"
              onChange={setDescription}
            />
          </div>
        </div>

        {/* Right pane: Live student preview + existing group timeline */}
        <aside className="min-h-0 space-y-5 bg-slate-50/55 p-5 sm:p-6 lg:overflow-y-auto">
          <div className="space-y-2.5">
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-xs font-semibold text-slate-600">
                Xem trước hiển thị của sinh viên
              </h3>
              <span className="inline-flex items-center gap-1 text-[11px] font-medium text-slate-500">
                <Flag className="size-3 text-primary" aria-hidden="true" />
                <span>Mốc thứ {nextMilestoneIndex}</span>
              </span>
            </div>

            <div className="rounded-xl border border-slate-200/90 bg-white p-4 shadow-2xs">
              <div className="flex items-start justify-between gap-2">
                <p
                  className={cn(
                    'text-sm font-semibold leading-snug',
                    title.trim() ? 'text-slate-900' : 'italic text-slate-400',
                  )}
                >
                  {title.trim() || `Mốc ${nextMilestoneIndex} — Tên mốc tiến độ sẽ hiển thị tại đây`}
                </p>
                <span className="shrink-0 rounded-full border border-slate-200/90 bg-slate-50 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                  Chưa nộp
                </span>
              </div>

              <div className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-500">
                <span className="inline-flex items-center gap-1 tabular-nums">
                  <CalendarClock className="size-3.5 text-slate-400" aria-hidden="true" />
                  <span>
                    {previewDeadlineIso
                      ? `${formatMilestoneDateShort(previewDeadlineIso)} · ${formatMilestoneTimeShort(previewDeadlineIso)}`
                      : 'Chưa chọn hạn nộp'}
                  </span>
                </span>
              </div>

              <div className="mt-3 border-t border-slate-100 pt-3 text-xs leading-relaxed text-slate-600">
                <RichTextContent
                  content={description}
                  emptyFallback="Mô tả chi tiết tài liệu, mã nguồn Git hoặc tiêu chí đánh giá sẽ hiển thị tại đây."
                />
              </div>
            </div>
          </div>

          <div className="space-y-2.5 border-t border-slate-200/80 pt-4">
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-xs font-semibold text-slate-600">Lộ trình mốc hiện có</h3>
              <span className="text-xs tabular-nums text-slate-500">
                {sortedExisting.length} mốc
              </span>
            </div>

            {sortedExisting.length === 0 ? (
              <p className="rounded-xl border border-dashed border-slate-200 bg-white/70 px-3.5 py-3 text-xs text-slate-500">
                Nhóm chưa có mốc tiến độ nào. Mốc này sẽ là cột mốc mở đầu lộ trình.
              </p>
            ) : (
              <ol className="space-y-2">
                {sortedExisting.map((item, idx) => {
                  const accepted = item.report?.currentStatus === 'Accepted';
                  return (
                    <li
                      key={item.milestone.id}
                      className="flex items-start justify-between gap-2.5 rounded-lg bg-white px-3 py-2 text-xs ring-1 ring-slate-200/70"
                    >
                      <div className="flex min-w-0 items-start gap-2">
                        {accepted ? (
                          <CheckCircle2
                            className="mt-0.5 size-3.5 shrink-0 text-emerald-600"
                            aria-hidden="true"
                          />
                        ) : (
                          <span className="mt-0.5 inline-flex size-4 shrink-0 items-center justify-center rounded-full bg-slate-100 text-[10px] font-semibold tabular-nums text-slate-600">
                            {idx + 1}
                          </span>
                        )}
                        <span className="line-clamp-1 font-medium text-slate-800">
                          {item.milestone.title}
                        </span>
                      </div>
                      <span className="shrink-0 whitespace-nowrap tabular-nums text-slate-500">
                        {formatMilestoneDateTime(item.milestone.deadlineUtc)}
                      </span>
                    </li>
                  );
                })}
              </ol>
            )}
          </div>
        </aside>
      </div>

      <div className="flex shrink-0 items-center justify-end gap-2.5 border-t border-slate-100 bg-slate-50/70 px-5 py-3.5 sm:px-6">
        <Button type="button" variant="outline" disabled={isSubmitting} onClick={handleCloseModal}>
          Hủy
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Đang tạo mốc...' : 'Tạo mốc tiến độ'}
        </Button>
      </div>
    </form>
  );
}
