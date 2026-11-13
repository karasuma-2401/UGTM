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
    <aside className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-2xs">
      <div className="space-y-2 border-b border-slate-100 p-5">
        <div className="flex items-center justify-between gap-2">
          <span className="inline-flex items-center rounded-md bg-blue-50 px-2 py-0.5 font-mono text-xs font-semibold text-primary">
            {groupDetail?.groupCode ?? 'Nhóm KLTN'}
          </span>
          {versionNo && (
            <span className="text-xs font-medium text-slate-600">Phiên bản v{versionNo}</span>
          )}
        </div>

        <h3 className="text-sm font-semibold leading-snug text-slate-900">
          {milestoneIndex != null && milestoneIndex > 0 ? `Mốc ${milestoneIndex}: ` : ''}
          {milestone?.title ?? 'Chi tiết mốc tiến độ'}
        </h3>

        {milestone?.deadlineUtc && (
          <p className="flex items-center gap-1.5 text-xs text-slate-600">
            <CalendarClock className="size-3.5 text-slate-500" aria-hidden="true" />
            <span>Hạn nộp: {formatMilestoneDateTime(milestone.deadlineUtc)}</span>
          </p>
        )}
      </div>

      {milestone && (
        <div className="space-y-2 border-b border-slate-100 p-5">
          <h4 className="text-xs font-semibold text-slate-600">Yêu cầu sản phẩm</h4>
          <div className="text-xs leading-relaxed text-slate-700">
            <RichTextContent
              content={milestone.description}
              emptyFallback="Chưa có mô tả chi tiết."
            />
          </div>
          {fileName && (
            <p className="truncate pt-1 text-xs text-slate-600">
              Tệp mới nhất: <span className="font-medium text-slate-800">{fileName}</span>
            </p>
          )}
        </div>
      )}

      <div className="space-y-2.5 p-5">
        <h4 className="flex items-center gap-1.5 text-xs font-semibold text-slate-600">
          <Users className="size-3.5 text-slate-500" aria-hidden="true" />
          <span>Thành viên nhóm ({members.length})</span>
        </h4>

        {members.length === 0 ? (
          <p className="text-xs text-slate-500">Chưa có thông tin thành viên.</p>
        ) : (
          <ul className="space-y-2">
            {members.map((member) => (
              <li
                key={member.studentId}
                className="flex items-center justify-between gap-2 rounded-lg bg-slate-50/80 px-2.5 py-2"
              >
                <div className="flex min-w-0 items-center gap-2">
                  <Avatar name={member.fullName} className="size-6 shrink-0 text-[10px]" />
                  <span className="truncate text-xs font-medium text-slate-800">
                    {member.fullName}
                  </span>
                </div>
                <span className="shrink-0 font-mono text-[11px] text-slate-600">
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
