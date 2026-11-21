import React, { useMemo } from 'react';
import { RefreshCw, Search, X } from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { cn } from '@/lib/utils';
import { SimilarityReportDialog } from '../components/similarity';
import { useCouncilSimilarityReports } from '../hooks/useCouncilSimilarityReports';
import {
  formatSimilarityPercent,
  type SimilarityRowView,
  type SimilaritySortField,
} from '../utils/similarity';

export const CouncilSimilarityReportsPage: React.FC = () => {
  const {
    batches,
    isLoadingBatches,
    selectedBatchId,
    filterKey,
    keyword,
    sort,
    pageNumber,
    pageSize,
    totalCount,
    allRowsCount,
    stats,
    rows,
    selectedRowId,
    selectedRow,
    inspectedRow,
    inspectedIndex,
    filteredTotalCount,
    isLoadingList,
    isFetchingList,
    listError,
    actions,
  } = useCouncilSimilarityReports();

  const batchOptions = useMemo(
    () => [
      { value: 'ALL', label: 'Tất cả đợt khóa luận' },
      ...batches.map((batch) => ({
        value: batch.id,
        label: `${batch.batchName} (${batch.academicYear} - HK${batch.semester})`,
      })),
    ],
    [batches],
  );

  const statItems: StatisticItem[] = useMemo(
    () => [
      {
        key: 'ALL',
        label: 'Tất cả báo cáo',
        value: stats.total,
        tone: 'primary',
        active: filterKey === 'ALL',
        onClick: () => actions.changeFilter('ALL'),
      },
      {
        key: 'NeedsAttention',
        label: 'Cần rà soát',
        value: stats.needsAttention,
        tone: 'warning',
        active: filterKey === 'NeedsAttention',
        onClick: () => actions.changeFilter('NeedsAttention'),
      },
      {
        key: 'LowSimilarity',
        label: 'Tương đồng thấp',
        value: stats.lowSimilarity,
        tone: 'success',
        active: filterKey === 'LowSimilarity',
        onClick: () => actions.changeFilter('LowSimilarity'),
      },
      {
        key: 'Pending',
        label: 'Đang quét / Chờ GPU',
        value: stats.pending,
        tone: 'primary',
        active: filterKey === 'Pending',
        onClick: () => actions.changeFilter('Pending'),
      },
      {
        key: 'IssueOrUnscanned',
        label: 'Lỗi / Chưa quét',
        value: stats.issueOrUnscanned,
        tone: 'danger',
        active: filterKey === 'IssueOrUnscanned',
        onClick: () => actions.changeFilter('IssueOrUnscanned'),
      },
    ],
    [stats, filterKey, actions],
  );

  const columns: DataTableColumn<SimilarityRowView, SimilaritySortField>[] = useMemo(
    () => [
      {
        id: 'groupCode',
        header: 'Nhóm',
        sortKey: 'groupCode',
        headerClassName: 'w-[104px] whitespace-nowrap',
        cell: (row) => (
          <span
            className="font-semibold text-primary tabular-nums whitespace-nowrap"
            title={`UUID nhóm: ${row.groupId}`}
          >
            {row.groupCode}
          </span>
        ),
      },
      {
        id: 'topicTitle',
        header: 'Đề tài và Sinh viên',
        sortKey: 'topicTitle',
        headerClassName: 'min-w-[280px]',
        cell: (row) => {
          const studentItems = (row.students ?? []).map((s) => ({
            id: s.id,
            name: s.fullName,
            code: s.userCode,
          }));
          return (
            <div className="min-w-0 space-y-1.5">
              <p
                className="font-semibold text-ink-primary line-clamp-2 leading-snug"
                title={row.topicTitle}
              >
                {row.topicTitle}
              </p>
              {studentItems.length > 0 ? (
                <div className="flex items-center gap-2 min-w-0">
                  <AvatarGroup
                    items={studentItems}
                    max={3}
                    size="sm"
                    ariaLabel="Sinh viên trong nhóm"
                  />
                  <span className="text-xs text-ink-muted truncate" title={row.studentsText}>
                    {row.studentsText}
                  </span>
                </div>
              ) : row.topicTitleEn ? (
                <p className="text-xs italic text-ink-muted truncate">{row.topicTitleEn}</p>
              ) : null}
            </div>
          );
        },
      },
      {
        id: 'version',
        header: 'Phiên bản',
        sortKey: 'version',
        headerClassName: 'w-[108px] whitespace-nowrap',
        cell: (row) => (
          <div className="inline-flex items-center gap-1.5 whitespace-nowrap">
            <span className="text-xs font-semibold text-ink-secondary tabular-nums">
              v{row.versionNo}
            </span>
            {row.versionNo > 1 && (
              <Badge variant="info" className="px-1.5 py-0 text-[10px]">
                Cập nhật
              </Badge>
            )}
          </div>
        ),
      },
      {
        id: 'coverage',
        header: 'Tỷ lệ tương đồng',
        sortKey: 'coverage',
        headerClassName: 'w-[176px] whitespace-nowrap',
        cell: (row) => {
          if (row.normalizedCoverage == null) {
            return <span className="text-xs text-ink-subtle tabular-nums">—</span>;
          }
          const barColor =
            row.normalizedCoverage >= 30
              ? 'bg-rose-500'
              : row.normalizedCoverage >= 15
                ? 'bg-amber-500'
                : 'bg-emerald-500';
          const textColor =
            row.normalizedCoverage >= 30
              ? 'text-tone-danger-text'
              : row.normalizedCoverage >= 15
                ? 'text-tone-warning-text'
                : 'text-tone-success-text';
          const noteText =
            row.suspectedCount > 0
              ? `${row.suspectedCount} nghi sai nguồn`
              : row.uncitedCount > 0
                ? `${row.uncitedCount} thiếu trích dẫn`
                : row.citedCount > 0
                  ? `${row.citedCount} có trích dẫn`
                  : 'Không có đoạn khớp';
          return (
            <div className="space-y-1 min-w-[140px]">
              <div className="flex items-center justify-between gap-2">
                <span className={cn('text-xs font-bold tabular-nums', textColor)}>
                  {formatSimilarityPercent(row.normalizedCoverage)}
                </span>
                <span className="text-[11px] text-ink-muted truncate">{noteText}</span>
              </div>
              <div className="h-1.5 w-full rounded-full bg-surface-muted overflow-hidden">
                <div
                  className={cn('h-full rounded-full', barColor)}
                  style={{ width: `${Math.min(100, Math.max(4, row.normalizedCoverage))}%` }}
                />
              </div>
            </div>
          );
        },
      },
      {
        id: 'status',
        header: 'Trạng thái quét',
        sortKey: 'status',
        headerClassName: 'w-[168px] whitespace-nowrap',
        cell: (row) => <StatusBadge tone={row.scanTone}>{row.scanLabel}</StatusBadge>,
      },
      {
        id: 'actions',
        header: 'Thao tác',
        align: 'right',
        headerClassName: 'w-[96px] whitespace-nowrap',
        cell: (row) => (
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="h-8 rounded-md px-3 text-xs font-medium bg-surface-card shadow-2xs"
            onClick={(event) => {
              event.stopPropagation();
              actions.openInspection(row.finalReportId);
            }}
          >
            Xem
          </Button>
        ),
      },
    ],
    [actions],
  );

  const hasCustomFilter = selectedBatchId !== 'ALL' || filterKey !== 'ALL' || keyword.trim() !== '';

  return (
    <main className="mx-auto max-w-7xl space-y-6 px-4 py-6 sm:px-6">
      <PageHeader
        accent
        title="Báo cáo tương đồng"
        description="Kiểm tra mức độ trùng lặp văn bản và đối chiếu bằng chứng trích dẫn cho các nhóm thuộc hội đồng của bạn."
        actionsClassName="w-full lg:w-auto flex-nowrap justify-between sm:justify-end items-center shrink-0"
      >
        <div className="min-w-0 flex-1 sm:flex-none sm:w-72">
          <Select
            aria-label="Chọn đợt khóa luận"
            value={selectedBatchId}
            onChange={(value) => actions.selectBatch(value)}
            options={batchOptions}
            disabled={isLoadingBatches}
            placeholder="Chọn đợt khóa luận..."
            triggerClassName="h-10 rounded-xl border-line/90 bg-surface-card text-xs font-semibold text-ink-heading shadow-2xs hover:border-line-strong"
            contentClassName="w-max min-w-full sm:min-w-[380px] max-w-[90vw] sm:right-0 sm:left-auto p-1.5 rounded-xl border-line/90 shadow-lg"
            optionClassName="text-xs py-2 px-3 rounded-lg font-medium text-ink-secondary hover:bg-surface-hover"
          />
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="h-10 rounded-xl border-line/90 bg-surface-card px-3.5 text-xs font-semibold text-ink-secondary shadow-2xs hover:bg-surface-hover shrink-0"
          leftIcon={<RefreshCw className={cn('size-3.5', isFetchingList && 'animate-spin')} />}
          onClick={actions.refresh}
          disabled={isFetchingList}
        >
          Làm mới
        </Button>
      </PageHeader>

      <StatisticsBar
        items={statItems}
        loading={isLoadingList}
        className="grid-cols-2 sm:grid-cols-3 grid-flow-row lg:grid-flow-col"
      />

      <Card className="rounded-2xl border-line/80 shadow-xs overflow-hidden bg-surface-card">
        <div className="flex flex-col gap-3 border-b border-line-subtle p-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="relative flex-1 max-w-md">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 size-4 text-ink-subtle" />
            <Input
              type="search"
              value={keyword}
              onChange={(event) => actions.changeKeyword(event.target.value)}
              placeholder="Tìm mã nhóm, tên đề tài, tên hoặc MSSV sinh viên..."
              aria-label="Tìm báo cáo tương đồng"
              className="h-9 pl-9 pr-8 text-xs sm:text-sm bg-surface-card"
            />
            {keyword && (
              <button
                type="button"
                aria-label="Xóa từ khóa tìm kiếm"
                onClick={() => actions.changeKeyword('')}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-ink-subtle hover:text-ink-body cursor-pointer"
              >
                <X className="size-3.5" />
              </button>
            )}
          </div>

          <div className="flex items-center gap-2 text-xs text-ink-muted">
            <span className="tabular-nums">
              Hiển thị <strong className="text-ink-secondary">{filteredTotalCount}</strong> /{' '}
              {allRowsCount} báo cáo
            </span>
            {hasCustomFilter && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-8 rounded-lg bg-surface-card text-xs shadow-2xs"
                onClick={actions.resetFilters}
              >
                Đặt lại mặc định
              </Button>
            )}
          </div>
        </div>

        {listError && (
          <div className="p-4 border-b border-line-subtle">
            <InlineAlert
              tone="error"
              title="Không tải được danh sách báo cáo được phân công"
              action={
                <Button type="button" variant="outline" size="sm" onClick={actions.refresh}>
                  Thử lại
                </Button>
              }
            >
              Vui lòng kiểm tra kết nối máy chủ hoặc thử tải lại danh sách.
            </InlineAlert>
          </div>
        )}

        <DataTable<SimilarityRowView, SimilaritySortField>
          ariaLabel="Danh sách báo cáo tương đồng được phân công cho Hội đồng"
          data={rows}
          columns={columns}
          getRowId={(row) => row.finalReportId}
          selectedRowId={selectedRowId}
          onRowSelect={(id) => actions.selectRow(id)}
          onRowOpen={(row) => actions.openInspection(row.finalReportId)}
          sort={sort}
          onSortChange={actions.changeSort}
          loading={isLoadingList && allRowsCount === 0}
          busy={isFetchingList && allRowsCount > 0}
          tableClassName="min-w-[820px]"
          rowClassName="h-[68px]"
          renderMobileItem={(row) => {
            const isSelected = selectedRowId === row.finalReportId;
            return (
              <div
                key={row.finalReportId}
                onClick={() => actions.openInspection(row.finalReportId)}
                className={cn(
                  'rounded-xl p-1 transition-colors cursor-pointer space-y-2.5',
                  isSelected ? 'bg-tone-info-bg' : 'bg-surface-card',
                )}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-bold text-primary tabular-nums">
                      {row.groupCode}
                    </span>
                    <span className="text-xs font-semibold text-ink-body tabular-nums">
                      v{row.versionNo}
                    </span>
                    {row.versionNo > 1 && (
                      <Badge variant="info" className="px-1.5 py-0 text-[10px]">
                        Cập nhật
                      </Badge>
                    )}
                  </div>
                  <StatusBadge tone={row.scanTone}>{row.scanLabel}</StatusBadge>
                </div>

                <p className="text-sm font-semibold text-ink-primary leading-snug line-clamp-2">
                  {row.topicTitle}
                </p>

                {row.studentsText && (
                  <p className="text-xs text-ink-muted leading-relaxed">{row.studentsText}</p>
                )}

                <div className="flex items-center justify-between gap-3 pt-1">
                  <div className="flex items-center gap-2 text-xs">
                    {row.normalizedCoverage != null ? (
                      <>
                        <span className="font-bold text-ink-heading tabular-nums">
                          {formatSimilarityPercent(row.normalizedCoverage)}
                        </span>
                        {row.attentionCount > 0 && (
                          <span className="text-[11px] text-tone-danger-text font-medium">
                            · {row.attentionCount} cần rà soát
                          </span>
                        )}
                      </>
                    ) : (
                      <span className="text-ink-subtle">Chưa có tỷ lệ</span>
                    )}
                  </div>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-8 rounded-md px-3 text-xs font-medium bg-surface-card"
                    onClick={(event) => {
                      event.stopPropagation();
                      actions.openInspection(row.finalReportId);
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
            itemLabel: 'báo cáo',
          }}
          empty={{
            title:
              allRowsCount === 0
                ? 'Chưa có báo cáo nào đến giai đoạn thẩm định'
                : 'Không có báo cáo phù hợp bộ lọc',
            description:
              allRowsCount === 0
                ? 'Các nhóm thuộc hội đồng của bạn sẽ xuất hiện khi báo cáo cuối kỳ chuyển sang trạng thái đánh giá.'
                : 'Thử chuyển sang "Tất cả báo cáo" hoặc xóa từ khóa tìm kiếm.',
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
                  Xem tất cả báo cáo
                </Button>
              ) : undefined,
          }}
        />
      </Card>

      <TableFloatingBar
        aria-label="Thao tác với báo cáo đã chọn"
        show={Boolean(selectedRow && !inspectedRow)}
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
              onClick={() => actions.openInspection(selectedRow.finalReportId)}
            >
              Xem kết quả tương đồng
            </Button>
          </>
        )}
      </TableFloatingBar>

      <SimilarityReportDialog
        open={Boolean(inspectedRow)}
        inspectedRow={inspectedRow}
        inspectedIndex={inspectedIndex}
        filteredTotalCount={filteredTotalCount}
        onClose={actions.closeInspection}
        onNavigate={actions.navigateInspection}
      />
    </main>
  );
};
