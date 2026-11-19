import { ArrowRight, CalendarClock, Check, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  formatRenameDateTime,
  getRenameApprovalSteps,
  type RenameApprovalStep,
  type RenameReviewLevel,
} from '../renameReview';
import type { RenameRequest } from '../types/renameRequest.types';

const PREVIEW_STEPS: RenameApprovalStep[] = [
  {
    level: 'advisor',
    number: 1,
    actorLabel: 'Giảng viên hướng dẫn',
    state: 'pending',
    stateLabel: 'Bước 1',
  },
  {
    level: 'faculty',
    number: 2,
    actorLabel: 'Khoa thẩm định',
    state: 'pending',
    stateLabel: 'Bước 2',
  },
  {
    level: 'academic-affairs',
    number: 3,
    actorLabel: 'Phòng đào tạo',
    state: 'pending',
    stateLabel: 'Bước 3',
  },
];

interface RenameApprovalTimelineProps {
  request?: RenameRequest | null;
  activeLevel?: RenameReviewLevel;
  onSwitchLevel?: (level: RenameReviewLevel) => void;
  title?: string;
  compact?: boolean;
  flat?: boolean;
  advisorName?: string | null;
}

export function RenameApprovalTimeline({
  request,
  activeLevel,
  onSwitchLevel,
  title = 'Tiến trình phê duyệt 3 cấp',
  compact = false,
  flat = false,
  advisorName,
}: RenameApprovalTimelineProps) {
  const isPreview = !request;
  const steps = request ? getRenameApprovalSteps(request) : PREVIEW_STEPS;
  const approvedCount = steps.filter((step) => step.state === 'approved').length;
  const isAllApproved = !isPreview && approvedCount === steps.length;
  const hasRejected = !isPreview && steps.some((step) => step.state === 'rejected');

  const summaryText = isPreview
    ? '3 cấp duyệt'
    : isAllApproved
      ? '3/3 hoàn tất'
      : hasRejected
        ? `Dừng ở ${approvedCount}/3`
        : `${approvedCount}/3 hoàn tất`;

  return (
    <section className="space-y-3" aria-label={title}>
      <div className="space-y-2">
        <div className="flex items-center justify-between gap-2">
          <h3
            className={cn(
              flat
                ? 'text-sm font-semibold text-ink-primary'
                : 'text-xs font-semibold uppercase tracking-wider text-ink-muted',
            )}
          >
            {title}
          </h3>
          {flat ? (
            <span
              className={cn(
                'text-xs font-medium tabular-nums',
                isPreview
                  ? 'text-ink-muted'
                  : isAllApproved
                    ? 'text-tone-success-text'
                    : hasRejected
                      ? 'text-tone-danger-text'
                      : 'text-primary',
              )}
            >
              {summaryText}
            </span>
          ) : (
            <span
              className={cn(
                'inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-semibold tabular-nums',
                isPreview
                  ? 'bg-surface-muted text-ink-secondary'
                  : isAllApproved
                    ? 'bg-tone-success-bg text-tone-success-text ring-1 ring-tone-success-border/70'
                    : hasRejected
                      ? 'bg-tone-danger-bg text-tone-danger-text ring-1 ring-tone-danger-border/70'
                      : 'bg-tone-info-bg text-primary ring-1 ring-tone-info-border/70',
              )}
            >
              {summaryText}
            </span>
          )}
        </div>

        <div
          className="flex items-center gap-1.5"
          role="progressbar"
          aria-label="Tiến độ phê duyệt 3 cấp"
          aria-valuenow={isPreview ? 0 : approvedCount}
          aria-valuemin={0}
          aria-valuemax={3}
        >
          {steps.map((step) => (
            <span
              key={step.level}
              className={cn(
                'h-1.5 flex-1 rounded-full transition-colors duration-300',
                step.state === 'approved' && 'bg-emerald-500',
                step.state === 'current' && 'bg-primary',
                step.state === 'rejected' && 'bg-rose-500',
                step.state === 'pending' && 'bg-surface-strong/90',
              )}
            />
          ))}
        </div>
      </div>

      <ol className="space-y-0">
        {steps.map((step, index) => {
          const isActiveTarget = activeLevel ? step.level === activeLevel : false;
          const isFirst = index === 0;
          const isLast = index === steps.length - 1;
          const prevStep = index > 0 ? steps[index - 1] : null;
          const displayActor =
            compact && step.level === 'advisor' ? 'GV hướng dẫn' : step.actorLabel;

          const subtitle = isPreview
            ? step.level === 'advisor'
              ? advisorName
                ? `GVHD ${advisorName} xem xét`
                : 'Xem xét lý do & tên đề tài mới'
              : step.level === 'faculty'
                ? 'Kiểm tra phù hợp chuyên môn'
                : 'Cập nhật tên & mở lại Đề cương'
            : step.timestamp
              ? null
              : step.state === 'current'
                ? activeLevel
                  ? isActiveTarget
                    ? 'Bước hiện tại cần bạn xử lý'
                    : 'Đang chờ cấp này xử lý'
                  : 'Đang chờ cấp này xem xét'
                : step.state === 'rejected'
                  ? 'Quy trình dừng tại bước này'
                  : 'Chờ hoàn tất bước trước';

          const topLineClass =
            prevStep?.state === 'approved'
              ? 'bg-emerald-300/90 dark:bg-emerald-700/70'
              : prevStep?.state === 'rejected'
                ? 'bg-rose-200 dark:bg-rose-800/70'
                : 'bg-surface-strong';

          const bottomLineClass =
            step.state === 'approved'
              ? 'bg-emerald-300/90 dark:bg-emerald-700/70'
              : step.state === 'rejected'
                ? 'bg-rose-200 dark:bg-rose-800/70'
                : 'bg-surface-strong';

          const flatStateLabel = isPreview
            ? null
            : step.state === 'approved'
              ? 'Đã duyệt'
              : step.state === 'current'
                ? 'Đang chờ'
                : step.state === 'rejected'
                  ? 'Từ chối'
                  : 'Chưa đến';

          return (
            <li key={step.level} className="flex items-stretch gap-3">
              <div className="flex w-6 shrink-0 flex-col items-center">
                <span
                  aria-hidden="true"
                  className={cn(
                    'w-0.5 flex-1 rounded-full',
                    isFirst ? 'bg-transparent' : cn('mb-1', topLineClass),
                  )}
                />
                <span
                  aria-hidden="true"
                  className={cn(
                    'flex shrink-0 items-center justify-center rounded-full text-xs font-bold tabular-nums transition-[background-color,border-color,box-shadow] duration-200',
                    flat ? 'size-6' : 'size-7',
                    step.state === 'approved' && 'bg-emerald-500 text-white',
                    step.state === 'current' && 'bg-brand-solid text-white shadow-xs',
                    step.state === 'rejected' && 'bg-rose-500 text-white',
                    step.state === 'pending' &&
                      'border border-line-input bg-surface-elevated text-ink-subtle',
                  )}
                >
                  {step.state === 'approved' ? (
                    <Check className="size-3.5 stroke-[2.5]" />
                  ) : step.state === 'rejected' ? (
                    <X className="size-3.5 stroke-[2.5]" />
                  ) : (
                    step.number
                  )}
                </span>
                <span
                  aria-hidden="true"
                  className={cn(
                    'w-0.5 flex-1 rounded-full',
                    isLast ? 'bg-transparent' : cn('mt-1', bottomLineClass),
                  )}
                />
              </div>

              <div className={cn('min-w-0 flex-1', flat ? 'py-1' : 'py-1.5')}>
                <div
                  className={cn(
                    'flex flex-col justify-center rounded-xl transition-[background-color,border-color,box-shadow] duration-200',
                    flat
                      ? cn(
                          'min-h-[54px] px-2.5 py-2',
                          step.state === 'current' && 'bg-tone-info-bg/55',
                          step.state === 'rejected' && 'bg-tone-danger-bg/65',
                        )
                      : cn(
                          'min-h-[64px] border px-3.5 py-2.5',
                          step.state === 'approved' && 'border-line/80 bg-surface-card',
                          step.state === 'current' && 'border-tone-info-border bg-surface-card',
                          step.state === 'rejected' &&
                            'border-tone-danger-border bg-tone-danger-bg',
                          step.state === 'pending' &&
                            'border-line/65 bg-surface-card/70',
                          isActiveTarget &&
                            step.state === 'pending' &&
                            'border-line-strong bg-surface-card/90',
                        ),
                  )}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span
                      className={cn(
                        'min-w-0 truncate text-[13px] font-semibold',
                        step.state === 'pending' && !isPreview
                          ? 'text-ink-muted'
                          : 'text-ink-primary',
                      )}
                      title={`Cấp ${step.number} · ${step.actorLabel}`}
                    >
                      {flat ? displayActor : `Cấp ${step.number} · ${displayActor}`}
                    </span>

                    {flat ? (
                      flatStateLabel && (
                        <span
                          className={cn(
                            'inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap text-[11px] font-medium',
                            step.state === 'approved' && 'text-tone-success-text',
                            step.state === 'current' && 'text-primary',
                            step.state === 'rejected' && 'text-tone-danger-text',
                            step.state === 'pending' && 'text-ink-subtle',
                          )}
                        >
                          {step.state === 'current' && (
                            <span
                              className="size-1.5 rounded-full bg-primary motion-safe:animate-pulse"
                              aria-hidden="true"
                            />
                          )}
                          {flatStateLabel}
                        </span>
                      )
                    ) : (
                      <span
                        className={cn(
                          'inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-medium',
                          step.state === 'approved' &&
                            'bg-tone-success-bg text-tone-success-text',
                          step.state === 'current' && 'bg-tone-info-bg text-primary',
                          step.state === 'rejected' &&
                            'bg-tone-danger-bg/90 text-tone-danger-text',
                          step.state === 'pending' && 'bg-surface-muted text-ink-muted',
                        )}
                      >
                        {step.state === 'current' && (
                          <span
                            className="size-1.5 rounded-full bg-primary motion-safe:animate-pulse"
                            aria-hidden="true"
                          />
                        )}
                        {step.stateLabel}
                      </span>
                    )}
                  </div>

                  <div className="mt-0.5 flex flex-wrap items-center justify-between gap-2 text-[11px]">
                    {step.timestamp ? (
                      <span className="inline-flex items-center gap-1 text-ink-muted tabular-nums">
                        <CalendarClock className="size-3 text-ink-subtle" aria-hidden="true" />
                        {formatRenameDateTime(step.timestamp)}
                      </span>
                    ) : (
                      <span
                        className={cn(
                          'line-clamp-1',
                          step.state === 'current'
                            ? 'font-medium text-tone-info-text/85'
                            : 'text-ink-muted',
                        )}
                      >
                        {subtitle}
                      </span>
                    )}

                    {!isActiveTarget && step.state === 'current' && onSwitchLevel ? (
                      <button
                        type="button"
                        onClick={() => onSwitchLevel(step.level)}
                        className="inline-flex items-center gap-1 font-medium text-primary transition-colors hover:text-tone-info-text focus-visible:outline-none focus-visible:underline"
                      >
                        Xem cấp này
                        <ArrowRight className="size-3" aria-hidden="true" />
                      </button>
                    ) : (
                      isActiveTarget &&
                      step.state !== 'current' && (
                        <span className="rounded bg-surface-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-body">
                          Đang xem
                        </span>
                      )
                    )}
                  </div>
                </div>
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
