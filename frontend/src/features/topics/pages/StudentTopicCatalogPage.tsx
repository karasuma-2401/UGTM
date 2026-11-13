import { OperationToast } from '@/shared/notifications';
import { useRef, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BookOpen,
  Eye,
  FunnelX,
  MailCheck,
  Search,
  Send,
  ShieldAlert,
  SlidersHorizontal,
} from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { Card } from '@/components/ui/card';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { InfoCell } from '@/components/ui/info-cell';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { Skeleton } from '@/components/ui/skeleton';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { TableActiveFilters } from '@/components/ui/table';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { studentTopicApi } from '../services/studentTopicApi';
import { getTopicErrorMessage } from '../services/topicApi';
import { validateStudentRegistration } from '../studentRegistration';
import { StudentRegistrationStatusCard } from '../components/StudentRegistrationStatusCard';
import { StudentTopicRegistrationDialog } from '../components/StudentTopicRegistrationDialog';
import { StudentParticipationDialog } from '../components/StudentParticipationDialog';
import { StudentTopicGroupDialog } from '../components/StudentTopicGroupDialog';
import { TopicCapacityStatus } from '../components/TopicCapacityStatus';
import type {
  CatalogAvailabilityFilter,
  CatalogSortField,
  PublishedTopic,
  RegistrationReceipt,
} from '../types/studentTopic.types';

const PAGE_SIZES = [10, 20, 50] as const;

interface CatalogFilters {
  keyword: string;
  batchId: string;
  availability: CatalogAvailabilityFilter;
}

const EMPTY_FILTERS: CatalogFilters = {
  keyword: '',
  batchId: '',
  availability: '',
};

const AVAILABILITY_LABELS: Record<Exclude<CatalogAvailabilityFilter, ''>, string> = {
  available: 'Chỉ đề tài còn chỗ',
  full: 'Đã đủ / tạm hết chỗ',
};

export function StudentTopicCatalogPage() {
  const { user } = useAuth();
  // User-owned receipts, invitation input and selections never survive an account switch.
  return <StudentTopicCatalogContent key={user?.id ?? 'anonymous'} />;
}

export function StudentTopicCatalogContent() {
  const { user, hasRole, hasPermission } = useAuth();
  const student = hasRole('Student');
  const canBrowse = student || hasRole('Admin');
  const canReadBatches = hasPermission('academic.batches.read');
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState<CatalogFilters>(EMPTY_FILTERS);
  const [filters, setFilters] = useState<CatalogFilters>(EMPTY_FILTERS);
  const [sortBy, setSortBy] = useState<CatalogSortField>('createdAt');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(PAGE_SIZES[0]);
  const [selection, setSelection] = useState<PublishedTopic | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [invitationOpen, setInvitationOpen] = useState(false);
  const [groupOpen, setGroupOpen] = useState(false);
  const [warningsOpen, setWarningsOpen] = useState(false);
  const catalogRef = useRef<HTMLDivElement>(null);
  const [receipt, setReceipt] = useState<RegistrationReceipt | null>(null);
  const [notice, setNotice] = useState('');
  const batchesQuery = useAcademicBatches();
  const batches = batchesQuery.data ?? [];

  const isDefaultSort = sortBy === 'createdAt' && sortOrder === 'desc';

  const topicsQuery = useQuery({
    queryKey: ['student-topics', user?.id, filters, page, pageSize, sortBy, sortOrder],
    queryFn: ({ signal }) =>
      studentTopicApi.getTopics(
        {
          keyword: filters.keyword,
          batchId: filters.batchId,
          ...(filters.availability ? { availability: filters.availability } : {}),
          ...(!isDefaultSort ? { sortBy, sortOrder } : {}),
          pageNumber: page,
          pageSize,
        },
        signal,
      ),
    enabled: Boolean(user?.id) && canBrowse,
    refetchInterval: 60_000,
    placeholderData: (previousData, previousQuery) =>
      previousQuery &&
      previousQuery.queryKey[1] === user?.id &&
      JSON.stringify(previousQuery.queryKey[2]) === JSON.stringify(filters) &&
      previousQuery.queryKey[5] === sortBy &&
      previousQuery.queryKey[6] === sortOrder
        ? previousData
        : undefined,
  });

  const totalQuery = useQuery({
    queryKey: ['student-topics-total', user?.id, filters.batchId],
    queryFn: ({ signal }) =>
      studentTopicApi.getTopics(
        { keyword: '', batchId: filters.batchId, pageNumber: 1, pageSize: 1 },
        signal,
      ),
    enabled: Boolean(user?.id) && canBrowse,
    refetchInterval: 60_000,
  });

  const statusQuery = useQuery({
    queryKey: ['student-registration', user?.id],
    queryFn: ({ signal }) => studentTopicApi.getMyStatus(signal),
    enabled: Boolean(user?.id) && student,
    refetchInterval: 30_000,
  });

  const registeredGroupId =
    statusQuery.data?.confirmationStatus === 'Confirmed' ? statusQuery.data.groupId : null;
  const registeredGroupQuery = useQuery({
    queryKey: ['student-topic-group', user?.id, registeredGroupId],
    queryFn: ({ signal }) => studentTopicApi.getGroupDetail(registeredGroupId!, signal),
    enabled: Boolean(user?.id && student && registeredGroupId),
  });

  const data = topicsQuery.isError ? undefined : topicsQuery.data;
  const status = statusQuery.isError
    ? undefined
    : statusQuery.data && registeredGroupQuery.data
      ? { ...statusQuery.data, batchId: registeredGroupQuery.data.batchId }
      : statusQuery.data;
  const total = totalQuery.isError ? undefined : totalQuery.data?.totalCount;
  const refreshing = topicsQuery.isFetching;

  function refresh() {
    for (const key of [
      'student-topics',
      'student-topics-total',
      'student-registration',
      'topic-advising',
      'topic-advising-detail',
      'my-proposed-topics',
      'student-topic-group',
      'student-topic-syllabus',
      'student-topic-midterm',
      'student-topic-final-report',
      'student-topic-milestones',
    ]) {
      void queryClient.invalidateQueries({ queryKey: [key] });
    }
  }

  function applyFilters(next: CatalogFilters) {
    const normalized: CatalogFilters = {
      ...next,
      keyword: next.keyword.trim(),
    };
    setDraft(normalized);
    setFilters(normalized);
    setPage(1);
    setSelectedRowId(null);
  }

  function apply(event: FormEvent) {
    event.preventDefault();
    applyFilters(draft);
  }

  function reset() {
    applyFilters(EMPTY_FILTERS);
  }

  function handleSort(field: CatalogSortField) {
    if (sortBy === field) {
      setSortOrder((prev) => (prev === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(field);
      setSortOrder(field === 'title' ? 'asc' : 'desc');
    }
    setPage(1);
    setSelectedRowId(null);
  }

  function openInvitation() {
    if (!student) return;
    setSelection(null);
    setInvitationOpen(true);
  }

  function batchName(id: string) {
    return batches.find((batch) => batch.id === id)?.batchName;
  }

  function openTopic(topic: PublishedTopic) {
    setSelectedRowId(topic.id);
    setSelection(topic);
    void topicsQuery.refetch();
  }

  if (!canBrowse) return <p role="alert">Bạn không có quyền xem danh mục đề tài này.</p>;

  const hasFilters = Boolean(filters.keyword || filters.batchId || filters.availability);
  const selectedBatchName = batches.find((batch) => batch.id === filters.batchId)?.batchName ?? 'Đợt đã chọn';
  const selectedTopic = data?.items.find((topic) => topic.id === selectedRowId) ?? null;
  const canQuickRegister = Boolean(
    student && selectedTopic && !validateStudentRegistration(selectedTopic, '', user?.userCode, status),
  );

  const renderAdvisorCell = (topic: PublishedTopic) => {
    const advisorDisplay = topic.advisorName || 'Giảng viên hướng dẫn';
    return (
      <InfoCell
        leading={
          <Avatar
            name={advisorDisplay}
            muted={!topic.advisorName}
            className="size-8 text-[11px]"
          />
        }
        title={
          <span className="truncate text-xs font-medium text-slate-800">{advisorDisplay}</span>
        }
        description={
          !topic.advisorName ? (
            <span className="truncate font-mono text-[10px] text-slate-400" title={topic.advisorId}>
              {topic.advisorId}
            </span>
          ) : undefined
        }
      />
    );
  };

  const renderBatchCell = (topic: PublishedTopic) => {
    const resolvedName = batchName(topic.batchId);
    if (resolvedName) {
      return <span className="text-xs text-slate-600">{resolvedName}</span>;
    }
    if (canReadBatches && batchesQuery.isPending) {
      return <Skeleton className="h-4 w-28 max-w-full" />;
    }
    return (
      <span className="text-xs text-slate-500" title={topic.batchId}>
        Mã đợt: {topic.batchId}
      </span>
    );
  };

  const renderViewButton = (topic: PublishedTopic) => (
    <Button
      type="button"
      variant="outline"
      size="sm"
      className="h-8 rounded-md border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-sm hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900"
      disabled={refreshing}
      aria-label={`Xem đề tài ${topic.titleVi}`}
      onClick={(event) => {
        event.stopPropagation();
        openTopic(topic);
      }}
    >
      Xem
    </Button>
  );

  const columns: DataTableColumn<PublishedTopic, CatalogSortField>[] = [
    {
      id: 'number',
      header: 'STT',
      widthClassName: 'w-14',
      align: 'center',
      headerClassName: 'px-3 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-4',
      skeleton: { variant: 'number' },
      cell: (_, index) => (
        <span className="text-xs font-medium tabular-nums text-slate-400">
          {((data?.pageNumber ?? page) - 1) * (data?.pageSize ?? pageSize) + index + 1}
        </span>
      ),
    },
    {
      id: 'title',
      header: 'Đề tài',
      sortKey: 'title',
      title: 'Sắp xếp theo tên đề tài',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'topic' },
      cell: (topic) => (
        <div className="min-w-0 space-y-1">
          <span
            className="line-clamp-2 block text-sm font-semibold leading-snug text-slate-900"
            title={topic.titleVi}
          >
            {topic.titleVi}
          </span>
          {topic.titleEn ? (
            <span
              className="block truncate text-[11px] font-normal text-slate-500"
              title={topic.titleEn}
            >
              {topic.titleEn}
            </span>
          ) : (
            <span className="block text-[11px] italic text-slate-400">
              Chưa có tên tiếng Anh
            </span>
          )}
        </div>
      ),
    },
    {
      id: 'advisor',
      header: 'Giảng viên',
      widthClassName: 'w-56',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'person' },
      cell: renderAdvisorCell,
    },
    {
      id: 'batch',
      header: 'Đợt KLTN',
      widthClassName: 'w-44',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      cell: renderBatchCell,
    },
    {
      id: 'capacity',
      header: 'Đăng ký',
      widthClassName: 'w-52',
      sortKey: 'availableSlots',
      title: 'Sắp xếp theo số chỗ trống còn lại',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'badge' },
      cell: (topic) => <TopicCapacityStatus topic={topic} />,
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-24',
      align: 'center',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-4',
      skeleton: { variant: 'actions', actionCount: 1 },
      cell: renderViewButton,
    },
  ];

  return (
    <div className="min-w-0 space-y-5 pb-24">
      <PageHeader
        title="Theo dõi & đăng ký đề tài"
        description={
          student
            ? 'Tra cứu đề tài đã công bố, theo dõi chỗ trống và gửi đăng ký thực hiện khóa luận'
            : 'Tra cứu danh mục đề tài đã công bố và tình trạng chỉ tiêu theo đợt KLTN'
        }
      >
        {student && (
          <Button
            type="button"
            variant="outline"
            leftIcon={<MailCheck />}
            onClick={openInvitation}
            className="h-10 rounded-xl border-slate-200 bg-white shadow-2xs hover:border-slate-300"
          >
            Xác nhận lời mời
          </Button>
        )}
      </PageHeader>

      <OperationToast message={student ? notice : ''} onConsumed={() => setNotice('')} />

      {student && (
        <StudentRegistrationStatusCard
          status={status}
          loading={statusQuery.isPending}
          busy={statusQuery.isFetching}
          error={statusQuery.isError ? getTopicErrorMessage(statusQuery.error) : ''}
          receipt={receipt}
          onRetry={() => void statusQuery.refetch()}
          onConfirm={openInvitation}
          onViewGroup={() => setGroupOpen(true)}
        />
      )}

      <Card
        ref={catalogRef}
        className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]"
      >
        <ListCardHeader
          title="Danh mục đề tài đã công bố"
          icon={<BookOpen />}
          className="min-h-0 lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]"
          description={
            filters.batchId
              ? `Đợt đang lọc: ${selectedBatchName}`
              : hasFilters
                ? 'Đang lọc theo điều kiện đã chọn'
                : student
                  ? 'Chọn đề tài còn chỗ để xem chi tiết hoặc gửi đăng ký'
                  : 'Toàn bộ danh mục đề tài đã công bố theo từng đợt KLTN'
          }
        >
          <div className="flex w-full items-center lg:justify-end">
            <StatisticsBar
              loading={totalQuery.isPending || (student && statusQuery.isPending)}
              items={[
                {
                  key: 'topics',
                  label: 'Tổng đề tài',
                  value: total ?? '—',
                  icon: <BookOpen />,
                  tone: 'primary',
                  active: !filters.keyword && !filters.availability,
                  actionLabel: 'Xem tất cả đề tài trong đợt đang lọc',
                  onClick: () => {
                    applyFilters({ ...filters, keyword: '', availability: '' });
                    catalogRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                  },
                },
                ...(student
                  ? [
                      {
                        key: 'warnings',
                        label: 'Lần cảnh báo',
                        value: status?.violationCount ?? '—',
                        icon: <ShieldAlert />,
                        tone: 'warning' as const,
                        actionLabel: 'Xem số lần cảnh báo',
                        onClick: () => setWarningsOpen(true),
                      },
                    ]
                  : []),
              ]}
            />
          </div>
        </ListCardHeader>

        <form
          onSubmit={apply}
          className="flex flex-wrap items-center gap-3 border-b border-slate-100 bg-slate-50/40 px-4 py-3.5 sm:px-6"
        >
          <div className="min-w-60 flex-1">
            <Input
              value={draft.keyword}
              aria-label="Tìm tên đề tài"
              placeholder="Tìm tên đề tài tiếng Việt hoặc tiếng Anh"
              leftIcon={<Search />}
              onChange={(event) =>
                setDraft((prev) => ({ ...prev, keyword: event.target.value }))
              }
              className="h-10 rounded-xl shadow-none"
            />
          </div>
          <Select<string>
            aria-label="Lọc theo đợt KLTN"
            placeholder="Tất cả đợt KLTN"
            value={draft.batchId}
            containerClassName="w-full sm:w-64"
            triggerClassName="h-10 rounded-xl shadow-none"
            disabled={!canReadBatches || batchesQuery.isPending}
            searchable
            options={[
              { value: '', label: 'Tất cả đợt KLTN' },
              ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
            ]}
            onChange={(batchId) => setDraft((prev) => ({ ...prev, batchId }))}
          />
          <Select<CatalogAvailabilityFilter>
            aria-label="Lọc theo tình trạng chỗ trống"
            placeholder="Tất cả tình trạng chỗ"
            value={draft.availability}
            containerClassName="w-full sm:w-52"
            triggerClassName="h-10 rounded-xl shadow-none"
            options={[
              { value: '', label: 'Tất cả tình trạng chỗ' },
              { value: 'available', label: 'Chỉ đề tài còn chỗ' },
              { value: 'full', label: 'Đã đủ / tạm hết chỗ' },
            ]}
            onChange={(availability) => setDraft((prev) => ({ ...prev, availability }))}
          />
          <div className="flex items-center gap-2">
            <Button type="submit" leftIcon={<SlidersHorizontal />} className="h-10 rounded-xl">
              Áp dụng
            </Button>
            <Button
              type="button"
              variant="outline"
              leftIcon={<FunnelX />}
              onClick={reset}
              className="h-10 rounded-xl shadow-none"
            >
              Bỏ lọc
            </Button>
          </div>
        </form>

        {batchesQuery.isError && (
          <p
            role="alert"
            className="border-b border-amber-100 bg-amber-50/70 px-5 py-2.5 text-xs text-amber-800 sm:px-6"
          >
            Chưa tải được danh sách đợt KLTN.{' '}
            <Button
              type="button"
              size="sm"
              variant="link"
              className="h-auto p-0 text-xs"
              onClick={() => void batchesQuery.refetch()}
            >
              Thử lại danh sách đợt
            </Button>
          </p>
        )}

        <TableActiveFilters
          chips={[
            ...(filters.keyword
              ? [
                  {
                    key: 'keyword',
                    label: 'Từ khóa',
                    value: filters.keyword,
                    onRemove: () => applyFilters({ ...filters, keyword: '' }),
                  },
                ]
              : []),
            ...(filters.batchId
              ? [
                  {
                    key: 'batchId',
                    label: 'Đợt KLTN',
                    value: selectedBatchName,
                    onRemove: () => applyFilters({ ...filters, batchId: '' }),
                  },
                ]
              : []),
            ...(filters.availability
              ? [
                  {
                    key: 'availability',
                    label: 'Chỗ trống',
                    value: AVAILABILITY_LABELS[filters.availability],
                    onRemove: () => applyFilters({ ...filters, availability: '' }),
                  },
                ]
              : []),
          ]}
          onClearAll={reset}
        />

        <DataTable<PublishedTopic, CatalogSortField>
          ariaLabel="Danh mục đề tài đã công bố"
          data={data?.items ?? []}
          columns={columns}
          getRowId={(topic) => topic.id}
          selectedRowId={selectedRowId}
          onRowSelect={(id) => setSelectedRowId(id)}
          onRowOpen={openTopic}
          sort={{ field: sortBy, direction: sortOrder }}
          onSortChange={handleSort}
          loading={topicsQuery.isPending}
          busy={refreshing}
          error={
            topicsQuery.isError
              ? {
                  message: getTopicErrorMessage(topicsQuery.error),
                  onRetry: () => void topicsQuery.refetch(),
                }
              : null
          }
          tableClassName="min-w-[960px]"
          rowClassName="h-[88px]"
          empty={{
            icon: <BookOpen className="size-8" />,
            title:
              page > 1 && data?.totalCount
                ? 'Trang này không còn đề tài'
                : hasFilters
                  ? 'Không có đề tài phù hợp'
                  : 'Chưa có đề tài công bố',
            description:
              page > 1 && data?.totalCount
                ? 'Danh sách có thể đã thay đổi. Quay lại trang đầu để xem các đề tài hiện có.'
                : hasFilters
                  ? 'Thử đổi từ khóa, đợt KLTN hoặc bỏ bớt bộ lọc đang áp dụng.'
                  : 'Các đề tài sau khi được Khoa thẩm định và công bố sẽ hiển thị tại đây.',
            action:
              page > 1 && data?.totalCount ? (
                <Button type="button" variant="outline" onClick={() => setPage(1)}>
                  Về trang đầu
                </Button>
              ) : hasFilters ? (
                <Button type="button" variant="outline" onClick={reset}>
                  Bỏ lọc
                </Button>
              ) : undefined,
          }}
          pagination={
            data && !topicsQuery.isError
              ? {
                  pageNumber: data.pageNumber,
                  pageSize: data.pageSize,
                  totalCount: data.totalCount,
                  itemLabel: 'đề tài',
                  isLoading: refreshing,
                  onPageChange: (next) => {
                    setPage(next);
                    setSelectedRowId(null);
                  },
                  onPageSizeChange: (nextSize) => {
                    setPageSize(nextSize);
                    setPage(1);
                    setSelectedRowId(null);
                  },
                }
              : undefined
          }
          renderMobileItem={(topic) => (
            <div className="space-y-3">
              <TopicCapacityStatus topic={topic} />
              <div className="space-y-1">
                <p className="text-sm font-semibold leading-snug text-slate-900">{topic.titleVi}</p>
                {topic.titleEn && <p className="text-xs text-slate-500">{topic.titleEn}</p>}
              </div>
              <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
                <div className="min-w-0 space-y-1">
                  {renderAdvisorCell(topic)}
                  <p className="text-xs text-slate-500">{renderBatchCell(topic)}</p>
                </div>
                {renderViewButton(topic)}
              </div>
            </div>
          )}
        />
      </Card>

      <TableFloatingBar
        selectedCount={selectedTopic ? 1 : 0}
        totalCount={data?.totalCount}
        onClearSelection={() => setSelectedRowId(null)}
      >
        {selectedTopic && (
          <>
            <span
              className="hidden max-w-60 truncate text-xs font-medium text-slate-600 md:inline-block"
              title={selectedTopic.titleVi}
            >
              {selectedTopic.titleVi}
            </span>
            {student ? (
              <Button
                type="button"
                size="sm"
                leftIcon={canQuickRegister ? <Send className="size-3.5" /> : <Eye className="size-3.5" />}
                className="h-8 rounded-xl px-3 text-xs"
                disabled={refreshing}
                onClick={() => openTopic(selectedTopic)}
              >
                {canQuickRegister ? 'Đăng ký nhanh' : 'Xem chi tiết'}
              </Button>
            ) : (
              <Button
                type="button"
                size="sm"
                variant="outline"
                leftIcon={<Eye className="size-3.5" />}
                className="h-8 rounded-xl px-3 text-xs shadow-none"
                disabled={refreshing}
                onClick={() => openTopic(selectedTopic)}
              >
                Xem chi tiết
              </Button>
            )}
          </>
        )}
      </TableFloatingBar>

      {selection && (
        <StudentTopicRegistrationDialog
          key={selection.id}
          topic={data?.items.find((topic) => topic.id === selection.id) ?? selection}
          availabilityLoading={topicsQuery.isFetching}
          batchName={batchName(selection.batchId)}
          status={status}
          onClose={() => setSelection(null)}
          onRefresh={refresh}
          onRegistered={(result, request) => {
            setSelection(null);
            setNotice('');
            setReceipt({
              ...result,
              topicTitle: request.topic.titleVi,
              partnerUserCode: request.partnerUserCode,
            });
            refresh();
          }}
        />
      )}

      {student && invitationOpen && (
        <StudentParticipationDialog
          onClose={() => setInvitationOpen(false)}
          onRefresh={refresh}
          onConfirmed={(result) => {
            setInvitationOpen(false);
            setReceipt(null);
            setNotice(result.message);
            refresh();
          }}
        />
      )}

      {student && groupOpen && status?.confirmationStatus === 'Confirmed' && status.groupId && (
        <StudentTopicGroupDialog groupId={status.groupId} onClose={() => setGroupOpen(false)} />
      )}

      {student && warningsOpen && (
        <Dialog
          title="Lần cảnh báo"
          onClose={() => setWarningsOpen(false)}
          className="max-w-md"
        >
          <div className="space-y-4 p-5 text-sm leading-6 text-slate-700 sm:p-6">
            <p>Số lần cảnh báo do quá hạn xác nhận lời mời tham gia đề tài.</p>
            <p className="font-semibold text-slate-900">
              {status?.violationCount === 0
                ? 'Bạn chưa có lần cảnh báo nào.'
                : status
                  ? `Bạn có ${status.violationCount} lần cảnh báo.`
                  : 'Chưa tải được số lần cảnh báo.'}
            </p>
            {status?.isRegistrationBlocked && (
              <p className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2.5 text-rose-700">
                Quyền đăng ký đang bị khóa. Vui lòng liên hệ Khoa để được hỗ trợ.
              </p>
            )}
          </div>
        </Dialog>
      )}
    </div>
  );
}
