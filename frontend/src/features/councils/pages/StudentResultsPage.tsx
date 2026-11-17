import React, { useMemo } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertCircle,
  BookOpen,
  Check,
  ExternalLink,
  Printer,
  RefreshCw,
  ShieldAlert,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
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
import { classifyThesisError, getThesisErrorMessage } from '@/features/thesis/utils/thesisErrors';
import { cn } from '@/lib/utils';
import {
  StudentResultsSidebar,
  StudentTranscriptPrintDocument,
} from '../components/results';
import { useStudentResults } from '../hooks/useStudentResults';
import { formatDateTimeVi, formatScoreDecimal } from '../utils/scoreSummaryFormatting';
import {
  buildStudentConditionRows,
  computeWeightSharePercent,
  formatScale4Score,
  getScoreClassificationMeta,
  replaceInterpunctWithHyphen,
} from '../utils/studentResults';

export const StudentResultsPage: React.FC = () => {
  const {
    statusQuery,
    groupDetailQuery,
    scoreQuery,
    finalSubmissionQuery,
    feedbackQuery,
    isConfirmedMember,
    refresh,
  } = useStudentResults();

  const scoreResult = scoreQuery.data;
  const isPublished = scoreResult?.kind === 'published';
  const summary = scoreResult?.kind === 'published' ? scoreResult.data : null;
  const groupDetail = groupDetailQuery.data;
  const finalSubmission = finalSubmissionQuery.data;

  const isFetching =
    statusQuery.isFetching ||
    groupDetailQuery.isFetching ||
    scoreQuery.isFetching ||
    finalSubmissionQuery.isFetching;

  const conditionRows = useMemo(
    () => buildStudentConditionRows(scoreResult, finalSubmission),
    [scoreResult, finalSubmission],
  );
  const readyConditionCount = useMemo(
    () => conditionRows.filter((row) => row.done).length,
    [conditionRows],
  );

  const feedbackComment = feedbackQuery.data?.comment?.trim() || '';
  const totalWeight = useMemo(
    () => (summary ? summary.components.reduce((acc, item) => acc + Number(item.weight || 0), 0) : 0),
    [summary],
  );
  const rankMeta = useMemo(
    () => getScoreClassificationMeta(summary?.finalScore),
    [summary?.finalScore],
  );

  const handlePrintTranscript = () => {
    window.print();
  };

  if (statusQuery.isLoading || (isConfirmedMember && scoreQuery.isLoading && !scoreQuery.data)) {
    return (
      <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
        <Skeleton className="h-24 w-full rounded-2xl" />
        <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_340px] gap-6 items-start">
          <div className="space-y-5">
            <Skeleton className="h-80 w-full rounded-2xl" />
          </div>
          <Skeleton className="h-72 w-full rounded-2xl" />
        </div>
      </div>
    );
  }

  if (statusQuery.isError) {
    const errorKind = classifyThesisError(statusQuery.error);
    const message = getThesisErrorMessage(
      statusQuery.error,
      'Không thể tải trạng thái đăng ký đề tài của bạn.',
    );

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <Card className="p-8 text-center space-y-4">
          <div className="size-12 mx-auto rounded-full bg-tone-danger-bg text-tone-danger-icon flex items-center justify-center">
            {errorKind === 'forbidden' ? (
              <ShieldAlert className="size-6 text-tone-warning-icon" />
            ) : (
              <AlertCircle className="size-6" />
            )}
          </div>
          <h2 className="text-lg font-bold text-ink-primary">
            {errorKind === 'forbidden'
              ? 'Không có quyền tra cứu kết quả'
              : 'Không tải được thông tin đề tài KLTN'}
          </h2>
          <p className="text-sm text-ink-muted max-w-md mx-auto">{message}</p>
          {errorKind !== 'forbidden' && (
            <Button
              type="button"
              variant="outline"
              leftIcon={<RefreshCw className="size-4" />}
              onClick={() => void statusQuery.refetch()}
            >
              Thử lại
            </Button>
          )}
        </Card>
      </div>
    );
  }

  if (!isConfirmedMember) {
    const isPendingConfirmation =
      Boolean(statusQuery.data?.hasRegistered) &&
      statusQuery.data?.confirmationStatus === 'Pending';

    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <Card className="p-8 text-center space-y-4">
          <div className="size-12 mx-auto rounded-full bg-tone-warning-bg text-tone-warning-icon flex items-center justify-center">
            <AlertCircle className="size-6" />
          </div>
          <h2 className="text-lg font-bold text-ink-primary">
            {isPendingConfirmation
              ? 'Bạn chưa xác nhận tham gia nhóm đề tài'
              : 'Chưa có thông tin nhóm đề tài KLTN'}
          </h2>
          <p className="text-sm text-ink-muted max-w-md mx-auto">
            {isPendingConfirmation
              ? 'Vui lòng hoàn tất xác nhận tham gia nhóm đề tài trước khi tra cứu điều kiện và kết quả khóa luận.'
              : 'Bạn chưa đăng ký đề tài nào trong đợt KLTN này. Vui lòng tra cứu và đăng ký đề tài để theo dõi tiến độ và kết quả.'}
          </p>
          <Button asChild>
            <Link to="/topics/catalog">
              <BookOpen className="size-4" />
              Xem danh mục đề tài
            </Link>
          </Button>
        </Card>
      </div>
    );
  }

  if (scoreQuery.isError) {
    const message = getThesisErrorMessage(
      scoreQuery.error,
      'Không thể tải dữ liệu bảng điểm của nhóm.',
    );
    return (
      <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
        <PageHeader
          accent
          title="Kết quả khóa luận"
          description="Tra cứu điều kiện mở điểm và bảng điểm chính thức theo nhóm."
        />
        <InlineAlert
          tone="error"
          title="Không tải được kết quả"
          action={
            <Button
              type="button"
              variant="outline"
              size="sm"
              leftIcon={<RefreshCw className="size-3.5" />}
              onClick={() => void refresh()}
            >
              Thử lại
            </Button>
          }
        >
          {message}
        </InlineAlert>
      </div>
    );
  }

  const groupCode =
    groupDetail?.groupCode ||
    (statusQuery.data?.groupId
      ? `Nhóm ${statusQuery.data.groupId.slice(0, 4).toUpperCase()}`
      : 'Nhóm KLTN');
  const topicCode = groupDetail?.topicCode || 'KLTN';
  const topicTitle =
    groupDetail?.topicTitleVi ||
    summary?.topicTitle ||
    statusQuery.data?.topicTitleVi ||
    'Chưa có tên đề tài';
  const topicTitleEn = groupDetail?.topicTitleEn ?? null;
  const batchName = replaceInterpunctWithHyphen(groupDetail?.batchName) || 'Học kỳ hiện tại';

  const studentItems = (groupDetail?.members ?? [])
    .filter((member) => member.confirmationStatus === 'Confirmed')
    .map((member) => ({
      id: member.studentId,
      name: member.fullName,
      userCode: member.userCode,
    }));

  const advisorName = groupDetail?.advisor?.fullName || 'Chưa phân công';
  const advisorItems = groupDetail?.advisor?.fullName
    ? [
        {
          id: groupDetail.advisor.advisorId || advisorName,
          name: advisorName,
        },
      ]
    : [];

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      {/* Screen Workspace (hidden when printing A4 transcript) */}
      <div className="space-y-5 print:hidden">
        <PageHeader
          accent
          title={isPublished ? 'Kết quả khóa luận chính thức' : 'Kết quả khóa luận'}
          description={
            <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
              <span className="font-medium text-ink-secondary">{groupCode}</span>
              <span aria-hidden="true" className="text-ink-faint">
                ·
              </span>
              <span className="tabular-nums">{topicCode}</span>
              <span aria-hidden="true" className="text-ink-faint">
                ·
              </span>
              <span>
                {isPublished && summary
                  ? `Công bố lúc ${formatDateTimeVi(summary.publishedAt)}`
                  : 'Theo dõi điều kiện mở bảng điểm chính thức'}
              </span>
            </span>
          }
        >
          <StatusBadge tone={isPublished ? 'success' : 'neutral'}>
            {isPublished ? 'Đã công bố' : 'Chưa mở điểm'}
          </StatusBadge>
          {isPublished && summary && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="bg-surface-card"
              leftIcon={<Printer className="size-3.5" />}
              onClick={handlePrintTranscript}
            >
              In phiếu điểm
            </Button>
          )}
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="bg-surface-card"
            isLoading={isFetching}
            leftIcon={!isFetching ? <RefreshCw className="size-3.5" /> : undefined}
            onClick={() => void refresh()}
          >
            Làm mới
          </Button>
        </PageHeader>

        <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_340px] gap-6 items-start">
          {/* Left Main State-Driven Dossier */}
          <div className="min-w-0 space-y-5">
            {!isPublished ? (
              /* STATE A: Locked / Gate Readiness Dossier */
              <Card className="overflow-hidden border-line/80 shadow-[0_1px_2px_rgba(15,23,42,0.03)] divide-y divide-line-subtle">
                <section className="p-5 sm:p-6 space-y-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <h2 className="text-base font-semibold text-ink-primary">
                        Điều kiện mở bảng điểm chính thức
                      </h2>
                      <p className="text-xs text-ink-muted mt-0.5 leading-relaxed">
                        Kết quả của sinh viên chỉ được mở khi hồ sơ lưu chiểu đã nghiệm thu và Khoa hoàn tất công bố điểm đợt.
                      </p>
                    </div>
                    <StatusBadge
                      tone={readyConditionCount === conditionRows.length ? 'success' : 'neutral'}
                      className="shrink-0 tabular-nums"
                    >
                      {readyConditionCount}/{conditionRows.length} đạt
                    </StatusBadge>
                  </div>

                  <InlineAlert tone="warning" title="Điểm hiện đang khóa">
                    {scoreResult?.kind === 'locked'
                      ? scoreResult.message
                      : 'Nhóm chưa hoàn tất lưu chiểu hoặc Khoa chưa công bố kết quả chính thức.'}
                  </InlineAlert>
                </section>

                {/* 2-Gate Readiness Stepper */}
                <section aria-label="Tiến độ 2 điều kiện mở điểm">
                  <ol className="divide-y divide-line-subtle">
                    {conditionRows.map((row, index) => (
                      <li
                        key={row.key}
                        className="p-5 sm:px-6 flex flex-col sm:flex-row sm:items-center justify-between gap-4"
                      >
                        <div className="flex items-start gap-3.5 min-w-0">
                          <span
                            aria-hidden="true"
                            className={cn(
                              'mt-0.5 size-6 rounded-full flex items-center justify-center text-xs font-semibold shrink-0 tabular-nums border',
                              row.done
                                ? 'border-tone-success-border bg-tone-success-bg text-tone-success-strong'
                                : 'border-line-input bg-surface-subtle text-ink-muted',
                            )}
                          >
                            {row.done ? (
                              <Check className="size-3.5 stroke-[2.5]" />
                            ) : (
                              index + 1
                            )}
                          </span>

                          <div className="min-w-0 space-y-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <p className="text-sm font-semibold text-ink-primary">
                                {row.stepLabel}
                              </p>
                              <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>
                            </div>
                            <p className="text-xs text-ink-muted leading-relaxed">
                              {row.detailLabel}
                            </p>
                          </div>
                        </div>

                        {row.actionTo && !row.done && row.actionLabel && (
                          <Button
                            asChild
                            variant={row.key === 'archive' ? 'outline' : 'ghost'}
                            size="sm"
                            className="shrink-0 self-start sm:self-center bg-surface-card"
                          >
                            <Link to={row.actionTo}>
                              {row.actionLabel}
                              <ExternalLink className="size-3.5" />
                            </Link>
                          </Button>
                        )}
                      </li>
                    ))}
                  </ol>
                </section>

                {feedbackComment && (
                  <section className="p-5 sm:p-6 space-y-2.5">
                    <h2 className="text-sm font-semibold text-ink-primary">
                      Góp ý sau bảo vệ
                    </h2>
                    <div className="rounded-xl border border-line/75 bg-surface-subtle/60 px-4 py-3 text-sm text-ink-heading leading-relaxed whitespace-pre-line">
                      {feedbackComment}
                    </div>
                  </section>
                )}
              </Card>
            ) : (
              /* STATE B: Published Official Transcript Sheet */
              summary && (
                <Card className="overflow-hidden border-line/80 shadow-[0_1px_2px_rgba(15,23,42,0.03)] divide-y divide-line-subtle">
                  {/* Headline Score Band */}
                  <section className="p-5 sm:p-6 space-y-4">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 flex-1">
                        <h2 className="text-base font-semibold text-ink-primary">
                          Bảng điểm tổng kết chính thức
                        </h2>
                        <p className="text-xs text-ink-muted mt-0.5 tabular-nums">
                          Công bố lúc {formatDateTimeVi(summary.publishedAt)}
                        </p>
                      </div>
                      <StatusBadge tone={rankMeta.tone} className="shrink-0">
                        {rankMeta.label}
                      </StatusBadge>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-1">
                      <div className="rounded-xl border border-line/80 bg-surface-subtle/60 px-4 py-3.5 space-y-1">
                        <p className="text-xs text-ink-muted">Điểm tổng kết · Thang 10</p>
                        <p className="text-3xl sm:text-4xl font-bold tracking-tight text-ink-primary tabular-nums">
                          {formatScoreDecimal(summary.finalScore, 2)}
                        </p>
                      </div>

                      <div className="rounded-xl border border-tone-info-border bg-tone-info-bg/70 px-4 py-3.5 space-y-1">
                        <p className="text-xs text-ink-body">Quy đổi · Thang 4</p>
                        <p className="text-3xl sm:text-4xl font-bold tracking-tight text-primary tabular-nums">
                          {formatScale4Score(summary.finalScore)}
                        </p>
                      </div>

                      <div className="rounded-xl border border-line/80 bg-surface-subtle/40 px-4 py-3.5 flex flex-col justify-between gap-1">
                        <p className="text-xs text-ink-muted">Xếp loại học lực (tham khảo)</p>
                        <div>
                          <p className="text-lg font-bold text-ink-primary leading-tight">
                            {rankMeta.label}
                          </p>
                          <p className="text-[11px] text-ink-muted mt-0.5 tabular-nums">
                            {summary.components.length} thành phần đánh giá
                          </p>
                        </div>
                      </div>
                    </div>

                    {summary.calculationFormula && (
                      <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-line/70 bg-surface-subtle/45 px-3.5 py-2.5 text-xs">
                        <span className="text-ink-muted">Công thức tính điểm</span>
                        <span className="font-semibold text-ink-primary tabular-nums">
                          {summary.calculationFormula}
                        </span>
                      </div>
                    )}
                  </section>

                  {/* Component Breakdown Table with Weight Proportion Bars */}
                  <section className="p-5 sm:p-6 space-y-3.5">
                    <div className="flex items-center justify-between gap-2">
                      <h2 className="text-sm font-semibold text-ink-primary">
                        Chi tiết các nguồn điểm
                      </h2>
                      <span className="text-xs text-ink-muted tabular-nums">
                        Tổng hệ số: {totalWeight}
                      </span>
                    </div>

                    {/* Mobile Stacked Breakdown (<md) */}
                    <ul className="md:hidden divide-y divide-line-subtle rounded-xl border border-line/80 bg-surface-card overflow-hidden">
                      {summary.components.map((component, index) => {
                        const sharePercent = computeWeightSharePercent(
                          component.weight,
                          totalWeight,
                        );
                        return (
                          <li key={`m-${component.componentName}-${index}`} className="p-3.5 space-y-2">
                            <div className="flex items-start justify-between gap-3">
                              <div className="min-w-0">
                                <p className="text-sm font-semibold text-ink-primary leading-snug">
                                  {component.componentName}
                                </p>
                                <p className="text-xs text-ink-muted tabular-nums mt-0.5">
                                  Hệ số {component.weight} · {sharePercent}%
                                </p>
                              </div>
                              <div className="text-right shrink-0">
                                <p className="text-base font-bold text-ink-primary tabular-nums">
                                  {formatScoreDecimal(component.score, 2)}
                                </p>
                                <span className="text-[11px] font-medium text-tone-success-strong">
                                  Đã chấm
                                </span>
                              </div>
                            </div>
                            <div
                              aria-hidden="true"
                              className="h-1.5 w-full rounded-full bg-surface-muted overflow-hidden"
                            >
                              <div
                                className="h-full rounded-full bg-primary"
                                style={{ width: `${Math.min(100, Math.max(0, sharePercent))}%` }}
                              />
                            </div>
                          </li>
                        );
                      })}
                      <li className="p-3.5 bg-surface-subtle/60 flex items-center justify-between gap-3">
                        <div>
                          <p className="text-sm font-semibold text-ink-primary">
                            Điểm tổng kết (làm tròn)
                          </p>
                          <p className="text-xs text-ink-muted tabular-nums">
                            Tổng hệ số {totalWeight} · 100%
                          </p>
                        </div>
                        <div className="text-right shrink-0">
                          <p className="text-lg font-bold text-primary tabular-nums">
                            {formatScoreDecimal(summary.finalScore, 2)}
                          </p>
                          <StatusBadge tone="success">Chính thức</StatusBadge>
                        </div>
                      </li>
                    </ul>

                    {/* Desktop Table (>=md) */}
                    <div className="hidden md:block overflow-x-auto rounded-xl border border-line/80">
                      <Table>
                        <TableHeader>
                          <TableRow className="bg-surface-subtle/90">
                            <TableHead>Nguồn / thành phần</TableHead>
                            <TableHead className="text-right whitespace-nowrap">Điểm</TableHead>
                            <TableHead className="text-right whitespace-nowrap">Hệ số</TableHead>
                            <TableHead className="whitespace-nowrap min-w-[140px]">
                              Tỷ trọng
                            </TableHead>
                            <TableHead className="whitespace-nowrap">Trạng thái</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {summary.components.map((component, index) => {
                            const sharePercent = computeWeightSharePercent(
                              component.weight,
                              totalWeight,
                            );
                            return (
                              <TableRow key={`${component.componentName}-${index}`}>
                                <TableCell className="font-semibold text-ink-primary">
                                  {component.componentName}
                                </TableCell>
                                <TableCell className="text-right font-semibold text-ink-primary tabular-nums">
                                  {formatScoreDecimal(component.score, 2)}
                                </TableCell>
                                <TableCell className="text-right text-ink-body tabular-nums">
                                  {component.weight}
                                </TableCell>
                                <TableCell>
                                  <div className="flex items-center gap-2.5">
                                    <div
                                      aria-hidden="true"
                                      className="h-1.5 flex-1 rounded-full bg-surface-muted overflow-hidden min-w-[64px]"
                                    >
                                      <div
                                        className="h-full rounded-full bg-primary"
                                        style={{ width: `${Math.min(100, Math.max(0, sharePercent))}%` }}
                                      />
                                    </div>
                                    <span className="text-xs font-medium text-ink-secondary tabular-nums w-11 text-right shrink-0">
                                      {sharePercent}%
                                    </span>
                                  </div>
                                </TableCell>
                                <TableCell>
                                  <StatusBadge tone="success">Đã chấm</StatusBadge>
                                </TableCell>
                              </TableRow>
                            );
                          })}
                          <TableRow className="bg-surface-subtle/60 font-semibold">
                            <TableCell className="text-ink-primary">
                              Điểm tổng kết (làm tròn)
                            </TableCell>
                            <TableCell className="text-right text-primary tabular-nums">
                              {formatScoreDecimal(summary.finalScore, 2)}
                            </TableCell>
                            <TableCell className="text-right text-ink-secondary tabular-nums">
                              {totalWeight}
                            </TableCell>
                            <TableCell className="text-xs text-ink-muted tabular-nums text-right">
                              100%
                            </TableCell>
                            <TableCell>
                              <StatusBadge tone="success">Chính thức</StatusBadge>
                            </TableCell>
                          </TableRow>
                        </TableBody>
                      </Table>
                    </div>
                  </section>

                  {/* Post-defense feedback */}
                  {feedbackComment && (
                    <section className="p-5 sm:p-6 space-y-2.5">
                      <h2 className="text-sm font-semibold text-ink-primary">
                        Góp ý sau bảo vệ
                      </h2>
                      <div className="rounded-xl border border-line/75 bg-surface-subtle/60 px-4 py-3 text-sm text-ink-heading leading-relaxed whitespace-pre-line">
                        {feedbackComment}
                      </div>
                    </section>
                  )}

                  {/* Integrated Print Footer */}
                  <div className="bg-surface-subtle/50 px-5 sm:px-6 py-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <p className="text-xs text-ink-muted">
                      Bảng điểm do Khoa công bố chính thức cho hồ sơ cá nhân của bạn.
                    </p>
                    <Button
                      type="button"
                      leftIcon={<Printer className="size-4" />}
                      onClick={handlePrintTranscript}
                    >
                      In phiếu kết quả (A4)
                    </Button>
                  </div>
                </Card>
              )
            )}
          </div>

          {/* Right Sticky Context Dock */}
          <StudentResultsSidebar
            isPublished={isPublished}
            topicCode={topicCode}
            groupCode={groupCode}
            topicTitle={topicTitle}
            topicTitleEn={topicTitleEn}
            batchName={batchName}
            publishedAt={summary?.publishedAt ?? null}
            archivedConfirmedAt={finalSubmission?.archivedConfirmedAt ?? null}
            studentItems={studentItems}
            advisorName={advisorName}
            advisorItems={advisorItems}
          />
        </div>
      </div>

      {/* Monochrome A4 Printable Official Transcript (shown only in @media print) */}
      {isPublished && summary && (
        <StudentTranscriptPrintDocument
          summary={summary}
          topicCode={topicCode}
          groupCode={groupCode}
          topicTitle={topicTitle}
          topicTitleEn={topicTitleEn}
          batchName={batchName}
          advisorName={advisorName}
          studentItems={studentItems}
          feedbackComment={feedbackComment}
        />
      )}
    </div>
  );
};
