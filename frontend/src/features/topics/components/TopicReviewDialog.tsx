import { useId, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  CalendarClock,
  Check,
  CheckCircle2,
  ChevronDown,
  Info,
  Megaphone,
  Plus,
  RotateCcw,
  Sparkles,
  X,
  XCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog, type ConfirmDialogType } from '@/components/ui/confirm-dialog';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { cn } from '@/lib/utils';
import { useAuth } from '@/shared/auth';
import { getTopicErrorMessage } from '../services/topicApi';
import { topicReviewApi, type ReviewDecision } from '../services/topicReviewApi';
import { TOPIC_STATUS_LABELS, type TopicBatch, type TopicStatus } from '../types/topic.types';
import { TopicDetailSkeleton } from './TopicDetailSkeleton';
import { TopicPreview } from './TopicPreview';
import { TopicStatusBadge } from './TopicStatusBadge';

export type ReviewAction =
  | { kind: 'review'; id: string; decision: ReviewDecision; reviewNote?: string | null }
  | { kind: 'publish'; batchId: string; topicIds: string[] };

const REVIEW_STATUS_HINTS: Record<TopicStatus, string> = {
  Draft: 'Giảng viên chưa gửi đề tài. Chưa thể thẩm định.',
  Submitted: 'Đề tài đang chờ Khoa thẩm định.',
  NeedRevision: 'Chờ giảng viên chỉnh sửa và gửi duyệt lại.',
  Approved: 'Đề tài đã duyệt, đang chờ công bố.',
  Rejected: 'Đề tài không được duyệt và không được công bố.',
  Published: 'Đề tài đã được công bố cho sinh viên tra cứu.',
  Closed: 'Đề tài đã đóng. Chỉ xem nội dung.',
  Archived: 'Đề tài đã lưu trữ. Chỉ xem nội dung.',
};

interface DecisionOptionConfig {
  value: ReviewDecision;
  label: string;
  hint: string;
  icon: typeof CheckCircle2;
  activeClass: string;
  iconClass: string;
  confirmType: ConfirmDialogType;
}

const DECISION_OPTIONS: readonly DecisionOptionConfig[] = [
  {
    value: 'Approved',
    label: 'Duyệt đề tài',
    hint: 'Đạt yêu cầu, chuyển sang chờ công bố cho sinh viên.',
    icon: CheckCircle2,
    activeClass: 'border-emerald-300 bg-emerald-50/75 text-emerald-950 ring-1 ring-emerald-200',
    iconClass: 'text-emerald-600',
    confirmType: 'success',
  },
  {
    value: 'NeedRevision',
    label: 'Yêu cầu chỉnh sửa',
    hint: 'Trả lại giảng viên bổ sung nội dung và gửi duyệt lại.',
    icon: RotateCcw,
    activeClass: 'border-amber-300 bg-amber-50/80 text-amber-950 ring-1 ring-amber-200',
    iconClass: 'text-amber-600',
    confirmType: 'warning',
  },
  {
    value: 'Rejected',
    label: 'Từ chối đề tài',
    hint: 'Không phù hợp định hướng hoặc trùng lặp, dừng quy trình.',
    icon: XCircle,
    activeClass: 'border-rose-300 bg-rose-50/80 text-rose-950 ring-1 ring-rose-200',
    iconClass: 'text-rose-600',
    confirmType: 'error',
  },
];

const QUICK_NOTE_PRESETS: Record<'NeedRevision' | 'Rejected', readonly string[]> = {
  NeedRevision: [
    'Cần làm rõ phạm vi nghiên cứu và sản phẩm đầu ra cụ thể.',
    'Bổ sung yêu cầu công nghệ và kiến thức nền tảng cho sinh viên.',
    'Cập nhật đầy đủ tên đề tài tiếng Anh theo quy định.',
    'Điều chỉnh khối lượng thực hiện cho phù hợp với chỉ tiêu số sinh viên.',
  ],
  Rejected: [
    'Đề tài trùng hướng với đề tài đã phê duyệt trong đợt.',
    'Phạm vi và độ phức tạp chưa đạt mức Khóa luận tốt nghiệp.',
    'Nội dung đề xuất chưa phù hợp định hướng chuyên ngành của Khoa.',
  ],
};

function formatTopicDate(iso?: string): string {
  if (!iso) return '—';
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('vi-VN');
}

interface TopicReviewDialogProps {
  topicId: string;
  batches: TopicBatch[];
  advisorNames: Record<string, string>;
  advisorsLoading?: boolean;
  batchesLoading?: boolean;
  busy: boolean;
  error: string;
  onClose: () => void;
  onAction: (action: ReviewAction) => void;
}

export function TopicReviewDialog({
  topicId,
  batches,
  advisorNames,
  advisorsLoading = false,
  batchesLoading = false,
  busy,
  error,
  onClose,
  onAction,
}: TopicReviewDialogProps) {
  const { user } = useAuth();
  const presetPopoverId = useId();
  const presetTriggerRef = useRef<HTMLButtonElement>(null);
  const presetPopoverRef = useRef<HTMLDivElement>(null);
  const [decision, setDecision] = useState<ReviewDecision | ''>('');
  const [confirmation, setConfirmation] = useState<ReviewAction | null>(null);
  const [discardConfirmation, setDiscardConfirmation] = useState(false);
  const [reviewNote, setReviewNote] = useState('');
  const [noteError, setNoteError] = useState('');

  const detail = useQuery({
    queryKey: ['topic-review', user?.id, 'detail', topicId],
    queryFn: ({ signal }) => topicReviewApi.getDetail(topicId, signal),
    staleTime: 0,
  });

  const topic = detail.data;
  const canReview = topic?.status === 'Submitted';
  const canPublish = topic?.status === 'Approved';
  const confirming = Boolean(
    confirmation &&
    (confirmation.kind === 'publish'
      ? canPublish
      : canReview && confirmation.decision === decision),
  );
  const isReady = Boolean(topic) && !detail.isFetching && !detail.isError;
  const selectedOption = DECISION_OPTIONS.find((option) => option.value === decision);
  const confirmLabel = canPublish
    ? 'Công bố đề tài'
    : decision
      ? TOPIC_STATUS_LABELS[decision]
      : '';
  const confirmType: ConfirmDialogType = canPublish
    ? 'brand'
    : (selectedOption?.confirmType ?? 'warning');
  const noteRequired = decision === 'NeedRevision' || decision === 'Rejected';
  const quickPresets =
    decision === 'NeedRevision' || decision === 'Rejected' ? QUICK_NOTE_PRESETS[decision] : [];

  function hidePresetPopover() {
    presetPopoverRef.current?.hidePopover?.();
  }

  function handleClose() {
    if (busy) return;
    hidePresetPopover();
    if (canReview && (decision || reviewNote.trim())) {
      setDiscardConfirmation(true);
      return;
    }
    onClose();
  }

  function toggleQuickPreset(preset: string) {
    if (busy || confirming || !isReady) return;
    setReviewNote((current) => {
      const trimmed = current.trim();
      const bulletItem = `- ${preset}`;
      if (!trimmed) return bulletItem;
      const lines = current.split('\n');
      const normalizeLine = (line: string) => line.replace(/^\s*[-*•]\s*/, '').trim();
      if (lines.some((line) => normalizeLine(line) === preset)) {
        return lines
          .filter((line) => normalizeLine(line) !== preset)
          .join('\n')
          .trim();
      }
      if (current.includes(preset)) {
        return current
          .replace(preset, '')
          .replace(/\n{3,}/g, '\n\n')
          .trim();
      }
      const lastLine = trimmed.split('\n').at(-1) ?? '';
      const separator = /^\s*[-*]\s+/.test(lastLine) ? '\n' : '\n\n';
      const merged = `${trimmed}${separator}${bulletItem}`;
      return merged.length <= 2000 ? merged : current;
    });
    setNoteError('');
    setConfirmation(null);
  }

  function handleSubmit() {
    if (!isReady || busy || !topic) return;
    hidePresetPopover();
    if (canPublish) {
      setConfirmation({
        kind: 'publish',
        batchId: topic.batchId,
        topicIds: [topic.id],
      });
      return;
    }
    if (canReview && decision) {
      const note = reviewNote.trim();
      if (decision !== 'Approved' && !note) {
        setNoteError('Vui lòng nhập góp ý cho kết luận này.');
        return;
      }
      if (note.length > 2000) {
        setNoteError('Góp ý không được vượt quá 2000 ký tự.');
        return;
      }
      setConfirmation({
        kind: 'review',
        id: topic.id,
        decision,
        reviewNote: note || null,
      });
    }
  }

  const batchName =
    topic &&
    (batches.find((batch) => batch.id === topic.batchId)?.batchName || `Mã đợt: ${topic.batchId}`);

  return (
    <>
      <Dialog
        title="Chi tiết & thẩm định đề tài"
        hideHeader
        onClose={handleClose}
        busy={busy}
        className="flex max-h-[90dvh] w-[calc(100%-2rem)] max-w-6xl flex-col overflow-hidden rounded-2xl border border-slate-200/90 p-0 shadow-[0_24px_60px_-15px_rgba(15,23,42,0.18)]"
      >
        <div className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 bg-white px-5 py-4 sm:px-6">
          <div className="min-w-0 space-y-1">
            <div className="flex flex-wrap items-center gap-2.5">
              <h2 className="text-base font-semibold tracking-tight text-slate-900">
                Chi tiết & thẩm định đề tài
              </h2>
              {topic && topic.status !== 'Draft' && <TopicStatusBadge status={topic.status} />}
            </div>
            {topic && topic.status !== 'Draft' && (
              <p className="flex items-center gap-1.5 text-xs text-slate-500">
                <CalendarClock className="size-3.5 text-slate-400" aria-hidden="true" />
                <span>Đề xuất ngày {formatTopicDate(topic.createdAt)}</span>
                {batchName && (
                  <>
                    <span aria-hidden="true">·</span>
                    <span className="truncate font-medium text-slate-600">{batchName}</span>
                  </>
                )}
              </p>
            )}
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-label="Đóng hộp thoại"
            disabled={busy}
            onClick={handleClose}
            className="size-8 shrink-0 rounded-lg p-0 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="size-4" />
          </Button>
        </div>

        {detail.isPending ? (
          <TopicDetailSkeleton />
        ) : detail.isError ? (
          <div role="alert" className="space-y-4 p-6">
            <InlineAlert tone="error" title="Không tải được chi tiết đề tài">
              {getTopicErrorMessage(detail.error)}
            </InlineAlert>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={handleClose}>
                Đóng
              </Button>
              <Button type="button" onClick={() => void detail.refetch()}>
                Thử lại
              </Button>
            </div>
          </div>
        ) : topic?.status === 'Draft' ? (
          <div className="space-y-4 p-6">
            <p role="status" className="text-sm text-slate-600">
              Đề tài là bản nháp của giảng viên, chưa được gửi thẩm định.
            </p>
            <div className="flex justify-end">
              <Button type="button" variant="outline" onClick={handleClose}>
                Đóng
              </Button>
            </div>
          </div>
        ) : (
          topic && (
            <div className="grid min-h-0 flex-1 grid-cols-1 overflow-y-auto lg:grid-cols-[minmax(0,1.1fr)_minmax(430px,1fr)] lg:grid-rows-[minmax(0,1fr)] lg:divide-x lg:divide-slate-100 lg:overflow-hidden">
              <div className="min-h-0 bg-white lg:overflow-y-auto">
                <TopicPreview
                  reviewNote={topic.reviewNote}
                  values={{
                    batchId: topic.batchId,
                    titleVi: topic.titleVi,
                    titleEn: topic.titleEn ?? '',
                    description: topic.description ?? '',
                    requirements: topic.requirements ?? '',
                    studentNumber: topic.studentNumber,
                  }}
                  advisorName={advisorNames[topic.advisorId] || `Mã GVHD: ${topic.advisorId}`}
                  advisorLoading={advisorsLoading && !advisorNames[topic.advisorId]}
                  batchLoading={
                    batchesLoading && !batches.some((batch) => batch.id === topic.batchId)
                  }
                  batchName={batchName}
                  previewLabel="Nội dung đề tài"
                />
              </div>

              <aside className="flex min-h-0 flex-col border-t border-slate-100 bg-slate-50/60 lg:border-t-0">
                <div className="flex min-h-0 flex-1 flex-col gap-4 p-5 sm:p-6 lg:overflow-y-auto">
                  {canReview ? (
                    <>
                      <div className="shrink-0 space-y-2">
                        <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                          Kết luận thẩm định
                        </h3>
                        <div
                          role="radiogroup"
                          aria-label="Kết luận thẩm định"
                          className="space-y-2"
                        >
                          {DECISION_OPTIONS.map((option) => {
                            const Icon = option.icon;
                            const isSelected = decision === option.value;
                            const disabled = busy || confirming || !isReady;
                            return (
                              <button
                                key={option.value}
                                type="button"
                                role="radio"
                                aria-checked={isSelected}
                                data-decision={option.value}
                                disabled={disabled}
                                onClick={() => {
                                  hidePresetPopover();
                                  setDecision(option.value);
                                  setNoteError('');
                                  setConfirmation(null);
                                }}
                                className={cn(
                                  'flex w-full items-start gap-3 rounded-xl border px-3 py-2.5 text-left transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 disabled:cursor-not-allowed disabled:opacity-50',
                                  isSelected
                                    ? option.activeClass
                                    : 'border-slate-200/90 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50/70',
                                )}
                              >
                                <Icon
                                  className={cn(
                                    'mt-0.5 size-4 shrink-0',
                                    isSelected ? option.iconClass : 'text-slate-400',
                                  )}
                                  aria-hidden="true"
                                />
                                <span className="min-w-0 flex-1 space-y-0.5">
                                  <span className="block text-xs font-semibold">{option.label}</span>
                                  <span className="block text-[11px] leading-snug text-slate-500">
                                    {option.hint}
                                  </span>
                                </span>
                              </button>
                            );
                          })}
                        </div>
                      </div>

                      <div className="flex min-h-[220px] flex-1 flex-col gap-2">
                        <div className="flex shrink-0 items-center justify-between gap-2">
                          <span
                            id="topic-review-note-label"
                            className="text-xs font-semibold uppercase tracking-wider text-slate-600"
                          >
                            Góp ý thẩm định
                            {noteRequired ? (
                              <span className="ml-1 text-rose-600">*</span>
                            ) : (
                              <span className="ml-1 font-normal normal-case text-slate-400">
                                (không bắt buộc)
                              </span>
                            )}
                          </span>
                        </div>

                        <RichTextEditor
                          id="topic-review-note"
                          ariaLabelledBy="topic-review-note-label"
                          ariaDescribedBy={noteError ? 'topic-review-note-error' : undefined}
                          value={reviewNote}
                          maxLength={2000}
                          required={noteRequired}
                          invalid={Boolean(noteError)}
                          disabled={busy || confirming || !isReady}
                          expandedTitle="Soạn góp ý thẩm định"
                          placeholder={
                            noteRequired
                              ? 'Nhập nhận xét chi tiết để giảng viên nắm rõ lý do...'
                              : 'Nhập nhận xét bổ sung nếu cần (không bắt buộc)...'
                          }
                          minHeightClassName="min-h-[190px] flex-1"
                          toolbarExtra={
                            quickPresets.length > 0 ? (
                              (() => {
                                const insertedCount = quickPresets.filter((preset) =>
                                  reviewNote.includes(preset),
                                ).length;
                                return (
                                  <button
                                    ref={presetTriggerRef}
                                    type="button"
                                    popoverTarget={presetPopoverId}
                                    disabled={busy || confirming || !isReady}
                                    className={cn(
                                      'inline-flex h-7 items-center gap-1 rounded-md border px-2 text-[11px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:cursor-not-allowed disabled:opacity-50',
                                      insertedCount > 0
                                        ? 'border-blue-200 bg-blue-50/80 text-primary hover:bg-blue-100/70'
                                        : 'border-slate-200 bg-white text-slate-600 shadow-2xs hover:border-blue-200 hover:bg-blue-50/50 hover:text-primary',
                                    )}
                                  >
                                    <Sparkles
                                      className="size-3 text-amber-500"
                                      aria-hidden="true"
                                    />
                                    <span>
                                      Mẫu{insertedCount > 0 ? ` (${insertedCount})` : ''}
                                    </span>
                                    <ChevronDown
                                      className="size-3 text-slate-400"
                                      aria-hidden="true"
                                    />
                                  </button>
                                );
                              })()
                            ) : undefined
                          }
                          onChange={(nextValue) => {
                            setReviewNote(nextValue);
                            setNoteError('');
                            setConfirmation(null);
                          }}
                        />

                        {quickPresets.length > 0 && (
                          <div
                            ref={presetPopoverRef}
                            id={presetPopoverId}
                            popover="auto"
                            aria-label="Mẫu góp ý thẩm định"
                            className="fixed inset-auto m-0 w-[min(22rem,calc(100vw-2rem))] rounded-xl border border-slate-200/90 bg-white p-1.5 text-left shadow-xl"
                            onToggle={(event) => {
                              if ((event as unknown as { newState?: string }).newState !== 'open')
                                return;
                              const rect = presetTriggerRef.current?.getBoundingClientRect();
                              const el = presetPopoverRef.current;
                              if (!rect || !el) return;
                              const width = el.offsetWidth;
                              const height = el.offsetHeight;
                              el.style.left = `${Math.max(8, Math.min(rect.right - width, window.innerWidth - width - 8))}px`;
                              el.style.top = `${Math.max(
                                8,
                                rect.bottom + height + 8 > window.innerHeight
                                  ? rect.top - height - 6
                                  : rect.bottom + 6,
                              )}px`;
                              el.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
                            }}
                          >
                            <div className="flex items-center justify-between border-b border-slate-100 px-2.5 py-1.5">
                              <span className="flex items-center gap-1.5 text-[11px] font-semibold text-slate-700">
                                <Sparkles className="size-3 text-amber-500" aria-hidden="true" />
                                Gợi ý nhận xét nhanh
                              </span>
                              <span className="text-[11px] text-slate-400">Nhấn để chèn / bỏ</span>
                            </div>
                            <div className="mt-1 max-h-60 space-y-0.5 overflow-y-auto">
                              {quickPresets.map((preset) => {
                                const inserted = reviewNote.includes(preset);
                                return (
                                  <button
                                    key={preset}
                                    type="button"
                                    disabled={busy || confirming || !isReady}
                                    onClick={() => toggleQuickPreset(preset)}
                                    className={cn(
                                      'flex w-full items-start gap-2 rounded-lg px-2.5 py-2 text-left text-xs leading-relaxed transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:opacity-50',
                                      inserted
                                        ? 'bg-blue-50/80 font-medium text-primary'
                                        : 'text-slate-700 hover:bg-slate-50',
                                    )}
                                  >
                                    {inserted ? (
                                      <Check
                                        className="mt-0.5 size-3.5 shrink-0 text-primary"
                                        aria-hidden="true"
                                      />
                                    ) : (
                                      <Plus
                                        className="mt-0.5 size-3.5 shrink-0 text-slate-400"
                                        aria-hidden="true"
                                      />
                                    )}
                                    <span className="whitespace-normal break-words">{preset}</span>
                                  </button>
                                );
                              })}
                            </div>
                          </div>
                        )}

                        {noteError && (
                          <p
                            id="topic-review-note-error"
                            role="alert"
                            className="shrink-0 text-xs font-medium text-rose-700"
                          >
                            {noteError}
                          </p>
                        )}
                      </div>
                    </>
                  ) : canPublish ? (
                    <div className="space-y-2.5 rounded-xl border border-emerald-200/80 bg-emerald-50/70 p-4">
                      <div className="flex items-center gap-2 text-emerald-900">
                        <Megaphone className="size-4 text-emerald-600" aria-hidden="true" />
                        <h3 className="text-sm font-semibold">Sẵn sàng công bố</h3>
                      </div>
                      <p className="text-xs leading-relaxed text-emerald-800/90">
                        Đề tài đã qua thẩm định đạt chuẩn. Nhấn công bố để đưa đề tài vào danh mục
                        chính thức cho sinh viên đăng ký trong đợt.
                      </p>
                    </div>
                  ) : (
                    <div className="space-y-2 rounded-xl border border-slate-200/80 bg-white p-4">
                      <div className="flex items-center gap-2 text-slate-700">
                        <Info className="size-4 text-slate-400" aria-hidden="true" />
                        <h3 className="text-sm font-semibold">Trạng thái xử lý</h3>
                      </div>
                      <p className="text-xs leading-relaxed text-slate-600">
                        {REVIEW_STATUS_HINTS[topic.status]}
                      </p>
                    </div>
                  )}

                  {error && (
                    <p
                      role="alert"
                      className="shrink-0 rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2.5 text-xs font-medium text-rose-700"
                    >
                      {error}
                    </p>
                  )}

                  {detail.isFetching && (
                    <p role="status" className="shrink-0 text-xs text-slate-500">
                      Đang cập nhật trạng thái...
                    </p>
                  )}
                </div>

                <div className="sticky bottom-0 z-10 flex shrink-0 flex-wrap items-center justify-end gap-2.5 border-t border-slate-200/80 bg-white/95 px-5 py-3.5 backdrop-blur-xs sm:px-6 lg:static">
                  <Button
                    type="button"
                    variant="outline"
                    disabled={busy}
                    onClick={handleClose}
                    className="rounded-xl border-slate-200 bg-white"
                  >
                    Đóng
                  </Button>
                  {(canReview || canPublish) && (
                    <Button
                      type="button"
                      variant={decision === 'Rejected' && canReview ? 'dangerOutline' : 'default'}
                      leftIcon={canPublish ? <Megaphone /> : <Check />}
                      isLoading={busy}
                      disabled={!isReady || (canReview && !decision)}
                      onClick={handleSubmit}
                      className={cn(
                        'rounded-xl shadow-xs',
                        canReview &&
                          decision === 'Approved' &&
                          'bg-emerald-600 text-white hover:bg-emerald-700',
                      )}
                    >
                      {canPublish ? 'Công bố đề tài' : 'Gửi kết luận'}
                    </Button>
                  )}
                </div>
              </aside>
            </div>
          )
        )}
      </Dialog>

      <ConfirmDialog
        open={confirming}
        onClose={() => setConfirmation(null)}
        onConfirm={() => {
          if (!confirmation || !isReady || busy) return;
          onAction(confirmation);
          setConfirmation(null);
        }}
        type={confirmType}
        title={`Xác nhận ${confirmLabel}`}
        description={
          <>
            <p className="break-words font-medium text-slate-700">{topic?.titleVi}</p>
            {confirmation?.kind === 'review' && confirmation.reviewNote && (
              <div className="mt-2 max-h-40 overflow-y-auto rounded-lg border border-slate-200/80 bg-slate-50 p-2.5 text-xs text-slate-600">
                <RichTextContent
                  content={confirmation.reviewNote}
                  className="text-xs leading-relaxed text-slate-600"
                />
              </div>
            )}
            <p className="mt-2 text-slate-600">
              {canPublish
                ? 'Đề tài sẽ được công bố để sinh viên tra cứu.'
                : decision === 'Approved'
                  ? 'Đề tài sẽ chuyển sang Đã duyệt (Chờ công bố).'
                  : decision === 'NeedRevision'
                    ? 'Giảng viên sẽ nhận được yêu cầu chỉnh sửa cùng góp ý của bạn.'
                    : 'Đề tài sẽ bị từ chối và dừng quy trình.'}
            </p>
          </>
        }
        confirmText={confirmLabel}
        isConfirming={busy}
      />

      <ConfirmDialog
        open={discardConfirmation}
        onClose={() => setDiscardConfirmation(false)}
        onConfirm={onClose}
        type="warning"
        title="Bỏ kết luận chưa gửi?"
        description="Kết luận và góp ý đang nhập sẽ bị bỏ."
        confirmText="Bỏ kết luận"
        isConfirming={busy}
      />
    </>
  );
}
