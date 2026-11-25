import {
  ArrowRight,
  Calculator,
  CheckCircle2,
  Circle,
  Lock,
  Printer,
  Save,
  Unlock,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { cn } from '@/lib/utils';
import type {
  MinuteCalculationPreviewDto,
  MinuteDiscrepancySummaryDto,
  MinuteScoreItemDto,
} from '../../types/councilMinute.types';
import {
  formatCalculationErrorMessage,
  getComponentShortLabel,
} from '../../utils/councilMinuteFormatting';
import { formatScoreDecimal } from '../../utils/scoreSummaryFormatting';

interface CouncilMinuteAuditDockProps {
  scoreItems: MinuteScoreItemDto[];
  calculationPreview: MinuteCalculationPreviewDto;
  discrepancySummary: MinuteDiscrepancySummaryDto;
  isPreviewActive: boolean;
  isPreviewing: boolean;
  previewError: string | null;
  isDirty: boolean;
  isLocked: boolean;
  isCouncilActive: boolean;
  canEditMinute: boolean;
  canLockMinute: boolean;
  canUnlockMinute: boolean;
  isSaving: boolean;
  isPrintPreview: boolean;
  onSave: () => void;
  onOpenLockModal: () => void;
  onOpenUnlockModal: () => void;
  onTogglePrintPreview: () => void;
  onPrint: () => void;
}

export function CouncilMinuteAuditDock({
  scoreItems,
  calculationPreview,
  discrepancySummary,
  isPreviewActive,
  isPreviewing,
  previewError,
  isDirty,
  isLocked,
  isCouncilActive,
  canEditMinute,
  canLockMinute,
  canUnlockMinute,
  isSaving,
  isPrintPreview,
  onSave,
  onOpenLockModal,
  onOpenUnlockModal,
  onTogglePrintPreview,
  onPrint,
}: CouncilMinuteAuditDockProps) {
  const sourceLabels: Record<string, string> = {
    Advisor: 'Giảng viên hướng dẫn', Reviewer: 'Giảng viên phản biện',
    CouncilChairman: 'Chủ tịch Hội đồng', CouncilSecretary: 'Thư ký Hội đồng',
    CouncilMember: 'Ủy viên Hội đồng', CouncilAverage: 'Điểm Hội đồng',
  };
  const calcErrorText = calculationPreview.calculationErrorCode === 'ScoreSummary.MissingRequiredSource'
    && calculationPreview.missingSources?.length
    ? `Thiếu đầu điểm: ${calculationPreview.missingSources.map((source) => sourceLabels[source] || source).join(', ')}. Kiểm tra thành viên và phiếu chấm theo cấu hình của đợt.`
    : formatCalculationErrorMessage(calculationPreview.calculationErrorCode);
  const roundAwayFromZero = (value: number): number => {
    const scaled = Math.abs(value) * 100 + 1e-9;
    const rounded = Math.round(scaled) / 100;
    return value < 0 ? -rounded : rounded;
  };
  const rawScoreDisplay =
    calculationPreview.rawDivisor > 0 ? roundAwayFromZero(calculationPreview.rawAverage) : null;
  const adjustedScoreDisplay = calculationPreview.finalRoundedScore;

  const delta =
    rawScoreDisplay !== null &&
    adjustedScoreDisplay !== null &&
    calculationPreview.adjustedDivisor !== calculationPreview.rawDivisor
      ? Number((adjustedScoreDisplay - rawScoreDisplay).toFixed(2))
      : null;

  const hasEnoughScores = !calculationPreview.calculationErrorCode;
  const discrepancyResolved =
    !discrepancySummary.hasDiscrepancy ||
    discrepancySummary.secretaryMarking === 'AcceptedAsPerMinute' ||
    discrepancySummary.secretaryMarking === 'RejectedAsPerMinute';

  const checklist = [
    {
      label: 'Hội đồng đã phê duyệt (Active)',
      done: isCouncilActive,
    },
    {
      label: 'Đủ đầu điểm bắt buộc và cấu hình điểm',
      done: hasEnoughScores && calculationPreview.adjustedDivisor > 0,
    },
    {
      label: 'Đã thống nhất xử lý sai biệt điểm',
      done: discrepancyResolved,
    },
    {
      label: 'Không còn thay đổi nháp chưa lưu',
      done: !isDirty,
    },
  ];

  const readyToLockNow =
    canLockMinute && calculationPreview.isReadyToLock && checklist.every((c) => c.done);

  return (
    <aside
      aria-label="Kiểm toán công thức và thao tác khóa biên bản"
      className="space-y-4 xl:sticky xl:top-6 print:hidden"
    >
      {/* Score Comparison: Before vs After */}
      <section className="rounded-2xl border border-line/90 bg-surface-card overflow-hidden shadow-[0_1px_3px_rgba(15,23,42,0.04)]">
        <div className="border-b border-line-subtle px-5 py-3.5 flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Calculator className="size-4 text-primary" aria-hidden="true" />
            <h3 className="text-sm font-semibold text-ink-primary">Đối chiếu &amp; Công thức</h3>
          </div>
          {isPreviewActive && (
            <span className="rounded-full border border-tone-info-border bg-tone-info-bg px-2 py-0.5 text-[11px] font-semibold text-tone-info-text">
              {isPreviewing ? 'Đang tính...' : 'Xem trước'}
            </span>
          )}
        </div>

        <div className="p-5 space-y-4">
          {previewError && (
            <InlineAlert tone="error" title="Lỗi xem trước công thức">
              {previewError}
            </InlineAlert>
          )}

          {calcErrorText && (
            <InlineAlert tone="warning" title="Chưa thể chốt điểm tổng kết">
              {calcErrorText}
            </InlineAlert>
          )}

          {/* Before / After Comparison Boxes */}
          <div className="grid grid-cols-2 gap-2.5">
            <div className="rounded-xl border border-line/80 bg-surface-subtle/80 p-3">
              <p className="text-[11px] font-medium text-ink-muted">Trước điều chỉnh</p>
              <p className="mt-1 text-xl font-bold tracking-tight tabular-nums text-ink-heading">
                {rawScoreDisplay !== null ? rawScoreDisplay.toFixed(2) : '—'}
              </p>
              <p className="mt-1 text-[11px] text-ink-muted tabular-nums">
                Mẫu số: {calculationPreview.rawDivisor > 0 ? calculationPreview.rawDivisor : 'Chưa xác định'}
              </p>
            </div>

            <div className="rounded-xl border border-tone-info-border bg-gradient-to-br from-tone-info-bg/90 via-surface-card to-tone-orange-bg/75 p-3">
              <p className="text-[11px] font-semibold text-tone-info-text">Sau điều chỉnh</p>
              <div className="mt-1 flex items-baseline gap-1.5">
                <p className="text-xl font-bold tracking-tight tabular-nums text-tone-info-strong">
                  {adjustedScoreDisplay !== null ? adjustedScoreDisplay.toFixed(2) : '—'}
                </p>
                {delta !== null && Math.abs(delta) >= 0.01 && (
                  <span
                    className={cn(
                      'text-[11px] font-semibold tabular-nums',
                      delta > 0 ? 'text-tone-success-text' : 'text-tone-danger-text',
                    )}
                  >
                    {delta > 0 ? `+${delta.toFixed(2)}` : delta.toFixed(2)}
                  </span>
                )}
              </div>
              <p className="mt-1 text-[11px] text-ink-body tabular-nums">
                Mẫu số: {calculationPreview.adjustedDivisor > 0 ? calculationPreview.adjustedDivisor : 'Chưa xác định'}
              </p>
            </div>
          </div>

          {/* Detailed Formula Breakdown (Ledger style - Phương án 1) */}
          <div className="rounded-xl border border-line/80 bg-surface-subtle/70 p-3.5 space-y-2.5 text-xs">
            <div className="flex items-center justify-between text-ink-muted">
              <span className="font-medium text-ink-secondary">Diễn giải phép tính</span>
              {calculationPreview.configurationVersion && (
                <span className="rounded-md bg-surface-card px-1.5 py-0.5 text-[11px] font-semibold text-ink-secondary ring-1 ring-line/80 tabular-nums">
                  Cấu hình v{calculationPreview.configurationVersion}
                </span>
              )}
            </div>

            {scoreItems.length > 0 ? (
              <ul className="space-y-1.5">
                {scoreItems.map((item, idx) => {
                  const isAttendOnly =
                    item.isDualRoleReviewer && item.componentCode === 'COUNCIL_MEMBER';
                  const isExcluded = item.isIgnoredInCalculation;
                  const shortLabel = getComponentShortLabel(item.componentCode, item.roleLabel);
                  const hasScore = item.score !== null;

                  return (
                    <li
                      key={`${item.componentCode}-ledger-${idx}`}
                      className="flex items-center justify-between gap-2 text-xs leading-5"
                    >
                      <span
                        className={cn(
                          'truncate',
                          isExcluded
                            ? 'line-through text-rose-500 dark:text-rose-400'
                            : isAttendOnly
                              ? 'text-ink-subtle'
                              : 'font-medium text-ink-secondary',
                        )}
                      >
                        {shortLabel}
                      </span>

                      <span className="shrink-0 tabular-nums">
                        {isAttendOnly ? (
                          <span className="text-[11px] text-ink-subtle">Chỉ tham dự (×0)</span>
                        ) : !hasScore ? (
                          <span className="text-[11px] text-tone-warning-icon">Chưa chấm</span>
                        ) : isExcluded ? (
                          <span className="inline-flex items-center gap-1.5 text-rose-500 dark:text-rose-400">
                            <span className="line-through">
                              {formatScoreDecimal(item.score, 2)} × {item.weight}
                            </span>
                            <span className="rounded bg-tone-danger-bg px-1 py-0.5 text-[10px] font-semibold text-tone-danger-icon ring-1 ring-tone-danger-border/70">
                              Loại
                            </span>
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1.5 text-ink-heading">
                            <span>
                              {formatScoreDecimal(item.score, 2)} × {item.weight}
                            </span>
                            <span className="text-[11px] text-ink-subtle">
                              = {((item.score ?? 0) * item.weight).toFixed(2)}
                            </span>
                          </span>
                        )}
                      </span>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <p className="text-ink-secondary leading-relaxed [overflow-wrap:anywhere]">
                {calculationPreview.formulaExplanation}
              </p>
            )}

            {calculationPreview.adjustedDivisor > 0 && (
              <div className="space-y-1 border-t border-line/80 pt-2 text-[11px] tabular-nums">
                <div className="flex items-center justify-between text-ink-muted">
                  <span>Tổng tử số / Mẫu số</span>
                  <span className="font-medium text-ink-secondary">
                    {calculationPreview.adjustedTotal.toFixed(2)} / {calculationPreview.adjustedDivisor}
                  </span>
                </div>
                <div className="flex items-center justify-between text-ink-muted">
                  <span>Làm tròn AwayFromZero</span>
                  <span className="inline-flex items-center gap-1 font-semibold text-ink-heading">
                    <span>{calculationPreview.adjustedAverage.toFixed(6)}</span>
                    <ArrowRight className="size-3 text-ink-subtle" aria-hidden="true" />
                    <span className="text-xs font-bold text-tone-info-strong">
                      {formatScoreDecimal(calculationPreview.finalRoundedScore, 2)}
                    </span>
                  </span>
                </div>
              </div>
            )}
          </div>
        </div>
      </section>

      {/* Lock Readiness và Action Controls */}
      <section className="rounded-2xl border border-line/90 bg-surface-card p-5 space-y-4 shadow-[0_1px_3px_rgba(15,23,42,0.04)]">
        <div className="space-y-2">
          <h3 className="text-sm font-semibold text-ink-primary">
            Điều kiện khóa biên bản
          </h3>
          <ul className="space-y-1.5 text-xs">
            {checklist.map((item) => (
              <li
                key={item.label}
                className={cn(
                  'flex items-center gap-2',
                  item.done ? 'text-ink-secondary' : 'text-ink-muted',
                )}
              >
                {item.done ? (
                  <CheckCircle2 className="size-3.5 shrink-0 text-tone-success-icon" aria-hidden="true" />
                ) : (
                  <Circle className="size-3.5 shrink-0 text-ink-faint" aria-hidden="true" />
                )}
                <span>{item.label}</span>
              </li>
            ))}
          </ul>
        </div>

        <div className="border-t border-line-subtle pt-4 space-y-2.5">
          {canEditMinute && (
            <>
              <Button
                type="button"
                variant="outline"
                onClick={onSave}
                disabled={isSaving || isPreviewing || (!isDirty && !isPreviewActive)}
                isLoading={isSaving}
                loadingText="Đang lưu..."
                className="w-full h-10 rounded-xl font-semibold"
                leftIcon={<Save className="size-4" />}
              >
                Lưu nháp biên bản
              </Button>

              <Button
                type="button"
                variant="default"
                onClick={onOpenLockModal}
                disabled={!canLockMinute || !calculationPreview.isReadyToLock || isSaving || isPreviewing}
                className="w-full h-10 rounded-xl font-semibold shadow-2xs"
                leftIcon={<Lock className="size-4" />}
              >
                {isDirty ? 'Lưu và Xác thực khóa biên bản' : 'Xác thực và Khóa biên bản'}
              </Button>

              {!readyToLockNow && !isDirty && !calculationPreview.isReadyToLock && (
                <p className="text-[11px] text-ink-muted leading-relaxed">
                  Nút khóa chỉ khả dụng khi đã đủ đầu điểm hợp lệ và hoàn tất xử lý sai biệt.
                </p>
              )}
            </>
          )}

          {!canEditMinute && !isLocked && (
            <div className="rounded-xl border border-line bg-surface-subtle px-3.5 py-2.5 text-xs text-ink-body">
              Bạn đang ở chế độ xem &amp; mô phỏng công thức. Chỉ Thư ký hội đồng, Phòng Đào tạo
              hoặc Quản trị viên mới được lưu và khóa biên bản.
            </div>
          )}

          {isLocked && (
            <div className="space-y-2.5">
              <div className="flex items-start gap-2 rounded-xl border border-tone-success-border bg-tone-success-bg px-3.5 py-2.5 text-xs text-tone-success-strong">
                <Lock className="size-4 shrink-0 text-tone-success-icon mt-0.5" aria-hidden="true" />
                <span>
                  Biên bản đã khóa. Toàn bộ đầu điểm và góp ý sinh viên đang được bảo vệ ở chế độ
                  chỉ đọc.
                </span>
              </div>

              {canUnlockMinute && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={onOpenUnlockModal}
                  className="w-full h-10 rounded-xl font-semibold border-tone-danger-border text-tone-danger-text hover:bg-tone-danger-bg hover:border-tone-danger-border"
                  leftIcon={<Unlock className="size-4" />}
                >
                  Mở khóa đặc biệt (Khoa / Admin)
                </Button>
              )}
            </div>
          )}

          <div className="grid grid-cols-2 gap-2 pt-1">
            <Button
              type="button"
              variant="outline"
              onClick={onTogglePrintPreview}
              className={cn(
                'h-9 rounded-xl text-xs font-medium',
                isPrintPreview &&
                  'border-blue-500 bg-tone-info-bg text-tone-info-text',
              )}
            >
              {isPrintPreview ? 'Thoát bản in' : 'Xem bản in A4'}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={onPrint}
              className="h-9 rounded-xl border-tone-danger-border bg-tone-danger-bg text-xs font-semibold text-tone-danger-text hover:border-tone-danger-border hover:bg-tone-danger-bg hover:text-tone-danger-strong"
              leftIcon={<Printer className="size-3.5 text-tone-danger-icon" />}
            >
              In / Lưu PDF
            </Button>
          </div>
        </div>
      </section>
    </aside>
  );
}
