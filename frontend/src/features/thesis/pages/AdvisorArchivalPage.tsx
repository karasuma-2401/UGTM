import React, { useMemo } from 'react';
import { History, RefreshCw, Search, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import { cn } from '@/lib/utils';
import {
  AdvisorArchivalReviewDialog,
  ArchivalHistoryDialog,
} from '../components/archival';
import { PdfPreviewDialog } from '../components/shared/PdfPreviewDialog';
import { useAdvisorArchival } from '../hooks/useAdvisorArchival';
import type { ArchivalReviewRow } from '../types/archival.types';
import type { ArchivalSortField } from '../utils/archival';
import { getThesisErrorMessage } from '../utils/thesisErrors';

export const AdvisorArchivalPage: React.FC = () => {
  const {
    canAccess,
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
    selectedRowId,
    selectedRow,
    inspectedRow,
    inspectedIndex,
    filteredTotalCount,
    submissionDetail,
    isLoadingDetail,
    detailError,
    groupDetail,
    packagesQuery,
    isLoadingList,
    isFetchingList,
    listError,
    decision,
    revisionReason,
    reasonError,
    pendingAction,
    isConfirming,
    isHistoryOpen,
    downloadingKey,
    previewingKey,
    pdfPreviewState,
    actions,
  } = useAdvisorArchival();

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
        key: 'Submitted',
        label: 'Chờ GVHD xác nhận',
        value: stats.submitted,
        tone: 'primary',
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
        key: 'AdvisorConfirmed',
        label: 'Chờ Khoa nghiệm thu',
        value: stats.advisorConfirmed,
        tone: 'warning',
        active: filterKey === 'AdvisorConfirmed',
        onClick: () => actions.changeFilter('AdvisorConfirmed'),
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

  const columns: DataTableColumn<ArchivalReviewRow, ArchivalSortField>[] = useMemo(
    () => [
      {
        id: 'groupCode',
        header: 'Nhóm',
        sortKey: 'groupCode',
        widthClassName: 'w-28',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <span
            className="font-semibold text-primary tabular-nums whitespace-nowrap"
            title={`UUID nhóm: ${row.groupId}`}
          >
            {row.groupCode}
          </span>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'topicAndStudents',
        header: 'Đề tài và Sinh viên',
        sortKey: 'topicTitle',
        cell: (row) => (
          <div className="min-w-0 space-y-0.5">
            <p className="truncate font-semibold text-slate-900" title={row.topicTitleVi}>
              {row.topicTitleVi}
            </p>
            <p className="truncate text-xs text-slate-500">
              {row.studentNames.length > 0
                ? row.studentNames
                    .map((name, idx) =>
                      row.studentCodes[idx] ? `${name} (${row.studentCodes[idx]})` : name,
                    )
                    .join(' · ')
                : 'Đang tải thông tin sinh viên...'}
            </p>
          </div>
        ),
        skeleton: { variant: 'topic' },
      },
      {
        id: 'version',
        header: 'Phiên bản',
        sortKey: 'version',
        widthClassName: 'w-32',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <div className="flex items-center gap-1.5 whitespace-nowrap">
            <span className="text-xs font-semibold text-slate-800 tabular-nums">
              v{row.currentVersionNo}
            </span>
            {row.currentVersionNo > 1 && (
              <Badge variant="info" className="px-1.5 py-0 text-[10px]">
                Nộp lại
              </Badge>
            )}
          </div>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'submittedAt',
        header: 'Thời điểm nộp',
        sortKey: 'submittedAt',
        widthClassName: 'w-40',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <span className="text-xs text-slate-600 tabular-nums whitespace-nowrap">
            {row.submittedAt ? formatDateTimeVi(row.submittedAt) : '—'}
          </span>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'status',
        header: 'Trạng thái',
        sortKey: 'status',
        widthClassName: 'w-44',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>,
        skeleton: { variant: 'badge' },
      },
      {
        id: 'action',
        header: 'Thao tác',
        align: 'right',
        widthClassName: 'w-24',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <div className="flex justify-end" onClick={(event) => event.stopPropagation()}>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 rounded-md px-3 text-xs font-medium bg-white"
              onClick={() => actions.openInspection(row.submissionId)}
            >
              Xem
            </Button>
          </div>
        ),
        skeleton: { variant: 'actions', actionCount: 1 },
      },
    ],
    [actions],
  );

  if (!canAccess) {
    return (
      <div className="max-w-4xl mx-auto py-10 px-4">
        <InlineAlert tone="warning" title="Không có quyền truy cập">
          Chức năng này chỉ dành cho Giảng viên hướng dẫn (Lecturer) hoặc Quản trị viên (Admin).
        </InlineAlert>
      </div>
    );
  }

  const activeVersion = submissionDetail?.currentVersionNo ?? inspectedRow?.currentVersionNo ?? 1;
  const hasActiveFilters = filterKey !== 'Submitted' || keyword.trim().length > 0;

  return (
    <div className="max-w-[1440px] mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title="Xác nhận lưu chiểu"
        description="Kiểm tra PDF/DOCX và Metadata Việt/Anh trước khi chuyển Khoa nghiệm thu."
        actionsClassName="w-full lg:w-auto flex-nowrap justify-between sm:justify-end items-center shrink-0"
      >
        <div className="min-w-0 flex-1 sm:flex-none sm:w-72">
          <Select
            aria-label="Chọn đợt KLTN"
            value={selectedBatchId}
            disabled={isLoadingBatches || batchOptions.length === 0}
            placeholder={isLoadingBatches ? 'Đang tải đợt KLTN...' : 'Chọn đợt KLTN'}
            options={batchOptions}
            onChange={(value) => actions.selectBatch(value)}
            triggerClassName="h-10 rounded-xl border-slate-200/90 bg-white text-xs font-semibold text-slate-800 shadow-2xs hover:border-slate-300"
            contentClassName="w-max min-w-full sm:min-w-[380px] max-w-[90vw] sm:right-0 sm:left-auto p-1.5 rounded-xl border-slate-200/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-slate-700 hover:bg-slate-50"
          />
        </div>

        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label="Làm mới danh sách hồ sơ lưu chiểu"
          title="Làm mới"
          disabled={!selectedBatchId || isFetchingList}
          onClick={() => actions.refresh()}
          className="size-10 shrink-0 rounded-xl border-slate-200 bg-white text-slate-600 hover:text-slate-900 shadow-2xs"
        >
          <RefreshCw className={cn('size-4', isFetchingList && 'animate-spin')} />
        </Button>
      </PageHeader>

      <StatisticsBar
        items={statItems}
        loading={isLoadingList && allRowsCount === 0}
        className="grid-flow-row grid-cols-2 sm:grid-cols-3 lg:grid-flow-col lg:auto-cols-fr [&>div:first-of-type]:col-span-2 sm:[&>div:first-of-type]:col-span-1"
      />

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 bg-white shadow-xs">
        <div className="border-b border-slate-100 bg-slate-50/45 px-5 py-3.5">
          <div className="flex flex-wrap items-center gap-2.5">
            <div className="relative flex-1 min-w-[220px]">
              <Input
                value={keyword}
                onChange={(event) => actions.changeKeyword(event.target.value)}
                leftIcon={<Search className="size-4 text-slate-400" />}
                placeholder="Tìm mã nhóm, tên đề tài hoặc tên/MSSV sinh viên..."
                aria-label="Tìm kiếm hồ sơ lưu chiểu"
                className="h-9 rounded-lg border-slate-200 bg-white pr-9 shadow-2xs"
              />
              {keyword && (
                <button
                  type="button"
                  aria-label="Xóa từ khóa tìm kiếm"
                  onClick={() => actions.changeKeyword('')}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 cursor-pointer"
                >
                  <X className="size-3.5" />
                </button>
              )}
            </div>

            {hasActiveFilters && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-9 rounded-lg bg-white shadow-2xs"
                onClick={actions.resetFilters}
              >
                Đặt lại mặc định
              </Button>
            )}
          </div>
        </div>

        {listError && (
          <div className="p-4 border-b border-slate-100">
            <InlineAlert
              tone="error"
              title="Không tải được danh sách hồ sơ"
              action={
                <Button type="button" variant="outline" size="sm" onClick={actions.refresh}>
                  Thử lại
                </Button>
              }
            >
              {listError}
            </InlineAlert>
          </div>
        )}

        <DataTable<ArchivalReviewRow, ArchivalSortField>
          ariaLabel="Danh sách hồ sơ lưu chiểu của các nhóm hướng dẫn"
          data={rows}
          columns={columns}
          getRowId={(row) => row.submissionId}
          selectedRowId={selectedRowId}
          onRowSelect={(id) => actions.selectRow(id)}
          onRowOpen={(row) => actions.openInspection(row.submissionId)}
          sort={sort}
          onSortChange={actions.changeSort}
          loading={isLoadingList && allRowsCount === 0}
          busy={isFetchingList && allRowsCount > 0}
          tableClassName="min-w-[820px]"
          rowClassName="h-[68px]"
          renderMobileItem={(row) => {
            const isSelected = selectedRowId === row.submissionId;
            return (
              <div
                key={row.submissionId}
                onClick={() => actions.openInspection(row.submissionId)}
                className={cn(
                  'rounded-xl p-1 transition-colors cursor-pointer space-y-2.5',
                  isSelected ? 'bg-blue-50/50' : 'bg-white',
                )}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-bold text-primary tabular-nums">
                      {row.groupCode}
                    </span>
                    <span className="text-xs font-semibold text-slate-600 tabular-nums">
                      v{row.currentVersionNo}
                    </span>
                    {row.currentVersionNo > 1 && (
                      <Badge variant="info" className="px-1.5 py-0 text-[10px]">
                        Nộp lại
                      </Badge>
                    )}
                  </div>
                  <StatusBadge tone={row.statusTone}>{row.statusLabel}</StatusBadge>
                </div>

                <p className="text-sm font-semibold text-slate-900 leading-snug line-clamp-2">
                  {row.topicTitleVi}
                </p>

                <p className="text-xs text-slate-500 leading-relaxed">
                  {row.studentNames.length > 0
                    ? row.studentNames
                        .map((name, idx) =>
                          row.studentCodes[idx] ? `${name} (${row.studentCodes[idx]})` : name,
                        )
                        .join(' · ')
                    : 'Đang tải thông tin sinh viên...'}
                </p>

                <div className="flex items-center justify-between gap-2 pt-1">
                  <span className="text-[11px] text-slate-500 tabular-nums">
                    {row.submittedAt ? `Nộp: ${formatDateTimeVi(row.submittedAt)}` : '—'}
                  </span>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-8 rounded-md px-3 text-xs font-medium bg-white"
                    onClick={(event) => {
                      event.stopPropagation();
                      actions.openInspection(row.submissionId);
                    }}
                  >
                    Xem
                  </Button>
                </div>
              </div>
            );
          }}
          pagination={{
            pageNumber,
            pageSize,
            totalCount,
            onPageChange: actions.changePage,
            onPageSizeChange: actions.changePageSize,
            itemLabel: 'hồ sơ',
          }}
          empty={{
            title:
              allRowsCount === 0
                ? 'Chưa có hồ sơ lưu chiểu nào trong đợt'
                : 'Không có hồ sơ phù hợp bộ lọc',
            description:
              allRowsCount === 0
                ? 'Các nhóm bạn hướng dẫn sẽ xuất hiện tại đây sau khi nộp hồ sơ lưu chiểu.'
                : 'Thử chuyển sang "Tất cả hồ sơ" hoặc xóa từ khóa tìm kiếm.',
            action:
              filterKey !== 'ALL' || keyword ? (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    actions.changeFilter('ALL');
                    actions.changeKeyword('');
                  }}
                >
                  Xem tất cả hồ sơ
                </Button>
              ) : undefined,
          }}
        />
      </Card>

      <TableFloatingBar
        aria-label="Thao tác với hồ sơ đã chọn"
        show={Boolean(selectedRow && !inspectedRow && !isHistoryOpen)}
        selectedCount={selectedRow ? 1 : 0}
        totalCount={totalCount}
        onClearSelection={actions.clearSelection}
      >
        {selectedRow && (
          <>
            <Badge variant="info" className="hidden sm:inline-flex tabular-nums">
              {selectedRow.groupCode}
            </Badge>
            <Button
              type="button"
              variant="default"
              size="sm"
              className="h-8 rounded-xl text-xs font-semibold shadow-xs"
              onClick={() => actions.openInspection(selectedRow.submissionId)}
            >
              {selectedRow.status === 'Submitted' ? 'Kiểm tra và Xác nhận' : 'Xem hồ sơ'}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 rounded-xl text-xs font-semibold bg-white"
              leftIcon={<History className="size-3.5" />}
              onClick={actions.openHistory}
            >
              Lịch sử
            </Button>
          </>
        )}
      </TableFloatingBar>

      <AdvisorArchivalReviewDialog
        open={Boolean(inspectedRow)}
        inspectedRow={inspectedRow}
        inspectedIndex={inspectedIndex}
        filteredTotalCount={filteredTotalCount}
        submissionDetail={submissionDetail}
        groupDetail={groupDetail}
        selectedBatchLabel={selectedBatchLabel}
        isLoadingDetail={isLoadingDetail}
        detailError={detailError}
        decision={decision}
        revisionReason={revisionReason}
        reasonError={reasonError}
        isConfirming={isConfirming}
        downloadingKey={downloadingKey}
        previewingKey={previewingKey}
        onClose={actions.closeInspection}
        onNavigate={actions.navigateInspection}
        onRefetchDetail={actions.refetchDetail}
        onSelectDecision={actions.selectDecision}
        onChangeRevisionReason={actions.setRevisionReason}
        onOpenDecisionConfirm={actions.openDecisionConfirm}
        onDownloadFile={(version, role, fileName) =>
          void actions.downloadFile(version, role, fileName)
        }
        onPreviewPdf={(version, fileName, fileSize) =>
          void actions.previewPdf(version, fileName, fileSize)
        }
        onOpenHistory={actions.openHistory}
      />

      <ArchivalHistoryDialog
        open={isHistoryOpen}
        onClose={actions.closeHistory}
        title={`Lịch sử lưu chiểu · ${inspectedRow?.groupCode ?? selectedRow?.groupCode ?? ''}`}
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

      <ConfirmDialog
        open={pendingAction !== null}
        onClose={actions.closeDecisionConfirm}
        onConfirm={actions.confirmDecision}
        isConfirming={isConfirming}
        type={pendingAction === 'RequestRevision' ? 'warning' : 'brand'}
        title={
          pendingAction === 'AdvisorConfirm'
            ? `Xác nhận bản chỉnh sửa · ${inspectedRow?.groupCode ?? ''}`
            : `Gửi yêu cầu chỉnh sửa · ${inspectedRow?.groupCode ?? ''}`
        }
        confirmText={
          pendingAction === 'AdvisorConfirm' ? 'Xác nhận chuyển Khoa' : 'Gửi yêu cầu chỉnh sửa'
        }
        cancelText="Hủy"
        description={
          pendingAction === 'AdvisorConfirm' ? (
            <div className="space-y-2 text-sm text-slate-600">
              <p>
                Xác nhận phiên bản <strong>v{activeVersion}</strong> (PDF, DOCX và Metadata
                Việt/Anh) của nhóm <strong>{inspectedRow?.groupCode}</strong> đã đạt yêu cầu.
              </p>
              <div className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
                Sau khi xác nhận, hồ sơ sẽ được chuyển sang Văn phòng Khoa nghiệm thu lưu chiểu.
              </div>
            </div>
          ) : (
            <div className="space-y-2 text-sm text-slate-600">
              <p>
                Hồ sơ nhóm <strong>{inspectedRow?.groupCode}</strong> sẽ chuyển về trạng thái{' '}
                <strong>Cần bổ sung</strong> để sinh viên chỉnh sửa và nộp lại.
              </p>
              <div className="rounded-xl border border-amber-200 bg-amber-50/70 px-3 py-2 text-xs text-amber-900 whitespace-pre-wrap">
                Lý do: {revisionReason.trim()}
              </div>
            </div>
          )
        }
      />
    </div>
  );
};
