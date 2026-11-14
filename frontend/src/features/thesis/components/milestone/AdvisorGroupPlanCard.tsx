import { ArrowUpRight, CalendarClock, Download, FileText, GitBranch, Users } from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { GROUP_STATUS_LABELS, type GroupStatus } from '@/features/topics/types/advising.types';
import { cn } from '@/lib/utils';
import type { MilestoneStats } from '../../hooks/useAdvisorMilestones';
import type {
  AdvisedGroupOption,
  MilestoneWithReportDto,
  ProgressReportStatus,
} from '../../types/milestone.types';
import {
  formatFileSize,
  formatMilestoneDateTime,
  getSafeHttpUrl,
  toReportStatus,
} from '../../utils/milestoneFormatters';

export interface AdvisorGroupPlanCardProps {
  selectedGroup: AdvisedGroupOption | null;
  selectedMilestone: MilestoneWithReportDto | null;
  stats: MilestoneStats;
  onOpenReview?: (milestoneId: string) => void;
  onDownloadFile?: (fileId: string, fileName?: string) => void;
  downloadingFileId?: string | null;
}

const GROUP_STATUS_DOT_CLASS: Record<string, string> = {
  SyllabusApproved: 'bg-blue-600',
  InProgress: 'bg-blue-600',
  MidtermSubmitted: 'bg-blue-600',
  FinalReportSubmitted: 'bg-blue-600',
  ReadyForReviewerAssignment: 'bg-blue-600',
  Defended: 'bg-emerald-600',
  ScoreLocked: 'bg-emerald-600',
  Completed: 'bg-emerald-600',
  SyllabusSubmitted: 'bg-amber-500',
  SyllabusRevisionRequired: 'bg-amber-500',
  Stopped: 'bg-rose-500',
  Registered: 'bg-slate-400',
};

const MILESTONE_STATUS_META: Record<
  ProgressReportStatus,
  { label: string; dotClass: string }
> = {
  Accepted: { label: 'Đạt', dotClass: 'bg-emerald-600' },
  NeedRevision: { label: 'Cần chỉnh sửa', dotClass: 'bg-amber-500' },
  Submitted: { label: 'Chờ đánh giá', dotClass: 'bg-blue-600' },
  Rejected: { label: 'Không đạt', dotClass: 'bg-rose-500' },
  NotSubmitted: { label: 'Chưa nộp', dotClass: 'bg-slate-400' },
};

export function AdvisorGroupPlanCard({
  selectedGroup,
  selectedMilestone,
  stats,
  onOpenReview,
  onDownloadFile,
  downloadingFileId = null,
}: AdvisorGroupPlanCardProps) {
  if (!selectedGroup) {
    return (
      <aside className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-2xs">
        <div className="space-y-2 text-center py-6">
          <span className="mx-auto inline-flex size-10 items-center justify-center rounded-xl bg-slate-100 text-slate-600">
            <Users className="size-5" aria-hidden="true" />
          </span>
          <h3 className="text-sm font-semibold text-slate-900">Thông tin nhóm thực hiện</h3>
          <p className="text-xs leading-relaxed text-slate-600">
            Chọn một nhóm hướng dẫn ở thanh công cụ phía trên để theo dõi tiến độ và chi tiết thành viên.
          </p>
        </div>
      </aside>
    );
  }

  const groupStatusKey = selectedGroup.status as GroupStatus | undefined;
  const groupStatusLabel =
    (groupStatusKey && GROUP_STATUS_LABELS[groupStatusKey]) ||
    selectedGroup.status ||
    'Đang theo dõi';
  const groupStatusDotClass =
    (selectedGroup.status && GROUP_STATUS_DOT_CLASS[selectedGroup.status]) || 'bg-slate-400';

  const progressPct =
    stats.total > 0 ? Math.round((stats.accepted / stats.total) * 100) : 0;

  const latestVersion =
    selectedMilestone?.report?.versions
      ?.slice()
      .sort((a, b) => b.versionNo - a.versionNo)[0] ?? null;
  const safeGitUrl = getSafeHttpUrl(latestVersion?.gitUrl);
  const selectedMilestoneStatus = toReportStatus(selectedMilestone?.report?.currentStatus);
  const selectedMilestoneMeta = MILESTONE_STATUS_META[selectedMilestoneStatus];

  return (
    <aside className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-2xs">
      {/* Group header & progress */}
      <div className="space-y-4 border-b border-slate-100 p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <span className="inline-flex items-center rounded-md border border-slate-200/90 bg-slate-50 px-2 py-0.5 font-mono text-xs font-semibold text-slate-700">
            {selectedGroup.groupCode}
          </span>
          <span className="inline-flex items-center gap-1.5 rounded-full border border-slate-200/90 bg-white px-2.5 py-0.5 text-xs font-medium text-slate-700">
            <span className={cn('size-1.5 rounded-full', groupStatusDotClass)} aria-hidden="true" />
            <span>{groupStatusLabel}</span>
          </span>
        </div>

        <h3 className="text-sm font-semibold leading-snug text-slate-900">
          {selectedGroup.topicTitleVi}
        </h3>

        <div className="space-y-1.5">
          <div className="flex items-center justify-between text-xs">
            <span className="font-medium text-slate-600">Tiến độ hoàn thành mốc</span>
            <span className="font-semibold tabular-nums text-slate-900">
              {stats.accepted}/{stats.total} mốc đạt ({progressPct}%)
            </span>
          </div>
          <div
            role="progressbar"
            aria-valuenow={progressPct}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-label="Tỷ lệ hoàn thành mốc tiến độ"
            className="h-2 w-full overflow-hidden rounded-full bg-slate-100"
          >
            <div
              className={cn(
                'h-full rounded-full transition-all duration-300',
                progressPct === 100 ? 'bg-emerald-600' : 'bg-primary',
              )}
              style={{ width: `${progressPct}%` }}
            />
          </div>
          {stats.submitted > 0 && (
            <p className="text-xs text-slate-600">
              Có <span className="font-semibold text-slate-800">{stats.submitted}</span> báo cáo đang chờ GVHD đánh giá.
            </p>
          )}
        </div>
      </div>

      {/* Team members */}
      <div className="space-y-2.5 border-b border-slate-100 p-5">
        <div className="flex items-center justify-between">
          <h4 className="text-xs font-semibold text-slate-600">Thành viên nhóm</h4>
          <span className="text-xs tabular-nums text-slate-600">
            {selectedGroup.students.length} sinh viên
          </span>
        </div>

        <ul className="space-y-2">
          {selectedGroup.students.map((student) => (
            <li
              key={student.studentId}
              className="flex items-center justify-between gap-2.5 rounded-lg border border-slate-100 bg-slate-50/70 px-2.5 py-2"
            >
              <div className="flex min-w-0 items-center gap-2.5">
                <Avatar name={student.fullName} className="size-7 shrink-0 text-[11px]" />
                <span className="truncate text-sm font-medium text-slate-800">
                  {student.fullName}
                </span>
              </div>
              {student.userCode && (
                <span className="shrink-0 font-mono text-xs text-slate-600">
                  {student.userCode}
                </span>
              )}
            </li>
          ))}
        </ul>
      </div>

      {/* Selected milestone preview */}
      <div className="space-y-3 p-5">
        <div className="flex items-center justify-between gap-2">
          <h4 className="text-xs font-semibold text-slate-600">Mốc đang chọn</h4>
          {selectedMilestone && (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-slate-200/90 bg-white px-2.5 py-0.5 text-xs font-medium text-slate-700">
              <span
                className={cn('size-1.5 rounded-full', selectedMilestoneMeta.dotClass)}
                aria-hidden="true"
              />
              <span>{selectedMilestoneMeta.label}</span>
            </span>
          )}
        </div>

        {!selectedMilestone ? (
          <p className="text-xs leading-relaxed text-slate-600">
            Chọn một mốc trong bảng bên cạnh để xem chi tiết yêu cầu sản phẩm và tệp báo cáo mới nhất.
          </p>
        ) : (
          <div className="space-y-3">
            <div className="space-y-1">
              <p className="text-sm font-semibold text-slate-900">
                {selectedMilestone.milestone.title}
              </p>
              <p className="flex items-center gap-1.5 text-xs text-slate-600">
                <CalendarClock className="size-3.5 text-slate-500" aria-hidden="true" />
                <span>Hạn nộp: {formatMilestoneDateTime(selectedMilestone.milestone.deadlineUtc)}</span>
              </p>
            </div>

            <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3 text-xs leading-relaxed text-slate-700">
              <RichTextContent
                content={selectedMilestone.milestone.description}
                emptyFallback="Chưa có mô tả chi tiết yêu cầu sản phẩm."
              />
            </div>

            {latestVersion && (
              <div className="space-y-2 border-t border-slate-100 pt-3">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-semibold text-slate-700">
                    Bài nộp mới nhất (v{latestVersion.versionNo})
                  </span>
                  <span className={cn('text-slate-600', latestVersion.isLate && 'font-medium text-amber-700')}>
                    {latestVersion.isLate ? 'Nộp trễ hạn' : 'Đúng hạn'}
                  </span>
                </div>

                {latestVersion.file && (
                  <div className="flex items-center justify-between gap-2 rounded-lg border border-slate-200/80 bg-white px-2.5 py-2">
                    <div className="flex min-w-0 items-center gap-2">
                      <FileText className="size-4 shrink-0 text-slate-500" aria-hidden="true" />
                      <div className="min-w-0">
                        <p className="truncate text-xs font-medium text-slate-800">
                          {latestVersion.file.fileName}
                        </p>
                        <p className="text-[11px] text-slate-500">
                          {formatFileSize(latestVersion.file.fileSize)}
                        </p>
                      </div>
                    </div>
                    {onDownloadFile && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        disabled={downloadingFileId === latestVersion.file.id}
                        onClick={() =>
                          onDownloadFile(latestVersion.file!.id, latestVersion.file!.fileName)
                        }
                        aria-label="Tải tệp báo cáo"
                        className="size-7 shrink-0 p-0 text-slate-600 hover:bg-slate-100 hover:text-slate-900"
                      >
                        <Download className="size-3.5" aria-hidden="true" />
                      </Button>
                    )}
                  </div>
                )}

                {safeGitUrl && (
                  <a
                    href={safeGitUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex max-w-full items-center gap-1.5 text-xs font-medium text-slate-700 hover:text-primary hover:underline"
                  >
                    <GitBranch className="size-3.5 shrink-0 text-slate-400" aria-hidden="true" />
                    <span className="truncate">{latestVersion.gitUrl}</span>
                    <ArrowUpRight className="size-3 shrink-0" aria-hidden="true" />
                  </a>
                )}
              </div>
            )}

            {onOpenReview && (
              <Button
                type="button"
                variant={
                  selectedMilestone.report?.currentStatus === 'Submitted'
                    ? 'default'
                    : 'outline'
                }
                size="sm"
                onClick={() => onOpenReview(selectedMilestone.milestone.id)}
                className="w-full text-xs"
              >
                {selectedMilestone.report?.currentStatus === 'Submitted'
                  ? 'Đánh giá báo cáo'
                  : 'Xem chi tiết & lịch sử nộp'}
              </Button>
            )}
          </div>
        )}
      </div>
    </aside>
  );
}
