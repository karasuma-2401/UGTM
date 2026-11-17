import React from 'react';
import { AlertCircle, Check } from 'lucide-react';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';

export interface ArchivalPipelineStripProps {
  submissionStatus: string;
  currentVersionNo: number;
  submittedAt: string | null;
  advisorConfirmedAt: string | null;
  archivedConfirmedAt: string | null;
}

type StepState = 'completed' | 'active' | 'revision' | 'upcoming';

interface MilestoneItem {
  number: string;
  title: string;
  detail: string;
  state: StepState;
}

/**
 * Compact 3-milestone hairline progress strip replacing oversized equal-width boxed step cards.
 */
export const ArchivalPipelineStrip: React.FC<ArchivalPipelineStripProps> = ({
  submissionStatus,
  currentVersionNo,
  submittedAt,
  advisorConfirmedAt,
  archivedConfirmedAt,
}) => {
  const step1State: StepState =
    submissionStatus === 'NeedRevision'
      ? 'revision'
      : submissionStatus === 'NotSubmitted'
        ? 'active'
        : 'completed';

  const step2State: StepState =
    submissionStatus === 'AdvisorConfirmed' || submissionStatus === 'ArchiveConfirmed'
      ? 'completed'
      : submissionStatus === 'Submitted'
        ? 'active'
        : submissionStatus === 'NeedRevision'
          ? 'revision'
          : 'upcoming';

  const step3State: StepState =
    submissionStatus === 'ArchiveConfirmed'
      ? 'completed'
      : submissionStatus === 'AdvisorConfirmed'
        ? 'active'
        : 'upcoming';

  const milestones: MilestoneItem[] = [
    {
      number: '1',
      title: 'Sinh viên nộp hồ sơ',
      detail:
        submissionStatus === 'NeedRevision'
          ? 'Yêu cầu chỉnh sửa và nộp lại'
          : submittedAt
            ? `Đã nộp v${Math.max(1, currentVersionNo)} · ${formatDateTimeVi(submittedAt)}`
            : 'Tải lên PDF, DOCX và metadata VI/EN',
      state: step1State,
    },
    {
      number: '2',
      title: 'GVHD xác nhận',
      detail: advisorConfirmedAt
        ? `Xác nhận lúc ${formatDateTimeVi(advisorConfirmedAt)}`
        : submissionStatus === 'Submitted'
          ? 'Đang chờ GVHD kiểm tra bản sửa'
          : 'Kiểm tra nội dung chỉnh sửa sau bảo vệ',
      state: step2State,
    },
    {
      number: '3',
      title: 'Khoa nghiệm thu',
      detail: archivedConfirmedAt
        ? `Hoàn tất lúc ${formatDateTimeVi(archivedConfirmedAt)}`
        : submissionStatus === 'AdvisorConfirmed'
          ? 'Đang chờ Khoa kiểm duyệt lưu chiểu'
          : 'Nghiệm thu & lưu kho khóa luận',
      state: step3State,
    },
  ];

  return (
    <section
      aria-label="Tiến trình lưu chiểu 3 bước"
      className="rounded-2xl border border-line/80 bg-surface-card shadow-[0_1px_2px_rgba(15,23,42,0.03)] overflow-hidden"
    >
      <ol className="grid grid-cols-1 md:grid-cols-3 divide-y md:divide-y-0 md:divide-x divide-line-subtle">
        {milestones.map((step) => (
          <li
            key={step.number}
            className={cn(
              'px-4 py-3.5 sm:px-5 flex items-start gap-3 transition-colors',
              step.state === 'active' && 'bg-primary/[0.035]',
              step.state === 'revision' && 'bg-tone-danger-bg/35',
            )}
          >
            <span
              aria-hidden="true"
              className={cn(
                'mt-0.5 size-6 rounded-full flex items-center justify-center text-xs font-semibold shrink-0 tabular-nums border',
                step.state === 'completed' &&
                  'border-tone-success-border bg-tone-success-bg text-tone-success-strong',
                step.state === 'active' &&
                  'border-primary bg-primary text-white shadow-2xs',
                step.state === 'revision' &&
                  'border-tone-danger-border bg-tone-danger-bg text-tone-danger-strong',
                step.state === 'upcoming' &&
                  'border-line-input bg-surface-subtle text-ink-muted',
              )}
            >
              {step.state === 'completed' ? (
                <Check className="size-3.5 stroke-[2.5]" />
              ) : step.state === 'revision' ? (
                <AlertCircle className="size-3.5" />
              ) : (
                step.number
              )}
            </span>

            <div className="min-w-0 flex-1">
              <p
                className={cn(
                  'text-sm font-semibold leading-tight',
                  step.state === 'active'
                    ? 'text-primary'
                    : step.state === 'revision'
                      ? 'text-tone-danger-strong'
                      : step.state === 'upcoming'
                        ? 'text-ink-muted'
                        : 'text-ink-primary',
                )}
              >
                {step.title}
              </p>
              <p className="mt-1 text-xs text-ink-muted leading-snug tabular-nums">
                {step.detail}
              </p>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
};
