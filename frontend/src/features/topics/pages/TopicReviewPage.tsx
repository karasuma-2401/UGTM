import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCheck,
  Clock3,
  FileText,
  FunnelX,
  Layers,
  Megaphone,
  RefreshCw,
  Search,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Dialog } from '@/components/ui/dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Input } from '@/components/ui/input';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { cn } from '@/lib/utils';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { OperationToast } from '@/shared/notifications';
import { TopicProposalSchedule } from '../components/TopicProposalSchedule';
import { TopicReviewDialog, type ReviewAction } from '../components/TopicReviewDialog';
import { TopicReviewTable } from '../components/TopicReviewTable';
import { getTopicErrorMessage } from '../services/topicApi';
import { topicReviewApi } from '../services/topicReviewApi';
import {
  REVIEW_TOPIC_STATUSES,
  TOPIC_STATUS_LABELS,
  type Topic,
  type TopicFilters,
  type TopicStatus,
} from '../types/topic.types';

const EMPTY_FILTERS: TopicFilters = { keyword: '', batchId: '', status: '' };
const INITIAL_FILTERS: TopicFilters = { ...EMPTY_FILTERS, status: 'Submitted' };

type PageReviewAction =
  | ReviewAction
  | { kind: 'publishMany'; groups: { batchId: string; topicIds: string[] }[] };

const SECONDARY_STATUS_PILLS: readonly { value: TopicStatus | ''; label: string }[] = [
  { value: '', label: 'Tất cả trạng thái' },
  ...REVIEW_TOPIC_STATUSES.map((status) => ({
    value: status,
    label: status === 'Approved' ? 'Chờ công bố' : TOPIC_STATUS_LABELS[status],
  })),
];

export function TopicReviewPage() {
  const { user, hasRole, hasPermission } = useAuth();
  const canReview = hasRole(['Faculty', 'Admin']);
  const queryClient = useQueryClient();

  const [draftFilters, setDraftFilters] = useState<TopicFilters>(INITIAL_FILTERS);
  const [filters, setFilters] = useState<TopicFilters>(INITIAL_FILTERS);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selected, setSelected] = useState<Topic[]>([]);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [topicId, setTopicId] = useState<string | null>(null);
  const [publishOpen, setPublishOpen] = useState(false);
  const [notice, setNotice] = useState('');

  const batchesQuery = useAcademicBatches();
  const batches = batchesQuery.data ?? [];

  const topicsQuery = useQuery({
    queryKey: ['topic-review', user?.id, 'list', filters, pageNumber, pageSize],
    queryFn: ({ signal }) => topicReviewApi.getTopics({ ...filters, pageNumber, pageSize }, signal),
    enabled: canReview && Boolean(user?.id),
    placeholderData: (previousData, previousQuery) =>
      previousQuery?.queryKey[1] === user?.id ? previousData : undefined,
  });

  const statisticsQuery = useQuery({
    queryKey: ['topic-review', user?.id, 'statistics', filters.batchId],
    queryFn: ({ signal }) => topicReviewApi.getStatistics(filters.batchId, signal),
    enabled: canReview && Boolean(user?.id),
  });

  const advisorsQuery = useQuery({
    queryKey: ['topic-review-advisors', user?.id],
    queryFn: ({ signal }) => topicReviewApi.getAdvisorNames(signal),
    enabled: canReview && Boolean(user?.id) && hasPermission('identity.users.read'),
    staleTime: 60_000,
  });

  async function refreshTopics() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['topic-review'] }),
      queryClient.invalidateQueries({ queryKey: ['my-proposed-topics'] }),
    ]);
  }

  const actionMutation = useMutation({
    mutationFn: async (action: PageReviewAction) => {
      if (action.kind === 'review') {
        return topicReviewApi.review(action.id, action.decision, action.reviewNote);
      }
      if (action.kind === 'publish') {
        return topicReviewApi.publish(action.batchId, action.topicIds);
      }
      let publishedCount = 0;
      for (const group of action.groups) {
        const res = await topicReviewApi.publish(group.batchId, group.topicIds);
        publishedCount += res.publishedCount;
      }
      return { publishedCount };
    },
    onSuccess: async (result, action) => {
      setNotice(
        action.kind === 'review'
          ? `Đã cập nhật kết luận: ${TOPIC_STATUS_LABELS[action.decision]}.${action.decision === 'Approved' ? ' Đề tài chưa được công bố.' : ''}`
          : `Đã công bố ${'publishedCount' in result ? result.publishedCount : 0} đề tài cho sinh viên tra cứu.`,
      );
      setSelected([]);
      setTopicId(null);
      setPublishOpen(false);
      setPageNumber(1);
      await refreshTopics();
    },
    onError: async () => {
      setSelected([]);
      await refreshTopics();
    },
  });

  const busy = actionMutation.isPending;
  const advisorsLoading =
    canReview &&
    Boolean(user?.id) &&
    hasPermission('identity.users.read') &&
    (advisorsQuery.isPending || advisorsQuery.isFetching);
  const batchesLoading =
    Boolean(user?.id) &&
    hasPermission('academic.batches.read') &&
    (batchesQuery.isPending || batchesQuery.isFetching);
  const data = topicsQuery.data;
  const isRefreshing = topicsQuery.isFetching && !topicsQuery.isPending;
  const statistics = statisticsQuery.isError ? undefined : statisticsQuery.data;
  const hasFilters = Boolean(
    Object.values(filters).some(Boolean) || Object.values(draftFilters).some(Boolean),
  );
  const formatCount = (count: number | undefined) =>
    count === undefined ? '—' : count.toLocaleString('vi-VN');

  const activeBatchName = filters.batchId
    ? batches.find((batch) => batch.id === filters.batchId)?.batchName || 'Đợt đã chọn'
    : 'Tất cả đợt KLTN';
  const activeStatusLabel = filters.status
    ? filters.status === 'Approved'
      ? 'Chờ công bố'
      : TOPIC_STATUS_LABELS[filters.status]
    : 'Tất cả đề xuất';
  const scopeDescription = `${activeStatusLabel} · ${activeBatchName}`;

  const batchNameById = new Map(batches.map((batch) => [batch.id, batch.batchName]));
  const selectedByBatch = Array.from(
    selected
      .reduce<Map<string, Topic[]>>((acc, topic) => {
        const list = acc.get(topic.batchId);
        if (list) list.push(topic);
        else acc.set(topic.batchId, [topic]);
        return acc;
      }, new Map())
      .entries(),
  ).map(([batchId, items]) => ({
    batchId,
    batchName: batchNameById.get(batchId),
    items,
  }));
  const selectedBatchName =
    selectedByBatch.length === 1 ? selectedByBatch[0]?.batchName : undefined;
  const selectedScopeLabel =
    selectedByBatch.length > 1 ? `${selectedByBatch.length} đợt KLTN` : selectedBatchName;

  function applyFilters(next: TopicFilters) {
    setDraftFilters(next);
    setFilters(next);
    setPageNumber(1);
    setSelected([]);
    setSelectedRowId(null);
  }

  function quickFilter(status: TopicStatus | '') {
    applyFilters({ keyword: filters.keyword, batchId: filters.batchId, status });
  }

  function handleApply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    applyFilters({ ...draftFilters, keyword: draftFilters.keyword.trim() });
  }

  function handleBatchChange(batchId: string) {
    applyFilters({
      ...draftFilters,
      keyword: draftFilters.keyword.trim(),
      batchId,
    });
  }

  function executeAction(action: PageReviewAction) {
    if (!canReview || busy) return;
    setNotice('');
    actionMutation.mutate(action);
  }

  return (
    <div className="min-w-0 space-y-5 pb-24">
      <PageHeader
        accent
        title="Thẩm định & công bố đề tài"
        description="Rà soát đề cương do giảng viên đề xuất, gửi góp ý chỉnh sửa hoặc công bố chính thức theo đợt."
      >
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={topicsQuery.isFetching || busy}
          leftIcon={<RefreshCw className={topicsQuery.isFetching ? 'animate-spin' : undefined} />}
          onClick={() => void refreshTopics()}
          className="h-9 rounded-xl border-slate-200 bg-white shadow-2xs hover:border-slate-300"
        >
          Làm mới
        </Button>
      </PageHeader>

      <OperationToast message={notice} onConsumed={() => setNotice('')} />

      {actionMutation.isError && !topicId && !publishOpen && (
        <InlineAlert tone="error">{getTopicErrorMessage(actionMutation.error)}</InlineAlert>
      )}

      {batchesQuery.isError && (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700"
        >
          <p>Không thể tải danh sách đợt KLTN.</p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void batchesQuery.refetch()}
          >
            Thử lại
          </Button>
        </div>
      )}

      {advisorsQuery.isError && (
        <p role="status" className="text-xs text-slate-500">
          Chưa tải được tên giảng viên. Danh sách tạm hiển thị thông tin rút gọn.
        </p>
      )}

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
        <ListCardHeader
          title="Danh sách đề tài"
          icon={<CheckCheck />}
          description={scopeDescription}
          className="min-h-0 lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]"
        >
          <section
            className="flex w-full items-center lg:justify-end"
            aria-label="Thống kê thẩm định đề tài"
            aria-busy={statisticsQuery.isFetching}
          >
            <div className="w-full lg:w-auto">
              <StatisticsBar
                loading={statisticsQuery.isPending}
                items={[
                  {
                    key: 'total',
                    onClick: () => quickFilter(''),
                    active: filters.status === '' && !filters.keyword,
                    label: 'Tổng đề xuất',
                    value: formatCount(statistics?.total),
                    icon: <Layers />,
                    tone: 'muted',
                    actionLabel: 'Hiển thị tất cả đề xuất',
                  },
                  {
                    key: 'submitted',
                    onClick: () => quickFilter('Submitted'),
                    active: filters.status === 'Submitted' && !filters.keyword,
                    label: 'Chờ duyệt',
                    value: formatCount(statistics?.Submitted),
                    icon: <Clock3 />,
                    tone: 'warning',
                    actionLabel: 'Lọc đề tài đang chờ duyệt',
                  },
                  {
                    key: 'approved',
                    onClick: () => quickFilter('Approved'),
                    active: filters.status === 'Approved' && !filters.keyword,
                    label: 'Chờ công bố',
                    value: formatCount(statistics?.Approved),
                    icon: <CheckCheck />,
                    tone: 'primary',
                    actionLabel: 'Lọc đề tài đã duyệt chờ công bố',
                  },
                  {
                    key: 'published',
                    onClick: () => quickFilter('Published'),
                    active: filters.status === 'Published' && !filters.keyword,
                    label: 'Đã công bố',
                    value: formatCount(statistics?.Published),
                    icon: <Megaphone />,
                    tone: 'success',
                    actionLabel: 'Lọc đề tài đã công bố',
                  },
                ]}
              />
              {statisticsQuery.isPending && (
                <p role="status" className="sr-only">
                  Đang tải thống kê...
                </p>
              )}
              {statisticsQuery.isError && (
                <div role="alert" className="mt-2 flex items-center gap-2 text-xs text-rose-700">
                  <span>Không thể tải thống kê.</span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={statisticsQuery.isFetching}
                    onClick={() => void statisticsQuery.refetch()}
                  >
                    Thử lại
                  </Button>
                </div>
              )}
            </div>
          </section>
        </ListCardHeader>

        <div className="space-y-3 border-b border-slate-100 bg-slate-50/45 px-5 py-3.5">
          <form
            onSubmit={handleApply}
            className="flex flex-col gap-2.5 lg:flex-row lg:items-center"
          >
            <div className="relative min-w-0 flex-1">
              <Input
                aria-label="Tìm đề tài theo tên tiếng Việt hoặc tiếng Anh"
                leftIcon={<Search className="size-4" />}
                placeholder="Tìm theo tên đề tài tiếng Việt hoặc tiếng Anh..."
                className="h-9 rounded-lg border-slate-200 bg-white pr-8 shadow-2xs"
                value={draftFilters.keyword}
                onChange={(event) =>
                  setDraftFilters({ ...draftFilters, keyword: event.target.value })
                }
              />
              {draftFilters.keyword && (
                <button
                  type="button"
                  aria-label="Xóa từ khóa tìm kiếm"
                  onClick={() => applyFilters({ ...filters, keyword: '' })}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded p-0.5 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-600"
                >
                  <X className="size-3.5" />
                </button>
              )}
            </div>

            <div className="w-full sm:w-64">
              <Select<string>
                aria-label="Lọc theo đợt KLTN"
                triggerClassName="h-9 rounded-lg border-slate-200 bg-white shadow-2xs"
                value={draftFilters.batchId}
                disabled={!batches.length}
                options={[
                  { value: '', label: 'Tất cả đợt KLTN' },
                  ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
                ]}
                onChange={handleBatchChange}
              />
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="submit"
                variant="soft"
                size="sm"
                leftIcon={<Search className="size-3.5" />}
                className="h-9 rounded-lg px-3.5"
              >
                Tìm
              </Button>
              {hasFilters && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  leftIcon={<FunnelX className="size-3.5" />}
                  onClick={() => applyFilters({ ...EMPTY_FILTERS })}
                  className="h-9 rounded-lg border-slate-200 bg-white px-3.5"
                >
                  Bỏ lọc
                </Button>
              )}
            </div>
          </form>

          <div
            role="group"
            aria-label="Lọc nhanh theo trạng thái đề tài"
            className="flex flex-wrap items-center gap-1.5"
          >
            {SECONDARY_STATUS_PILLS.map((pill) => {
              const active = filters.status === pill.value;
              return (
                <button
                  key={pill.value || 'all'}
                  type="button"
                  aria-pressed={active}
                  disabled={busy}
                  onClick={() => quickFilter(pill.value)}
                  className={cn(
                    'inline-flex h-7 items-center rounded-full border px-3 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 disabled:opacity-50',
                    active
                      ? 'border-blue-200 bg-blue-50 text-primary shadow-2xs'
                      : 'border-slate-200/90 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900',
                  )}
                >
                  {pill.label}
                </button>
              );
            })}
          </div>
        </div>

        {filters.batchId && (
          <div className="border-b border-slate-100 bg-white px-5 py-3">
            <TopicProposalSchedule batchId={filters.batchId} />
          </div>
        )}

        <div
          className={cn(
            'min-h-[16rem] transition-opacity duration-200',
            isRefreshing && 'opacity-60',
          )}
          aria-busy={topicsQuery.isFetching}
        >
          {topicsQuery.isPending ? (
            <TableSkeleton
              columns={[
                { label: '', width: 'w-11', variant: 'checkbox' },
                { label: 'STT', width: 'w-12', variant: 'number' },
                { label: 'Đề tài', variant: 'topic' },
                { label: 'Giảng viên hướng dẫn', width: 'w-56', variant: 'person' },
                { label: 'Đợt KLTN', width: 'w-44' },
                { label: 'Chỉ tiêu', width: 'w-24' },
                { label: 'Trạng thái', width: 'w-40', variant: 'badge' },
                { label: 'Thao tác', width: 'w-32', variant: 'actions' },
              ]}
            />
          ) : topicsQuery.isError ? (
            <div role="alert" className="space-y-3 p-10 text-center text-sm text-rose-700">
              <p>{getTopicErrorMessage(topicsQuery.error)}</p>
              <Button type="button" variant="outline" onClick={() => void topicsQuery.refetch()}>
                Thử lại
              </Button>
            </div>
          ) : data?.items.length ? (
            <TopicReviewTable
              topics={data.items}
              batches={batches}
              advisorNames={advisorsQuery.data ?? {}}
              advisorsLoading={advisorsLoading}
              batchesLoading={batchesLoading}
              selected={selected}
              selectedRowId={selectedRowId}
              onSelectRow={(id) => setSelectedRowId((prev) => (prev === id ? null : id))}
              onActiveRowChange={setSelectedRowId}
              pageNumber={data.pageNumber}
              pageSize={data.pageSize}
              busy={busy || topicsQuery.isFetching}
              onSelect={setSelected}
              onOpen={(topic) => {
                actionMutation.reset();
                setSelectedRowId(topic.id);
                setTopicId(topic.id);
              }}
            />
          ) : (
            <div className="flex flex-col items-center justify-center gap-3 px-6 py-14 text-center">
              <span className="flex size-12 items-center justify-center rounded-2xl border border-slate-200/80 bg-slate-50 text-slate-400">
                <FileText className="size-6" aria-hidden="true" />
              </span>
              <div className="space-y-1">
                <p className="text-sm font-medium text-slate-700">Không có đề tài phù hợp.</p>
                <p className="text-xs text-slate-500">
                  Thử chuyển sang trạng thái khác hoặc xóa bộ lọc đang áp dụng.
                </p>
              </div>
              {hasFilters && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => applyFilters({ ...EMPTY_FILTERS })}
                  className="mt-1 rounded-xl border-slate-200 bg-white"
                >
                  Bỏ lọc
                </Button>
              )}
            </div>
          )}
        </div>

        {data && !topicsQuery.isError && (
          <TablePagination
            pageNumber={data.pageNumber}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            hasPreviousPage={data.hasPreviousPage}
            hasNextPage={data.hasNextPage}
            isLoading={topicsQuery.isFetching || busy}
            itemLabel="đề tài"
            onPageChange={(page) => {
              setSelected([]);
              setSelectedRowId(null);
              setPageNumber(page);
            }}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPageNumber(1);
              setSelected([]);
              setSelectedRowId(null);
            }}
          />
        )}
      </Card>

      {topicId && (
        <TopicReviewDialog
          key={topicId}
          topicId={topicId}
          batches={batches}
          advisorNames={advisorsQuery.data ?? {}}
          advisorsLoading={advisorsLoading}
          batchesLoading={batchesLoading}
          busy={busy}
          error={actionMutation.error ? getTopicErrorMessage(actionMutation.error) : ''}
          onClose={() => {
            if (!busy) {
              setTopicId(null);
              actionMutation.reset();
            }
          }}
          onAction={executeAction}
        />
      )}

      {selected.length > 0 && (
        <TableFloatingBar
          selectedCount={selected.length}
          totalCount={data?.totalCount}
          onClearSelection={() => {
            setSelected([]);
            setSelectedRowId(null);
          }}
        >
          {selectedScopeLabel && (
            <span className="hidden rounded-lg bg-slate-100 px-2.5 py-1 text-[11px] font-medium text-slate-600 sm:inline-block">
              {selectedScopeLabel}
            </span>
          )}
          <Button
            type="button"
            size="sm"
            variant="default"
            leftIcon={<Megaphone className="size-3.5" />}
            className="h-8 rounded-xl px-3.5 text-xs font-semibold shadow-xs"
            disabled={busy || !selected.length || topicsQuery.isFetching || topicsQuery.isError}
            onClick={() => {
              actionMutation.reset();
              setPublishOpen(true);
            }}
          >
            Công bố ({selected.length})
          </Button>
        </TableFloatingBar>
      )}

      {publishOpen && (
        <Dialog
          title="Xác nhận công bố đề tài"
          busy={busy}
          onClose={() => {
            if (!busy) {
              setPublishOpen(false);
              actionMutation.reset();
            }
          }}
        >
          <div className="space-y-4 p-6">
            {selected.length > 0 ? (
              <>
                <p className="text-sm text-slate-600">
                  Công bố <strong className="text-slate-900">{selected.length}</strong> đề tài đã
                  chọn{' '}
                  {selectedByBatch.length === 1 ? (
                    <>
                      thuộc đợt{' '}
                      <strong className="text-slate-900">
                        {selectedBatchName ||
                          (batchesLoading ? (
                            <Skeleton className="inline-block h-4 w-28 align-middle" />
                          ) : (
                            selected[0]?.batchId || '—'
                          ))}
                      </strong>{' '}
                    </>
                  ) : (
                    <>
                      thuộc <strong className="text-slate-900">{selectedByBatch.length}</strong> đợt{' '}
                    </>
                  )}
                  để sinh viên tra cứu.
                </p>
                <div className="max-h-60 space-y-3 overflow-auto rounded-xl border border-slate-200/70 bg-slate-50 p-4 text-sm text-slate-700">
                  {selectedByBatch.map((group) => (
                    <div key={group.batchId} className="space-y-1.5">
                      {selectedByBatch.length > 1 && (
                        <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                          {group.batchName || group.batchId} ({group.items.length})
                        </p>
                      )}
                      <ul className="space-y-1.5">
                        {group.items.map((topic) => (
                          <li key={topic.id} className="break-words leading-snug">
                            • {topic.titleVi}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
                <p className="text-xs text-slate-500">
                  Chỉ công bố các đề tài trong danh sách này. Lựa chọn được xóa khi đổi bộ lọc hoặc
                  trang.
                </p>
              </>
            ) : (
              <p className="text-sm text-slate-600">
                Danh sách lựa chọn đã được làm mới. Đóng cửa sổ và chọn lại đề tài trước khi công
                bố.
              </p>
            )}
            {actionMutation.error && (
              <p role="alert" className="text-sm text-rose-700">
                {getTopicErrorMessage(actionMutation.error)}
              </p>
            )}
            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => {
                  setPublishOpen(false);
                  actionMutation.reset();
                }}
              >
                Đóng
              </Button>
              <Button
                leftIcon={<Megaphone />}
                isLoading={busy}
                disabled={!selected.length}
                onClick={() => {
                  if (selectedByBatch.length === 1 && selectedByBatch[0]) {
                    executeAction({
                      kind: 'publish',
                      batchId: selectedByBatch[0].batchId,
                      topicIds: selectedByBatch[0].items.map((topic) => topic.id),
                    });
                    return;
                  }
                  if (selectedByBatch.length > 1) {
                    executeAction({
                      kind: 'publishMany',
                      groups: selectedByBatch.map((group) => ({
                        batchId: group.batchId,
                        topicIds: group.items.map((topic) => topic.id),
                      })),
                    });
                  }
                }}
              >
                Xác nhận công bố
              </Button>
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
}
