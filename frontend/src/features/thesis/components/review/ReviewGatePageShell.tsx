import { useMemo } from 'react';
import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog, type ConfirmDialogType } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import type { ReviewDecision } from '../../types/review.types';
import { useReviewGateWorkflow } from '../../hooks/useReviewGateWorkflow';
import { ThesisReviewNavigation } from '../navigation/ThesisReviewNavigation';
import { PdfPreviewDialog } from '../shared/PdfPreviewDialog';
import { ReviewGateHistoryDialog } from './ReviewGateHistoryDialog';
import { ReviewGateInspector } from './ReviewGateInspector';
import { ReviewGateTable } from './ReviewGateTable';

interface ReviewGatePageShellProps {
  title: string;
  description: string;
  workflow: ReturnType<typeof useReviewGateWorkflow>;
}

export function ReviewGatePageShell({
  title,
  description,
  workflow,
}: ReviewGatePageShellProps) {
  const {
    gateKind,
    batches,
    selectedBatchId,
    isLoadingBatches,
    filterKey,
    keyword,
    sort,
    pageNumber,
    pageSize,
    totalCount,
    stats,
    isLoadingStats,
    rows,
    selectedRow,
    isAlreadyReviewed,
    hasInspectedFile,
    isLoadingList,
    isFetchingList,
    queueErrorKind,
    queueErrorMessage,
    historyData,
    isLoadingHistory,
    historyError,
    feedback,
    feedbackError,
    pendingDecision,
    isSubmittingReview,
    isHistoryOpen,
    downloadingFileId,
    previewingFileId,
    pdfPreviewState,
    actions,
  } = workflow;

  const gateNoun = gateKind === 'syllabus' ? 'đề cương' : 'báo cáo giữa kỳ';

  const batchOptions = useMemo(
    () => [
      { value: '', label: 'Tất cả đợt KLTN' },
      ...batches.map((batch) => ({
        value: batch.id,
        label: `${batch.batchName} (${batch.academicYear} - HK${batch.semester})`,
      })),
    ],
    [batches],
  );

  const selectedBatchLabel = useMemo(() => {
    if (!selectedBatchId) return 'Tất cả đợt KLTN';
    const current = batches.find((batch) => batch.id === selectedBatchId);
    return current ? `${current.batchName} (${current.academicYear} - HK${current.semester})` : '—';
  }, [batches, selectedBatchId]);

  const statItems: StatisticItem[] = useMemo(
    () => [
      {
        key: 'ALL',
        label: 'Tất cả hồ sơ',
        value: stats.all,
        tone: 'muted',
        active: filterKey === 'ALL',
        onClick: () => actions.changeFilter('ALL'),
      },
      {
        key: 'Submitted',
        label: 'Chờ thẩm định',
        value: stats.submitted,
        tone: 'primary',
        active: filterKey === 'Submitted',
        onClick: () => actions.changeFilter('Submitted'),
      },
      {
        key: 'NeedRevision',
        label: 'Yêu cầu chỉnh sửa',
        value: stats.needRevision,
        tone: 'warning',
        active: filterKey === 'NeedRevision',
        onClick: () => actions.changeFilter('NeedRevision'),
      },
      {
        key: 'Approved',
        label: 'Đạt',
        value: stats.approved,
        tone: 'success',
        active: filterKey === 'Approved',
        onClick: () => actions.changeFilter('Approved'),
      },
      {
        key: 'Rejected',
        label: 'Không đạt',
        value: stats.rejected,
        tone: 'danger',
        active: filterKey === 'Rejected',
        onClick: () => actions.changeFilter('Rejected'),
      },
    ],
    [stats, filterKey, actions],
  );

  const confirmDialogConfig = useMemo<{
    type: ConfirmDialogType;
    title: string;
    confirmText: string;
  }>(() => {
    const groupLabel = selectedRow?.groupCode ?? '';
    const map: Record<
      ReviewDecision,
      { type: ConfirmDialogType; title: string; confirmText: string }
    > = {
      Approved: {
        type: 'brand',
        title: `Xác nhận Đạt hồ sơ ${groupLabel}`,
        confirmText: 'Xác nhận Đạt',
      },
      NeedRevision: {
        type: 'warning',
        title: `Yêu cầu chỉnh sửa hồ sơ ${groupLabel}`,
        confirmText: 'Gửi yêu cầu sửa',
      },
      Rejected: {
        type: 'error',
        title: `Đánh giá Không đạt hồ sơ ${groupLabel}`,
        confirmText: 'Xác nhận Không đạt',
      },
    };
    return pendingDecision ? map[pendingDecision] : map.Approved;
  }, [pendingDecision, selectedRow?.groupCode]);

  if (queueErrorKind === 'forbidden') {
    return (
      <div className="max-w-4xl mx-auto py-10 px-4 space-y-4">
        <PageHeader
          title={title}
          description={description}
          navigation={<ThesisReviewNavigation />}
        />
        <InlineAlert tone="warning" title="Không có quyền truy cập">
          {queueErrorMessage ||
            `Tài khoản hiện tại không có quyền xem hoặc thẩm định danh sách hồ sơ ${gateNoun}.`}
        </InlineAlert>
      </div>
    );
  }

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title={title}
        description={description}
        navigation={<ThesisReviewNavigation />}
        actionsClassName="w-full sm:w-auto flex-1 justify-between items-center"
      >
        <div className="w-full sm:w-80 md:w-[380px]">
          <Select
            aria-label="Chọn đợt khóa luận tốt nghiệp"
            value={selectedBatchId}
            disabled={isLoadingBatches}
            placeholder={isLoadingBatches ? 'Đang tải đợt KLTN...' : 'Tất cả đợt KLTN'}
            options={batchOptions}
            onChange={(value) => actions.selectBatch(String(value ?? ''))}
            triggerClassName="h-10 rounded-xl border-line/90 bg-surface-card text-xs font-semibold text-ink-heading shadow-2xs hover:border-line-strong"
            contentClassName="w-max min-w-full sm:min-w-[380px] max-w-[90vw] left-0 right-auto p-1.5 rounded-xl border-line/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-ink-secondary hover:bg-surface-hover"
          />
        </div>

        <div className="flex items-center gap-2.5 ml-auto">
          <StatusBadge tone="info" className="tabular-nums whitespace-nowrap">
            {stats.submitted} chờ thẩm định
          </StatusBadge>
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label={`Làm mới danh sách hồ sơ ${gateNoun}`}
            title="Làm mới"
            disabled={isFetchingList}
            onClick={() => actions.refresh()}
            className="size-10 rounded-xl border-line bg-surface-card text-ink-body hover:text-ink-primary shadow-2xs"
          >
            <RefreshCw className={`size-4 ${isFetchingList ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </PageHeader>

      <StatisticsBar
        items={statItems}
        loading={isLoadingStats && isLoadingList}
        className="grid-flow-row sm:grid-flow-col"
      />

      <div className="grid grid-cols-1 xl:grid-cols-12 gap-5 items-start">
        <div className="xl:col-span-7 min-w-0">
          <ReviewGateTable
            gateKind={gateKind}
            items={rows}
            totalCount={totalCount}
            allRowsCount={stats.all}
            selectedGroupId={selectedRow?.groupId ?? null}
            onSelectRow={actions.selectRow}
            sort={sort}
            onSortChange={actions.changeSort}
            pageNumber={pageNumber}
            pageSize={pageSize}
            onPageChange={actions.changePage}
            onPageSizeChange={actions.changePageSize}
            filterKey={filterKey}
            keyword={keyword}
            onKeywordChange={actions.changeKeyword}
            onResetFilters={actions.resetFilters}
            onShowAll={() => actions.changeFilter('ALL')}
            isLoading={isLoadingList}
            isFetching={isFetchingList}
            listError={queueErrorMessage}
            onRetry={actions.refresh}
          />
        </div>

        <aside
          aria-label={`Chi tiết kiểm tra và thẩm định ${gateNoun}`}
          className="xl:col-span-5 min-w-0 xl:sticky xl:top-20"
        >
          <Card className="rounded-2xl border-line/80 bg-surface-card shadow-xs overflow-hidden">
            <ReviewGateInspector
              gateKind={gateKind}
              selectedRow={selectedRow}
              selectedBatchLabel={selectedBatchLabel}
              historyData={historyData}
              hasInspectedFile={hasInspectedFile}
              isAlreadyReviewed={isAlreadyReviewed}
              feedback={feedback}
              feedbackError={feedbackError}
              isSubmitting={isSubmittingReview}
              downloadingFileId={downloadingFileId}
              previewingFileId={previewingFileId}
              onChangeFeedback={actions.setFeedback}
              onOpenDecisionConfirm={actions.openDecisionConfirm}
              onOpenHistory={actions.openHistory}
              onDownloadFile={(fileId, fileName, groupId) =>
                void actions.downloadFile(fileId, fileName, groupId)
              }
              onPreviewPdf={(fileId, fileName, fileSize, groupId) =>
                void actions.previewPdf(fileId, fileName, fileSize, groupId)
              }
            />
          </Card>
        </aside>
      </div>

      <ConfirmDialog
        open={pendingDecision !== null}
        onClose={actions.closeDecisionConfirm}
        onConfirm={actions.confirmDecision}
        isConfirming={isSubmittingReview}
        type={confirmDialogConfig.type}
        title={confirmDialogConfig.title}
        confirmText={confirmDialogConfig.confirmText}
        cancelText="Hủy"
        description={
          pendingDecision === 'Approved' ? (
            <div className="space-y-2 text-sm text-ink-body">
              <p>
                Phê duyệt <strong>Đạt</strong> cho hồ sơ {gateNoun} của nhóm{' '}
                <strong>{selectedRow?.groupCode}</strong> (v{selectedRow?.latestVersionNo}).
              </p>
              {feedback.trim() && (
                <div className="rounded-xl border border-line bg-surface-subtle px-3 py-2 text-xs text-ink-body">
                  Nhận xét: {feedback.trim()}
                </div>
              )}
            </div>
          ) : pendingDecision === 'NeedRevision' ? (
            <div className="space-y-2 text-sm text-ink-body">
              <p>
                Hồ sơ nhóm <strong>{selectedRow?.groupCode}</strong> sẽ chuyển sang{' '}
                <strong>Yêu cầu chỉnh sửa</strong> để sinh viên cập nhật và nộp lại trước hạn chốt.
              </p>
              <div className="rounded-xl border border-tone-warning-border bg-tone-warning-bg px-3 py-2 text-xs text-tone-warning-strong">
                Lý do: {feedback.trim()}
              </div>
            </div>
          ) : (
            <div className="space-y-2 text-sm text-ink-body">
              <p>
                Đánh giá <strong>Không đạt</strong> cho hồ sơ {gateNoun} của nhóm{' '}
                <strong>{selectedRow?.groupCode}</strong>. Nhóm sẽ bị dừng KLTN và khóa các giai
                đoạn sau.
              </p>
              <div className="rounded-xl border border-tone-danger-border bg-tone-danger-bg px-3 py-2 text-xs text-tone-danger-strong">
                Lý do: {feedback.trim()}
              </div>
            </div>
          )
        }
      />

      <ReviewGateHistoryDialog
        open={isHistoryOpen}
        onClose={actions.closeHistory}
        gateKind={gateKind}
        title={`Lịch sử ${gateNoun} - ${selectedRow?.groupCode ?? ''}`}
        historyData={historyData}
        isLoading={isLoadingHistory}
        error={historyError}
        onRetry={actions.refetchHistory}
        downloadingFileId={downloadingFileId}
        previewingFileId={previewingFileId}
        onDownloadFile={(fileId, fileName) =>
          void actions.downloadFile(fileId, fileName, selectedRow?.groupId)
        }
        onPreviewPdf={(fileId, fileName, fileSize) =>
          void actions.previewPdf(fileId, fileName, fileSize, selectedRow?.groupId)
        }
      />

      <PdfPreviewDialog
        open={pdfPreviewState.open}
        onClose={actions.closePdfPreview}
        previewUrl={pdfPreviewState.url}
        fileName={pdfPreviewState.fileName}
        fileSize={pdfPreviewState.fileSize}
      />
    </div>
  );
}
