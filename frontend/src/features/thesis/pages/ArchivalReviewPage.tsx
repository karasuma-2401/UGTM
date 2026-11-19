import React, { useMemo } from 'react';
import { Lock, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import {
  ArchivalHistoryDialog,
  ArchivalReviewInspector,
  ArchivalReviewTable,
} from '../components/archival';
import { PdfPreviewDialog } from '../components/shared/PdfPreviewDialog';
import { useArchivalReview } from '../hooks/useArchivalReview';
import { getThesisErrorMessage } from '../utils/thesisErrors';

export const ArchivalReviewPage: React.FC = () => {
  const {
    permissions,
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
    allRowsCount,
    rows,
    selectedRow,
    submissionDetail,
    isLoadingDetail,
    detailError,
    academicDetail,
    packagesQuery,
    isLoadingList,
    isFetchingList,
    listError,
    facultyComment,
    commentError,
    pendingAction,
    isConfirming,
    isHistoryOpen,
    downloadingKey,
    previewingKey,
    pdfPreviewState,
    actions,
  } = useArchivalReview();

  const batchOptions = useMemo(
    () =>
      batches.map((batch) => ({
        value: batch.id,
        label: `${batch.batchName} (${batch.academicYear} - HK${batch.semester})`,
      })),
    [batches],
  );

  const selectedBatchLabel = useMemo(() => {
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
        key: 'AdvisorConfirmed',
        label: 'Chờ nghiệm thu',
        value: stats.advisorConfirmed,
        tone: 'primary',
        active: filterKey === 'AdvisorConfirmed',
        onClick: () => actions.changeFilter('AdvisorConfirmed'),
      },
      {
        key: 'Submitted',
        label: 'Chờ GVHD xác nhận',
        value: stats.submitted,
        tone: 'warning',
        active: filterKey === 'Submitted',
        onClick: () => actions.changeFilter('Submitted'),
      },
      {
        key: 'NeedRevision',
        label: 'Cần bổ sung',
        value: stats.needRevision,
        tone: 'danger',
        active: filterKey === 'NeedRevision',
        onClick: () => actions.changeFilter('NeedRevision'),
      },
      {
        key: 'ArchiveConfirmed',
        label: 'Đã nghiệm thu',
        value: stats.archiveConfirmed,
        tone: 'success',
        active: filterKey === 'ArchiveConfirmed',
        onClick: () => actions.changeFilter('ArchiveConfirmed'),
      },
    ],
    [stats, filterKey, actions],
  );

  if (!permissions.canRead) {
    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <InlineAlert tone="warning" title="Không có quyền truy cập">
          Tài khoản hiện tại không có quyền xem danh sách hồ sơ lưu chiểu của Khoa.
        </InlineAlert>
      </div>
    );
  }

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title="Nghiệm thu lưu chiểu"
        description="Kiểm tra tệp báo cáo PDF/DOCX, metadata song ngữ và hoàn thành nghiệm thu hồ sơ GVHD đã xác nhận."
        actionsClassName="w-full sm:w-auto flex-1 justify-between items-center"
      >
        <div className="w-full sm:w-80 md:w-[380px]">
          <Select
            aria-label="Chọn đợt khóa luận tốt nghiệp"
            value={selectedBatchId}
            disabled={isLoadingBatches || batches.length === 0}
            placeholder={isLoadingBatches ? 'Đang tải đợt KLTN...' : 'Chọn đợt KLTN'}
            options={batchOptions}
            onChange={(value) => actions.selectBatch(value)}
            triggerClassName="h-10 rounded-xl border-line/90 bg-surface-card text-xs font-semibold text-ink-heading shadow-2xs hover:border-line-strong"
            contentClassName="w-max min-w-full sm:min-w-[380px] max-w-[90vw] left-0 right-auto p-1.5 rounded-xl border-line/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-ink-secondary hover:bg-surface-hover"
          />
        </div>

        <div className="flex items-center gap-2.5 ml-auto">
          <StatusBadge tone="info" className="tabular-nums whitespace-nowrap">
            {stats.advisorConfirmed} chờ nghiệm thu
          </StatusBadge>
          <Button
            type="button"
            variant="outline"
            size="icon"
            aria-label="Làm mới danh sách hồ sơ lưu chiểu"
            title="Làm mới"
            disabled={!selectedBatchId || isFetchingList}
            onClick={() => actions.refresh()}
            className="size-10 rounded-xl border-line bg-surface-card text-ink-body hover:text-ink-primary shadow-2xs"
          >
            <RefreshCw className={`size-4 ${isFetchingList ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </PageHeader>

      <StatisticsBar
        items={statItems}
        loading={isLoadingList && allRowsCount === 0}
        className="grid-flow-row sm:grid-flow-col"
      />

      {listError && (
        <InlineAlert
          title="Không tải được danh sách hồ sơ lưu chiểu"
          action={
            <Button
              type="button"
              variant="outline"
              size="sm"
              leftIcon={<RefreshCw className="size-3.5" />}
              onClick={() => actions.refresh()}
            >
              Thử lại
            </Button>
          }
        >
          {listError}
        </InlineAlert>
      )}

      <div className="grid grid-cols-1 xl:grid-cols-12 gap-5 items-start">
        <div className="xl:col-span-7 min-w-0">
          <ArchivalReviewTable
            items={rows}
            totalCount={totalCount}
            allRowsCount={allRowsCount}
            selectedSubmissionId={selectedRow?.submissionId ?? null}
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
            listError={listError}
            onRetry={actions.refresh}
          />
        </div>

        <aside
          aria-label="Chi tiết kiểm tra và quyết định nghiệm thu"
          className="xl:col-span-5 min-w-0 xl:sticky xl:top-20"
        >
          <Card className="rounded-2xl border-line/80 bg-surface-card shadow-xs overflow-hidden">
            <ArchivalReviewInspector
              selectedRow={selectedRow}
              submissionDetail={submissionDetail}
              isLoadingDetail={isLoadingDetail}
              detailError={detailError}
              academicDetail={academicDetail}
              selectedBatchLabel={selectedBatchLabel}
              canManage={permissions.canManage}
              facultyComment={facultyComment}
              commentError={commentError}
              isConfirming={isConfirming}
              downloadingKey={downloadingKey}
              previewingKey={previewingKey}
              onChangeComment={actions.setFacultyComment}
              onOpenDecisionConfirm={actions.openDecisionConfirm}
              onOpenHistory={actions.openHistory}
              onRefetchDetail={actions.refetchDetail}
              onDownloadFile={(version, role, fileName) =>
                void actions.downloadFile(version, role, fileName)
              }
              onPreviewPdf={(version, fileName, fileSize) =>
                void actions.previewPdf(version, fileName, fileSize)
              }
            />
          </Card>
        </aside>
      </div>

      <ConfirmDialog
        open={pendingAction !== null}
        onClose={actions.closeDecisionConfirm}
        onConfirm={actions.confirmDecision}
        isConfirming={isConfirming}
        type={pendingAction === 'RequestRevision' ? 'warning' : 'brand'}
        title={
          pendingAction === 'RequestRevision'
            ? `Yêu cầu chỉnh sửa hồ sơ ${selectedRow?.groupCode ?? ''}`
            : `Xác nhận nghiệm thu lưu chiểu ${selectedRow?.groupCode ?? ''}`
        }
        confirmText={
          pendingAction === 'RequestRevision' ? 'Gửi yêu cầu sửa' : 'Hoàn thành lưu chiểu'
        }
        cancelText="Hủy"
        description={
          pendingAction === 'RequestRevision' ? (
            <div className="space-y-2 text-sm text-ink-body">
              <p>
                Hồ sơ nhóm <strong>{selectedRow?.groupCode}</strong> sẽ chuyển về trạng thái{' '}
                <strong>Cần bổ sung</strong> để sinh viên chỉnh sửa và nộp lại.
              </p>
              <div className="rounded-xl border border-tone-warning-border bg-tone-warning-bg px-3 py-2 text-xs text-tone-warning-strong">
                Lý do: {facultyComment.trim()}
              </div>
            </div>
          ) : (
            <div className="space-y-2 text-sm text-ink-body">
              <p>
                Xác nhận nghiệm thu hồ sơ lưu chiểu phiên bản{' '}
                <strong>v{selectedRow?.currentVersionNo}</strong> của nhóm{' '}
                <strong>{selectedRow?.groupCode}</strong>.
              </p>
              <div className="flex items-start gap-2 rounded-xl border border-line bg-surface-subtle px-3 py-2 text-xs text-ink-body">
                <Lock className="size-3.5 shrink-0 mt-0.5 text-ink-muted" />
                <span>
                  Sau khi xác nhận hoàn thành, hồ sơ lưu chiểu sẽ được khóa để phục vụ tổng hợp và
                  công bố điểm.
                </span>
              </div>
            </div>
          )
        }
      />

      <ArchivalHistoryDialog
        open={isHistoryOpen}
        onClose={actions.closeHistory}
        title={`Lịch sử lưu chiểu - ${selectedRow?.groupCode ?? ''}`}
        packages={packagesQuery.data ?? []}
        isLoading={packagesQuery.isLoading}
        error={
          packagesQuery.isError
            ? getThesisErrorMessage(packagesQuery.error, 'Không tải được lịch sử hồ sơ.')
            : null
        }
        onRetry={() => void packagesQuery.refetch()}
        onDownloadFile={(version, role, fileName) =>
          void actions.downloadFile(version, role, fileName)
        }
        downloadingKey={downloadingKey}
        onPreviewPdf={(version, fileName, fileSize) =>
          void actions.previewPdf(version, fileName, fileSize)
        }
        previewingKey={previewingKey}
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
};
