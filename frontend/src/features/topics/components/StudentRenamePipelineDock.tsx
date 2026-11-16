import { Avatar } from '@/components/ui/avatar';
import { Card } from '@/components/ui/card';
import { ProgressBar } from '@/components/ui/progress';
import { cn } from '@/lib/utils';
import { GROUP_STATUS_LABELS, type GroupStatus } from '../types/advising.types';
import type { RenameRequest } from '../types/renameRequest.types';
import type {
  RegistrationTimeline,
  StudentTopicGroupDetail,
} from '../types/studentTopic.types';
import { formatRenameDate, formatRenameDateTime } from '../renameReview';
import { RenameApprovalTimeline } from './RenameApprovalTimeline';

interface StudentRenamePipelineDockProps {
  group: StudentTopicGroupDetail;
  timeline?: RegistrationTimeline;
  activeRequest?: RenameRequest;
  latestRequest?: RenameRequest;
  windowClosed: boolean;
  windowReason: 'batch-closed' | 'not-started' | 'expired' | 'missing-stages' | null;
  nowMs: number;
}

const memberStatusLabels: Record<string, string> = {
  Confirmed: 'Đã xác nhận',
  Pending: 'Chờ xác nhận',
  Expired: 'Hết hạn',
};

function computeWindowProgress(
  nowMs: number,
  startDate?: string,
  deadlineAtUtc?: string,
  windowClosed?: boolean,
  windowReason?: StudentRenamePipelineDockProps['windowReason'],
) {
  if (!startDate || !deadlineAtUtc) {
    return { percentage: 0, statusLabel: 'Chưa có lịch', statusClass: 'text-ink-muted' };
  }
  const startMs = Date.parse(`${startDate}T00:00:00+07:00`);
  const endMs = Date.parse(deadlineAtUtc);

  if (Number.isNaN(startMs) || Number.isNaN(endMs) || endMs <= startMs) {
    return { percentage: 0, statusLabel: 'Không xác định', statusClass: 'text-ink-muted' };
  }

  if (windowReason === 'not-started') {
    const daysUntilStart = Math.max(1, Math.ceil((startMs - nowMs) / 86_400_000));
    return {
      percentage: 0,
      statusLabel: `Mở sau ${daysUntilStart} ngày`,
      statusClass: 'text-primary',
    };
  }

  if (windowClosed) {
    return {
      percentage: 100,
      statusLabel: windowReason === 'batch-closed' ? 'Đợt chưa mở' : 'Đã hết hạn',
      statusClass: 'text-tone-warning-text',
    };
  }

  const ratio = ((nowMs - startMs) / (endMs - startMs)) * 100;
  const percentage = Math.min(100, Math.max(4, Math.round(ratio)));
  const remainingMs = endMs - nowMs;
  const remainingDays = Math.ceil(remainingMs / 86_400_000);

  return {
    percentage,
    statusLabel: `Còn ${Math.max(1, remainingDays)} ngày`,
    statusClass: remainingDays <= 3 ? 'text-tone-warning-text' : 'text-tone-success-text',
  };
}

export function StudentRenamePipelineDock({
  group,
  timeline,
  activeRequest,
  latestRequest,
  windowClosed,
  windowReason,
  nowMs,
}: StudentRenamePipelineDockProps) {
  const stage3 = timeline?.stages.find(
    (stage) => stage.stageCode === 'PROGRESS_EXECUTION' || stage.orderIndex === 3,
  );
  const stage4 = timeline?.stages.find(
    (stage) => stage.stageCode === 'MIDTERM_GATE' || stage.orderIndex === 4,
  );

  const trackedRequest = activeRequest ?? latestRequest ?? null;
  const pipelineHeading = activeRequest
    ? 'Tiến trình đơn đang chờ'
    : latestRequest
      ? 'Tiến trình đơn gần nhất'
      : 'Quy trình 3 cấp';

  const windowStats = computeWindowProgress(
    nowMs,
    stage3?.startDate,
    stage4?.deadlineAtUtc,
    windowClosed,
    windowReason,
  );

  const groupStatusLabel =
    GROUP_STATUS_LABELS[group.groupStatus as GroupStatus] ?? group.groupStatus;
  const shortGroupId = group.groupId.slice(0, 8).toUpperCase();

  return (
    <aside className="lg:sticky lg:top-20" aria-label="Theo dõi phê duyệt và thông tin nhóm">
      <Card className="divide-y divide-line-subtle overflow-hidden rounded-2xl border-line/80 bg-surface-card shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
        {/* 1. Thời hạn Tuần 3 - 10 */}
        <section className="space-y-2.5 p-5" aria-label="Thời hạn đổi tên đề tài">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-ink-primary">Thời hạn (Tuần 3–10)</h2>
            <span className={cn('text-xs font-semibold tabular-nums', windowStats.statusClass)}>
              {windowStats.statusLabel}
            </span>
          </div>

          {stage3 && stage4 ? (
            <>
              <ProgressBar
                value={windowStats.percentage}
                showValue={false}
                size="sm"
                variant={windowClosed ? 'warning' : 'gradient'}
                aria-label="Tiến độ thời gian cho phép"
              />
              <div className="flex items-center justify-between gap-2 text-xs tabular-nums text-ink-muted">
                <span>Mở: {formatRenameDate(stage3.startDate)}</span>
                <span>Hạn: {formatRenameDateTime(stage4.deadlineAtUtc)}</span>
              </div>
            </>
          ) : (
            <p className="text-xs text-ink-muted">
              Chưa có thông tin mốc thời gian thực hiện của đợt.
            </p>
          )}
        </section>

        {/* 2. Tiến trình phê duyệt 3 cấp dạng phẳng */}
        <div className="p-5">
          <RenameApprovalTimeline
            request={trackedRequest}
            title={pipelineHeading}
            compact
            flat
            advisorName={group.advisor.fullName}
          />
        </div>

        {/* 3. Thông tin nhóm & GVHD dạng key-value cô đọng */}
        <section className="space-y-4 p-5" aria-label="Thông tin nhóm và giảng viên hướng dẫn">
          <div className="flex items-baseline justify-between gap-2">
            <h2 className="text-sm font-semibold text-ink-primary">Thông tin nhóm</h2>
            <span
              className="font-mono text-xs text-ink-muted"
              title={`Mã nhóm đầy đủ: ${group.groupId}`}
            >
              #{shortGroupId}
            </span>
          </div>

          <dl className="space-y-2 text-xs">
            <div className="flex items-baseline justify-between gap-3">
              <dt className="shrink-0 text-ink-muted">Đợt khóa luận</dt>
              <dd className="min-w-0 truncate text-right font-medium text-ink-heading" title={group.batchName ?? group.batchId}>
                {group.batchName || 'Đợt hiện hành'}
              </dd>
            </div>
            <div className="flex items-baseline justify-between gap-3">
              <dt className="shrink-0 text-ink-muted">Trạng thái</dt>
              <dd className="text-right font-medium text-ink-primary">{groupStatusLabel}</dd>
            </div>
          </dl>

          <div className="space-y-2 border-t border-line-subtle/80 pt-3">
            <p className="text-xs font-medium text-ink-muted">Giảng viên hướng dẫn</p>
            <div className="flex items-center gap-2.5">
              <Avatar
                name={group.advisor.fullName || 'GVHD'}
                muted={!group.advisor.fullName}
                className="size-7 text-[10px]"
              />
              <span className="min-w-0 truncate text-xs font-semibold text-ink-heading">
                {group.advisor.fullName || 'Chưa phân công'}
              </span>
            </div>
          </div>

          <div className="space-y-2 border-t border-line-subtle/80 pt-3">
            <p className="text-xs font-medium text-ink-muted">
              Thành viên ({group.members.length})
            </p>
            <ul className="space-y-2">
              {group.members.map((member) => {
                const displayName = member.fullName || member.userCode || 'Sinh viên';
                const needsAttention = member.confirmationStatus !== 'Confirmed';
                return (
                  <li key={member.studentId} className="flex items-center justify-between gap-2">
                    <div className="flex min-w-0 items-center gap-2.5">
                      <Avatar name={displayName} className="size-7 text-[10px]" />
                      <div className="min-w-0">
                        <p className="truncate text-xs font-semibold text-ink-heading">
                          {displayName}
                        </p>
                        {member.userCode && (
                          <p className="text-[11px] tabular-nums text-ink-muted">
                            {member.userCode}
                          </p>
                        )}
                      </div>
                    </div>
                    {needsAttention && (
                      <span className="shrink-0 text-[11px] font-medium text-tone-warning-text">
                        {memberStatusLabels[member.confirmationStatus] ?? member.confirmationStatus}
                      </span>
                    )}
                  </li>
                );
              })}
            </ul>
          </div>
        </section>
      </Card>
    </aside>
  );
}
