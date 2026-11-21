import { useMemo } from 'react';
import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { cn } from '@/lib/utils';
import { useCouncilApproval } from '../hooks/useCouncilApproval';
import {
  CouncilApprovalTable,
  CouncilApprovalDecisionCard,
  CouncilApprovalDetailCard,
} from '../components/council-approval';
import { formatViDate } from '../utils/councilApprovalReadiness';

export function CouncilApprovalPage() {
  const {
    batches,
    isLoadingBatches,
    batchFilter,
    setBatchFilter,
    keyword,
    setKeyword,
    readinessFilter,
    setReadinessFilter,
    sort,
    handleSortChange,
    pageNumber,
    setPageNumber,
    pageSize,
    setPageSize,
    handleResetFilter,
    draftsQuery,
    filteredDrafts,
    pagedDrafts,
    readinessMap,
    stats,
    selectedCouncilId,
    setSelectedCouncilId,
    selectedCouncil,
    selectedReadiness,
    scheduleQuery,
    selectedFile,
    setSelectedFile,
    customDecisionFileUrl,
    setCustomDecisionFileUrl,
    fileError,
    customDecisionNumber,
    setCustomDecisionNumber,
    customSignedDate,
    setCustomSignedDate,
    confirmOpen,
    setConfirmOpen,
    handleRequestApprove,
    errorMessage,
    canApprove,
    approveMutation,
  } = useCouncilApproval();

  const batchOptions = useMemo(
    () => [
      { value: 'all', label: 'Tất cả đợt khóa luận' },
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
        key: 'all',
        label: 'Tất cả hội đồng',
        value: stats.total,
        tone: 'muted',
        active: readinessFilter === 'all',
        onClick: () => setReadinessFilter('all'),
      },
      {
        key: 'ready',
        label: 'Sẵn sàng duyệt',
        value: stats.ready,
        tone: 'primary',
        active: readinessFilter === 'ready',
        onClick: () => setReadinessFilter('ready'),
      },
      {
        key: 'needs_action',
        label: 'Cần bổ sung',
        value: stats.needsAction,
        tone: 'warning',
        active: readinessFilter === 'needs_action',
        onClick: () => setReadinessFilter('needs_action'),
      },
      {
        key: 'approved',
        label: 'Đã phê duyệt',
        value: stats.approved,
        tone: 'success',
        active: readinessFilter === 'approved',
        onClick: () => setReadinessFilter('approved'),
      },
    ],
    [stats, readinessFilter, setReadinessFilter],
  );

  const hasActiveFilters =
    Boolean(keyword.trim()) || readinessFilter !== 'all' || batchFilter !== 'all';

  const tableError = draftsQuery.isError
    ? {
        message:
          draftsQuery.error instanceof Error
            ? draftsQuery.error.message
            : 'Không thể tải danh sách Hội đồng.',
        onRetry: () => {
          void draftsQuery.refetch();
        },
      }
    : null;

  return (
    <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 space-y-5">
      <PageHeader
        title="Phê duyệt quyết định Hội đồng"
        description="Thẩm định điều kiện thành lập và ban hành quyết định Hội đồng bảo vệ."
        accent
        actionsClassName="w-full sm:w-auto"
      >
        <div className="w-full sm:w-64">
          <Select<string>
            value={batchFilter}
            onChange={(val) => setBatchFilter(val)}
            options={batchOptions}
            disabled={isLoadingBatches}
            placeholder="Chọn đợt khóa luận..."
            aria-label="Chọn đợt khóa luận"
            size="default"
          />
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => {
            void draftsQuery.refetch();
          }}
          disabled={draftsQuery.isFetching}
          className="h-9 px-3 rounded-xl text-xs font-medium shrink-0"
        >
          <RefreshCw className={cn('size-3.5 mr-1.5', draftsQuery.isFetching && 'animate-spin')} />
          Làm mới
        </Button>
      </PageHeader>

      <StatisticsBar
        items={statItems}
        loading={draftsQuery.isLoading}
        className="grid-cols-2 md:grid-cols-4 grid-flow-row"
      />

      {errorMessage && !draftsQuery.isError && (
        <InlineAlert title="Không thể hoàn tất thao tác">{errorMessage}</InlineAlert>
      )}

      <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start">
        <div className="xl:col-span-7 min-w-0">
          <CouncilApprovalTable
            items={pagedDrafts}
            totalCount={filteredDrafts.length}
            readinessMap={readinessMap}
            selectedCouncilId={selectedCouncilId}
            onSelectCouncilId={setSelectedCouncilId}
            sort={sort}
            onSortChange={handleSortChange}
            pageNumber={pageNumber}
            pageSize={pageSize}
            onPageChange={setPageNumber}
            onPageSizeChange={setPageSize}
            keyword={keyword}
            onKeywordChange={setKeyword}
            readinessFilter={readinessFilter}
            onReadinessFilterChange={setReadinessFilter}
            hasActiveFilters={hasActiveFilters}
            onResetFilters={handleResetFilter}
            isLoading={draftsQuery.isLoading}
            isFetching={draftsQuery.isFetching}
            error={tableError}
          />
        </div>

        <aside className="xl:col-span-5 xl:sticky xl:top-20 min-w-0">
          <Card className="rounded-2xl border border-line/80 bg-surface-card shadow-xs overflow-hidden divide-y divide-line-subtle">
            <CouncilApprovalDetailCard
              selectedCouncil={selectedCouncil}
              readiness={selectedReadiness}
              schedule={scheduleQuery.data}
              isLoadingSchedule={scheduleQuery.isLoading}
            />
            <CouncilApprovalDecisionCard
              selectedCouncil={selectedCouncil}
              readiness={selectedReadiness}
              schedule={scheduleQuery.data}
              selectedFile={selectedFile}
              onFileSelect={setSelectedFile}
              decisionFileUrl={customDecisionFileUrl}
              onDecisionFileUrlChange={setCustomDecisionFileUrl}
              fileError={fileError}
              decisionNumber={customDecisionNumber}
              onDecisionNumberChange={setCustomDecisionNumber}
              signedDate={customSignedDate}
              onSignedDateChange={setCustomSignedDate}
              onRequestApprove={handleRequestApprove}
              isApproving={approveMutation.isPending}
              canApprove={canApprove}
            />
          </Card>
        </aside>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => approveMutation.mutate()}
        type="brand"
        title={`Xác nhận phê duyệt ${selectedCouncil?.councilCode ?? 'Hội đồng'}`}
        description={
          <div className="space-y-2">
            <p>
              Hội đồng sẽ chuyển sang trạng thái <strong>Đã phê duyệt (Active)</strong> và mở quyền chấm điểm, xem lịch bảo vệ chính thức.
            </p>
            <dl className="rounded-xl border border-line/80 bg-surface-subtle/70 p-3 space-y-1 text-xs">
              <div className="flex justify-between gap-2">
                <dt className="text-ink-muted">Số quyết định:</dt>
                <dd className="font-semibold text-ink-heading tabular-nums">
                  {customDecisionNumber.trim()}
                </dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-ink-muted">Ngày ký:</dt>
                <dd className="font-medium text-ink-heading tabular-nums">
                  {formatViDate(customSignedDate)}
                </dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-ink-muted">Quy mô:</dt>
                <dd className="font-medium text-ink-heading">
                  {selectedCouncil?.groupCodes.length ?? 0} nhóm bảo vệ
                </dd>
              </div>
            </dl>
          </div>
        }
        confirmText="Xác nhận phê duyệt"
        cancelText="Quay lại"
        isConfirming={approveMutation.isPending}
      />
    </div>
  );
}
