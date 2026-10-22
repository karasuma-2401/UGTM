import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCheck,
  Clock3,
  FileText,
  Megaphone,
  Search,
  SlidersHorizontal,
  X,
  FunnelX,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { Skeleton } from '@/components/ui/skeleton';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { TablePagination } from '@/components/ui/table-pagination';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { TopicProposalSchedule } from '../components/TopicProposalSchedule';
import { TopicReviewDialog, type ReviewAction } from '../components/TopicReviewDialog';
import { TopicReviewTable } from '../components/TopicReviewTable';
import { getTopicErrorMessage } from '../services/topicApi';
import { topicReviewApi } from '../services/topicReviewApi';
import {
  TOPIC_STATUS_LABELS,
  REVIEW_TOPIC_STATUSES,
  type Topic,
  type TopicFilters,
  type TopicStatus,
} from '../types/topic.types';

const EMPTY_FILTERS: TopicFilters = { keyword: '', batchId: '', status: '' };
const INITIAL_FILTERS: TopicFilters = { ...EMPTY_FILTERS, status: 'Submitted' };

export function TopicReviewPage() {
  const { user, hasRole, hasPermission } = useAuth();
  const canReview = hasRole(['Faculty', 'Admin']);
  const queryClient = useQueryClient();
  const [draftFilters, setDraftFilters] = useState<TopicFilters>(INITIAL_FILTERS);
  const [filters, setFilters] = useState<TopicFilters>(INITIAL_FILTERS);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selected, setSelected] = useState<Topic[]>([]);
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
    mutationFn: async (action: ReviewAction) =>
      action.kind === 'review'
        ? topicReviewApi.review(action.id, action.decision)
        : topicReviewApi.publish(action.batchId, action.topicIds),
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
  const hasAppliedFilters = Object.values(filters).some(Boolean);
  const formatCount = (count: number | undefined) =>
    count === undefined ? '—' : count.toLocaleString('vi-VN');

  function applyFilters(next: TopicFilters) {
    setDraftFilters(next);
    setFilters(next);
    setPageNumber(1);
    setSelected([]);
  }
  function handleApply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    applyFilters({ ...draftFilters, keyword: draftFilters.keyword.trim() });
  }
  function quickFilter(status: TopicStatus | '') {
    applyFilters({ keyword: '', batchId: filters.batchId, status });
  }
  function executeAction(action: ReviewAction) {
    if (!canReview || busy) return;
    setNotice('');
    actionMutation.mutate(action);
  }

  return (
    <div className="min-w-0 space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">
            Thẩm định & công bố đề tài
          </h1>
          {/* <p className="mt-2 text-sm text-slate-500">
            Thẩm định đề xuất của giảng viên và công bố các đề tài đã duyệt cho sinh viên tra cứu.
          </p> */}
        </div>
        <Button
          type="button"
          leftIcon={<Megaphone />}
          className="h-10 rounded-lg px-4 shadow-sm"
          disabled={busy || !selected.length || topicsQuery.isFetching || topicsQuery.isError}
          onClick={() => {
            actionMutation.reset();
            setPublishOpen(true);
          }}
        >
          Công bố đề tài đã chọn{selected.length > 0 ? ` (${selected.length})` : ''}
        </Button>
      </div>

      {notice && (
        <p role="status" className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700">
          {notice}
        </p>
      )}
      {actionMutation.isError && !topicId && !publishOpen && (
        <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
          {getTopicErrorMessage(actionMutation.error)}
        </p>
      )}

      <form
        onSubmit={handleApply}
        className="grid items-end gap-4 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm md:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr_auto]"
      >
        <label className="block space-y-2 text-sm text-slate-700">
          <span>Tìm đề tài</span>
          <Input
            leftIcon={<Search className="size-4" />}
            placeholder="Tên đề tài tiếng Việt hoặc tiếng Anh"
            className="h-10 rounded-lg bg-slate-50/70 shadow-none"
            value={draftFilters.keyword}
            onChange={(event) => setDraftFilters({ ...draftFilters, keyword: event.target.value })}
          />
        </label>
        <div className="space-y-2 text-sm text-slate-700">
          <span>Đợt KLTN</span>
          <Select<string>
            aria-label="Lọc theo đợt KLTN"
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            value={draftFilters.batchId}
            disabled={!batches.length}
            options={[
              { value: '', label: 'Tất cả đợt' },
              ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
            ]}
            onChange={(batchId) => setDraftFilters({ ...draftFilters, batchId })}
          />
        </div>
        <div className="space-y-2 text-sm text-slate-700">
          <span>Trạng thái</span>
          <Select<TopicStatus | ''>
            aria-label="Lọc theo trạng thái đề tài"
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            value={draftFilters.status}
            options={[
              { value: '', label: 'Tất cả trạng thái' },
              ...REVIEW_TOPIC_STATUSES.map((value) => ({
                value,
                label: TOPIC_STATUS_LABELS[value],
              })),
            ]}
            onChange={(status) => setDraftFilters({ ...draftFilters, status })}
          />
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="submit"
            variant="soft"
            className="h-10 rounded-lg px-5"
            leftIcon={<SlidersHorizontal />}
          >
            Áp dụng
          </Button>
          <Button
            type="button"
            variant="outline"
            className="h-10 rounded-lg px-4"
            leftIcon={<FunnelX />}
            disabled={!hasFilters}
            onClick={() => applyFilters({ ...EMPTY_FILTERS })}
          >
            Bỏ lọc
          </Button>
        </div>
      </form>
      {hasAppliedFilters && (
        <div aria-label="Bộ lọc đang áp dụng" className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-slate-500">Đang lọc:</span>
          {(Object.keys(filters) as (keyof TopicFilters)[])
            .filter((key) => filters[key])
            .map((key) => (
              <Button
                key={key}
                type="button"
                variant="secondary"
                size="sm"
                rightIcon={<X />}
                aria-label={`Bỏ lọc ${key === 'keyword' ? 'từ khóa' : key === 'batchId' ? 'đợt KLTN' : 'trạng thái'}`}
                onClick={() => applyFilters({ ...filters, [key]: '' })}
              >
                {key === 'keyword'
                  ? `Từ khóa: ${filters.keyword}`
                  : key === 'status' && filters.status
                    ? TOPIC_STATUS_LABELS[filters.status]
                    : batches.find((batch) => batch.id === filters.batchId)?.batchName ||
                      'Đợt đã chọn'}
              </Button>
            ))}
        </div>
      )}
      {batchesQuery.isError && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-lg bg-rose-50 p-3 text-sm text-rose-700"
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
          Chưa tải được tên giảng viên. Danh sách tạm hiển thị mã GVHD.
        </p>
      )}
      {filters.batchId && <TopicProposalSchedule batchId={filters.batchId} />}

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <CardHeader className="grid min-h-[104px] grid-cols-1 items-center gap-4 space-y-0 border-b border-slate-100 bg-gradient-to-r from-slate-50/80 to-white px-5 py-4 xl:grid-cols-[minmax(0,1fr)_560px]">
          <div className="space-y-1">
            <CardTitle className="text-base text-slate-900">Danh sách đề tài</CardTitle>
            <p className="text-xs text-slate-500">
              Chỉ thẩm định đề tài chờ duyệt; chỉ công bố đề tài đã duyệt.
            </p>
          </div>
          <section
            className="w-full"
            aria-label="Thống kê thẩm định đề tài"
            aria-busy={statisticsQuery.isFetching}
          >
            <StatisticsBar
              loading={statisticsQuery.isPending}
              items={[
                {
                  key: 'total',
                  label: 'Tổng đề xuất',
                  value: formatCount(statistics?.total),
                  icon: <FileText />,
                  tone: 'primary',
                  active: !filters.status && !filters.keyword,
                  onClick: () => quickFilter(''),
                },
                {
                  key: 'submitted',
                  label: 'Chờ duyệt',
                  value: formatCount(statistics?.Submitted),
                  icon: <Clock3 />,
                  tone: 'warning',
                  active: filters.status === 'Submitted' && !filters.keyword,
                  onClick: () => quickFilter('Submitted'),
                },
                {
                  key: 'approved',
                  label: 'Chờ công bố',
                  value: formatCount(statistics?.Approved),
                  icon: <CheckCheck />,
                  tone: 'muted',
                  active: filters.status === 'Approved' && !filters.keyword,
                  onClick: () => quickFilter('Approved'),
                },
                {
                  key: 'published',
                  label: 'Đã công bố',
                  value: formatCount(statistics?.Published),
                  icon: <Megaphone />,
                  tone: 'primary',
                  active: filters.status === 'Published' && !filters.keyword,
                  onClick: () => quickFilter('Published'),
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
          </section>
        </CardHeader>
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-5 py-3 text-xs text-slate-500">
          <span>
            {selected.length
              ? `Đã chọn ${selected.length} đề tài để công bố trong cùng một đợt.`
              : 'Chọn đề tài đã duyệt trên trang này để công bố cùng đợt.'}
          </span>
          {selected.length > 0 && (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              disabled={busy}
              onClick={() => setSelected([])}
            >
              Bỏ chọn
            </Button>
          )}
        </div>
        <div
          className={`h-[min(60vh,40rem)] min-h-[20rem] overflow-auto transition-opacity ${isRefreshing ? 'opacity-60' : ''}`}
          aria-busy={topicsQuery.isFetching}
        >
          {topicsQuery.isPending ? (
            <TableSkeleton
              className="min-w-[1180px]"
              columns={[
                { label: 'STT', width: 'w-14' },
                { label: '', width: 'w-12' },
                { label: 'Đề tài' },
                { label: 'Giảng viên hướng dẫn', width: 'w-48' },
                { label: 'Đợt KLTN', width: 'w-40' },
                { label: 'Chỉ tiêu', width: 'w-20' },
                { label: 'Trạng thái', width: 'w-44' },
                { label: 'Thao tác', width: 'w-36' },
              ]}
            />
          ) : topicsQuery.isError ? (
            <div role="alert" className="space-y-3 p-8 text-center text-sm text-rose-700">
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
              pageNumber={data.pageNumber}
              pageSize={data.pageSize}
              busy={busy || topicsQuery.isFetching}
              onSelect={setSelected}
              onOpen={(topic) => {
                actionMutation.reset();
                setTopicId(topic.id);
              }}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-sm text-slate-500">
              <p>Không có đề tài phù hợp.</p>
              {hasFilters && (
                <Button
                  variant="outline"
                  leftIcon={<FunnelX />}
                  onClick={() => applyFilters({ ...EMPTY_FILTERS })}
                >
                  Bỏ lọc
                </Button>
              )}
            </div>
          )}
        </div>
        {data && !topicsQuery.isError && (
          <>
            <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-3 text-sm">
              <label htmlFor="review-page-size">Số đề tài mỗi trang</label>
              <select
                id="review-page-size"
                value={pageSize}
                disabled={busy || topicsQuery.isFetching}
                className="rounded-md border border-slate-200 bg-white p-2"
                onChange={(event) => {
                  setPageSize(Number(event.target.value));
                  setPageNumber(1);
                  setSelected([]);
                }}
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
            <TablePagination
              pageNumber={data.pageNumber}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              hasNextPage={data.hasNextPage}
              hasPreviousPage={data.hasPreviousPage}
              isLoading={topicsQuery.isFetching || busy}
              itemLabel="đề tài"
              onPageChange={(page) => {
                setPageNumber(page);
                setSelected([]);
              }}
            />
          </>
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
                  Công bố {selected.length} đề tài đã chọn thuộc đợt{' '}
                  <strong>
                    {batches.find((batch) => batch.id === selected[0]?.batchId)?.batchName ||
                      (batchesLoading ? (
                        <Skeleton className="inline-block h-4 w-28 align-middle" />
                      ) : (
                        selected[0]?.batchId || '—'
                      ))}
                  </strong>{' '}
                  để sinh viên tra cứu.
                </p>
                <ul className="max-h-60 space-y-2 overflow-auto rounded-xl bg-slate-50 p-4 text-sm text-slate-700">
                  {selected.map((topic) => (
                    <li key={topic.id} className="break-words">
                      • {topic.titleVi}
                    </li>
                  ))}
                </ul>
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
                  const batchId = selected[0]?.batchId;
                  if (batchId)
                    executeAction({
                      kind: 'publish',
                      batchId,
                      topicIds: selected.map((topic) => topic.id),
                    });
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
