import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BookOpen,
  FileText,
  GraduationCap,
  Search,
  SlidersHorizontal,
  FunnelX,
  Eye,
  MailCheck,
  ShieldAlert,
} from 'lucide-react';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { InfoCell } from '@/components/ui/info-cell';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { Skeleton } from '@/components/ui/skeleton';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { TablePagination } from '@/components/ui/table-pagination';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { studentTopicApi } from '../services/studentTopicApi';
import { getTopicErrorMessage } from '../services/topicApi';
import { StudentRegistrationStatusCard } from '../components/StudentRegistrationStatusCard';
import { StudentTopicRegistrationDialog } from '../components/StudentTopicRegistrationDialog';
import { StudentParticipationDialog } from '../components/StudentParticipationDialog';
import { TopicCapacityStatus } from '../components/TopicCapacityStatus';
import type { PublishedTopic, RegistrationReceipt } from '../types/studentTopic.types';

const PAGE_SIZES = [10, 20, 50] as const;
const EMPTY_FILTERS = { keyword: '', batchId: '' };

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
  const [draft, setDraft] = useState(EMPTY_FILTERS);
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(PAGE_SIZES[0]);
  const [selection, setSelection] = useState<PublishedTopic | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [invitationOpen, setInvitationOpen] = useState(false);
  const [receipt, setReceipt] = useState<RegistrationReceipt | null>(null);
  const [notice, setNotice] = useState('');
  const batchesQuery = useAcademicBatches();
  const batches = batchesQuery.data ?? [];
  const topicsQuery = useQuery({
    queryKey: ['student-topics', user?.id, filters, page, pageSize],
    queryFn: ({ signal }) =>
      studentTopicApi.getTopics({ ...filters, pageNumber: page, pageSize }, signal),
    enabled: Boolean(user?.id) && canBrowse,
    refetchInterval: 60_000,
    placeholderData: (previousData, previousQuery) =>
      previousQuery &&
      previousQuery.queryKey[1] === user?.id &&
      JSON.stringify(previousQuery.queryKey[2]) === JSON.stringify(filters)
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
  const data = topicsQuery.isError ? undefined : topicsQuery.data;
  const status = statusQuery.isError ? undefined : statusQuery.data;
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
    ]) {
      void queryClient.invalidateQueries({ queryKey: [key] });
    }
  }
  function apply(event: FormEvent) {
    event.preventDefault();
    setFilters({ ...draft, keyword: draft.keyword.trim() });
    setPage(1);
    setSelectedRowId(null);
  }
  function reset() {
    setFilters(EMPTY_FILTERS);
    setDraft(EMPTY_FILTERS);
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

  return (
    <div className="min-w-0 space-y-5">
      <PageHeader title="Đề tài & đăng ký" icon={<GraduationCap />}>
        {student ? (
          <Button
            type="button"
            variant="outline"
            leftIcon={<MailCheck />}
            onClick={openInvitation}
            className="h-10 rounded-lg shadow-none"
          >
            Xác nhận lời mời
          </Button>
        ) : (
          <StatusBadge tone="neutral">Chỉ xem</StatusBadge>
        )}
      </PageHeader>
      {student && notice && (
        <p
          role="status"
          className="rounded-xl border border-emerald-100 bg-emerald-50 px-5 py-3 text-sm text-emerald-800"
        >
          {notice}
        </p>
      )}
      {student && (
        <StudentRegistrationStatusCard
          status={status}
          loading={statusQuery.isPending}
          busy={statusQuery.isFetching}
          error={statusQuery.isError ? getTopicErrorMessage(statusQuery.error) : ''}
          receipt={receipt}
          onRetry={() => void statusQuery.refetch()}
          onConfirm={openInvitation}
        />
      )}
      <form
        onSubmit={apply}
        className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200/80 bg-white p-4"
      >
        <div className="min-w-60 flex-1">
          <Input
            value={draft.keyword}
            aria-label="Tìm tên đề tài"
            placeholder="Tìm tên đề tài tiếng Việt hoặc tiếng Anh"
            leftIcon={<Search />}
            onChange={(event) => setDraft({ ...draft, keyword: event.target.value })}
            className="h-10 rounded-lg shadow-none"
          />
        </div>
        <Select<string>
          aria-label="Lọc theo đợt KLTN"
          value={draft.batchId}
          containerClassName="w-full sm:w-64"
          triggerClassName="h-10 rounded-lg shadow-none"
          disabled={!canReadBatches || batchesQuery.isPending}
          searchable
          options={[
            { value: '', label: 'Tất cả đợt KLTN' },
            ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
          ]}
          onChange={(batchId) => setDraft({ ...draft, batchId })}
        />
        <Button type="submit" leftIcon={<SlidersHorizontal />} className="h-10 rounded-lg">
          Áp dụng
        </Button>
        <Button
          type="button"
          variant="outline"
          leftIcon={<FunnelX />}
          onClick={reset}
          className="h-10 rounded-lg shadow-none"
        >
          Bỏ lọc
        </Button>
      </form>
      {batchesQuery.isError && (
        <p role="alert" className="text-sm text-amber-700">
          Chưa tải được danh sách đợt KLTN.{' '}
          <Button
            type="button"
            size="sm"
            variant="link"
            onClick={() => void batchesQuery.refetch()}
          >
            Thử lại danh sách đợt
          </Button>
        </p>
      )}
      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <ListCardHeader
          title="Danh mục đề tài đã công bố"
          icon={<BookOpen />}
          className={
            student ? 'xl:grid-cols-[minmax(0,1fr)_440px]' : 'xl:grid-cols-[minmax(0,1fr)_180px]'
          }
          description={
            data
              ? `${data.totalCount.toLocaleString('vi-VN')} đề tài${filters.keyword ? ' phù hợp' : ''}`
              : 'Tra cứu và đăng ký đề tài'
          }
        >
          <StatisticsBar
            loading={totalQuery.isPending || (student && statusQuery.isPending)}
            items={[
              {
                key: 'topics',
                label: 'Tổng đề tài',
                value: total ?? '—',
                icon: <BookOpen />,
                tone: 'primary',
                active: !filters.keyword,
                onClick: () => {
                  const next = { ...filters, keyword: '' };
                  setDraft(next);
                  setFilters(next);
                  setPage(1);
                  setSelectedRowId(null);
                },
              },
              ...(student
                ? [
                    {
                      key: 'registration',
                      label: 'Hồ sơ hiện tại',
                      value: status ? (status.hasRegistered ? 1 : 0) : '—',
                      icon: <GraduationCap />,
                      tone: 'primary' as const,
                    },
                    {
                      key: 'warnings',
                      label: 'Lần cảnh báo',
                      value: status?.violationCount ?? '—',
                      icon: <ShieldAlert />,
                      tone: 'warning' as const,
                    },
                  ]
                : []),
            ]}
          />
        </ListCardHeader>
        <div aria-busy={refreshing} className="h-[min(60vh,40rem)] min-h-[20rem] overflow-auto">
          {topicsQuery.isPending ? (
            <TableSkeleton
              className="min-w-[1080px]"
              columns={[
                { label: 'STT', width: 'w-14', variant: 'number' },
                { label: 'Đề tài', variant: 'topic' },
                { label: 'Giảng viên', width: 'w-48', variant: 'person' },
                { label: 'Đợt KLTN', width: 'w-44' },
                { label: 'Đăng ký', width: 'w-40', variant: 'badge' },
                { label: 'Thao tác', width: 'w-40', variant: 'actions' },
              ]}
            />
          ) : topicsQuery.isError ? (
            <div role="alert" className="space-y-3 p-10 text-center">
              <p className="text-sm text-rose-700">{getTopicErrorMessage(topicsQuery.error)}</p>
              <Button type="button" variant="outline" onClick={() => void topicsQuery.refetch()}>
                Thử lại danh mục
              </Button>
            </div>
          ) : !data?.items.length ? (
            <div className="flex min-h-64 flex-col items-center justify-center gap-3 p-8 text-center">
              <Search className="size-9 text-slate-300" />
              <p className="font-medium text-slate-700">
                {filters.keyword || filters.batchId
                  ? 'Không có đề tài phù hợp với bộ lọc.'
                  : 'Chưa có đề tài được công bố.'}
              </p>
              {(filters.keyword || filters.batchId) && (
                <Button type="button" variant="outline" onClick={reset}>
                  Bỏ bộ lọc
                </Button>
              )}
            </div>
          ) : (
            <table className="w-full min-w-[1080px] table-fixed text-left text-sm">
              <colgroup>
                <col className="w-14" />
                <col />
                <col className="w-48" />
                <col className="w-44" />
                <col className="w-40" />
                <col className="w-40" />
              </colgroup>
              <thead className="sticky top-0 z-10 h-14 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600">
                <tr>
                  {['STT', 'Đề tài', 'Giảng viên', 'Đợt KLTN', 'Đăng ký', 'Thao tác'].map(
                    (heading) => (
                      <th
                        scope="col"
                        key={heading}
                        className={`px-4 py-4 font-bold ${['STT', 'Thao tác'].includes(heading) ? 'text-center' : ''}`}
                      >
                        {heading}
                      </th>
                    ),
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.map((topic, index) => (
                  <SelectableTableRow
                    key={topic.id}
                    selected={selectedRowId === topic.id}
                    disabled={refreshing}
                    className="h-[88px]"
                    onSelect={() => setSelectedRowId(topic.id)}
                  >
                    <td className="px-3 py-4 text-center text-xs tabular-nums text-slate-400">
                      {(data.pageNumber - 1) * data.pageSize + index + 1}
                    </td>
                    <td className="px-4 py-4">
                      <InfoCell
                        title={
                          <span className="line-clamp-2" title={topic.titleVi}>
                            {topic.titleVi}
                          </span>
                        }
                        description={topic.titleEn || undefined}
                        titleClassName="block text-sm leading-5"
                        descriptionClassName="truncate text-[11px]"
                        leading={
                          <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-blue-50 text-primary ring-1 ring-inset ring-blue-100">
                            <FileText className="size-4" aria-hidden="true" />
                          </span>
                        }
                      />
                    </td>
                    <td className="px-4 py-4">
                      <p className="text-xs font-medium text-slate-700">
                        {topic.advisorName || 'Giảng viên hướng dẫn'}
                      </p>
                      {!topic.advisorName && (
                        <p
                          className="mt-1 truncate font-mono text-[10px] text-slate-400"
                          title={topic.advisorId}
                        >
                          {topic.advisorId}
                        </p>
                      )}
                    </td>
                    <td className="px-4 py-4 text-xs text-slate-500">
                      {batchName(topic.batchId) ||
                        (canReadBatches && batchesQuery.isPending ? (
                          <Skeleton className="h-4 w-28" />
                        ) : (
                          <span title={topic.batchId}>Mã đợt: {topic.batchId}</span>
                        ))}
                    </td>
                    <td className="px-4 py-4">
                      <TopicCapacityStatus topic={topic} />
                    </td>
                    <td className="px-4 py-4 text-center">
                      <Button
                        type="button"
                        variant="soft"
                        size="sm"
                        className="h-9 w-28 rounded-lg shadow-none"
                        leftIcon={<Eye />}
                        disabled={refreshing}
                        aria-label={`Xem đề tài ${topic.titleVi}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          openTopic(topic);
                        }}
                      >
                        {topic.availableSlots === 0 ? 'Chỉ xem' : 'Chi tiết'}
                      </Button>
                    </td>
                  </SelectableTableRow>
                ))}
              </tbody>
            </table>
          )}
        </div>
        {data && !topicsQuery.isError && (
          <>
            <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-3 text-sm">
              <label htmlFor="student-topic-page-size">Số dòng mỗi trang</label>
              <select
                id="student-topic-page-size"
                value={pageSize}
                className="rounded-md border border-slate-200 bg-white p-2"
                onChange={(event) => {
                  setPageSize(Number(event.target.value));
                  setPage(1);
                  setSelectedRowId(null);
                }}
              >
                {PAGE_SIZES.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </div>
            <TablePagination
              pageNumber={data.pageNumber}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              itemLabel="đề tài"
              isLoading={refreshing}
              onPageChange={(next) => {
                setPage(next);
                setSelectedRowId(null);
              }}
            />
          </>
        )}
      </Card>
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
    </div>
  );
}
