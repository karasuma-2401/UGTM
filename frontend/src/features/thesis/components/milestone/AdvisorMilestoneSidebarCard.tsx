import { CalendarClock, Users } from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { RichTextContent } from '@/components/ui/rich-text-content';
import type { ProgressMilestoneDto } from '../../types/milestone.types';
import type { StudentGroupDetailDto } from '../../types/syllabus.types';
import { formatMilestoneDateTime } from '../../utils/milestoneFormatters';

export interface AdvisorMilestoneSidebarCardProps {
  milestone?: ProgressMilestoneDto | null;
  milestoneIndex?: number;
  groupDetail?: StudentGroupDetailDto | null;
  versionNo?: number;
  fileName?: string | null;
  gitUrl?: string | null;
}

export function AdvisorMilestoneSidebarCard({
  milestone,
  milestoneIndex,
  groupDetail,
  versionNo,
  fileName,
}: AdvisorMilestoneSidebarCardProps) {
  const members = groupDetail?.members ?? [];

  return (
    <aside className="overflow-hidden rounded-2xl border border-line/80 bg-surface-card shadow-2xs">
      <div className="space-y-2 border-b border-line-subtle p-5">
        <div className="flex items-center justify-between gap-2">
          <span className="inline-flex items-center rounded-md bg-tone-info-bg px-2 py-0.5 font-mono text-xs font-semibold text-primary">
            {groupDetail?.groupCode ?? 'Nhóm KLTN'}
          </span>
          {versionNo && (
            <span className="text-xs font-medium text-ink-body">Phiên bản v{versionNo}</span>
          )}
        </div>

        <h3 className="text-sm font-semibold leading-snug text-ink-primary">
          {milestoneIndex != null && milestoneIndex > 0 ? `Mốc ${milestoneIndex}: ` : ''}
          {milestone?.title ?? 'Chi tiết mốc tiến độ'}
        </h3>

        {milestone?.deadlineUtc && (
          <p className="flex items-center gap-1.5 text-xs text-ink-body">
            <CalendarClock className="size-3.5 text-ink-muted" aria-hidden="true" />
            <span>Hạn nộp: {formatMilestoneDateTime(milestone.deadlineUtc)}</span>
          </p>
        )}
      </div>

      {milestone && (
        <div className="space-y-2 border-b border-line-subtle p-5">
          <h4 className="text-xs font-semibold text-ink-body">Yêu cầu sản phẩm</h4>
          <div className="text-xs leading-relaxed text-ink-secondary">
            <RichTextContent
              content={milestone.description}
              emptyFallback="Chưa có mô tả chi tiết."
            />
          </div>
          {fileName && (
            <p className="truncate pt-1 text-xs text-ink-body">
              Tệp mới nhất: <span className="font-medium text-ink-heading">{fileName}</span>
            </p>
          )}
        </div>
      )}

      <div className="space-y-2.5 p-5">
        <h4 className="flex items-center gap-1.5 text-xs font-semibold text-ink-body">
          <Users className="size-3.5 text-ink-muted" aria-hidden="true" />
          <span>Thành viên nhóm ({members.length})</span>
        </h4>

        {members.length === 0 ? (
          <p className="text-xs text-ink-muted">Chưa có thông tin thành viên.</p>
        ) : (
          <ul className="space-y-2">
            {members.map((member) => (
              <li
                key={member.studentId}
                className="flex items-center justify-between gap-2 rounded-lg bg-surface-subtle/80 px-2.5 py-2"
              >
                <div className="flex min-w-0 items-center gap-2">
                  <Avatar name={member.fullName} className="size-6 shrink-0 text-[10px]" />
                  <span className="truncate text-xs font-medium text-ink-heading">
                    {member.fullName}
                  </span>
                </div>
                <span className="shrink-0 font-mono text-[11px] text-ink-body">
                  {member.userCode}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </aside>
  );
}
