import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  BookOpen,
  Users,
  Mail,
  Search,
  SlidersHorizontal,
  FunnelX,
  Eye,
  Info,
  Copy,
  ArrowUpRight,
  CheckCircle2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { StatusBadge } from '@/components/ui/status-badge';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { Card } from '@/components/ui/card';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { Dialog } from '@/components/ui/dialog';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { InfoCell } from '@/components/ui/info-cell';
import { useAuth } from '@/shared/auth';
import { TopicStatusBadge } from '../components/TopicStatusBadge';
import { TOPIC_STATUS_LABELS } from '../types/topic.types';
import {
  REGISTRATION_LABELS,
  type AdvisingRegistration,
  type AdvisingTab,
  type RegistrationStatus,
} from '../types/advising.types';
import { loadAdvisingDemo } from '../services/advisingDemo';
import {
  getConfirmedStudentCount,
  getTopicRegistrations,
  matchesAdvisingSearch,
} from '../advising';

const initialFilters = { keyword: '', batchId: '2026-1', status: '' };
const PAGE_SIZES = [10, 20, 50] as const;
const tabs: { id: AdvisingTab; label: string }[] = [
  { id: 'topics', label: 'Danh sách đề tài' },
  { id: 'registrations', label: 'Nhóm đăng ký' },
  { id: 'advising', label: 'Đang hướng dẫn' },
];
const date = (value: string | null) =>
  value
    ? new Intl.DateTimeFormat('vi-VN', {
        dateStyle: 'short',
        timeStyle: 'short',
        timeZone: 'Asia/Ho_Chi_Minh',
      }).format(new Date(value))
    : '—';

function RegistrationBadge({ status }: { status: RegistrationStatus }) {
  const tone = {
    PendingEmail: 'warning',
    Confirmed: 'success',
    Expired: 'neutral',
    Cancelled: 'error',
  } as const;
  return (
    <StatusBadge tone={tone[status]}>
      {REGISTRATION_LABELS[status]}
    </StatusBadge>
  );
}

export function TopicAdvisingPage({
  embedded = false,
  initialTab = 'topics',
  initialStatus = '',
  initialBatchId = initialFilters.batchId,
  onQuickFilter,
}: {
  embedded?: boolean;
  initialTab?: AdvisingTab;
  initialStatus?: string;
  initialBatchId?: string;
  onQuickFilter?: (tab: AdvisingTab, status: string, batchId: string) => void;
} = {}) {
  const { user } = useAuth();
  const [tab, setTab] = useState<AdvisingTab>(initialTab);
  const [draft, setDraft] = useState({
    ...initialFilters,
    batchId: initialBatchId,
    status: initialStatus,
  });
  const [filters, setFilters] = useState({
    ...initialFilters,
    batchId: initialBatchId,
    status: initialStatus,
  });
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(PAGE_SIZES[0]);
  const [detail, setDetail] = useState<{ topicId: string; groupId?: string } | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [facultyGroupId, setFacultyGroupId] = useState<string | null>(null);
  const [copyNotice, setCopyNotice] = useState('');
  const query = useQuery({
    queryKey: ['advising-demo', user?.id],
    queryFn: ({ signal }) => loadAdvisingDemo(signal),
    staleTime: Infinity,
  });
  const data = query.data;
  const topics = data?.topics ?? [];
  const registrations = data?.registrations ?? [];
  const batchTopics = topics.filter(
    (topic) => !filters.batchId || topic.batchId === filters.batchId,
  );
  const batchGroups = registrations.filter((group) =>
    batchTopics.some((topic) => topic.id === group.topicId),
  );
  const confirmedGroups = batchGroups.filter((group) => group.status === 'Confirmed');
  // Completed groups remain in registration history, outside the current advising roster.
  const advisingGroups = confirmedGroups.filter((group) => group.stage !== 'Đã hoàn thành');
  const pendingGroups = batchGroups.filter((group) => group.status === 'PendingEmail');
  const topicRows = batchTopics.filter((topic) => {
    const groups = data ? getTopicRegistrations(data, topic.id) : [];
    return (
      (!filters.status || topic.status === filters.status) &&
      matchesAdvisingSearch(topic, groups, filters.keyword)
    );
  });
  const groupRows = (tab === 'advising' ? advisingGroups : batchGroups)
    .filter((group) => {
      const topic = topics.find((item) => item.id === group.topicId);
      return (
        topic &&
        (!filters.status ||
          (tab === 'advising'
            ? group.stage === filters.status
            : group.status === filters.status)) &&
        matchesAdvisingSearch(topic, [group], filters.keyword)
      );
    })
    .sort((a, b) => b.registeredAt.localeCompare(a.registeredAt));
  const total = tab === 'topics' ? topicRows.length : groupRows.length;
  const currentPage = Math.min(page, Math.max(1, Math.ceil(total / pageSize)));
  const start = (currentPage - 1) * pageSize;
  const selectedTopic = topics.find((topic) => topic.id === detail?.topicId);
  const selectedGroups =
    data && selectedTopic
      ? getTopicRegistrations(data, selectedTopic.id).filter(
          (group) => !detail?.groupId || group.id === detail.groupId,
        )
      : [];
  const facultyGroup = registrations.find((group) => group.id === facultyGroupId);
  const facultyTopic = topics.find((topic) => topic.id === facultyGroup?.topicId);
  const facultyText =
    facultyGroup && facultyTopic
      ? `Kính gửi Văn phòng Khoa,\nTôi là ${user?.fullName ?? 'giảng viên phụ trách'}. Tôi cần trao đổi về nhóm ${facultyGroup.groupCode}, hồ sơ ${facultyGroup.code}, đề tài ${facultyTopic.code} – ${facultyTopic.title}.\nSinh viên: ${facultyGroup.students.map((s) => `${s.name} (${s.code})`).join(', ')}.\nLý do đề nghị xử lý: [Giảng viên bổ sung lý do và minh chứng].\nKính nhờ Khoa xem xét và xử lý theo quy định.`
      : '';

  function changeTab(next: AdvisingTab, status = '') {
    if (embedded && onQuickFilter) {
      onQuickFilter(next, status, filters.batchId);
      return;
    }
    setTab(next);
    const nextFilters = { keyword: '', batchId: filters.batchId, status };
    setFilters(nextFilters);
    setDraft(nextFilters);
    setPage(1);
  }
  function resetFilters() {
    const cleared = { keyword: '', batchId: '', status: '' };
    setFilters(cleared);
    setDraft(cleared);
    setPage(1);
  }
  function applyFilters(event: FormEvent) {
    event.preventDefault();
    setFilters({ ...draft, keyword: draft.keyword.trim() });
    setPage(1);
  }
  async function copy(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      setCopyNotice('Đã sao chép nội dung.');
    } catch {
      setCopyNotice(
        'Không thể sao chép tự động. Bạn có thể chọn và sao chép nội dung trong khung bên dưới.',
      );
    }
  }
  const statusOptions =
    tab === 'topics'
      ? Object.entries(TOPIC_STATUS_LABELS)
      : tab === 'registrations'
        ? Object.entries(REGISTRATION_LABELS)
        : [
            ...new Set(
              advisingGroups
                .map((group) => group.stage)
                .filter((stage): stage is string => Boolean(stage)),
            ),
          ].map((stage) => [stage, stage]);

  function renderGroup(group: AdvisingRegistration) {
    return (
      <section key={group.id} className="space-y-4 rounded-xl border border-slate-200 p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="font-semibold text-slate-900">Nhóm {group.groupCode}</h3>
            <p className="mt-1 text-xs text-slate-500">{group.code}</p>
          </div>
          <RegistrationBadge status={group.status} />
        </div>
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs text-slate-500">Thời điểm đăng ký</dt>
            <dd className="mt-1">{date(group.registeredAt)}</dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500">
              {group.status === 'PendingEmail' || group.status === 'Expired'
                ? 'Hạn xác nhận tham gia'
                : 'Đăng ký có hiệu lực'}
            </dt>
            <dd className="mt-1">
              {date(
                group.status === 'PendingEmail' || group.status === 'Expired'
                  ? group.confirmationDeadline
                  : group.confirmedAt,
              )}
            </dd>
          </div>
        </dl>
        {group.stage && group.status === 'Confirmed' && (
          <p className="text-sm text-slate-600">
            Tiến độ khóa luận: <strong>{group.stage}</strong>
          </p>
        )}
        <div className="space-y-3">
          {group.students.map((student) => (
            <div key={student.id} className="rounded-lg bg-slate-50 p-3 text-sm">
              <div className="flex flex-wrap justify-between gap-2">
                <p className="font-medium">
                  {student.name}{' '}
                  <span className="font-normal text-slate-500">· {student.code}</span>
                </p>
                <StatusBadge
                  tone={
                    student.registrationRole === 'Registrant'
                      ? 'info'
                      : student.participationConfirmedAt
                        ? 'success'
                        : 'warning'
                  }
                >
                  {student.registrationRole === 'Registrant'
                    ? 'Người đăng ký'
                    : student.participationConfirmedAt
                      ? 'Đã xác nhận tham gia'
                      : 'Chờ xác nhận tham gia'}
                </StatusBadge>
              </div>
              <p className="mt-2 break-all text-slate-600">{student.email}</p>
              <p className="mt-1 text-xs text-slate-500">
                {student.phone}
                {student.participationConfirmedAt &&
                  ` · Xác nhận: ${date(student.participationConfirmedAt)}`}
              </p>
            </div>
          ))}
        </div>
        {group.status === 'Cancelled' && (
          <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
            {group.cancellationReason}
          </p>
        )}
        {group.status === 'Confirmed' && group.stage !== 'Đã hoàn thành' && (
          <Button
            variant="outline"
            size="sm"
            leftIcon={<Info />}
            onClick={() => {
              setFacultyGroupId(group.id);
              setCopyNotice('');
            }}
          >
            Liên hệ Khoa
          </Button>
        )}
      </section>
    );
  }

  return (
    <div className="min-w-0 space-y-5">
      {!embedded && (
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              Đề tài & nhóm hướng dẫn
            </h1>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="warning">Dữ liệu mẫu</Badge>
            <Button asChild variant="outline" leftIcon={<ArrowUpRight />}>
              <Link to="/topics/propose">Đề xuất đề tài</Link>
            </Button>
          </div>
        </div>
      )}
      {!embedded && (
        <nav className="flex flex-wrap gap-2" aria-label="Danh mục quản lý hướng dẫn">
          {tabs.map((item) => (
            <Button
              key={item.id}
              type="button"
              variant={tab === item.id ? 'default' : 'outline'}
              aria-current={tab === item.id ? 'page' : undefined}
              onClick={() => changeTab(item.id)}
            >
              {item.label}
            </Button>
          ))}
        </nav>
      )}
      <form
        onSubmit={applyFilters}
        className="grid items-end gap-4 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm md:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr_auto]"
      >
        <label className="block space-y-2 text-sm text-slate-700">
          <span>Tìm kiếm</span>
          <Input
            leftIcon={<Search className="size-4" />}
            className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
            placeholder="Đề tài, nhóm hoặc sinh viên"
            value={draft.keyword}
            onChange={(event) => setDraft({ ...draft, keyword: event.target.value })}
          />
        </label>
        <div className="space-y-2 text-sm text-slate-700">
          <span>Đợt KLTN</span>
          <Select
            aria-label="Lọc theo đợt KLTN"
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            value={draft.batchId}
            options={[
              { value: '', label: 'Tất cả đợt' },
              ...(data?.batches ?? []).map((batch) => ({ value: batch.id, label: batch.name })),
            ]}
            onChange={(batchId: string) => setDraft({ ...draft, batchId })}
          />
        </div>
        <div className="space-y-2 text-sm text-slate-700">
          <span>{tab === 'advising' ? 'Tiến độ khóa luận' : 'Trạng thái'}</span>
          <Select
            aria-label="Lọc trạng thái"
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            value={draft.status}
            options={[
              { value: '', label: 'Tất cả trạng thái' },
              ...statusOptions.map(([value, label]) => ({ value, label })),
            ]}
            onChange={(status: string) => setDraft({ ...draft, status })}
          />
        </div>
        <div className="flex gap-2">
          <Button type="submit" variant="soft" className="h-10" leftIcon={<SlidersHorizontal />}>
            Áp dụng
          </Button>
          <Button
            type="button"
            variant="outline"
            className="h-10"
            leftIcon={<FunnelX />}
            onClick={resetFilters}
          >
            Bỏ lọc
          </Button>
        </div>
      </form>
      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <ListCardHeader
          title={tabs.find((item) => item.id === tab)?.label}
          icon={<Users />}
          description={`${total} ${tab === 'topics' ? 'đề tài' : 'nhóm'}`}
        >
          <section className="w-full" aria-label="Thống kê hướng dẫn" aria-busy={query.isFetching}>
            <StatisticsBar
              loading={query.isPending}
              items={[
                {
                  key: 'topics',
                  onClick: () => changeTab('topics'),
                  active: tab === 'topics' && !filters.status && !filters.keyword,
                  label: 'Tổng đề tài',
                  value: batchTopics.length,
                  icon: <BookOpen />,
                  tone: 'primary',
                },
                {
                  key: 'pending',
                  onClick: () => changeTab('registrations', 'PendingEmail'),
                  active:
                    tab === 'registrations' &&
                    filters.status === 'PendingEmail' &&
                    !filters.keyword,
                  label: 'Chờ xác nhận',
                  value: pendingGroups.length,
                  icon: <Mail />,
                  tone: 'warning',
                },
                {
                  key: 'advising',
                  onClick: () => changeTab('advising'),
                  active: tab === 'advising' && !filters.status && !filters.keyword,
                  label: 'Nhóm hướng dẫn',
                  value: advisingGroups.length,
                  icon: <Users />,
                  tone: 'primary',
                },
                {
                  key: 'students',
                  onClick: () => changeTab('advising'),
                  label: 'SV hướng dẫn',
                  value: getConfirmedStudentCount(advisingGroups),
                  icon: <CheckCircle2 />,
                  tone: 'muted',
                },
              ]}
            />
          </section>
        </ListCardHeader>
        <div
          aria-busy={query.isFetching}
          className="h-[min(60vh,40rem)] min-h-[20rem] overflow-auto"
        >
          {query.isPending ? (
            <TableSkeleton
              className="min-w-[1120px]"
              rows={5}
              columns={(tab === 'topics'
                ? ['Đề tài', 'Đợt KLTN', 'Sinh viên', 'Trạng thái', 'Thao tác']
                : [
                    'Nhóm / hồ sơ',
                    'Đề tài',
                    'Sinh viên',
                    tab === 'advising' ? 'Tiến độ' : 'Trạng thái',
                    'Thao tác',
                  ]
              ).map((label) => ({ label }))}
            />
          ) : query.isError ? (
            <div role="alert" className="space-y-3 p-10 text-center">
              <p className="text-sm text-rose-700">Không thể tải danh sách. Vui lòng thử lại.</p>
              <Button variant="outline" onClick={() => void query.refetch()}>
                Thử lại
              </Button>
            </div>
          ) : total === 0 ? (
            <div className="flex min-h-64 flex-col items-center justify-center gap-3 p-8 text-center">
              <Search className="size-9 text-slate-300" />
              <p className="font-medium text-slate-700">
                Không có {tab === 'topics' ? 'đề tài' : 'nhóm'} phù hợp
              </p>
              <p className="text-sm text-slate-500">
                Thử thay đổi từ khóa, đợt KLTN hoặc trạng thái.
              </p>
              <Button variant="outline" onClick={resetFilters}>
                Bỏ bộ lọc
              </Button>
            </div>
          ) : (
            <table className="w-full min-w-[1120px] table-fixed text-left text-sm">
              <colgroup>
                <col className={tab === 'topics' ? '' : 'w-44'} />
                <col className={tab === 'topics' ? 'w-40' : ''} />
                <col className={tab === 'topics' ? 'w-24' : 'w-48'} />
                <col className="w-56" />
                <col className="w-44" />
              </colgroup>
              <thead className="sticky top-0 z-10 border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600">
                <tr>
                  {(tab === 'topics'
                    ? ['Đề tài', 'Đợt KLTN', 'Sinh viên', 'Trạng thái', 'Thao tác']
                    : [
                        'Nhóm / hồ sơ',
                        'Đề tài',
                        'Sinh viên',
                        tab === 'advising' ? 'Tiến độ khóa luận' : 'Trạng thái đăng ký',
                        'Thao tác',
                      ]
                  ).map((label) => (
                    <th
                      scope="col"
                      key={label}
                      className={`px-5 py-4 font-bold ${label === 'Thao tác' ? 'text-center' : ''}`}
                    >
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {tab === 'topics'
                  ? topicRows.slice(start, start + pageSize).map((topic) => {
                      const groups = data ? getTopicRegistrations(data, topic.id) : [];
                      const count = getConfirmedStudentCount(groups);
                      const pending = groups.filter(
                        (group) => group.status === 'PendingEmail',
                      ).length;
                      return (
                        <SelectableTableRow
                          key={topic.id}
                          selected={selectedRowId === topic.id}
                          onSelect={() => setSelectedRowId(topic.id)}
                          disabled={query.isFetching}
                        >
                          <td className="max-w-sm px-5 py-4">
                            <p className="text-xs font-medium text-primary">{topic.code}</p>
                            <button
                              className="mt-1 text-left font-semibold leading-6 text-slate-800 hover:text-primary focus-visible:underline"
                              onClick={() => setDetail({ topicId: topic.id })}
                            >
                              {topic.title}
                            </button>
                            {pending > 0 && (
                              <StatusBadge tone="warning" className="mt-2">
                                Chờ xác nhận
                              </StatusBadge>
                            )}
                          </td>
                          <td className="px-5 py-5 text-xs text-slate-500">
                            {data?.batches.find((batch) => batch.id === topic.batchId)?.name}
                          </td>
                          <td className="px-5 py-4">
                            <span className="font-semibold tabular-nums">
                              {count}/{topic.capacity}
                            </span>
                          </td>
                          <td className="px-5 py-4">
                            <TopicStatusBadge status={topic.status} />
                          </td>
                          <td className="px-5 py-4 text-center">
                            <Button
                              variant="soft"
                              size="sm"
                              leftIcon={<Eye />}
                              onClick={() => setDetail({ topicId: topic.id })}
                              aria-label={`Xem đề tài ${topic.code}`}
                              className="h-9 w-28 rounded-lg shadow-none"
                            >
                              Chi tiết
                            </Button>
                          </td>
                        </SelectableTableRow>
                      );
                    })
                  : groupRows.slice(start, start + pageSize).map((group) => {
                      const topic = topics.find((item) => item.id === group.topicId)!;
                      return (
                        <SelectableTableRow
                          key={group.id}
                          selected={selectedRowId === group.id}
                          onSelect={() => setSelectedRowId(group.id)}
                          disabled={query.isFetching}
                        >
                          <td className="px-5 py-4">
                            <Badge
                              variant={
                                group.status === 'PendingEmail'
                                  ? 'warning'
                                  : group.status === 'Confirmed'
                                    ? 'info'
                                    : 'secondary'
                              }
                              className="h-8 min-w-12 justify-center rounded-lg font-semibold ring-1 ring-inset ring-current/10"
                            >
                              {group.groupCode}
                            </Badge>
                            <p className="mt-1 text-xs text-slate-500">{group.code}</p>
                            <p className="mt-1 text-xs text-slate-400">
                              {date(group.registeredAt)}
                            </p>
                          </td>
                          <td className="max-w-xs px-5 py-4">
                            <p
                              className="line-clamp-2 font-semibold leading-6 text-slate-800"
                              title={topic.title}
                            >
                              {topic.title}
                            </p>
                            <p className="mt-1 text-xs text-slate-500">
                              {topic.code} ·{' '}
                              {data?.batches.find((batch) => batch.id === topic.batchId)?.name}
                            </p>
                          </td>
                          <td className="px-5 py-4">
                            <div className="space-y-2">
                              {group.students.map((student) => (
                                <InfoCell
                                  key={student.id}
                                  title={student.name}
                                  description={student.code}
                                  titleClassName="font-medium"
                                  leading={
                                    <span
                                      aria-hidden="true"
                                      className="flex size-7 shrink-0 items-center justify-center rounded-full bg-slate-100 text-[10px] font-semibold text-slate-500"
                                    >
                                      {student.name
                                        .trim()
                                        .split(/\s+/)
                                        .slice(-2)
                                        .map((part) => part[0])
                                        .join('')}
                                    </span>
                                  }
                                />
                              ))}
                            </div>
                          </td>
                          <td className="px-5 py-4">
                            {tab === 'advising' ? (
                              <StatusBadge tone="info">{group.stage}</StatusBadge>
                            ) : (
                              <>
                                <RegistrationBadge status={group.status} />
                              </>
                            )}
                          </td>
                          <td className="px-5 py-4 text-center">
                            <Button
                              variant="soft"
                              size="sm"
                              leftIcon={<Eye />}
                              onClick={() => setDetail({ topicId: topic.id, groupId: group.id })}
                              aria-label={`Xem nhóm ${group.groupCode}`}
                              className="h-9 w-28 rounded-lg shadow-none"
                            >
                              Chi tiết
                            </Button>
                          </td>
                        </SelectableTableRow>
                      );
                    })}
              </tbody>
            </table>
          )}
        </div>
        {!query.isPending && !query.isError && (
          <>
            <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-3 text-sm">
              <label htmlFor="advising-page-size">Số dòng mỗi trang</label>
              <select
                id="advising-page-size"
                className="rounded-md border border-slate-200 bg-white p-2"
                value={pageSize}
                onChange={(event) => {
                  setPageSize(Number(event.target.value));
                  setPage(1);
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
              pageNumber={currentPage}
              pageSize={pageSize}
              totalCount={total}
              onPageChange={setPage}
              itemLabel={tab === 'topics' ? 'đề tài' : 'nhóm'}
            />
          </>
        )}
      </Card>
      {selectedTopic && (
        <Dialog
          title={detail?.groupId ? 'Chi tiết nhóm đăng ký' : 'Chi tiết đề tài hướng dẫn'}
          onClose={() => setDetail(null)}
          className="max-w-4xl"
        >
          <div className="space-y-6 p-5 sm:p-6">
            <div>
              <div className="mb-3 flex flex-wrap gap-2">
                <Badge variant="secondary">{selectedTopic.code}</Badge>
                <TopicStatusBadge status={selectedTopic.status} />
                <Badge variant="warning">Dữ liệu mẫu</Badge>
              </div>
              <h2 className="text-xl font-bold leading-8 text-slate-900">{selectedTopic.title}</h2>
              <p className="mt-1 text-sm text-slate-500">{selectedTopic.titleEn}</p>
            </div>
            <dl className="grid gap-4 rounded-xl bg-slate-50 p-4 text-sm sm:grid-cols-3">
              <div>
                <dt className="text-xs text-slate-500">Giảng viên hướng dẫn</dt>
                <dd className="mt-1 font-medium">{user?.fullName ?? 'Giảng viên hiện tại'}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Đợt KLTN</dt>
                <dd className="mt-1">
                  {data?.batches.find((batch) => batch.id === selectedTopic.batchId)?.name}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Chỉ tiêu</dt>
                <dd className="mt-1">{selectedTopic.capacity} sinh viên</dd>
              </div>
            </dl>
            <section>
              <h3 className="text-sm font-semibold text-slate-800">Mục tiêu và mô tả</h3>
              <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-7 text-slate-600">
                {selectedTopic.description}
              </p>
              <h3 className="mt-4 text-sm font-semibold text-slate-800">
                Yêu cầu công nghệ và kiến thức
              </h3>
              <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-7 text-slate-600">
                {selectedTopic.requirements}
              </p>
            </section>
            <section className="space-y-3">
              <h3 className="flex items-center gap-2 font-semibold">
                <Users className="size-4 text-primary" />
                {detail?.groupId ? 'Thành viên' : 'Nhóm đăng ký'}
              </h3>
              {selectedGroups.length ? (
                selectedGroups.map(renderGroup)
              ) : (
                <p className="rounded-xl border border-dashed border-slate-200 p-6 text-center text-sm text-slate-500">
                  Chưa có nhóm đăng ký đề tài này.
                </p>
              )}
            </section>
            <div className="flex justify-end border-t border-slate-100 pt-4">
              <Button variant="outline" onClick={() => setDetail(null)}>
                Đóng
              </Button>
            </div>
          </div>
        </Dialog>
      )}
      {facultyGroup && (
        <Dialog
          title="Liên hệ Văn phòng Khoa"
          onClose={() => {
            setFacultyGroupId(null);
            setCopyNotice('');
          }}
        >
          <div className="space-y-4 p-5">
            <p className="text-sm text-slate-500">
              Sao chép nội dung để liên hệ Khoa xử lý. Thao tác này chưa gửi yêu cầu.
            </p>
            <label className="block space-y-2 text-sm">
              <span className="font-medium">Nội dung tham khảo</span>
              <textarea
                readOnly
                value={facultyText}
                rows={9}
                className="w-full rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm leading-6"
              />
            </label>
            <p role="status" className="text-sm text-slate-600">
              {copyNotice}
            </p>
            <div className="flex flex-wrap justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  setFacultyGroupId(null);
                  setCopyNotice('');
                }}
              >
                Đóng
              </Button>
              <Button leftIcon={<Copy />} onClick={() => void copy(facultyText)}>
                Sao chép nội dung
              </Button>
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
}
