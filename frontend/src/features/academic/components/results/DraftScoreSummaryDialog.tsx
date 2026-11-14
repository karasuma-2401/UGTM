import React from 'react';
import { Calculator, CheckCircle2, History, Lock, Send } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type {
  AcademicResultDetailDto,
  MergedResultRow,
  ScoreReadinessDto,
  ScoreSummaryDto,
} from '../../types/results.types';
import {
  formatDateTimeVi,
  formatScoreDecimal,
  formatScoreReason,
  formatScoreSourceLabel,
} from '../../utils/scoreSummaryFormatting';

interface DraftScoreSummaryDialogProps {
  open: boolean;
  row: MergedResultRow | null;
  detail: {
    summary: ScoreSummaryDto | null;
    readiness: ScoreReadinessDto;
    academicDetail: AcademicResultDetailDto;
  } | null;
  isLoading: boolean;
  canCalculate: boolean;
  canPublish: boolean;
  isCalculating: boolean;
  onClose: () => void;
  onCalculate: (groupId: string, expectedSummaryVersion: number | null) => void;
  onConfirmPublish: (groupId: string) => void;
}

export const DraftScoreSummaryDialog: React.FC<DraftScoreSummaryDialogProps> = ({
  open,
  row,
  detail,
  isLoading,
  canCalculate,
  canPublish,
  isCalculating,
  onClose,
  onCalculate,
  onConfirmPublish,
}) => {
  if (!row) {
    return null;
  }

  const summary = detail?.summary ?? null;
  const readiness = detail?.readiness ?? null;
  const academic = detail?.academicDetail ?? null;
  const isPublished = (summary?.status ?? row.summaryStatus) === 'PublishedToStudent';
  const activeRevision =
    summary?.revisions.find((rev) => rev.revisionId === summary.activeRevisionId) ??
    summary?.revisions[0] ??
    null;

  const blockingReasons = (readiness?.blockingReasons ?? []).filter(
    (code) => code !== 'ScoreSummary.AlreadyPublished',
  );
  const canRunCalculate =
    canCalculate && !isPublished && Boolean(readiness?.isReadyForCalculation ?? row.isReadyForCalculation);
  const canRunPublish =
    canPublish &&
    !isPublished &&
    Boolean(readiness?.isReadyForPublication ?? row.isReadyForPublication) &&
    (summary?.summaryVersion ?? row.summaryVersion) !== null;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      className="max-w-3xl"
      title={
        <div className="flex flex-wrap items-center gap-2.5">
          <span>
            {isPublished
              ? `Bảng điểm chính thức · ${row.groupCode}`
              : `Bảng điểm dự thảo · ${row.groupCode}`}
          </span>
          <StatusBadge tone={row.displayStatusTone}>{row.displayStatusLabel}</StatusBadge>
          {summary?.configVersionNo ? (
            <Badge variant="outline" className="text-[11px] font-medium text-slate-600">
              Cấu hình v{summary.configVersionNo}
            </Badge>
          ) : null}
        </div>
      }
    >
      <div className="p-5 sm:p-6 space-y-5">
        <div className="flex flex-wrap items-start justify-between gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 px-4 py-3">
          <div className="min-w-0 space-y-1">
            <p className="text-sm font-semibold text-slate-900">{row.topicTitleVi}</p>
            <p className="text-xs text-slate-500">
              {isPublished
                ? 'Bảng điểm đã được công bố chính thức và khóa bất biến.'
                : 'Chỉ Khoa và Phòng Đào tạo xem bản dự thảo; sinh viên chưa thể tra cứu.'}
            </p>
          </div>
          <div className="text-right shrink-0">
            <p className="text-xs text-slate-500">Điểm tổng kết</p>
            <p className="text-xl font-bold text-slate-900 tabular-nums">
              {formatScoreDecimal(summary?.finalScore ?? row.finalScore)}
            </p>
          </div>
        </div>

        {isLoading ? (
          <div className="space-y-3">
            <Skeleton className="h-36 w-full rounded-xl" />
            <Skeleton className="h-20 w-full rounded-xl" />
          </div>
        ) : (
          <>
            {summary?.isStale && (
              <InlineAlert tone="warning" title="Dữ liệu đã thay đổi sau lần tính trước">
                Hệ thống phát hiện điểm thành phần, cấu hình hoặc hồ sơ lưu chiểu đã cập nhật. Cần
                bấm &ldquo;Tính lại điểm&rdquo; trước khi công bố.
              </InlineAlert>
            )}

            {blockingReasons.length > 0 && (
              <InlineAlert tone="warning" title="Điều kiện đang chặn thao tác">
                <ul className="space-y-1 mt-1">
                  {blockingReasons.map((code) => (
                    <li key={code}>• {formatScoreReason(code)}</li>
                  ))}
                </ul>
              </InlineAlert>
            )}

            <section className="space-y-2.5">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold text-slate-900">Nguồn điểm</h3>
                {summary?.calculationFormula && (
                  <span className="text-xs text-slate-500 tabular-nums">
                    Công thức: <strong className="text-slate-700">{summary.calculationFormula}</strong>
                  </span>
                )}
              </div>

              <div className="overflow-hidden rounded-xl border border-slate-200/80">
                <Table>
                  <TableHeader>
                    <TableRow className="bg-slate-50/90">
                      <TableHead>Nguồn / thành phần</TableHead>
                      <TableHead className="whitespace-nowrap">Vai trò nguồn</TableHead>
                      <TableHead className="text-right whitespace-nowrap">Điểm</TableHead>
                      <TableHead className="text-right whitespace-nowrap">Hệ số</TableHead>
                      <TableHead className="whitespace-nowrap">Trạng thái</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {summary && summary.components.length > 0 ? (
                      summary.components.map((component) => (
                        <TableRow key={component.componentCode}>
                          <TableCell className="font-medium text-slate-900">
                            <div>{component.componentName}</div>
                            {component.isIgnoredInCalculation && component.exclusionReason && (
                              <p className="text-xs text-rose-700 font-normal mt-0.5">
                                Lý do loại: {component.exclusionReason}
                              </p>
                            )}
                          </TableCell>
                          <TableCell className="text-xs text-slate-600">
                            {formatScoreSourceLabel(component.source)}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            <span
                              className={
                                component.isIgnoredInCalculation
                                  ? 'line-through text-slate-400'
                                  : 'font-semibold text-slate-900'
                              }
                            >
                              {formatScoreDecimal(component.score, 2)}
                            </span>
                          </TableCell>
                          <TableCell className="text-right tabular-nums text-slate-700">
                            {component.isIgnoredInCalculation ? (
                              <span className="line-through text-slate-400">{component.weight}</span>
                            ) : (
                              component.weight
                            )}
                          </TableCell>
                          <TableCell>
                            {component.isIgnoredInCalculation ? (
                              <StatusBadge tone="error">Không công nhận</StatusBadge>
                            ) : (
                              <StatusBadge tone="success">Hợp lệ</StatusBadge>
                            )}
                          </TableCell>
                        </TableRow>
                      ))
                    ) : academic && academic.scoreSources.length > 0 ? (
                      academic.scoreSources.map((source, index) => (
                        <TableRow key={`${source.sourceCode}-${source.evaluatorId ?? index}`}>
                          <TableCell className="font-medium text-slate-900">
                            <div>{source.sourceLabel}</div>
                            {source.evaluatorName && (
                              <p className="text-xs text-slate-500 font-normal">
                                {source.evaluatorName}
                              </p>
                            )}
                          </TableCell>
                          <TableCell className="text-xs text-slate-600">
                            {source.status}
                          </TableCell>
                          <TableCell className="text-right tabular-nums font-semibold text-slate-900">
                            {source.score !== null ? formatScoreDecimal(source.score, 2) : '—'}
                          </TableCell>
                          <TableCell className="text-right tabular-nums text-slate-500">—</TableCell>
                          <TableCell>
                            {source.score === null ? (
                              <StatusBadge tone="neutral">Chưa chấm</StatusBadge>
                            ) : source.isIgnored ? (
                              <StatusBadge tone="error">Không công nhận</StatusBadge>
                            ) : (
                              <StatusBadge tone="info">Đã chấm</StatusBadge>
                            )}
                          </TableCell>
                        </TableRow>
                      ))
                    ) : (
                      <TableRow>
                        <TableCell colSpan={5} className="text-center text-xs text-slate-500 py-6">
                          Chưa có đầu điểm nào được ghi nhận.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
            </section>

            <section className="rounded-xl border border-slate-200/80 bg-slate-50/50 px-4 py-3.5 space-y-2">
              <div className="flex items-center justify-between gap-3">
                <h3 className="text-sm font-semibold text-slate-900">Rà soát</h3>
                <div className="flex items-center gap-2 text-xs">
                  {readiness?.isArchiveConfirmed ? (
                    <span className="inline-flex items-center gap-1 text-emerald-700 font-medium">
                      <CheckCircle2 className="size-3.5" />
                      Khoa đã nghiệm thu lưu chiểu
                    </span>
                  ) : (
                    <span className="text-amber-700 font-medium">Chưa nghiệm thu lưu chiểu</span>
                  )}
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs pt-1">
                <div className="rounded-lg bg-white border border-slate-200/70 px-3 py-2">
                  <span className="text-slate-500">Tổng trọng số hợp lệ</span>
                  <p className="font-semibold text-slate-900 tabular-nums mt-0.5">
                    {summary?.totalWeight ?? '—'}
                  </p>
                </div>
                <div className="rounded-lg bg-white border border-slate-200/70 px-3 py-2">
                  <span className="text-slate-500">Giá trị trước làm tròn</span>
                  <p className="font-semibold text-slate-900 tabular-nums mt-0.5">
                    {activeRevision ? activeRevision.finalScoreRaw.toFixed(6) : '—'}
                  </p>
                </div>
                <div className="rounded-lg bg-white border border-slate-200/70 px-3 py-2">
                  <span className="text-slate-500">Kết quả làm tròn (2 số)</span>
                  <p className="font-bold text-primary tabular-nums mt-0.5">
                    {formatScoreDecimal(summary?.finalScore ?? row.finalScore)}
                  </p>
                </div>
              </div>
            </section>

            {summary && summary.revisions.length > 0 && (
              <section className="space-y-2">
                <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-700">
                  <History className="size-3.5 text-slate-500" />
                  <span>Lịch sử các lần tính ({summary.revisions.length})</span>
                </div>
                <div className="overflow-hidden rounded-xl border border-slate-200/80">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-slate-50/90">
                        <TableHead className="whitespace-nowrap">Lần tính</TableHead>
                        <TableHead className="whitespace-nowrap">Cấu hình</TableHead>
                        <TableHead className="text-right whitespace-nowrap">Trước làm tròn</TableHead>
                        <TableHead className="text-right whitespace-nowrap">Điểm TK</TableHead>
                        <TableHead className="whitespace-nowrap">Thời điểm</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {summary.revisions.map((revision) => (
                        <TableRow key={revision.revisionId}>
                          <TableCell className="text-xs font-medium text-slate-800 tabular-nums">
                            #{revision.revisionNo}
                          </TableCell>
                          <TableCell className="text-xs text-slate-600 tabular-nums">
                            v{revision.configVersionNo}
                          </TableCell>
                          <TableCell className="text-right text-xs text-slate-600 tabular-nums">
                            {revision.finalScoreRaw.toFixed(6)}
                          </TableCell>
                          <TableCell className="text-right text-xs font-semibold text-slate-900 tabular-nums">
                            {revision.finalScore.toFixed(2)}
                          </TableCell>
                          <TableCell className="text-xs text-slate-500 tabular-nums">
                            {formatDateTimeVi(revision.calculatedAt)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </section>
            )}
          </>
        )}

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4">
          <div className="text-xs text-slate-500">
            {isPublished && (
              <span className="inline-flex items-center gap-1.5 text-emerald-700 font-medium">
                <Lock className="size-3.5" />
                Đã khóa sau công bố
              </span>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Đóng
            </Button>

            {canRunCalculate && !isPublished && (
              <Button
                type="button"
                variant={canRunPublish ? 'outline' : 'default'}
                disabled={isCalculating}
                isLoading={isCalculating}
                loadingText="Đang tính..."
                leftIcon={<Calculator className="size-3.5" />}
                onClick={() =>
                  onCalculate(row.groupId, summary?.summaryVersion ?? row.summaryVersion ?? null)
                }
              >
                {summary ? 'Tính lại điểm' : 'Tính điểm'}
              </Button>
            )}

            {canRunPublish && (
              <Button
                type="button"
                variant="default"
                leftIcon={<Send className="size-3.5" />}
                onClick={() => onConfirmPublish(row.groupId)}
              >
                Xác nhận công bố
              </Button>
            )}
          </div>
        </div>
      </div>
    </Dialog>
  );
};
