import React from 'react';
import { Link } from 'react-router-dom';
import { Check, Circle, ExternalLink, History, ShieldCheck } from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';

export interface ArchivalReadinessItem {
  key: string;
  label: string;
  ready: boolean;
  hint?: string;
}

export interface ArchivalDossierSidebarProps {
  submissionStatus: string;
  isLockedStatus: boolean;
  canEdit: boolean;
  hasSubmission: boolean;
  currentVersionNo: number;
  submissionDeadline: string | null;
  readinessItems: readonly ArchivalReadinessItem[];
  readyCount: number;
  feedbackText: string | null;
  feedbackContextLabel: string | null;
  feedbackLoading: boolean;
  advisorName: string;
  advisorStatusText: string;
  groupMembers: readonly { id: string; name: string; studentCode?: string }[];
  onOpenHistory: () => void;
}

/**
 * Sticky 340px hairline-divided right rail consolidating readiness checks, Stage 7 deadline,
 * post-defense feedback context, group/advisor info, and archival version history access.
 */
export const ArchivalDossierSidebar: React.FC<ArchivalDossierSidebarProps> = ({
  submissionStatus,
  isLockedStatus,
  canEdit,
  hasSubmission,
  currentVersionNo,
  submissionDeadline,
  readinessItems,
  readyCount,
  feedbackText,
  feedbackContextLabel,
  feedbackLoading,
  advisorName,
  advisorStatusText,
  groupMembers,
  onOpenHistory,
}) => {
  const showReadinessSection = canEdit || !hasSubmission;
  const totalChecks = readinessItems.length;

  return (
    <aside className="lg:sticky lg:top-6 space-y-4">
      <Card className="overflow-hidden border-line/80 shadow-[0_1px_2px_rgba(15,23,42,0.03)] divide-y divide-line-subtle">
        {/* Section 1: Readiness & Deadline */}
        <section className="p-5 space-y-3.5">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-ink-primary">
              {showReadinessSection ? 'Kiểm tra trước khi nộp' : 'Thời hạn & Phiên bản'}
            </h2>
            {showReadinessSection ? (
              <StatusBadge tone={readyCount === totalChecks ? 'success' : 'neutral'}>
                {readyCount}/{totalChecks}
              </StatusBadge>
            ) : (
              <span className="text-xs font-semibold text-ink-secondary tabular-nums">
                v{Math.max(1, currentVersionNo)}
              </span>
            )}
          </div>

          {showReadinessSection ? (
            <ul className="space-y-2.5">
              {readinessItems.map((item) => (
                <li key={item.key} className="flex items-start justify-between gap-2.5 text-sm">
                  <div className="flex items-start gap-2.5 min-w-0">
                    <span
                      aria-hidden="true"
                      className={cn(
                        'mt-0.5 size-4 rounded-full flex items-center justify-center shrink-0',
                        item.ready
                          ? 'bg-tone-success-bg text-tone-success-strong'
                          : 'text-ink-faint',
                      )}
                    >
                      {item.ready ? (
                        <Check className="size-3 stroke-[2.5]" />
                      ) : (
                        <Circle className="size-3.5" />
                      )}
                    </span>
                    <span
                      className={cn(
                        'leading-snug',
                        item.ready ? 'text-ink-primary font-medium' : 'text-ink-muted',
                      )}
                    >
                      {item.label}
                    </span>
                  </div>
                  {item.hint && (
                    <span className="text-xs text-ink-subtle tabular-nums shrink-0">
                      {item.hint}
                    </span>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex items-center gap-2.5 text-xs text-ink-body">
              <ShieldCheck className="size-4 text-primary shrink-0" aria-hidden="true" />
              <span>
                {submissionStatus === 'ArchiveConfirmed'
                  ? 'Hồ sơ đã hoàn tất nghiệm thu và lưu chiểu chính thức.'
                  : isLockedStatus
                    ? 'GVHD đã xác nhận bản chỉnh sửa, đang chờ Khoa nghiệm thu.'
                    : 'Hồ sơ đã nộp thành công, đang chờ GVHD kiểm tra và xác nhận.'}
              </span>
            </div>
          )}

          <div className="pt-3 border-t border-line/60 flex items-center justify-between gap-3 text-xs">
            <span className="text-ink-muted">Hạn nộp lưu chiểu</span>
            <span className="font-semibold text-ink-primary tabular-nums text-right">
              {submissionDeadline ? formatDateTimeVi(submissionDeadline) : 'Chưa cấu hình'}
            </span>
          </div>
        </section>

        {/* Section 2: Post-defense feedback */}
        <section className="p-5 space-y-3">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-ink-primary">Góp ý sau bảo vệ</h2>
            <Link
              to="/thesis/final-report"
              className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
            >
              Xem chi tiết
              <ExternalLink className="size-3" />
            </Link>
          </div>

          {feedbackLoading ? (
            <p className="text-xs text-ink-muted">Đang tải góp ý sau bảo vệ…</p>
          ) : feedbackText ? (
            <div className="rounded-xl border border-line/70 bg-surface-subtle/60 p-3 space-y-1.5">
              {feedbackContextLabel && (
                <p className="text-[11px] font-medium text-ink-muted">{feedbackContextLabel}</p>
              )}
              <p className="text-xs text-ink-heading leading-relaxed whitespace-pre-wrap line-clamp-5">
                {feedbackText}
              </p>
            </div>
          ) : (
            <p className="text-xs text-ink-muted leading-relaxed">
              Chưa có nhận xét bổ sung từ phản biện hoặc hội đồng sau bảo vệ.
            </p>
          )}

          <Link
            to="/councils/results"
            className="inline-flex items-center gap-1.5 text-xs font-medium text-ink-body hover:text-primary transition-colors"
          >
            Tra cứu điểm chính thức
            <ExternalLink className="size-3" />
          </Link>
        </section>

        {/* Section 3: Group, Advisor & Version History */}
        <section className="p-5 space-y-3.5">
          <div className="space-y-2.5">
            <div className="flex items-start justify-between gap-3 text-xs">
              <span className="text-ink-muted shrink-0">GVHD xác nhận</span>
              <div className="text-right min-w-0">
                <p className="font-semibold text-ink-primary truncate max-w-[190px]">
                  {advisorName}
                </p>
                <p className="text-[11px] text-ink-muted tabular-nums mt-0.5">
                  {advisorStatusText}
                </p>
              </div>
            </div>

            {groupMembers.length > 0 && (
              <div className="flex items-center justify-between gap-3 text-xs">
                <span className="text-ink-muted shrink-0">
                  Thành viên ({groupMembers.length})
                </span>
                <AvatarGroup
                  items={groupMembers.map((member) => ({
                    id: member.id,
                    name: member.name,
                    userCode: member.studentCode,
                  }))}
                  max={4}
                  size="sm"
                  ariaLabel="Thành viên nhóm khóa luận"
                />
              </div>
            )}
          </div>

          {hasSubmission ? (
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="w-full justify-center bg-surface-card"
              leftIcon={<History className="size-4" />}
              onClick={onOpenHistory}
            >
              Xem lịch sử hồ sơ (v1–v{Math.max(1, currentVersionNo)})
            </Button>
          ) : (
            <p className="text-xs text-ink-muted leading-relaxed">
              Lịch sử các phiên bản sẽ xuất hiện tại đây sau khi nhóm nộp hồ sơ đầu tiên.
            </p>
          )}
        </section>
      </Card>
    </aside>
  );
};
