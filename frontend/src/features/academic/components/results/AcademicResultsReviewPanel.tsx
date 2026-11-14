import React from 'react';
import { Calculator, CheckCircle2, FileSearch, Lock, RefreshCw, Send } from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
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
  onCalculate: (groupId: string, expectedSummaryVersion: number | null) => void;
  onOpenDraftDialog: (groupId: string) => void;
  onPublishSingle: (groupId: string) => void;
  onRetry: () => void;
}

export const AcademicResultsReviewPanel: React.FC<AcademicResultsReviewPanelProps> = ({
  row,
  detail,
  isLoading,
  error,
  canCalculate,
  canPublish,
  isCalculating,
  calculatingGroupId,
  onCalculate,
  onOpenDraftDialog,
  onPublishSingle,
  onRetry,
}) => {
  if (!row) {
    return (
      <div className="p-6 text-center space-y-2">
        <p className="text-sm font-semibold text-slate-800">Chưa chọn hồ sơ</p>
        <p className="text-xs text-slate-500 leading-relaxed">
          Chọn một nhóm trong danh sách bên trái để rà soát nguồn điểm, điều kiện lưu chiểu và trạng
          thái công bố.
        </p>
      </div>
    );
  }

  const summary = detail?.summary ?? null;
  const readiness = detail?.readiness ?? null;
  const academic = detail?.academicDetail ?? null;
  const isPublished = (summary?.status ?? row.summaryStatus) === 'PublishedToStudent';
  const isStale = summary?.isStale ?? row.isStale;
  const isRowCalculating = isCalculating && calculatingGroupId === row.groupId;
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
      code: student.studentCode,
    })) ??
    row.studentNames.map((name, index) => ({
      id: `${row.groupId}-${index}`,
      name,
      code: row.studentCodes[index] ?? '',
    }));

  const blockingReasons = (readiness?.blockingReasons ?? []).filter(
    (code) => code !== 'ScoreSummary.AlreadyPublished',
  );

  return (
    <div className="flex flex-col divide-y divide-slate-100">
      <div className="px-5 py-4 space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h2 className="text-base font-bold text-slate-900 tracking-tight">
              Hồ sơ đang rà soát · {row.groupCode}
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">{row.facultyName}</p>
          </div>
          <StatusBadge tone={row.displayStatusTone}>{row.displayStatusLabel}</StatusBadge>
        </div>

        <div className="space-y-1">
          <p className="text-xs font-medium text-slate-500">Đề tài</p>
          <p className="text-sm font-semibold text-slate-900 leading-snug">{row.topicTitleVi}</p>
          {row.topicTitleEn && (
            <p className="text-xs text-slate-500 leading-snug">{row.topicTitleEn}</p>
          )}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-1 gap-2.5 pt-1">
          <div className="space-y-1">
            <p className="text-xs font-medium text-slate-500">Sinh viên</p>
            {studentItems.length > 0 ? (
              <div className="flex items-center gap-2.5">
                <AvatarGroup items={studentItems} max={3} size="sm" />
                <div className="min-w-0 text-xs text-slate-700 leading-relaxed">
                  {studentItems.map((student, index) => (
                    <span key={student.id}>
                      {index > 0 && ' · '}
                      <span className="font-medium text-slate-800">{student.name}</span>
                      {student.code ? (
                        <span className="text-slate-500 tabular-nums"> ({student.code})</span>
                      ) : null}
                    </span>
                  ))}
                </div>
              </div>
            ) : (
              <p className="text-xs text-slate-500">Chưa có danh sách sinh viên</p>
            )}
          </div>

          {academic?.advisorName && (
            <div className="flex items-center justify-between text-xs pt-0.5">
              <span className="text-slate-500">GVHD</span>
              <span className="font-medium text-slate-800">{academic.advisorName}</span>
            </div>
          )}
        </div>
      </div>

      {isLoading ? (
        <div className="p-5 space-y-3">
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-14 w-full rounded-xl" />
          <Skeleton className="h-24 w-full rounded-xl" />
        </div>
      ) : error ? (
        <div className="p-5">
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
        <>
          <div className="px-5 py-4 space-y-3">
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                Nguồn điểm
              </h3>
              {(summary?.finalScore ?? row.finalScore) !== null && (
                <span className="inline-flex items-baseline gap-1 text-xs text-slate-600">
                  Tổng kết:
                  <strong className="text-base font-bold text-slate-900 tabular-nums">
                    {formatScoreDecimal(summary?.finalScore ?? row.finalScore)}
                  </strong>
                </span>
              )}
            </div>

            {isStale && (
              <InlineAlert tone="warning" title="Bảng điểm cần tính lại">
                Nguồn điểm, lưu chiểu hoặc cấu hình đã thay đổi sau lần tính gần nhất.
              </InlineAlert>
            )}

            {summary && summary.components.length > 0 ? (
              <div className="space-y-2">
                {summary.components.map((component) => (
                  <div
                    key={component.componentCode}
                    className="flex items-start justify-between gap-3 text-xs py-1"
                  >
                    <div className="min-w-0">
                      <p className="font-medium text-slate-800">{component.componentName}</p>
                      <p className="text-slate-500">{formatScoreSourceLabel(component.source)}</p>
                      {component.isIgnoredInCalculation && (
                        <p className="text-rose-700 mt-0.5">
                          Loại khỏi tính điểm
                          {component.exclusionReason ? `: ${component.exclusionReason}` : ''}
                        </p>
                      )}
                    </div>
                    <div className="shrink-0 text-right tabular-nums">
                      <span
                        className={
                          component.isIgnoredInCalculation
                            ? 'line-through text-slate-400'
                            : 'font-semibold text-slate-900'
                        }
                      >
                        {formatScoreDecimal(component.score, 1)}
                      </span>
                      <span className="text-slate-500"> · Hệ số {component.weight}</span>
                    </div>
                  </div>
                ))}
              </div>
            ) : academic && academic.scoreSources.length > 0 ? (
              <div className="space-y-2">
                {academic.scoreSources.map((source, index) => (
                  <div
                    key={`${source.sourceCode}-${source.evaluatorId ?? index}`}
                    className="flex items-start justify-between gap-3 text-xs py-1"
                  >
                    <div className="min-w-0">
                      <p className="font-medium text-slate-800">{source.sourceLabel}</p>
                      {source.evaluatorName && (
                        <p className="text-slate-500 truncate">{source.evaluatorName}</p>
                      )}
                    </div>
                    <div className="shrink-0 text-right tabular-nums">
                      {source.score !== null ? (
                        <span
                          className={
                            source.isIgnored
                              ? 'line-through text-slate-400'
                              : 'font-semibold text-slate-900'
                          }
                        >
                          {formatScoreDecimal(source.score, 1)}
                        </span>
                      ) : (
                        <Badge variant="outline" className="text-[11px] font-normal text-slate-500">
                          Chưa chấm
                        </Badge>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-xs text-slate-500">Chưa có dữ liệu điểm thành phần.</p>
            )}
          </div>

          <div className="px-5 py-4 space-y-2.5">
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

          <div className="px-5 py-4 bg-slate-50/50 space-y-2.5">
            {canCalculate && !isPublished && (
              <Button
                type="button"
                variant={summary ? 'outline' : 'default'}
                className="w-full h-9 rounded-xl text-xs font-semibold"
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

            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                className="flex-1 h-9 rounded-xl text-xs font-semibold"
                leftIcon={<FileSearch className="size-3.5" />}
                onClick={() => onOpenDraftDialog(row.groupId)}
              >
                {isPublished ? `Xem bảng điểm ${row.groupCode}` : `Rà soát dự thảo ${row.groupCode}`}
              </Button>

              {canRunPublish && (
                <Button
                  type="button"
                  variant="default"
                  className="h-9 rounded-xl px-3.5 text-xs font-semibold"
                  leftIcon={<Send className="size-3.5" />}
                  onClick={() => onPublishSingle(row.groupId)}
                >
                  Công bố
                </Button>
              )}
            </div>

            {isPublished && (
              <div className="flex items-center justify-center gap-1.5 text-xs text-emerald-700 pt-0.5">
                <CheckCircle2 className="size-3.5" />
                <span>Sinh viên đủ lưu chiểu đã có thể tra cứu điểm</span>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};
