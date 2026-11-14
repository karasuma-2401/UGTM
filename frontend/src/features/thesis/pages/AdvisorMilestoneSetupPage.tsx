import { useMemo } from 'react';
import {
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  Flag,
  Layers,
  Plus,
  RefreshCw,
  RotateCcw,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PageHeader } from '@/components/ui/page-header';
import { Select, type SelectOption } from '@/components/ui/select';
import { StatisticsBar, type StatisticItem } from '@/components/ui/statistics-bar';
import { cn } from '@/lib/utils';
import {
  AdvisorGroupPlanCard,
  AdvisorMilestoneCreateDialog,
  AdvisorMilestoneReviewDialog,
  AdvisorMilestoneTable,
} from '../components/milestone';
import { useAdvisorMilestones, type MilestoneStatusFilter } from '../hooks/useAdvisorMilestones';

export function AdvisorMilestoneSetupPage() {
  const {
    batches,
    selectedBatchId,
    setSelectedBatchId,
    batchesLoading,

    advisedGroups,
    selectedGroup,
    selectedGroupId,
    setSelectedGroupId,
    groupsLoading,
    topicsError,

    milestones,
    filteredMilestones,
    stats,
    statusFilter,
    setStatusFilter,

    selectedMilestone,
    selectedMilestoneId,
    selectMilestone,

    milestonesLoading,
    isFetching,
    milestonesError,

    canCreateMilestones,
    ineligibleReason,

    createDialogOpen,
    openCreateDialog,
    closeCreateDialog,
    createMilestone,
    isCreating,
    createError,

    reviewingMilestone,
    openReviewDialog,
    closeReviewDialog,
    reviewReport,
    isReviewing,
    reviewError,

    downloadingFileId,
    handleDownloadFile,
    refresh,
  } = useAdvisorMilestones();

  const batchOptions = useMemo<SelectOption<string>[]>(
    () =>
      batches.map((batch) => ({
        value: batch.id,
        label: batch.batchName,
        description: batch.status === 'Open' ? 'Đợt đang mở' : 'Đợt đã đóng',
      })),
    [batches],
  );

  const groupOptions = useMemo<SelectOption<string>[]>(
    () =>
      advisedGroups.map((group) => ({
        value: group.groupId,
        label: `${group.groupCode} · ${group.studentNames}`,
        description: group.topicTitleVi,
      })),
    [advisedGroups],
  );

  const statisticsItems = useMemo<StatisticItem[]>(() => {
    const toggle = (target: MilestoneStatusFilter) => {
      setStatusFilter(statusFilter === target ? 'All' : target);
    };

    return [
      {
        key: 'all',
        label: 'Tổng mốc tiến độ',
        value: stats.total,
        icon: <Flag className="size-4" aria-hidden="true" />,
        tone: 'primary',
        active: statusFilter === 'All',
        onClick: () => setStatusFilter('All'),
      },
      {
        key: 'submitted',
        label: 'Chờ GV đánh giá',
        value: stats.submitted,
        icon: <ClipboardCheck className="size-4" aria-hidden="true" />,
        tone: 'warning',
        active: statusFilter === 'Submitted',
        onClick: () => toggle('Submitted'),
      },
      {
        key: 'need-revision',
        label: 'Cần chỉnh sửa',
        value: stats.needRevision,
        icon: <RotateCcw className="size-4" aria-hidden="true" />,
        tone: 'danger',
        active: statusFilter === 'NeedRevision',
        onClick: () => toggle('NeedRevision'),
      },
      {
        key: 'accepted',
        label: 'Đã đạt',
        value: stats.accepted,
        icon: <CheckCircle2 className="size-4" aria-hidden="true" />,
        tone: 'success',
        active: statusFilter === 'Accepted',
        onClick: () => toggle('Accepted'),
      },
      {
        key: 'not-submitted',
        label: 'Chưa nộp',
        value: stats.notSubmitted,
        icon: <Clock3 className="size-4" aria-hidden="true" />,
        tone: 'muted',
        active: statusFilter === 'NotSubmitted',
        onClick: () => toggle('NotSubmitted'),
      },
    ];
  }, [setStatusFilter, stats, statusFilter]);

  const showEmptyGroupsBanner = !groupsLoading && !topicsError && advisedGroups.length === 0;

  return (
    <div className="space-y-6 pb-8">
      <PageHeader
        accent
        title="Tiến độ sinh viên"
        description="Giao mốc kiểm tra, theo dõi tiến độ nộp báo cáo và nhận xét trực tiếp cho từng nhóm hướng dẫn."
      >
        <div className="flex flex-wrap items-center gap-2.5">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={isFetching}
            onClick={() => void refresh()}
            className="h-9 gap-1.5 rounded-xl border-slate-200 bg-white shadow-2xs hover:border-slate-300"
          >
            <RefreshCw className={cn('size-4', isFetching && 'animate-spin')} aria-hidden="true" />
            <span>Làm mới</span>
          </Button>

          <Button
            type="button"
            size="sm"
            disabled={!canCreateMilestones}
            title={
              !canCreateMilestones && ineligibleReason
                ? ineligibleReason
                : undefined
            }
            onClick={openCreateDialog}
            className="h-9 gap-1.5 rounded-xl"
          >
            <Plus className="size-4" aria-hidden="true" />
            <span>Thêm mốc tiến độ</span>
          </Button>
        </div>
      </PageHeader>

      {/* Fluent context toolbar: Batch + Advised group selector */}
      <section
        aria-label="Bộ lọc đợt và nhóm hướng dẫn"
        className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-2xs"
      >
        <div className="grid gap-4 lg:grid-cols-[minmax(240px,1fr)_minmax(320px,1.5fr)]">
          <div className="space-y-1.5">
            <label
              htmlFor="advisor-milestone-batch-select"
              className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 select-none"
            >
              <Layers className="size-3.5 text-slate-500" aria-hidden="true" />
              <span>Đợt khóa luận</span>
            </label>
            <Select
              id="advisor-milestone-batch-select"
              options={batchOptions}
              value={selectedBatchId}
              disabled={batchesLoading || batches.length === 0}
              placeholder={batchesLoading ? 'Đang tải đợt khóa luận...' : 'Chọn đợt khóa luận'}
              noOptionsMessage="Chưa có đợt khóa luận nào"
              onChange={(value) => {
                if (typeof value === 'string' && value) {
                  setSelectedBatchId(value);
                }
              }}
            />
          </div>

          <div className="space-y-1.5">
            <label
              htmlFor="advisor-milestone-group-select"
              className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 select-none"
            >
              <Users className="size-3.5 text-slate-500" aria-hidden="true" />
              <span>Nhóm hướng dẫn ({advisedGroups.length})</span>
            </label>
            <Select
              id="advisor-milestone-group-select"
              options={groupOptions}
              value={selectedGroupId ?? ''}
              disabled={groupsLoading || advisedGroups.length === 0}
              placeholder={
                groupsLoading
                  ? 'Đang tải danh sách nhóm...'
                  : advisedGroups.length === 0
                    ? 'Không có nhóm nào trong đợt này'
                    : 'Chọn nhóm hướng dẫn'
              }
              noOptionsMessage="Không tìm thấy nhóm hướng dẫn"
              onChange={(value) => {
                if (typeof value === 'string' && value) {
                  setSelectedGroupId(value);
                }
              }}
            />
          </div>
        </div>
      </section>

      {topicsError && (
        <InlineAlert
          tone="error"
          title="Không tải được danh sách nhóm hướng dẫn"
          action={
            <Button type="button" size="sm" variant="outline" onClick={() => void refresh()}>
              Thử lại
            </Button>
          }
        >
          {topicsError}
        </InlineAlert>
      )}

      {showEmptyGroupsBanner && (
        <InlineAlert tone="info" title="Chưa có nhóm hướng dẫn trong đợt này">
          Tài khoản hiện tại chưa được phân công nhóm sinh viên thực hiện khóa luận ở đợt đã chọn.
          Hãy chọn đợt khóa luận khác hoặc đăng nhập bằng tài khoản giảng viên hướng dẫn có nhóm.
        </InlineAlert>
      )}

      {selectedGroup && ineligibleReason && (
        <InlineAlert tone="warning" title="Chưa thể thiết lập thêm mốc tiến độ">
          {ineligibleReason}
        </InlineAlert>
      )}

      {selectedGroup && (
        <>
          <StatisticsBar
            layout="card"
            loading={milestonesLoading && milestones.length === 0}
            items={statisticsItems}
            className="grid-cols-2 sm:grid-cols-3 lg:grid-cols-5"
          />

          <div className="grid items-start gap-6 lg:grid-cols-12">
            <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)] lg:col-span-8">
              <CardHeader className="flex-row items-center justify-between gap-3 space-y-0 border-b border-slate-100/90 bg-gradient-to-r from-slate-50/80 via-white to-white px-5 py-3.5">
                <CardTitle className="text-base font-semibold tracking-tight text-slate-900">
                  Danh sách mốc tiến độ
                </CardTitle>
                <span className="text-xs font-medium tabular-nums text-slate-500">
                  {statusFilter === 'All'
                    ? `${milestones.length} mốc`
                    : `${filteredMilestones.length}/${milestones.length} mốc`}
                </span>
              </CardHeader>

              <AdvisorMilestoneTable
                milestones={filteredMilestones}
                selectedMilestoneId={selectedMilestoneId}
                onSelectMilestone={selectMilestone}
                onOpenReview={openReviewDialog}
                loading={milestonesLoading}
                busy={isFetching && !milestonesLoading}
                error={milestonesError}
                onRetry={() => void refresh()}
                hasActiveFilter={statusFilter !== 'All'}
                onClearFilter={() => setStatusFilter('All')}
                canCreate={canCreateMilestones}
                onOpenCreate={openCreateDialog}
              />
            </Card>

            <div className="min-w-0 lg:col-span-4 lg:sticky lg:top-6">
              <AdvisorGroupPlanCard
                selectedGroup={selectedGroup}
                selectedMilestone={selectedMilestone}
                stats={stats}
                onOpenReview={openReviewDialog}
                onDownloadFile={(fileId, fileName) => void handleDownloadFile(fileId, fileName)}
                downloadingFileId={downloadingFileId}
              />
            </div>
          </div>
        </>
      )}

      <AdvisorMilestoneCreateDialog
        open={createDialogOpen}
        onClose={closeCreateDialog}
        selectedGroup={selectedGroup}
        existingMilestones={milestones}
        onSubmit={createMilestone}
        isSubmitting={isCreating}
        serverError={createError}
      />

      <AdvisorMilestoneReviewDialog
        item={reviewingMilestone}
        selectedGroup={selectedGroup}
        onClose={closeReviewDialog}
        onSubmitReview={reviewReport}
        onDownloadFile={handleDownloadFile}
        downloadingFileId={downloadingFileId}
        isSubmitting={isReviewing}
        serverError={reviewError}
      />
    </div>
  );
}
