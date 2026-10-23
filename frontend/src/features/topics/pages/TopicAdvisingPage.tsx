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
  RefreshCw,
  Info,
  Copy,
  ArrowUpRight,
  CheckCircle2,
  Clock3,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog } from '@/components/ui/dialog';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { Skeleton } from '@/components/ui/skeleton';
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
    Expired: 'inactive',
    Cancelled: 'error',
  } as const;
  return (
    <Badge variant={tone[status]} className="whitespace-nowrap">
      {REGISTRATION_LABELS[status]}
    </Badge>
  );
}

export function TopicAdvisingPage() {
  const { user } = useAuth();
  const [tab, setTab] = useState<AdvisingTab>('topics');
  const [draft, setDraft] = useState(initialFilters);
  const [filters, setFilters] = useState(initialFilters);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);
  const [detail, setDetail] = useState<{ topicId: string; groupId?: string } | null>(null);
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
                <Badge
                  variant={
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
                </Badge>
              </div>
              <p className="mt-2 break-all text-slate-600">{student.email}</p>
              <p className="mt-1 text-xs text-slate-500">
                Điện thoại: {student.phone} ·{' '}
                {student.registrationRole === 'Registrant'
                  ? 'Không cần xác nhận qua email'
                  : `Xác nhận tham gia: ${date(student.participationConfirmedAt)}`}
              </p>
            </div>
          ))}
        </div>
        {group.status === 'PendingEmail' && (
          <p className="rounded-lg bg-amber-50 p-3 text-sm leading-6 text-amber-800">
            Đang chờ sinh viên được mời xác nhận tham gia qua email. Người đăng ký không cần xác
            nhận lại. Khi thành viên còn lại đồng ý, hệ thống tự động đưa nhóm vào danh sách hướng
            dẫn.
          </p>
        )}
        {group.status === 'Expired' && (
          <p className="rounded-lg bg-slate-50 p-3 text-sm text-slate-600">
            Đăng ký đã hết hạn xác nhận email. Nhóm chưa được ghi nhận vào danh sách hướng dẫn.
          </p>
        )}
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
            Hướng dẫn liên hệ Khoa
          </Button>
        )}
      </section>
    );
  }

  return (
    <div className="min-w-0 space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">
            Đề tài & nhóm hướng dẫn
          </h1>
          <p className="mt-2 text-sm text-slate-500">
            Theo dõi đề tài phụ trách, đăng ký của sinh viên và các nhóm đang hướng dẫn.
          </p>
        </div>
        <Button asChild variant="outline" leftIcon={<ArrowUpRight />}>
          <Link to="/topics/propose">Đề xuất đề tài</Link>
        </Button>
      </div>
      <div className="flex flex-wrap items-center gap-3 rounded-xl border border-amber-200 bg-amber-50/70 px-4 py-3 text-sm text-amber-900">
        <Badge variant="warning">Dữ liệu mẫu</Badge>
        <p className="flex-1">
          Dữ liệu minh họa để xem thử giao diện, chưa phản ánh đăng ký thực tế.
        </p>
        <Button
          variant="ghost"
          size="sm"
          leftIcon={<RefreshCw />}
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          Tải lại
        </Button>
      </div>
      <div className="flex gap-3 rounded-xl border border-blue-100 bg-blue-50/60 p-4 text-sm leading-6 text-slate-600">
        <Info className="mt-1 size-4 shrink-0 text-primary" />
        <p>
          Đăng ký 1 sinh viên được ghi nhận ngay. Với nhóm 2 sinh viên, hệ thống chỉ gửi email mời
          người còn lại xác nhận tham gia; sau khi người này đồng ý, nhóm tự động vào danh sách
          hướng dẫn. Trường hợp cần hủy nhóm, giảng viên liên hệ Văn phòng Khoa xử lý.
        </p>
      </div>
      <StatisticsBar
        loading={query.isPending}
        items={[
          {
            key: 'topics',
            label: 'Đề tài phụ trách',
            value: batchTopics.length,
            icon: <BookOpen />,
            tone: 'primary',
            onClick: () => changeTab('topics'),
            active: tab === 'topics' && !filters.status,
          },
          {
            key: 'pending',
            label: 'Nhóm chờ email',
            value: pendingGroups.length,
            icon: <Mail />,
            tone: 'warning',
            onClick: () => changeTab('registrations', 'PendingEmail'),
            active: tab === 'registrations' && filters.status === 'PendingEmail',
          },
          {
            key: 'advising',
            label: 'Nhóm đang hướng dẫn',
            value: advisingGroups.length,
            icon: <Users />,
            tone: 'primary',
            onClick: () => changeTab('advising'),
            active: tab === 'advising',
          },
          {
            key: 'students',
            label: 'Sinh viên hướng dẫn',
            value: getConfirmedStudentCount(advisingGroups),
            icon: <CheckCircle2 />,
            tone: 'muted',
          },
        ]}
      />
      <nav
        className="flex gap-1 overflow-x-auto border-b border-slate-200"
        aria-label="Danh mục quản lý hướng dẫn"
      >
        {tabs.map((item) => (
          <button
            key={item.id}
            type="button"
            aria-current={tab === item.id ? 'page' : undefined}
            onClick={() => changeTab(item.id)}
            className={`whitespace-nowrap border-b-2 px-4 py-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${tab === item.id ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:bg-slate-100 hover:text-slate-800'}`}
          >
            {item.label}{' '}
            <span className="ml-2 rounded-md bg-slate-100 px-1.5 py-0.5 text-xs text-slate-600">
              {item.id === 'topics'
                ? batchTopics.length
                : item.id === 'registrations'
                  ? batchGroups.length
                  : advisingGroups.length}
            </span>
          </button>
        ))}
      </nav>
      <form
        onSubmit={applyFilters}
        className="grid items-end gap-4 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm md:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr_auto]"
      >
        <label className="space-y-2 text-sm text-slate-700">
          <span>Tìm kiếm</span>
          <Input
            leftIcon={<Search className="size-4" />}
            className="h-10 bg-slate-50/70"
            placeholder="Tên đề tài, mã nhóm, hồ sơ, họ tên hoặc MSSV"
            value={draft.keyword}
            onChange={(event) => setDraft({ ...draft, keyword: event.target.value })}
          />
        </label>
        <div className="space-y-2 text-sm text-slate-700">
          <span>Đợt KLTN</span>
          <Select
            aria-label="Lọc theo đợt KLTN"
            triggerClassName="h-10 bg-slate-50/70"
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
            triggerClassName="h-10 bg-slate-50/70"
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
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 border-b border-slate-100 bg-gradient-to-r from-slate-50/80 to-white px-5 py-4">
          <div>
            <CardTitle className="text-base">
              {tabs.find((item) => item.id === tab)?.label}
            </CardTitle>
            <p className="mt-1 text-xs text-slate-500">
              {tab === 'topics'
                ? 'Chỉ tiêu hiển thị theo số sinh viên đã đăng ký thành công.'
                : tab === 'registrations'
                  ? 'Theo dõi xác nhận email và lịch sử đăng ký của nhóm.'
                  : 'Nhóm đăng ký thành công và chưa hoàn thành khóa luận.'}
            </p>
          </div>
          <Badge variant="secondary">
            {total} {tab === 'topics' ? 'đề tài' : 'nhóm'}
          </Badge>
        </CardHeader>
        <div aria-busy={query.isFetching} className="min-h-64 overflow-x-auto">
          {query.isPending ? (
            <div className="space-y-4 p-5" role="status" aria-label="Đang tải danh sách">
              {[1, 2, 3, 4].map((key) => (
                <Skeleton key={key} className="h-16 w-full" />
              ))}
            </div>
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
            <table className="w-full min-w-[1000px] text-left text-sm">
              <thead className="border-b border-slate-100 bg-slate-50 text-[11px] uppercase tracking-wider text-slate-600">
                <tr>
                  {(tab === 'topics'
                    ? [
                        'Đề tài',
                        'Đợt KLTN',
                        'SV đăng ký thành công',
                        'Trạng thái đề tài',
                        'Thao tác',
                      ]
                    : [
                        'Nhóm / hồ sơ',
                        'Đề tài',
                        'Sinh viên',
                        tab === 'advising' ? 'Tiến độ khóa luận' : 'Trạng thái đăng ký',
                        'Thao tác',
                      ]
                  ).map((label) => (
                    <th scope="col" key={label} className="px-5 py-4 font-bold">
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
                        <tr key={topic.id} className="hover:bg-blue-50/30">
                          <td className="max-w-sm px-5 py-5">
                            <p className="text-xs font-medium text-primary">{topic.code}</p>
                            <button
                              className="mt-1 text-left font-semibold leading-6 text-slate-800 hover:text-primary focus-visible:underline"
                              onClick={() => setDetail({ topicId: topic.id })}
                            >
                              {topic.title}
                            </button>
                            <p className="mt-1 text-xs text-slate-500">
                              {pending
                                ? `${pending} nhóm chờ xác nhận email`
                                : groups.some((group) => group.status === 'Confirmed')
                                  ? 'Đã có nhóm đăng ký thành công'
                                  : 'Chưa có nhóm đăng ký thành công'}
                            </p>
                          </td>
                          <td className="px-5 py-5 text-xs text-slate-500">
                            {data?.batches.find((batch) => batch.id === topic.batchId)?.name}
                          </td>
                          <td className="px-5 py-5">
                            <span className="font-semibold tabular-nums">
                              {count}/{topic.capacity}
                            </span>
                            <p className="mt-1 text-xs text-slate-500">
                              {Math.max(0, topic.capacity - count)} chỗ chưa ghi nhận
                            </p>
                          </td>
                          <td className="px-5 py-5">
                            <TopicStatusBadge status={topic.status} />
                          </td>
                          <td className="px-5 py-5">
                            <Button
                              variant="outline"
                              size="sm"
                              leftIcon={<Eye />}
                              onClick={() => setDetail({ topicId: topic.id })}
                              aria-label={`Xem đề tài ${topic.code}`}
                            >
                              Chi tiết
                            </Button>
                          </td>
                        </tr>
                      );
                    })
                  : groupRows.slice(start, start + pageSize).map((group) => {
                      const topic = topics.find((item) => item.id === group.topicId)!;
                      return (
                        <tr key={group.id} className="hover:bg-blue-50/30">
                          <td className="px-5 py-5">
                            <p className="font-semibold">{group.groupCode}</p>
                            <p className="mt-1 text-xs text-slate-500">{group.code}</p>
                            <p className="mt-1 text-xs text-slate-400">
                              {date(group.registeredAt)}
                            </p>
                          </td>
                          <td className="max-w-xs px-5 py-5">
                            <p className="font-medium leading-6">{topic.title}</p>
                            <p className="mt-1 text-xs text-slate-500">
                              {topic.code} ·{' '}
                              {data?.batches.find((batch) => batch.id === topic.batchId)?.name}
                            </p>
                          </td>
                          <td className="px-5 py-5">
                            <div className="space-y-2">
                              {group.students.map((student) => (
                                <div key={student.id}>
                                  <p className="text-sm">{student.name}</p>
                                  <p className="mt-0.5 text-xs text-slate-500">{student.code}</p>
                                </div>
                              ))}
                            </div>
                          </td>
                          <td className="px-5 py-5">
                            {tab === 'advising' ? (
                              <Badge variant="info">{group.stage}</Badge>
                            ) : (
                              <>
                                <RegistrationBadge status={group.status} />
                                <p className="mt-2 text-xs text-slate-500">
                                  {group.students.length === 1
                                    ? 'Cá nhân · Ghi nhận ngay, không cần email'
                                    : group.status === 'PendingEmail'
                                      ? `Chờ ${group.students.find((student) => student.registrationRole === 'Invitee')?.name ?? 'thành viên được mời'}`
                                      : 'Nhóm 2 sinh viên'}
                                </p>
                              </>
                            )}
                          </td>
                          <td className="px-5 py-5">
                            <Button
                              variant="outline"
                              size="sm"
                              leftIcon={<Eye />}
                              onClick={() => setDetail({ topicId: topic.id, groupId: group.id })}
                              aria-label={`Xem nhóm ${group.groupCode}`}
                            >
                              Chi tiết
                            </Button>
                          </td>
                        </tr>
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
                {[5, 10, 20].map((size) => (
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
                <dt className="text-xs text-slate-500">Giảng viên phụ trách</dt>
                <dd className="mt-1 font-medium">{user?.fullName ?? 'Giảng viên hiện tại'}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Đợt KLTN</dt>
                <dd className="mt-1">
                  {data?.batches.find((batch) => batch.id === selectedTopic.batchId)?.name}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Chỉ tiêu sinh viên</dt>
                <dd className="mt-1">{selectedTopic.capacity} sinh viên</dd>
              </div>
            </dl>
            <section>
              <h3 className="font-semibold">Nội dung & mục tiêu</h3>
              <p className="mt-2 whitespace-pre-line text-sm leading-7 text-slate-600">
                {selectedTopic.description}
              </p>
              <h3 className="mt-4 font-semibold">Yêu cầu thực hiện</h3>
              <p className="mt-2 whitespace-pre-line text-sm leading-7 text-slate-600">
                {selectedTopic.requirements}
              </p>
            </section>
            <section className="space-y-3">
              <h3 className="flex items-center gap-2 font-semibold">
                <Users className="size-4 text-primary" />
                {detail?.groupId ? 'Thông tin nhóm sinh viên' : 'Các nhóm đăng ký'}
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
            <div className="flex gap-3 rounded-xl bg-blue-50 p-4 text-sm leading-6 text-slate-700">
              <Info className="mt-1 size-5 shrink-0 text-primary" />
              <p>
                Giảng viên trao đổi với Văn phòng Khoa qua kênh liên hệ chính thức, cung cấp mã hồ
                sơ, lý do và minh chứng. Khoa xem xét và thực hiện hủy nhóm nếu phù hợp.
              </p>
            </div>
            <p className="text-sm text-slate-600">
              Bạn có thể sao chép nội dung bên dưới để bổ sung và gửi cho Khoa. Thao tác này chỉ sao
              chép văn bản, chưa gửi yêu cầu.
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
      <p className="flex items-center gap-2 text-xs text-slate-500">
        <Clock3 className="size-3.5" />
        Thời gian hiển thị theo giờ Việt Nam (UTC+7).
      </p>
    </div>
  );
}
