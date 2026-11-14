import React, { useMemo } from 'react';
import {
  CheckSquare,
  FileSpreadsheet,
  FileText,
  RefreshCw,
  Search,
  Send,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import {
  AcademicResultsReviewPanel,
  DraftScoreSummaryDialog,
  PublishScoresConfirmDialog,
  SynthesisPrinciplesCard,
} from '../components/results';
import { useAcademicResults } from '../hooks/results/useAcademicResults';
import type { MergedResultRow, ResultSortField } from '../types/results.types';
import { formatScoreDecimal } from '../utils/scoreSummaryFormatting';

const batchStatusLabels: Record<string, string> = {
  Draft: 'Nháp',
  Open: 'Đang mở',
  Closed: 'Đã đóng',
  Archived: 'Lưu trữ',
};

export const AcademicResultsPage: React.FC = () => {
  const {
    permissions,
    batches,
    selectedBatch,
    selectedBatchId,
    isLoadingBatches,
    rows,
    allRowsCount,
    totalCount,
    pageNumber,
    pageSize,
    stats,
    eligibleGroupIds,
    keyword,
    filterKey,
    sort,
    inspectedGroupId,
    inspectedRow,
    checkedGroupIds,
    detailData,
    isLoadingDetail,
    detailError,
    draftDialogGroupId,
    draftDialogRow,
    publishConfirmRows,
    errorMessage,
    isLoadingList,
    isFetchingList,
    listError,
    isCalculating,
    calculatingGroupId,
    isPublishing,
    isExporting,
    exportingFormat,
    actions,
  } = useAcademicResults();

  const batchOptions = useMemo(
    () =>
      batches.map((batch) => ({
        value: batch.id,
        label: `${batch.batchName} (${batch.academicYear} - HK${batch.semester})`,
      })),
    [batches],
  );

  const batchSubtitle = selectedBatch
    ? `${selectedBatch.batchName} · ${selectedBatch.academicYear} · HK${selectedBatch.semester} · ${batchStatusLabels[selectedBatch.status] ?? selectedBatch.status}`
    : 'Theo dõi tổng hợp điểm, rà soát bảng điểm dự thảo và công bố kết quả theo đợt';

  const statItems: StatisticItem[] = useMemo(
    () => [
      {
        key: 'all',
        label: 'Tất cả hồ sơ',
        value: stats.all,
        tone: 'muted',
        active: filterKey === 'all',
        onClick: () => actions.handleFilterChange('all'),
      },
      {
        key: 'readyToPublish',
        label: 'Đủ điều kiện',
        value: stats.readyToPublish,
        tone: 'primary',
        active: filterKey === 'readyToPublish',
        onClick: () => actions.handleFilterChange('readyToPublish'),
      },
      {
        key: 'needsCalculation',
        label: 'Chờ tính / Tính lại',
        value: stats.needsCalculation,
        tone: 'warning',
        active: filterKey === 'needsCalculation',
        onClick: () => actions.handleFilterChange('needsCalculation'),
      },
      {
        key: 'needsAction',
        label: 'Cần xử lý',
        value: stats.needsAction,
        tone: 'danger',
        active: filterKey === 'needsAction',
        onClick: () => actions.handleFilterChange('needsAction'),
      },
      {
        key: 'published',
        label: 'Đã công bố',
        value: stats.published,
        tone: 'success',
        active: filterKey === 'published',
        onClick: () => actions.handleFilterChange('published'),
      },
    ],
    [actions, filterKey, stats],
  );

  const allEligibleChecked =
    eligibleGroupIds.length > 0 && eligibleGroupIds.every((id) => checkedGroupIds.includes(id));

  const columns: DataTableColumn<MergedResultRow, ResultSortField>[] = useMemo(() => {
    const baseColumns: DataTableColumn<MergedResultRow, ResultSortField>[] = [];

    if (permissions.canPublish) {
      baseColumns.push({
        id: 'select',
        widthClassName: 'w-11',
        header: (
          <div className="flex items-center justify-center" onClick={(e) => e.stopPropagation()}>
            <Checkbox
              aria-label="Chọn tất cả nhóm đủ điều kiện công bố"
              checked={
                allEligibleChecked
                  ? true
                  : checkedGroupIds.length > 0
                    ? 'indeterminate'
                    : false
              }
              disabled={eligibleGroupIds.length === 0}
              onCheckedChange={actions.toggleAllEligibleChecked}
            />
          </div>
        ),
        cell: (row) => {
          const canSelect = row.isReadyForPublication && row.summaryVersion !== null;
          return (
            <div className="flex items-center justify-center" onClick={(e) => e.stopPropagation()}>
              <Checkbox
                aria-label={`Chọn nhóm ${row.groupCode}`}
                checked={checkedGroupIds.includes(row.groupId)}
                disabled={!canSelect}
                onCheckedChange={() => actions.toggleGroupChecked(row.groupId)}
              />
            </div>
          );
        },
        skeleton: { variant: 'checkbox' },
      });
    }

    baseColumns.push(
      {
        id: 'groupCode',
        header: 'Nhóm',
        sortKey: 'groupCode',
        widthClassName: 'w-24',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <span className="font-semibold text-primary tabular-nums">{row.groupCode}</span>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'topicTitleVi',
        header: 'Đề tài & Sinh viên',
        sortKey: 'topicTitleVi',
        cell: (row) => (
          <div className="min-w-0 space-y-0.5">
            <p className="font-semibold text-slate-900 line-clamp-1">{row.topicTitleVi}</p>
            <p className="text-xs text-slate-500 line-clamp-1">
              {row.studentNames.length > 0
                ? row.studentNames
                    .map((name, index) =>
                      row.studentCodes[index] ? `${name} (${row.studentCodes[index]})` : name,
                    )
                    .join(' · ')
                : row.conditionSummary}
            </p>
          </div>
        ),
        skeleton: { variant: 'topic' },
      },
      {
        id: 'finalScore',
        header: 'Điểm TK',
        sortKey: 'finalScore',
        align: 'right',
        widthClassName: 'w-24',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <span className="font-semibold text-slate-900 tabular-nums">
            {formatScoreDecimal(row.finalScore)}
          </span>
        ),
        skeleton: { variant: 'number' },
      },
      {
        id: 'condition',
        header: 'Điều kiện hồ sơ',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <span className="text-xs text-slate-600 line-clamp-2">{row.conditionSummary}</span>
        ),
        skeleton: { variant: 'text' },
      },
      {
        id: 'status',
        header: 'Trạng thái',
        sortKey: 'status',
        widthClassName: 'w-36',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <StatusBadge tone={row.displayStatusTone}>{row.displayStatusLabel}</StatusBadge>
        ),
        skeleton: { variant: 'badge' },
      },
      {
        id: 'actions',
        header: 'Thao tác',
        align: 'right',
        widthClassName: 'w-24',
        headerClassName: 'whitespace-nowrap',
        cell: (row) => (
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="h-8 rounded-md px-3 text-xs font-medium"
            onClick={(event) => {
              event.stopPropagation();
              actions.setInspectedGroupId(row.groupId);
              actions.openDraftDialog(row.groupId);
            }}
          >
            Xem
          </Button>
        ),
        skeleton: { variant: 'actions', actionCount: 1 },
      },
    );

    return baseColumns;
  }, [
    actions,
    allEligibleChecked,
    checkedGroupIds,
    eligibleGroupIds.length,
    permissions.canPublish,
  ]);

  if (!permissions.canRead) {
    return (
      <div className="space-y-6 pb-10">
        <PageHeader
          accent
          title="Tổng hợp kết quả"
          description="Rà soát điểm tổng kết, điều kiện lưu chiểu và công bố kết quả theo đợt."
        />
        <InlineAlert tone="error" title="Không có quyền truy cập">
          Tài khoản hiện tại chưa được cấp quyền xem bảng tổng hợp kết quả khóa luận.
        </InlineAlert>
      </div>
    );
  }

  return (
    <div className="space-y-6 pb-20 selection:bg-blue-100 selection:text-blue-950">
      <PageHeader accent title="Tổng hợp kết quả" description={batchSubtitle}>
        <div className="w-full sm:w-80 md:w-96">
          <Select
            aria-label="Chọn đợt khóa luận"
            options={batchOptions}
            value={selectedBatchId}
            onChange={(value) => actions.handleBatchChange(String(value ?? ''))}
            disabled={isLoadingBatches || batchOptions.length === 0}
            placeholder="Chọn đợt khóa luận..."
            size="lg"
            triggerClassName="h-10 rounded-xl border-slate-200/90 bg-white text-xs font-semibold text-slate-800 shadow-2xs hover:border-slate-300"
            contentClassName="w-max min-w-full sm:min-w-[420px] max-w-[90vw] sm:right-0 sm:left-auto p-1.5 rounded-xl border-slate-200/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-slate-700 hover:bg-slate-50"
          />
        </div>

        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label="Làm mới danh sách kết quả"
          disabled={!selectedBatchId || isFetchingList}
          onClick={() => actions.refreshList()}
          className="size-10 rounded-xl border-slate-200 bg-white text-slate-600 hover:text-slate-900 shadow-2xs"
          title="Làm mới"
        >
          <RefreshCw className={`size-4 ${isFetchingList ? 'animate-spin' : ''}`} />
        </Button>

        {permissions.canExport && (
          <>
            <Button
              type="button"
              variant="successOutline"
              className="h-10 rounded-xl px-3.5 text-xs font-semibold"
              disabled={!selectedBatchId || isExporting || allRowsCount === 0}
              isLoading={isExporting && exportingFormat === 'xlsx'}
              loadingText="Đang xuất..."
              leftIcon={<FileSpreadsheet className="size-4" />}
              onClick={() => actions.exportReport('xlsx')}
            >
              Xuất Excel
            </Button>

            <Button
              type="button"
              variant="outline"
              className="h-10 rounded-xl px-3.5 text-xs font-semibold"
              disabled={!selectedBatchId || isExporting || allRowsCount === 0}
              isLoading={isExporting && exportingFormat === 'pdf'}
              loadingText="Đang xuất..."
              leftIcon={<FileText className="size-4" />}
              onClick={() => actions.exportReport('pdf')}
            >
              Xuất PDF
            </Button>
          </>
        )}

        {permissions.canPublish && (
          <Button
            type="button"
            variant="default"
            className="h-10 rounded-xl px-4 text-xs font-semibold"
            disabled={
              !selectedBatchId ||
              isPublishing ||
              (checkedGroupIds.length === 0 && eligibleGroupIds.length === 0)
            }
            leftIcon={<Send className="size-4" />}
            onClick={() => actions.openPublishConfirm()}
          >
            {checkedGroupIds.length > 0
              ? `Công bố kết quả (${checkedGroupIds.length})`
              : eligibleGroupIds.length > 0
                ? `Công bố đủ điều kiện (${eligibleGroupIds.length})`
                : 'Công bố kết quả'}
          </Button>
        )}
      </PageHeader>

      {errorMessage && (
        <InlineAlert title="Thao tác chưa hoàn tất" onDismiss={actions.dismissError}>
          {errorMessage}
        </InlineAlert>
      )}

      {!isLoadingBatches && batches.length === 0 && (
        <InlineAlert tone="warning" title="Chưa có đợt khóa luận">
          Hệ thống chưa có đợt khóa luận nào để tra cứu kết quả.
        </InlineAlert>
      )}

      <StatisticsBar
        items={statItems}
        loading={isLoadingList && allRowsCount === 0}
        className="grid-flow-row sm:grid-flow-col"
      />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <section
          aria-label="Hồ sơ trong đợt"
          className="min-w-0 lg:col-span-8 rounded-2xl border border-slate-200/80 bg-white shadow-xs overflow-hidden"
        >
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
            <div>
              <h2 className="text-base font-semibold text-slate-900">Hồ sơ trong đợt</h2>
              <p className="text-xs text-slate-500 mt-0.5 tabular-nums">
                {stats.all} nhóm · {stats.readyToPublish} đủ điều kiện công bố · {stats.published}{' '}
                đã công bố
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2 w-full sm:w-auto">
              <div className="w-full sm:w-72">
                <Input
                  type="search"
                  value={keyword}
                  onChange={(event) => actions.handleKeywordChange(event.target.value)}
                  placeholder="Tìm mã nhóm, tên đề tài, MSSV..."
                  aria-label="Tìm kiếm hồ sơ kết quả"
                  leftIcon={<Search className="size-4 text-slate-400" />}
                  rightIcon={
                    keyword ? (
                      <button
                        type="button"
                        aria-label="Xóa từ khóa tìm kiếm"
                        onClick={() => actions.handleKeywordChange('')}
                        className="text-slate-400 hover:text-slate-700"
                      >
                        <X className="size-3.5" />
                      </button>
                    ) : undefined
                  }
                />
              </div>

              {permissions.canPublish && eligibleGroupIds.length > 0 && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-9 rounded-lg text-xs font-medium"
                  leftIcon={<CheckSquare className="size-3.5 text-primary" />}
                  onClick={actions.toggleAllEligibleChecked}
                >
                  {allEligibleChecked
                    ? 'Bỏ chọn tất cả'
                    : `Chọn đủ điều kiện (${eligibleGroupIds.length})`}
                </Button>
              )}
            </div>
          </div>

          <DataTable
            ariaLabel="Danh sách hồ sơ tổng hợp kết quả"
            data={rows}
            columns={columns}
            getRowId={(row) => row.groupId}
            selectedRowId={inspectedGroupId}
            onRowSelect={(id) => actions.setInspectedGroupId(id)}
            onRowOpen={(row) => {
              actions.setInspectedGroupId(row.groupId);
              actions.openDraftDialog(row.groupId);
            }}
            sort={sort}
            onSortChange={actions.handleSortChange}
            loading={isLoadingList}
            busy={isFetchingList && !isLoadingList}
            error={
              listError
                ? {
                    message: listError,
                    onRetry: () => {
                      void actions.refreshList();
                    },
                  }
                : null
            }
            empty={{
              title: keyword || filterKey !== 'all' ? 'Không có hồ sơ phù hợp' : 'Chưa có nhóm trong đợt',
              description:
                keyword || filterKey !== 'all'
                  ? 'Thử đổi từ khóa tìm kiếm hoặc chọn bộ lọc trạng thái khác.'
                  : 'Đợt khóa luận này chưa có nhóm sinh viên để tổng hợp kết quả.',
              action:
                keyword || filterKey !== 'all' ? (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      actions.handleKeywordChange('');
                      actions.handleFilterChange('all');
                    }}
                  >
                    Xóa bộ lọc
                  </Button>
                ) : undefined,
            }}
            pagination={{
              pageNumber,
              pageSize,
              totalCount,
              itemLabel: 'nhóm',
              onPageChange: actions.setPageNumber,
              onPageSizeChange: actions.setPageSize,
            }}
            renderMobileItem={(row) => (
              <div
                key={row.groupId}
                onClick={() => actions.setInspectedGroupId(row.groupId)}
                className={`p-4 space-y-2.5 border-b border-slate-100 last:border-b-0 ${
                  inspectedGroupId === row.groupId ? 'bg-blue-50/40' : 'bg-white'
                }`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-center gap-2 min-w-0">
                    {permissions.canPublish &&
                      row.isReadyForPublication &&
                      row.summaryVersion !== null && (
                        <div onClick={(event) => event.stopPropagation()}>
                          <Checkbox
                            aria-label={`Chọn nhóm ${row.groupCode}`}
                            checked={checkedGroupIds.includes(row.groupId)}
                            onCheckedChange={() => actions.toggleGroupChecked(row.groupId)}
                          />
                        </div>
                      )}
                    <span className="font-semibold text-primary tabular-nums">{row.groupCode}</span>
                    <StatusBadge tone={row.displayStatusTone}>{row.displayStatusLabel}</StatusBadge>
                  </div>
                  <span className="text-sm font-bold text-slate-900 tabular-nums">
                    {formatScoreDecimal(row.finalScore)}
                  </span>
                </div>

                <p className="text-sm font-semibold text-slate-900 line-clamp-2">
                  {row.topicTitleVi}
                </p>
                <p className="text-xs text-slate-500">{row.conditionSummary}</p>

                <div className="flex items-center justify-end gap-2 pt-1">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-8 rounded-md px-3 text-xs"
                    onClick={(event) => {
                      event.stopPropagation();
                      actions.setInspectedGroupId(row.groupId);
                      actions.openDraftDialog(row.groupId);
                    }}
                  >
                    Xem
                  </Button>
                </div>
              </div>
            )}
          />
        </section>

        <aside
          aria-label="Hồ sơ đang rà soát"
          className="min-w-0 lg:col-span-4 lg:sticky lg:top-6 rounded-2xl border border-slate-200/80 bg-white shadow-xs overflow-hidden"
        >
          <AcademicResultsReviewPanel
            row={inspectedRow}
            detail={detailData}
            isLoading={isLoadingDetail}
            error={detailError}
            canCalculate={permissions.canCalculate}
            canPublish={permissions.canPublish}
            isCalculating={isCalculating}
            calculatingGroupId={calculatingGroupId}
            onCalculate={actions.calculateScore}
            onOpenDraftDialog={actions.openDraftDialog}
            onPublishSingle={(groupId) => actions.openPublishConfirm([groupId])}
            onRetry={() => {
              void actions.refreshList();
            }}
          />
        </aside>
      </div>

      <SynthesisPrinciplesCard />

      {permissions.canPublish && (
        <TableFloatingBar
          selectedCount={checkedGroupIds.length}
          totalCount={eligibleGroupIds.length}
          onClearSelection={actions.clearCheckedGroups}
        >
          <Button
            type="button"
            variant="default"
            size="sm"
            className="h-8 rounded-lg px-3 text-xs font-semibold"
            leftIcon={<Send className="size-3.5" />}
            onClick={() => actions.openPublishConfirm(checkedGroupIds)}
          >
            Công bố {checkedGroupIds.length} nhóm
          </Button>
        </TableFloatingBar>
      )}

      <DraftScoreSummaryDialog
        open={Boolean(draftDialogGroupId && draftDialogRow)}
        row={draftDialogRow}
        detail={detailData}
        isLoading={isLoadingDetail}
        canCalculate={permissions.canCalculate}
        canPublish={permissions.canPublish}
        isCalculating={isCalculating && calculatingGroupId === draftDialogGroupId}
        onClose={actions.closeDraftDialog}
        onCalculate={actions.calculateScore}
        onConfirmPublish={(groupId) => actions.openPublishConfirm([groupId])}
      />

      <PublishScoresConfirmDialog
        open={publishConfirmRows.length > 0}
        rows={publishConfirmRows}
        isConfirming={isPublishing}
        onClose={actions.closePublishConfirm}
        onConfirm={actions.confirmPublish}
      />
    </div>
  );
};
