import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Check, Megaphone } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { useAuth } from '@/shared/auth';
import { getTopicErrorMessage } from '../services/topicApi';
import { topicReviewApi, type ReviewDecision } from '../services/topicReviewApi';
import { TOPIC_STATUS_LABELS, type TopicBatch, type TopicStatus } from '../types/topic.types';
import { TopicPreview } from './TopicPreview';
import { TopicStatusBadge } from './TopicStatusBadge';
import { TopicDetailSkeleton } from './TopicDetailSkeleton';

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
  const [decision, setDecision] = useState<ReviewDecision | ''>('');
  const [confirmation, setConfirmation] = useState<ReviewAction | null>(null);
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
  const confirmLabel = canPublish
    ? 'Công bố đề tài'
    : decision
      ? TOPIC_STATUS_LABELS[decision]
      : '';

  function handleClose() {
    if (busy) return;
    if (
      canReview &&
      (decision || reviewNote.trim()) &&
      !window.confirm('Bạn muốn đóng và bỏ kết luận chưa gửi?')
    )
      return;
    onClose();
  }

  return (
    <Dialog
      title="Chi tiết & thẩm định đề tài"
      onClose={handleClose}
      busy={busy}
      className="max-w-4xl"
    >
      {detail.isPending ? (
        <TopicDetailSkeleton />
      ) : detail.isError ? (
        <div role="alert" className="space-y-3 p-6 text-sm text-rose-700">
          <p>{getTopicErrorMessage(detail.error)}</p>
          <Button variant="outline" onClick={() => void detail.refetch()}>
            Thử lại
          </Button>
        </div>
      ) : topic?.status === 'Draft' ? (
        <p role="status" className="p-6 text-sm text-slate-500">
          Đề tài là bản nháp của giảng viên, chưa được gửi thẩm định.
        </p>
      ) : (
        topic && (
          <>
            <div className="flex flex-wrap items-center gap-3 border-b border-slate-100 px-6 py-4">
              <TopicStatusBadge status={topic.status} />
              <p className="text-xs text-slate-500">{REVIEW_STATUS_HINTS[topic.status]}</p>
            </div>
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
              batchLoading={batchesLoading && !batches.some((batch) => batch.id === topic.batchId)}
              batchName={
                batches.find((batch) => batch.id === topic.batchId)?.batchName ||
                `Mã đợt: ${topic.batchId}`
              }
              previewLabel="Nội dung đề tài"
            />
            <div className="space-y-4 border-t border-slate-100 p-5 sm:p-6">
              {canReview && (
                <>
                  <div className="space-y-2">
                    <h3 className="text-sm font-semibold text-slate-800">Kết luận thẩm định</h3>
                    <Select<ReviewDecision | ''>
                      aria-label="Kết luận thẩm định"
                      value={decision}
                      disabled={busy || confirming || !isReady}
                      placeholder="Chọn kết luận"
                      options={[
                        { value: '', label: 'Chọn kết luận' },
                        { value: 'Approved', label: 'Duyệt đề tài' },
                        { value: 'NeedRevision', label: 'Yêu cầu chỉnh sửa' },
                        { value: 'Rejected', label: 'Từ chối đề tài' },
                      ]}
                      onChange={(value) => {
                        setDecision(value);
                        setConfirmation(null);
                        setNoteError('');
                      }}
                    />
                  </div>
                  <div className="space-y-2">
                    <label
                      htmlFor="topic-review-note"
                      className="text-sm font-semibold text-slate-800"
                    >
                      Góp ý thẩm định
                      {decision === 'NeedRevision' || decision === 'Rejected'
                        ? ' *'
                        : ' (tùy chọn)'}
                    </label>
                    <textarea
                      id="topic-review-note"
                      value={reviewNote}
                      maxLength={2000}
                      rows={4}
                      required={decision === 'NeedRevision' || decision === 'Rejected'}
                      aria-invalid={Boolean(noteError)}
                      aria-describedby={
                        noteError ? 'topic-review-note-error' : 'topic-review-note-count'
                      }
                      disabled={busy || confirming || !isReady}
                      placeholder="Nhập nhận xét hoặc nội dung cần chỉnh sửa"
                      className="w-full rounded-lg border border-input bg-slate-50/70 px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:opacity-50"
                      onChange={(event) => {
                        setReviewNote(event.target.value);
                        setNoteError('');
                        setConfirmation(null);
                      }}
                    />
                    <p id="topic-review-note-count" className="text-right text-xs text-slate-500">
                      {reviewNote.length}/2000
                    </p>
                    {noteError && (
                      <p
                        id="topic-review-note-error"
                        role="alert"
                        className="text-sm text-rose-700"
                      >
                        {noteError}
                      </p>
                    )}
                  </div>
                </>
              )}
              {canPublish && (
                <p className="text-sm text-slate-600">
                  Đề tài đã được duyệt. Công bố để sinh viên có thể tra cứu trong danh mục đề tài.
                </p>
              )}
              {confirming && (canReview || canPublish) && (
                <section
                  aria-label="Xác nhận thao tác"
                  className="space-y-2 rounded-xl border border-primary/20 bg-primary/5 p-4 text-sm"
                >
                  <h3 className="font-semibold text-slate-900">Xác nhận: {confirmLabel}</h3>
                  <p className="break-words text-slate-700">{topic.titleVi}</p>
                  {confirmation?.kind === 'review' && confirmation.reviewNote && (
                    <p className="whitespace-pre-wrap break-words text-slate-700">
                      {confirmation.reviewNote}
                    </p>
                  )}
                  <p className="text-xs leading-6 text-slate-500">
                    {canPublish
                      ? 'Chỉ công bố đề tài này, không công bố toàn bộ đợt.'
                      : decision === 'Approved'
                        ? 'Đề tài chuyển sang Đã duyệt; cần công bố riêng để sinh viên tra cứu.'
                        : decision === 'NeedRevision'
                          ? 'Giảng viên có thể chỉnh sửa và gửi duyệt lại đề tài.'
                          : 'Đề tài chuyển sang Không được duyệt và không được công bố.'}
                  </p>
                </section>
              )}
              {error && (
                <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
                  {error}
                </p>
              )}
              {detail.isFetching && (
                <p role="status" className="text-xs text-slate-500">
                  Đang cập nhật trạng thái...
                </p>
              )}
              <div className="flex flex-wrap justify-end gap-2">
                <Button
                  type="button"
                  variant="outline"
                  disabled={busy}
                  onClick={confirming ? () => setConfirmation(null) : handleClose}
                >
                  {confirming ? 'Quay lại' : 'Đóng'}
                </Button>
                {(canReview || canPublish) && (
                  <Button
                    type="button"
                    variant={decision === 'Rejected' && canReview ? 'dangerOutline' : 'default'}
                    leftIcon={canPublish ? <Megaphone /> : <Check />}
                    isLoading={busy}
                    disabled={!isReady || (canReview && !decision)}
                    onClick={() => {
                      if (!isReady || busy) return;
                      if (!confirming) {
                        if (canPublish)
                          setConfirmation({
                            kind: 'publish',
                            batchId: topic.batchId,
                            topicIds: [topic.id],
                          });
                        else if (canReview && decision) {
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
                        return;
                      }
                      if (confirmation) onAction(confirmation);
                    }}
                  >
                    {confirming ? 'Xác nhận' : canPublish ? 'Công bố đề tài' : 'Gửi kết luận'}
                  </Button>
                )}
              </div>
            </div>
          </>
        )
      )}
    </Dialog>
  );
}
