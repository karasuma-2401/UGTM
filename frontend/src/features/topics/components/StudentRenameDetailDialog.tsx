import {
  ArrowDown,
  ArrowUpRight,
  CalendarClock,
  MessageSquareQuote,
  RotateCcw,
  UserCheck,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { Avatar } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InfoCell } from '@/components/ui/info-cell';
import { InlineAlert } from '@/components/ui/inline-alert';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { StatusBadge } from '@/components/ui/status-badge';
import { computeTitleWordDiff, formatRenameDateTime } from '../renameReview';
import {
  renameStatusLabels,
  renameStatusTones,
  type RenameRequest,
} from '../types/renameRequest.types';
import type { StudentTopicGroupDetail } from '../types/studentTopic.types';
import { RenameApprovalTimeline } from './RenameApprovalTimeline';
import { TitleDiffText } from './TitleDiffText';

interface StudentRenameDetailDialogProps {
  request: RenameRequest;
  group: StudentTopicGroupDetail;
  canReuse: boolean;
  onClose: () => void;
  onReuse: (request: RenameRequest) => void;
}

export function StudentRenameDetailDialog({
  request,
  group,
  canReuse,
  onClose,
  onReuse,
}: StudentRenameDetailDialogProps) {
  const viTitleDiff = computeTitleWordDiff(request.oldTitleVi, request.newTitleVi);
  const enTitleDiff = computeTitleWordDiff(request.oldTitleEn, request.newTitleEn);

  const requester = group.members.find(
    (member) => member.studentId === request.requestedByStudentId,
  );
  const requesterName = requester?.fullName || requester?.userCode || 'Thành viên nhóm';
  const advisorDisplay = request.advisorName || group.advisor.fullName || 'Chưa cập nhật';
  const isRejected = request.status.startsWith('Rejected');

  return (
    <Dialog
      open
      onClose={onClose}
      className="max-w-4xl"
      headerClassName="before:content-[''] before:absolute before:inset-x-0 before:top-0 before:h-1 before:[background-image:var(--gradient-brand)]"
      title={
        <div className="space-y-1.5 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-base font-bold text-ink-primary">
              Chi tiết đơn xin đổi tên đề tài
            </span>
            <StatusBadge tone={renameStatusTones[request.status]}>
              {renameStatusLabels[request.status] ?? request.status}
            </StatusBadge>
            <Badge variant="secondary" rounded="full" className="text-[11px] font-medium">
              Chế độ chỉ đọc
            </Badge>
          </div>
          <p className="flex items-center gap-1.5 text-xs font-normal text-ink-muted tabular-nums">
            <CalendarClock className="size-3.5 text-ink-subtle" aria-hidden="true" />
            <span>Gửi lúc {formatRenameDateTime(request.createdAt)}</span>
          </p>
        </div>
      }
    >
      <div className="grid grid-cols-1 lg:grid-cols-12">
        <div className="space-y-4 border-b border-line-subtle p-5 sm:p-6 lg:col-span-7 lg:border-b-0 lg:border-r">
          <section className="space-y-2.5" aria-label="Đối chiếu tên đề tài">
            <h3 className="text-sm font-semibold text-ink-primary">
              Đối chiếu tên đề tài
            </h3>

            <div className="overflow-hidden rounded-xl border border-line/85 bg-surface-card">
              <div className="bg-surface-subtle/60 px-4 py-3.5">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-xs font-medium text-ink-muted">
                    Tên tại thời điểm gửi đơn
                  </span>
                  {!request.oldTitleEn && (
                    <span className="text-xs text-ink-subtle">Chưa có bản EN</span>
                  )}
                </div>
                <p className="mt-1 text-[13.5px] font-medium leading-relaxed text-ink-body">
                  <TitleDiffText segments={viTitleDiff.oldSegments} variant="old" />
                </p>
                {request.oldTitleEn && (
                  <p className="mt-1 text-xs leading-relaxed text-ink-muted">
                    EN: <TitleDiffText segments={enTitleDiff.oldSegments} variant="old" subtle />
                  </p>
                )}
              </div>

              <div className="relative border-t border-line/80" aria-hidden="true">
                <span className="absolute left-1/2 top-0 flex size-6 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border border-tone-info-border bg-surface-card text-primary shadow-2xs">
                  <ArrowDown className="size-3.5" />
                </span>
              </div>

              <div className="bg-tone-info-bg/25 px-4 py-4">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-xs font-semibold text-primary">
                    Tên đề xuất mới
                  </span>
                  {!request.newTitleEn && (
                    <span className="text-xs text-ink-subtle">Chưa có bản EN</span>
                  )}
                </div>
                <p className="mt-1 text-[15px] font-semibold leading-snug text-ink-primary">
                  <TitleDiffText segments={viTitleDiff.newSegments} variant="new" />
                </p>
                {request.newTitleEn && (
                  <p className="mt-1 text-xs leading-relaxed text-ink-body">
                    EN: <TitleDiffText segments={enTitleDiff.newSegments} variant="new" subtle />
                  </p>
                )}
              </div>

              <section
                className="border-t border-line/75 bg-surface-subtle/40 px-4 py-3"
                aria-label="Lý do điều chỉnh"
              >
                <p className="flex items-center gap-1.5 text-xs font-medium text-ink-muted">
                  <MessageSquareQuote className="size-3.5 text-ink-subtle" aria-hidden="true" />
                  Lý do đổi tên
                </p>
                <RichTextContent
                  content={request.reason}
                  className="mt-1 text-[13px] leading-relaxed text-ink-secondary"
                />
              </section>
            </div>
          </section>

          {request.rejectionReason && (
            <InlineAlert tone="error" title="Lý do từ chối đã ghi nhận">
              <p>{request.rejectionReason}</p>
              {request.rejectedAt && (
                <p className="mt-1 text-xs opacity-80 tabular-nums">
                  Thời điểm phản hồi: {formatRenameDateTime(request.rejectedAt)}
                </p>
              )}
            </InlineAlert>
          )}

          {request.status === 'Completed' && (
            <InlineAlert
              tone="success"
              title="Tên đề tài mới đã có hiệu lực"
              action={
                <Link
                  to="/thesis/proposal"
                  onClick={onClose}
                  className="inline-flex items-center gap-1 rounded-lg border border-tone-success-border bg-surface-card px-3 py-1.5 text-xs font-semibold text-tone-success-text transition-colors hover:bg-tone-success-bg"
                >
                  Nộp lại Đề cương cập nhật
                  <ArrowUpRight className="size-3.5" aria-hidden="true" />
                </Link>
              }
            >
              Phòng đào tạo đã cập nhật tên chính thức vào danh mục và mở khóa nộp lại Đề cương cho nhóm.
            </InlineAlert>
          )}

          <section
            className="grid grid-cols-1 gap-3 rounded-xl border border-line/80 bg-surface-subtle/50 p-3.5 sm:grid-cols-2"
            aria-label="Người gửi và giảng viên hướng dẫn"
          >
            <InfoCell
              leading={<Avatar name={requesterName} className="size-8 text-[11px]" />}
              title={<span className="text-xs font-semibold">{requesterName}</span>}
              titleClassName="text-xs"
              description={
                <span className="tabular-nums">
                  Người gửi đơn{requester?.userCode ? ` · ${requester.userCode}` : ''}
                </span>
              }
            />
            <InfoCell
              leading={
                <span className="flex size-8 items-center justify-center rounded-full border border-line-input bg-surface-card text-ink-muted">
                  <UserCheck className="size-4" aria-hidden="true" />
                </span>
              }
              title={<span className="text-xs font-semibold">{advisorDisplay}</span>}
              titleClassName="text-xs"
              description="Giảng viên hướng dẫn (Cấp 1)"
            />
          </section>
        </div>

        <div className="flex flex-col justify-between gap-5 bg-surface-subtle/60 p-5 sm:p-6 lg:col-span-5">
          <RenameApprovalTimeline request={request} advisorName={advisorDisplay} />

          <div className="flex flex-wrap items-center justify-end gap-2.5 border-t border-line/80 pt-4">
            {isRejected && canReuse && (
              <Button
                type="button"
                leftIcon={<RotateCcw />}
                onClick={() => {
                  onReuse(request);
                  onClose();
                }}
                className="flex-1 rounded-xl sm:flex-none"
              >
                Dùng lại nội dung
              </Button>
            )}
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              className="rounded-xl border-line-input bg-surface-card"
            >
              Đóng
            </Button>
          </div>
        </div>
      </div>
    </Dialog>
  );
}
