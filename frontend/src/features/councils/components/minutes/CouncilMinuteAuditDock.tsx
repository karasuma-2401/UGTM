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
  const calcErrorText = formatCalculationErrorMessage(calculationPreview.calculationErrorCode);
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
      <section className="rounded-2xl border border-slate-200/90 bg-white overflow-hidden shadow-[0_1px_3px_rgba(15,23,42,0.04)]">
        <div className="border-b border-slate-100 px-5 py-3.5 flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Calculator className="size-4 text-primary" aria-hidden="true" />
            <h3 className="text-sm font-semibold text-slate-900">Đối chiếu &amp; Công thức</h3>
          </div>
          {isPreviewActive && (
            <span className="rounded-full border border-blue-200 bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700">
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
            <div className="rounded-xl border border-slate-200/80 bg-slate-50/80 p-3">
              <p className="text-[11px] font-medium text-slate-500">Trước điều chỉnh</p>
              <p className="mt-1 text-xl font-bold tracking-tight tabular-nums text-slate-800">
                {rawScoreDisplay !== null ? rawScoreDisplay.toFixed(2) : '—'}
              </p>
              <p className="mt-1 text-[11px] text-slate-500 tabular-nums">
                Mẫu số: {calculationPreview.rawDivisor}
              </p>
            </div>

            <div className="rounded-xl border border-blue-200/80 bg-gradient-to-br from-blue-50/80 via-white to-orange-50/70 p-3">
              <p className="text-[11px] font-semibold text-blue-700">Sau điều chỉnh</p>
              <div className="mt-1 flex items-baseline gap-1.5">
                <p className="text-xl font-bold tracking-tight tabular-nums text-blue-900">
                  {adjustedScoreDisplay !== null ? adjustedScoreDisplay.toFixed(2) : '—'}
                </p>
                {delta !== null && Math.abs(delta) >= 0.01 && (
                  <span
                    className={cn(
                      'text-[11px] font-semibold tabular-nums',
                      delta > 0 ? 'text-emerald-700' : 'text-rose-700',
                    )}
                  >
                    {delta > 0 ? `+${delta.toFixed(2)}` : delta.toFixed(2)}
                  </span>
                )}
              </div>
              <p className="mt-1 text-[11px] text-slate-600 tabular-nums">
                Mẫu số: {calculationPreview.adjustedDivisor}
              </p>
            </div>
          </div>

          {/* Detailed Formula Breakdown (Ledger style - Phương án 1) */}
          <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5 space-y-2.5 text-xs">
            <div className="flex items-center justify-between text-slate-500">
              <span className="font-medium text-slate-700">Diễn giải phép tính</span>
              {calculationPreview.configurationVersion && (
                <span className="rounded-md bg-white px-1.5 py-0.5 text-[11px] font-semibold text-slate-700 ring-1 ring-slate-200/80 tabular-nums">
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
                            ? 'line-through text-rose-500'
                            : isAttendOnly
                              ? 'text-slate-400'
                              : 'font-medium text-slate-700',
                        )}
                      >
                        {shortLabel}
                      </span>

                      <span className="shrink-0 tabular-nums">
                        {isAttendOnly ? (
                          <span className="text-[11px] text-slate-400">Chỉ tham dự (×0)</span>
                        ) : !hasScore ? (
                          <span className="text-[11px] text-amber-600">Chưa chấm</span>
                        ) : isExcluded ? (
                          <span className="inline-flex items-center gap-1.5 text-rose-500">
                            <span className="line-through">
                              {formatScoreDecimal(item.score, 2)} × {item.weight}
                            </span>
                            <span className="rounded bg-rose-50 px-1 py-0.5 text-[10px] font-semibold text-rose-600 ring-1 ring-rose-200/70">
                              Loại
                            </span>
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1.5 text-slate-800">
                            <span>
                              {formatScoreDecimal(item.score, 2)} × {item.weight}
                            </span>
                            <span className="text-[11px] text-slate-400">
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
              <p className="text-slate-700 leading-relaxed [overflow-wrap:anywhere]">
                {calculationPreview.formulaExplanation}
              </p>
            )}

            {calculationPreview.adjustedDivisor > 0 && (
              <div className="space-y-1 border-t border-slate-200/80 pt-2 text-[11px] tabular-nums">
                <div className="flex items-center justify-between text-slate-500">
                  <span>Tổng tử số / Mẫu số</span>
                  <span className="font-medium text-slate-700">
                    {calculationPreview.adjustedTotal.toFixed(2)} / {calculationPreview.adjustedDivisor}
                  </span>
                </div>
                <div className="flex items-center justify-between text-slate-500">
                  <span>Làm tròn AwayFromZero</span>
                  <span className="inline-flex items-center gap-1 font-semibold text-slate-800">
                    <span>{calculationPreview.adjustedAverage.toFixed(6)}</span>
                    <ArrowRight className="size-3 text-slate-400" aria-hidden="true" />
                    <span className="text-xs font-bold text-blue-800">
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
      <section className="rounded-2xl border border-slate-200/90 bg-white p-5 space-y-4 shadow-[0_1px_3px_rgba(15,23,42,0.04)]">
        <div className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-900">Điều kiện khóa biên bản</h3>
          <ul className="space-y-1.5 text-xs">
            {checklist.map((item) => (
              <li
                key={item.label}
                className={cn(
                  'flex items-center gap-2',
                  item.done ? 'text-slate-700' : 'text-slate-500',
                )}
              >
                {item.done ? (
                  <CheckCircle2 className="size-3.5 shrink-0 text-emerald-600" aria-hidden="true" />
                ) : (
                  <Circle className="size-3.5 shrink-0 text-slate-300" aria-hidden="true" />
                )}
                <span>{item.label}</span>
              </li>
            ))}
          </ul>
        </div>

        <div className="border-t border-slate-100 pt-4 space-y-2.5">
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
                <p className="text-[11px] text-slate-500 leading-relaxed">
                  Nút khóa chỉ khả dụng khi đã đủ đầu điểm hợp lệ và hoàn tất xử lý sai biệt.
                </p>
              )}
            </>
          )}

          {!canEditMinute && !isLocked && (
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2.5 text-xs text-slate-600">
              Bạn đang ở chế độ xem &amp; mô phỏng công thức. Chỉ Thư ký hội đồng, Phòng Đào tạo
              hoặc Quản trị viên mới được lưu và khóa biên bản.
            </div>
          )}

          {isLocked && (
            <div className="space-y-2.5">
              <div className="flex items-start gap-2 rounded-xl border border-emerald-200 bg-emerald-50/70 px-3.5 py-2.5 text-xs text-emerald-900">
                <Lock className="size-4 shrink-0 text-emerald-600 mt-0.5" aria-hidden="true" />
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
                  className="w-full h-10 rounded-xl font-semibold border-rose-200 text-rose-700 hover:bg-rose-50 hover:border-rose-300"
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
                isPrintPreview && 'border-blue-500 bg-blue-50 text-blue-700',
              )}
            >
              {isPrintPreview ? 'Thoát bản in' : 'Xem bản in A4'}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={onPrint}
              className="h-9 rounded-xl border-red-200 bg-red-50/75 text-xs font-semibold text-red-700 hover:border-red-300 hover:bg-red-100/80 hover:text-red-800"
              leftIcon={<Printer className="size-3.5 text-red-600" />}
            >
              In / Lưu PDF
            </Button>
          </div>
        </div>
      </section>
    </aside>
  );
}
