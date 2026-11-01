import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
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
  CheckCircle2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { StatusBadge } from '@/components/ui/status-badge';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { Card } from '@/components/ui/card';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { Dialog } from '@/components/ui/dialog';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { Skeleton } from '@/components/ui/skeleton';
import { InfoCell } from '@/components/ui/info-cell';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { TopicStatusBadge } from '../components/TopicStatusBadge';
import { TopicDetailSkeleton } from '../components/TopicDetailSkeleton';
import { TOPIC_STATUS_LABELS } from '../types/topic.types';
import { GROUP_STATUS_LABELS, type AdvisingTab, type TrackingTopic } from '../types/advising.types';
import { advisingApi } from '../services/advisingApi';
import { getTopicErrorMessage } from '../services/topicApi';
import { getAdvisingRows, getConfirmationStatistics, isAdvisingGroup } from '../advising';

const PAGE_SIZES = [10, 20, 50] as const;
const tabs: { id: AdvisingTab; label: string }[] = [
  { id: 'topics', label: 'Danh sách đề tài' },
  { id: 'registrations', label: 'Nhóm đăng ký' },
  { id: 'advising', label: 'Đang hướng dẫn' },
];
const date = (value: string) =>
  new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date(value));

function GroupStatusBadge({ status }: { status: string }) {
  const tone =
    status === 'Stopped'
      ? 'error'
      : status === 'SyllabusRevisionRequired'
        ? 'warning'
        : ['Completed', 'Defended', 'ScoreLocked'].includes(status)
          ? 'success'
          : 'info';
  return <StatusBadge tone={tone}>{GROUP_STATUS_LABELS[status] ?? status}</StatusBadge>;
}

const unavailable = <span title="Chưa có dữ liệu xác nhận thành viên và lời mời">—</span>;

export function TopicAdvisingPage({
  embedded = false,
  initialTab = 'topics',
  initialStatus = '',
  initialBatchId = '',
  onQuickFilter,
}: {
  embedded?: boolean;
  initialTab?: AdvisingTab;
  initialStatus?: string;
  initialBatchId?: string;
  onQuickFilter?: (tab: AdvisingTab, status: string, batchId: string) => void;
} = {}) {
  const { user, hasPermission } = useAuth();
  const [tab, setTab] = useState<AdvisingTab>(initialTab);
  const initialFilters = {
    keyword: '',
    batchId: initialBatchId,
    status: Object.hasOwn(
      initialTab === 'topics' ? TOPIC_STATUS_LABELS : GROUP_STATUS_LABELS,
      initialStatus,
    )
      ? initialStatus
      : '',
  };
  const [draft, setDraft] = useState(initialFilters);
  const [filters, setFilters] = useState(initialFilters);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(PAGE_SIZES[0]);
  const [detailId, setDetailId] = useState<string | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [facultyTopic, setFacultyTopic] = useState<TrackingTopic | null>(null);
  const [copyNotice, setCopyNotice] = useState('');
  const batchesQuery = useAcademicBatches();
  const batches = batchesQuery.data ?? [];
  const canReadBatches = hasPermission('academic.batches.read');
  const query = useQuery({
    queryKey: ['topic-advising', user?.id, filters.batchId],
    queryFn: ({ signal }) => advisingApi.getTopics(user!.id, filters.batchId, signal),
    enabled: Boolean(user?.id),
    refetchInterval: 60_000,
  });
  const detailQuery = useQuery({
    queryKey: ['topic-advising-detail', user?.id, detailId],
    queryFn: ({ signal }) => advisingApi.getDetail(user!.id, detailId!, signal),
    enabled: Boolean(user?.id && detailId),
    staleTime: 0,
  });
  // Do not present stale counts as current data after a failed refresh.
  const topics = query.isError ? undefined : query.data;
  const confirmationStatistics = getConfirmationStatistics(topics);
  const rows = getAdvisingRows(topics ?? [], tab, filters.keyword, filters.status);
  const total = rows.length;
  const currentPage = Math.min(page, Math.max(1, Math.ceil(total / pageSize)));
  const visibleRows = rows.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const detail = detailQuery.isError ? undefined : detailQuery.data;
  const busy = query.isFetching;
  const statusOptions = Object.entries(
    tab === 'topics' ? TOPIC_STATUS_LABELS : GROUP_STATUS_LABELS,
  ).filter(([status]) => tab !== 'advising' || !['Completed', 'Stopped'].includes(status));
  const facultyText = facultyTopic?.group
    ? `Kính gửi Văn phòng Khoa,\nTôi là ${user?.fullName ?? ''}. Tôi cần trao đổi về nhóm ${facultyTopic.group.groupId}, đề tài ${facultyTopic.titleVi} (${facultyTopic.id}).\nSinh viên: ${facultyTopic.group.students.map((s) => `${s.fullName} (${s.userCode ?? s.studentId})`).join(', ')}.\nLý do đề nghị xử lý: [Giảng viên bổ sung lý do và minh chứng].\nKính nhờ Khoa xem xét và xử lý theo quy định.`
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
    setDetailId(null);
    setSelectedRowId(null);
  }
  function resetFilters() {
    const cleared = { keyword: '', batchId: '', status: '' };
    setFilters(cleared);
    setDraft(cleared);
    setPage(1);
    setSelectedRowId(null);
  }
  function applyFilters(event: FormEvent) {
    event.preventDefault();
    setFilters({ ...draft, keyword: draft.keyword.trim() });
    setPage(1);
    setSelectedRowId(null);
  }
  async function copy() {
    try {
      await navigator.clipboard.writeText(facultyText);
      setCopyNotice('Đã sao chép nội dung.');
    } catch {
      setCopyNotice('Không thể sao chép tự động. Bạn có thể chọn và sao chép nội dung bên dưới.');
    }
  }
  function batchName(id: string) {
    const name = batches.find((batch) => batch.id === id)?.batchName;
    if (name) return name;
    if (canReadBatches && batchesQuery.isPending)
      return <Skeleton className="h-4 w-28 max-w-full" />;
    return <span title={id}>Mã đợt: {id}</span>;
  }
  function openDetail(id: string) {
    setSelectedRowId(id);
    setDetailId(id);
  }

  return (
    <div className="min-w-0 space-y-5">
      {!embedded && (
        <PageHeader
          title="Đề tài hướng dẫn"
          icon={<BookOpen />}
          navigation={
            <nav className="flex gap-1 overflow-x-auto" aria-label="Đề tài hướng dẫn">
              {tabs.map((item) => (
                <Button
                  key={item.id}
                  type="button"
                  variant={tab === item.id ? 'secondary' : 'ghost'}
                  onClick={() => changeTab(item.id)}
                >
                  {item.label}
                </Button>
              ))}
            </nav>
          }
        />
      )}
      <form
        onSubmit={applyFilters}
        className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-white p-4"
      >
        <div className="min-w-60 flex-1">
          <Input
            aria-label="Tìm đề tài hoặc sinh viên"
            placeholder="Tên đề tài, sinh viên, MSSV hoặc mã nhóm"
            leftIcon={<Search />}
            value={draft.keyword}
            onChange={(event) => setDraft({ ...draft, keyword: event.target.value })}
          />
        </div>
        <Select
          aria-label="Đợt KLTN"
          value={draft.batchId}
          containerClassName="w-full sm:w-56"
          disabled={!canReadBatches || batchesQuery.isPending}
          options={[
            { value: '', label: 'Tất cả đợt KLTN' },
            ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
          ]}
          onChange={(batchId: string) => setDraft({ ...draft, batchId })}
        />
        <Select
          aria-label="Trạng thái"
          value={draft.status}
          containerClassName="w-full sm:w-56"
          options={[
            { value: '', label: 'Tất cả trạng thái' },
            ...statusOptions.map(([value, label]) => ({ value, label })),
          ]}
          onChange={(status: string) => setDraft({ ...draft, status })}
        />
        <Button type="submit" leftIcon={<SlidersHorizontal />}>
          Áp dụng
        </Button>
        <Button type="button" variant="outline" leftIcon={<FunnelX />} onClick={resetFilters}>
          Bỏ lọc
        </Button>
      </form>
      {batchesQuery.isError && (
        <p role="alert" className="text-sm text-amber-700">
          Không thể tải danh sách đợt KLTN.{' '}
          <Button variant="link" size="sm" onClick={() => void batchesQuery.refetch()}>
            Thử lại
          </Button>
        </p>
      )}
      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <ListCardHeader
          title={tabs.find((item) => item.id === tab)?.label}
          icon={<Users />}
          description={
            topics ? `${total} ${tab === 'topics' ? 'đề tài' : 'nhóm'}` : 'Đang cập nhật danh sách'
          }
        >
          <StatisticsBar
            loading={query.isPending}
            items={[
              {
                key: 'topics',
                label: 'Tổng đề tài',
                value: topics?.length ?? '—',
                icon: <BookOpen />,
                tone: 'primary',
                onClick: () => changeTab('topics'),
                active: tab === 'topics' && !filters.status && !filters.keyword,
              },
              {
                key: 'pending',
                label: 'Chờ xác nhận',
                value: confirmationStatistics.pending ?? unavailable,
                icon: <Mail />,
                tone: 'warning',
              },
              {
                key: 'advising',
                label: 'Nhóm hướng dẫn',
                value: topics?.filter(isAdvisingGroup).length ?? '—',
                icon: <Users />,
                tone: 'primary',
                onClick: () => changeTab('advising'),
                active: tab === 'advising' && !filters.status && !filters.keyword,
              },
              {
                key: 'students',
                label: 'SV đã xác nhận',
                value: confirmationStatistics.confirmed ?? unavailable,
                icon: <CheckCircle2 />,
                tone: 'muted',
              },
            ]}
          />
        </ListCardHeader>
        <div aria-busy={busy} className="h-[min(60vh,40rem)] min-h-[20rem] overflow-auto">
          {query.isPending ? (
            <TableSkeleton
              className="min-w-[1120px]"
              columns={
                tab === 'topics'
                  ? [
                      { label: 'Đề tài', variant: 'topic' },
                      { label: 'Đợt KLTN', width: 'w-40' },
                      { label: 'Sinh viên', width: 'w-24', variant: 'number' },
                      { label: 'Trạng thái', width: 'w-56', variant: 'badge' },
                      { label: 'Thao tác', width: 'w-44', variant: 'actions' },
                    ]
                  : [
                      { label: 'Nhóm', width: 'w-44' },
                      { label: 'Đề tài', variant: 'topic' },
                      { label: 'Sinh viên', width: 'w-48', variant: 'person' },
                      { label: 'Tiến độ khóa luận', width: 'w-56', variant: 'badge' },
                      { label: 'Thao tác', width: 'w-44', variant: 'actions' },
                    ]
              }
            />
          ) : query.isError ? (
            <div role="alert" className="space-y-3 p-10 text-center">
              <p className="text-sm text-rose-700">{getTopicErrorMessage(query.error)}</p>
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
                    : ['Nhóm', 'Đề tài', 'Sinh viên', 'Tiến độ khóa luận', 'Thao tác']
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
                {visibleRows.map((topic) => (
                  <SelectableTableRow
                    key={topic.id}
                    selected={selectedRowId === topic.id}
                    onSelect={() => setSelectedRowId(topic.id)}
                    disabled={busy}
                  >
                    {tab === 'topics' ? (
                      <>
                        <td className="px-5 py-4">
                          <p className="line-clamp-2 font-semibold leading-6 text-slate-800">
                            {topic.titleVi}
                          </p>
                        </td>
                        <td className="px-5 py-4 text-xs text-slate-500">
                          {batchName(topic.batchId)}
                        </td>
                        <td className="px-5 py-4 tabular-nums">
                          {topic.registeredStudentsCount}/{topic.studentNumber ?? '—'}
                        </td>
                        <td className="px-5 py-4">
                          <TopicStatusBadge status={topic.status} />
                        </td>
                      </>
                    ) : (
                      <>
                        <td className="px-5 py-4">
                          <Badge variant="info" className="rounded-lg">
                            Nhóm
                          </Badge>
                          <p
                            className="mt-2 truncate font-mono text-xs text-slate-500"
                            title={topic.group!.groupId}
                          >
                            {topic.group!.groupId}
                          </p>
                          <p className="mt-1 text-xs text-slate-400">
                            {date(topic.group!.createdAt)}
                          </p>
                        </td>
                        <td className="px-5 py-4">
                          <p
                            className="line-clamp-2 font-semibold leading-6 text-slate-800"
                            title={topic.titleVi}
                          >
                            {topic.titleVi}
                          </p>
                          <div className="mt-1 text-xs text-slate-500">
                            {batchName(topic.batchId)}
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <div className="space-y-2">
                            {topic.group!.students.map((student) => (
                              <InfoCell
                                key={student.studentId}
                                title={student.fullName}
                                description={student.userCode ?? 'Chưa có MSSV'}
                                titleClassName="font-medium"
                                leading={
                                  <span
                                    aria-hidden="true"
                                    className="flex size-7 shrink-0 items-center justify-center rounded-full bg-slate-100 text-[10px] font-semibold text-slate-500"
                                  >
                                    {student.fullName
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
                          <GroupStatusBadge status={topic.group!.status} />
                        </td>
                      </>
                    )}
                    <td className="px-5 py-4 text-center">
                      <Button
                        variant="soft"
                        size="sm"
                        leftIcon={<Eye />}
                        disabled={busy}
                        className="h-9 w-28 rounded-lg shadow-none"
                        onClick={() => openDetail(topic.id)}
                        aria-label={`Xem chi tiết ${topic.titleVi}`}
                      >
                        Chi tiết
                      </Button>
                    </td>
                  </SelectableTableRow>
                ))}
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
              pageNumber={currentPage}
              pageSize={pageSize}
              totalCount={total}
              isLoading={busy}
              onPageChange={(next) => {
                setPage(next);
                setSelectedRowId(null);
              }}
              itemLabel={tab === 'topics' ? 'đề tài' : 'nhóm'}
            />
          </>
        )}
      </Card>
      {detailId && (
        <Dialog
          title={tab === 'topics' ? 'Chi tiết đề tài hướng dẫn' : 'Chi tiết nhóm đăng ký'}
          onClose={() => setDetailId(null)}
          className="max-w-4xl"
        >
          {detailQuery.isPending ? (
            <TopicDetailSkeleton />
          ) : detailQuery.isError ? (
            <div role="alert" className="space-y-3 p-6">
              <p className="text-sm text-rose-700">{getTopicErrorMessage(detailQuery.error)}</p>
              <Button variant="outline" onClick={() => void detailQuery.refetch()}>
                Thử lại
              </Button>
            </div>
          ) : (
            detail && (
              <div className="space-y-6 p-5 sm:p-6">
                <div>
                  <TopicStatusBadge status={detail.status} />
                  <h2 className="mt-3 text-xl font-bold leading-8 text-slate-900">
                    {detail.titleVi}
                  </h2>
                  {detail.titleEn && (
                    <p className="mt-1 text-sm text-slate-500">{detail.titleEn}</p>
                  )}
                </div>
                <dl className="grid gap-4 rounded-xl bg-slate-50 p-4 text-sm sm:grid-cols-3">
                  <div>
                    <dt className="text-xs text-slate-500">Giảng viên hướng dẫn</dt>
                    <dd className="mt-1 font-medium">{user?.fullName}</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-slate-500">Đợt KLTN</dt>
                    <dd className="mt-1">{batchName(detail.batchId)}</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-slate-500">Sinh viên / chỉ tiêu</dt>
                    <dd className="mt-1">
                      {detail.registeredStudentsCount}/{detail.studentNumber ?? '—'}
                    </dd>
                  </div>
                </dl>
                <section>
                  <h3 className="text-sm font-semibold text-slate-800">Mục tiêu và mô tả</h3>
                  <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-7 text-slate-600">
                    {detail.description || 'Chưa có nội dung.'}
                  </p>
                  <h3 className="mt-4 text-sm font-semibold text-slate-800">
                    Yêu cầu công nghệ và kiến thức
                  </h3>
                  <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-7 text-slate-600">
                    {detail.requirements || 'Chưa có nội dung.'}
                  </p>
                </section>
                {detail.group ? (
                  <section className="space-y-4 rounded-xl border border-slate-200 p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="min-w-0">
                        <h3 className="font-semibold">Nhóm đăng ký</h3>
                        <p className="mt-1 break-all font-mono text-xs text-slate-500">
                          {detail.group.groupId}
                        </p>
                        <p className="mt-1 text-xs text-slate-500">
                          Ngày tạo: {date(detail.group.createdAt)}
                        </p>
                      </div>
                      <GroupStatusBadge status={detail.group.status} />
                    </div>
                    <p className="text-xs text-slate-500">
                      Chưa có trạng thái xác nhận riêng của từng thành viên.
                    </p>
                    {detail.group.students.map((student) => (
                      <div key={student.studentId} className="rounded-lg bg-slate-50 p-3 text-sm">
                        <p className="font-medium">
                          {student.fullName}{' '}
                          <span className="font-normal text-slate-500">
                            - {student.userCode ?? 'Chưa có MSSV'}
                          </span>
                        </p>
                        <p className="mt-1 break-all text-slate-600">
                          {student.email || 'Chưa có email'}
                        </p>
                        <p className="mt-1 text-xs text-slate-500">
                          {student.phoneNumber || 'Chưa có số điện thoại'} - Ghi nhận:{' '}
                          {date(student.joinedAt)}
                        </p>
                      </div>
                    ))}
                    {isAdvisingGroup(detail) && (
                      <Button
                        variant="outline"
                        size="sm"
                        leftIcon={<Info />}
                        onClick={() => {
                          setFacultyTopic(detail);
                          setCopyNotice('');
                        }}
                      >
                        Liên hệ Khoa
                      </Button>
                    )}
                  </section>
                ) : (
                  <p className="rounded-xl border border-dashed border-slate-200 p-6 text-center text-sm text-slate-500">
                    Chưa có nhóm đăng ký đề tài này.
                  </p>
                )}
                <div className="flex justify-end border-t border-slate-100 pt-4">
                  <Button variant="outline" onClick={() => setDetailId(null)}>
                    Đóng
                  </Button>
                </div>
              </div>
            )
          )}
        </Dialog>
      )}
      {facultyTopic && (
        <Dialog title="Liên hệ Văn phòng Khoa" onClose={() => setFacultyTopic(null)}>
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
              <Button variant="outline" onClick={() => setFacultyTopic(null)}>
                Đóng
              </Button>
              <Button leftIcon={<Copy />} onClick={() => void copy()}>
                Sao chép nội dung
              </Button>
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
}
