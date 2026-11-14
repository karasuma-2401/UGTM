import { ArrowRight, Calendar, CheckCircle2, Clock3, MessageSquareText, RotateCcw } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { cn } from '@/lib/utils';
import type {
  ProgressMilestoneDto,
  ProgressReportStatus,
  ProgressReportVersionDto,
} from '../../types/milestone.types';
import {
  formatMilestoneDateTime,
  getDeadlineInfo,
} from '../../utils/milestoneFormatters';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';

interface StudentMilestoneBriefCardProps {
  milestone: ProgressMilestoneDto;
  milestoneIndex: number;
  currentStatus: ProgressReportStatus;
  currentVersionNo?: number | null;
  versions: ProgressReportVersionDto[];
  nextActionLabel?: string | null;
  onJumpToNextMilestone?: () => void;
}

export function StudentMilestoneBriefCard({
  milestone,
  milestoneIndex,
  currentStatus,
  currentVersionNo,
  versions,
  nextActionLabel,
  onJumpToNextMilestone,
}: StudentMilestoneBriefCardProps) {
  const deadline = getDeadlineInfo(milestone.deadlineUtc, currentStatus);

  const sortedVersions = [...versions].sort((a, b) => b.versionNo - a.versionNo);
  const latestReviewed = sortedVersions.find((version) => version.review != null);
  const latestVersion = sortedVersions[0];

  const reviewTone =
    currentStatus === 'Accepted' || latestReviewed?.review?.decision === 'Accepted'
      ? 'emerald'
      : currentStatus === 'NeedRevision' || latestReviewed?.review?.decision === 'NeedRevision'
        ? 'amber'
        : currentStatus === 'Submitted'
          ? 'blue'
          : 'neutral';

  const reviewAccentClass = {
    emerald: 'border-slate-200/80 bg-slate-50/70 text-slate-800',
    amber: 'border-amber-200/80 bg-amber-50/55 text-amber-950',
    blue: 'border-slate-200/80 bg-slate-50/70 text-slate-800',
    neutral: 'border-slate-200/80 bg-slate-50/60 text-slate-700',
  }[reviewTone];

  const deadlinePillClass = {
    completed: 'bg-slate-100/80 text-slate-600 border-slate-200/70',
    overdue: 'bg-rose-50 text-rose-700 border-rose-200/70',
    soon: 'bg-amber-50 text-amber-800 border-amber-200/70',
    normal: 'bg-slate-100/80 text-slate-600 border-slate-200/70',
  }[deadline.urgency];

  return (
    <Card className="bg-white border-slate-200/80 shadow-xs rounded-2xl overflow-hidden">
      <div className="px-5 sm:px-6 py-4 border-b border-slate-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-400 tabular-nums">
              Mốc {String(milestoneIndex + 1).padStart(2, '0')}
            </span>
            <span aria-hidden="true" className="text-slate-300">·</span>
            <MilestoneStatusBadge
              status={currentStatus}
              versionNo={currentVersionNo && currentVersionNo > 0 ? currentVersionNo : null}
            />
          </div>
          <h2 className="text-base sm:text-lg font-bold text-slate-900 leading-snug">
            {milestone.title}
          </h2>
        </div>

        <div className="flex flex-col sm:items-end gap-1.5 shrink-0">
          <div
            className={cn(
              'inline-flex items-center gap-2 px-3 py-1.5 rounded-xl border text-xs shrink-0 self-start sm:self-auto',
              deadlinePillClass,
            )}
          >
            <Calendar className="size-3.5 shrink-0 opacity-75" />
            <div className="tabular-nums">
              <span className="font-semibold">{formatMilestoneDateTime(milestone.deadlineUtc)}</span>
              <span className="mx-1.5 opacity-60">·</span>
              <span>{deadline.label}</span>
            </div>
          </div>

          {onJumpToNextMilestone && nextActionLabel && (
            <button
              type="button"
              onClick={onJumpToNextMilestone}
              className="inline-flex items-center gap-1 text-xs font-medium text-[#2054a5] hover:underline cursor-pointer self-start sm:self-end"
            >
              <span>{nextActionLabel}</span>
              <ArrowRight className="size-3" />
            </button>
          )}
        </div>
      </div>

      <div className="divide-y divide-slate-100">
        <div className="px-5 sm:px-6 py-4 space-y-1.5">
          <p className="text-xs font-semibold text-slate-500">Yêu cầu & đầu mục cần nộp</p>
          <RichTextContent
            content={milestone.description}
            className="text-sm text-slate-700"
            emptyFallback={
              <span className="text-xs text-slate-500">
                GVHD chưa ghi chú mô tả chi tiết cho mốc này. Sinh viên nộp báo cáo tiến độ và liên kết mã nguồn theo kế hoạch nhóm.
              </span>
            }
          />
        </div>

        <div className="px-5 sm:px-6 py-4 space-y-2">
          <p className="text-xs font-semibold text-slate-500">Đánh giá & nhận xét từ GVHD</p>
          <div className={cn('rounded-xl border px-4 py-3 space-y-1.5', reviewAccentClass)}>
            {latestReviewed?.review ? (
              <>
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                  <span className="inline-flex items-center gap-1.5 font-semibold">
                    {latestReviewed.review.decision === 'Accepted' ? (
                      <CheckCircle2 className="size-4 text-emerald-600" />
                    ) : (
                      <RotateCcw className="size-4 text-amber-600" />
                    )}
                    {latestReviewed.review.decision === 'Accepted'
                      ? `GVHD đã xác nhận Đạt (v${latestReviewed.versionNo})`
                      : `GVHD yêu cầu chỉnh sửa (v${latestReviewed.versionNo})`}
                  </span>
                  <span className="opacity-80 tabular-nums">
                    {formatMilestoneDateTime(latestReviewed.review.reviewedAtUtc)}
                  </span>
                </div>
                {latestReviewed.review.feedback ? (
                  <RichTextContent content={latestReviewed.review.feedback} className="text-sm" />
                ) : (
                  <p className="text-xs opacity-90">
                    {latestReviewed.review.decision === 'Accepted'
                      ? 'Mốc báo cáo đã đáp ứng yêu cầu và được GVHD ghi nhận Đạt.'
                      : 'Vui lòng rà soát lại nội dung báo cáo hoặc mã nguồn và nộp phiên bản cập nhật.'}
                  </p>
                )}
              </>
            ) : currentStatus === 'Accepted' ? (
              <div className="flex items-start gap-2.5 text-sm">
                <CheckCircle2 className="size-4 text-emerald-600 shrink-0 mt-0.5" />
                <p>
                  GVHD đã xác nhận <strong>Đạt</strong> cho mốc báo cáo này
                  {latestVersion ? ` (phiên bản v${latestVersion.versionNo})` : ''}. Nhóm có thể chuyển sang mốc tiếp theo.
                </p>
              </div>
            ) : currentStatus === 'Submitted' ? (
              <div className="flex items-start gap-2.5 text-sm">
                <Clock3 className="size-4 text-[#2054a5] shrink-0 mt-0.5" />
                <p>
                  Bài nộp phiên bản <strong>v{latestVersion?.versionNo ?? currentVersionNo ?? 1}</strong> đang chờ GVHD xem xét và phản hồi.
                </p>
              </div>
            ) : currentStatus === 'NeedRevision' ? (
              <div className="flex items-start gap-2.5 text-sm">
                <RotateCcw className="size-4 text-amber-600 shrink-0 mt-0.5" />
                <p>
                  GVHD yêu cầu chỉnh sửa lại bài nộp. Hãy cập nhật tệp báo cáo hoặc liên kết mã nguồn và gửi phiên bản tiếp theo.
                </p>
              </div>
            ) : (
              <div className="flex items-start gap-2.5 text-xs text-slate-600">
                <MessageSquareText className="size-4 text-slate-400 shrink-0 mt-0.5" />
                <p>Chưa có bài nộp cho mốc này. Hãy tải lên báo cáo và liên kết mã nguồn để GVHD đánh giá.</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </Card>
  );
}
