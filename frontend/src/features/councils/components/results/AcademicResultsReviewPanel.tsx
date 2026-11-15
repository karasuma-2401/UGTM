import React from 'react';
import { Calculator, CheckCircle2, History, Lock, RefreshCw, Send } from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
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

interface AcademicResultsReviewPanelProps {
  open: boolean;
  row: MergedResultRow | null;
  detail: {
    summary: ScoreSummaryDto | null;
    readiness: ScoreReadinessDto;
    academicDetail: AcademicResultDetailDto;
  } | null;
  isLoading: boolean;
  error: string | null;
  canCalculate: boolean;
  canPublish: boolean;
  isCalculating: boolean;
  calculatingGroupId: string | null;
  onClose: () => void;
  onCalculate: (groupId: string, expectedSummaryVersion: number | null) => void;
  onPublishSingle: (groupId: string) => void;
  onRetry: () => void;
}

export const AcademicResultsReviewPanel: React.FC<AcademicResultsReviewPanelProps> = ({
  open,
  row,
  detail,
  isLoading,
  error,
  canCalculate,
  canPublish,
  isCalculating,
  calculatingGroupId,
  onClose,
  onCalculate,
  onPublishSingle,
  onRetry,
}) => {
  if (!open || !row) {
    return null;
  }

  const summary = detail?.summary ?? null;
  const readiness = detail?.readiness ?? null;
  const academic = detail?.academicDetail ?? null;
  const isPublished = (summary?.status ?? row.summaryStatus) === 'PublishedToStudent';
  const isStale = summary?.isStale ?? row.isStale;
  const isRowCalculating = isCalculating && calculatingGroupId === row.groupId;
  const activeRevision =
    summary?.revisions.find((rev) => rev.revisionId === summary.activeRevisionId) ??
    summary?.revisions[0] ??
    null;

  const canRunCalculate =
    canCalculate && !isPublished && Boolean(readiness?.isReadyForCalculation ?? row.isReadyForCalculation);
  const canRunPublish =
    canPublish &&
    !isPublished &&
    Boolean(readiness?.isReadyForPublication ?? row.isReadyForPublication) &&
    (summary?.summaryVersion ?? row.summaryVersion) !== null;

  const studentItems =
    academic?.students.map((student) => ({
      id: student.studentId,
      name: student.fullName,
      userCode: student.studentCode,
      code: student.studentCode,
    })) ??
    row.studentNames.map((name, index) => ({
      id: `${row.groupId}-${index}`,
      name,
      userCode: row.studentCodes[index] ?? '',
      code: row.studentCodes[index] ?? '',
    }));

  const blockingReasons = (readiness?.blockingReasons ?? []).filter(
    (code) => code !== 'ScoreSummary.AlreadyPublished',
  );

  const headerContent = (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-2.5 min-w-0">
        <h2 className="text-base font-bold text-slate-900 tracking-tight truncate">
          Hồ sơ đang rà soát - {row.groupCode}
        </h2>
        <StatusBadge tone={row.displayStatusTone}>{row.displayStatusLabel}</StatusBadge>
        {summary?.configVersionNo ? (
          <Badge variant="outline" className="text-[11px] font-medium text-slate-600">
            Cấu hình v{summary.configVersionNo}
          </Badge>
        ) : null}
      </div>
      <span className="text-xs font-medium text-slate-500">
        {isPublished ? `Bảng điểm chính thức - ${row.groupCode}` : `Bảng điểm dự thảo - ${row.groupCode}`}
      </span>
    </div>
  );

  return (
    <Dialog
      open={open}
      onClose={onClose}
      closeOnBackdropClick
      variant="drawer"
      title={headerContent}
      closeLabel={`Đóng hồ sơ ${row.groupCode}`}
      className="w-full max-w-[100vw] sm:max-w-[92vw] lg:max-w-[1040px] xl:max-w-[1120px] bg-white"
      headerClassName="border-b border-slate-200/80 bg-white/95 px-5 py-4 sm:px-6"
    >
      <div className="flex min-h-[calc(100dvh-5rem)] flex-col">
        <div className="flex-1 flex flex-col divide-y divide-slate-200/80 lg:divide-y-0 lg:grid lg:grid-cols-[360px_minmax(0,1fr)] xl:grid-cols-[390px_minmax(0,1fr)] lg:divide-x lg:divide-slate-200/80">
          <div className="flex flex-col divide-y divide-slate-100">
            <div className="px-5 sm:px-6 py-4 space-y-3">
              <div className="space-y-1">
                <p className="text-xs font-medium text-slate-500">Đề tài</p>
                <p className="text-sm font-semibold text-slate-900 leading-snug">{row.topicTitleVi}</p>
                {row.topicTitleEn && (
                  <p className="text-xs text-slate-500 leading-snug">{row.topicTitleEn}</p>
                )}
              </div>

              <div className="grid grid-cols-1 gap-2.5 pt-1">
                <div className="space-y-1">
                  <p className="text-xs font-medium text-slate-500">Sinh viên</p>
                  {studentItems.length > 0 ? (
                    <div className="flex items-start gap-2.5">
                      <AvatarGroup items={studentItems} max={3} size="sm" />
                      <div className="min-w-0 space-y-0.5 text-xs text-slate-700 leading-relaxed">
                        {studentItems.map((student) => (
                          <div key={student.id} className="truncate">
                            <span className="font-medium text-slate-800">{student.name}</span>
                            {student.code ? (
                              <span className="text-slate-500 tabular-nums"> ({student.code})</span>
                            ) : null}
                          </div>
                        ))}
                      </div>
                    </div>
                  ) : (
                    <p className="text-xs text-slate-500">Chưa có danh sách sinh viên</p>
                  )}
                </div>

                {academic?.advisorName && (
                  <div className="space-y-1 pt-0.5">
                    <p className="text-xs font-medium text-slate-500">GVHD</p>
                    <div className="flex items-center gap-2.5">
                      <AvatarGroup
                        items={[
                          {
                            id: academic.advisorId || academic.advisorName,
                            name: academic.advisorName,
                          },
                        ]}
                        max={1}
                        size="sm"
                        ariaLabel="Giảng viên hướng dẫn"
                      />
                      <span className="min-w-0 truncate text-xs font-medium text-slate-800">
                        {academic.advisorName}
                      </span>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {isLoading ? (
              <div className="p-5 sm:p-6 space-y-3">
                <Skeleton className="h-4 w-32" />
                <div className="grid grid-cols-2 gap-2">
                  <Skeleton className="h-14 rounded-xl" />
                  <Skeleton className="h-14 rounded-xl" />
                </div>
                <Skeleton className="h-16 w-full rounded-xl" />
              </div>
            ) : error ? (
              <div className="p-5 sm:p-6">
                <InlineAlert
                  title="Không tải được chi tiết hồ sơ"
                  action={
                    <Button type="button" variant="outline" size="sm" onClick={onRetry}>
                      <RefreshCw className="size-3.5" />
                      Thử lại
                    </Button>
                  }
                >
                  {error}
                </InlineAlert>
              </div>
            ) : (
              <div className="px-5 sm:px-6 py-4 space-y-3">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Điều kiện & lưu chiểu
                </h3>

                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 px-3 py-2">
                    <p className="text-slate-500">Biên bản HĐ</p>
                    <p
                      className={
                        readiness?.isCouncilMinuteLocked
                          ? 'font-semibold text-emerald-700 mt-0.5'
                          : 'font-semibold text-amber-700 mt-0.5'
                      }
                    >
                      {readiness?.isCouncilMinuteLocked ? 'Đã khóa' : 'Chưa khóa'}
                    </p>
                  </div>
                  <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 px-3 py-2">
                    <p className="text-slate-500">Lưu chiểu</p>
                    <p
                      className={
                        readiness?.isArchiveConfirmed
                          ? 'font-semibold text-emerald-700 mt-0.5'
                          : 'font-semibold text-amber-700 mt-0.5'
                      }
                    >
                      {readiness?.isArchiveConfirmed ? 'Khoa đã nghiệm thu' : 'Chưa hoàn thành'}
                    </p>
                  </div>
                </div>

                {blockingReasons.length > 0 && (
                  <ul className="space-y-1 pt-1">
                    {blockingReasons.map((code) => (
                      <li key={code} className="text-xs text-amber-800 leading-relaxed">
                        • {formatScoreReason(code)}
                      </li>
                    ))}
                  </ul>
                )}

                {isPublished && (
                  <div className="flex items-center gap-2 rounded-xl border border-emerald-200/80 bg-emerald-50/70 px-3 py-2 text-xs text-emerald-800">
                    <Lock className="size-3.5 shrink-0" />
                    <span>
                      Đã công bố và khóa vĩnh viễn lúc{' '}
                      <strong className="font-semibold">
                        {formatDateTimeVi(summary?.publishedAt ?? row.publishedAt)}
                      </strong>
                    </span>
                  </div>
                )}
              </div>
            )}
          </div>

          <div className="p-5 sm:p-6 space-y-5 min-w-0">
            <div className="flex flex-wrap items-start justify-between gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 px-4 py-3">
              <div className="min-w-0 space-y-1">
                <p className="text-sm font-semibold text-slate-900">
                  {isPublished
                    ? `Bảng điểm chính thức - ${row.groupCode}`
                    : `Bảng điểm dự thảo - ${row.groupCode}`}
                </p>
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
                <Skeleton className="h-44 w-full rounded-xl" />
                <Skeleton className="h-24 w-full rounded-xl" />
              </div>
            ) : error ? null : (
              <>
                {isStale && (
                  <InlineAlert tone="warning" title="Dữ liệu đã thay đổi sau lần tính trước">
                    Hệ thống phát hiện điểm thành phần, cấu hình hoặc hồ sơ lưu chiểu đã cập nhật. Cần
                    bấm &ldquo;Tính lại điểm dự thảo&rdquo; trước khi công bố.
                  </InlineAlert>
                )}

                {academic && academic.students.length > 0 && (
                  <section className="space-y-2.5">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <h3 className="text-sm font-semibold text-slate-900">
                        Kết quả theo từng sinh viên
                      </h3>
                      <span className="text-xs text-slate-500">
                        Chấm điểm cá nhân hóa theo từng thành viên trong nhóm
                      </span>
                    </div>
                    <div className="overflow-x-auto rounded-xl border border-slate-200/80">
                      <Table>
                        <TableHeader>
                          <TableRow className="bg-slate-50/90">
                            <TableHead>Sinh viên</TableHead>
                            <TableHead className="text-right whitespace-nowrap">Điểm CBHD</TableHead>
                            <TableHead className="text-right whitespace-nowrap">Điểm CBPB</TableHead>
                            <TableHead className="text-right whitespace-nowrap">Điểm HĐ</TableHead>
                            <TableHead className="text-right whitespace-nowrap">Tổng kết</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {academic.students.map((student) => {
                            const advScore = student.advisorScore ?? academic.advisorScore;
                            const revScore = student.reviewerScore ?? academic.reviewerScore;
                            const cScore = student.councilScore ?? academic.councilScore;
                            const finScore =
                              student.finalScore ?? summary?.finalScore ?? academic.finalScore;
                            return (
                              <TableRow key={student.studentId}>
                                <TableCell className="font-medium text-slate-900">
                                  <div>{student.fullName}</div>
                                  <div className="text-xs text-slate-500 tabular-nums">
                                    {student.studentCode}
                                  </div>
                                </TableCell>
                                <TableCell className="text-right tabular-nums text-slate-700">
                                  {advScore !== null && advScore !== undefined
                                    ? formatScoreDecimal(advScore, 2)
                                    : '—'}
                                </TableCell>
                                <TableCell className="text-right tabular-nums text-slate-700">
                                  {revScore !== null && revScore !== undefined
                                    ? formatScoreDecimal(revScore, 2)
                                    : '—'}
                                </TableCell>
                                <TableCell className="text-right tabular-nums text-slate-700">
                                  {cScore !== null && cScore !== undefined
                                    ? formatScoreDecimal(cScore, 2)
                                    : '—'}
                                </TableCell>
                                <TableCell className="text-right tabular-nums font-bold text-primary">
                                  {finScore !== null && finScore !== undefined
                                    ? formatScoreDecimal(finScore, 2)
                                    : '—'}
                                </TableCell>
                              </TableRow>
                            );
                          })}
                        </TableBody>
                      </Table>
                    </div>
                  </section>
                )}

                <section className="space-y-2.5">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h3 className="text-sm font-semibold text-slate-900">Nguồn điểm</h3>
                    {summary?.calculationFormula && (
                      <span className="text-xs text-slate-500 tabular-nums">
                        Công thức: <strong className="text-slate-700">{summary.calculationFormula}</strong>
                      </span>
                    )}
                  </div>

                  <div className="overflow-x-auto rounded-xl border border-slate-200/80">
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
                  <div className="flex flex-wrap items-center justify-between gap-3">
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
                    <div className="overflow-x-auto rounded-xl border border-slate-200/80">
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
          </div>
        </div>

        <div className="sticky bottom-0 z-10 flex flex-wrap items-center justify-between gap-3 border-t border-slate-200/80 bg-slate-50/95 px-5 sm:px-6 py-3.5 backdrop-blur-md">
          <div className="text-xs text-slate-500">
            {isPublished && (
              <span className="inline-flex items-center gap-1.5 text-emerald-700 font-medium">
                <Lock className="size-3.5 shrink-0" />
                Đã khóa sau công bố - Sinh viên đủ lưu chiểu đã có thể tra cứu điểm
              </span>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2 ml-auto">
            <Button type="button" variant="outline" className="h-9 rounded-xl text-xs font-semibold" onClick={onClose}>
              Đóng
            </Button>

            {canCalculate && !isPublished && (
              <Button
                type="button"
                variant={summary ? 'outline' : 'default'}
                className="h-9 rounded-xl text-xs font-semibold"
                disabled={!canRunCalculate || isRowCalculating}
                isLoading={isRowCalculating}
                loadingText="Đang tính điểm..."
                leftIcon={<Calculator className="size-3.5" />}
                onClick={() =>
                  onCalculate(row.groupId, summary?.summaryVersion ?? row.summaryVersion ?? null)
                }
              >
                {summary ? 'Tính lại điểm dự thảo' : 'Tính điểm dự thảo'}
              </Button>
            )}

            {canRunPublish && (
              <Button
                type="button"
                variant="default"
                className="h-9 rounded-xl px-4 text-xs font-semibold"
                leftIcon={<Send className="size-3.5" />}
                onClick={() => onPublishSingle(row.groupId)}
              >
                Công bố
              </Button>
            )}
          </div>
        </div>
      </div>
    </Dialog>
  );
};
