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
  GraduationCap,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { StatusBadge } from '@/components/ui/status-badge';
import { SelectableTableRow } from '@/components/ui/selectable-table-row';
import { PageHeader } from '@/components/ui/page-header';
import { Dialog } from '@/components/ui/dialog';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { StatusTabs } from '@/components/ui/status-tabs';
import { TablePagination } from '@/components/ui/table-pagination';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { Skeleton } from '@/components/ui/skeleton';
import { InfoCell } from '@/components/ui/info-cell';
import { NavigationTabs } from '@/components/ui/navigation-tabs';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { TopicStatusBadge } from '../components/TopicStatusBadge';
import { TopicDetailSkeleton } from '../components/TopicDetailSkeleton';
import { TOPIC_STATUS_LABELS } from '../types/topic.types';
import { GROUP_STATUS_LABELS, type AdvisingTab, type TrackingTopic } from '../types/advising.types';
import { advisingApi } from '../services/advisingApi';
import { getTopicErrorMessage } from '../services/topicApi';
import { getAdvisingRows, getConfirmationStatistics, isAdvisingGroup } from '../advising';
import { syllabusApi } from '@/features/thesis/services/syllabusApi';
import { midtermApi } from '@/features/thesis/services/midtermApi';
import { finalReportApi } from '@/features/thesis/services/finalReportApi';
import { milestoneApi } from '@/features/thesis/services/milestoneApi';
import { PROGRESS_REPORT_STATUS_LABELS } from '@/features/thesis/types/milestone.types';
import { SYLLABUS_STATUS_LABELS } from '@/features/thesis/types/syllabus.types';
import { MIDTERM_STATUS_LABELS } from '@/features/thesis/types/midterm.types';
import { FINAL_REPORT_STATUS_LABELS } from '@/features/thesis/types/finalReport.types';
import { SimilarityReportPanel } from '@/features/thesis/components/SimilarityReportPanel';

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
  const detailGroupId =
    detailId && detailQuery.data?.id === detailId ? detailQuery.data.group?.groupId ?? '' : '';
  const groupDetailQuery = useQuery({
    queryKey: ['topic-advising-group-detail', user?.id, detailGroupId],
    queryFn: ({ signal }) => advisingApi.getGroupDetail(detailGroupId, signal),
    enabled: Boolean(user?.id && detailGroupId),
  });
  const progressQuery = useQuery({
    queryKey: ['topic-advising-group-progress', user?.id, detailGroupId],
    queryFn: async ({ signal }) => {
      const [syllabus, midterm, finalReport, milestones] = await Promise.all([
        syllabusApi.getSyllabusHistory(detailGroupId, signal),
        midtermApi.getMidtermHistory(detailGroupId, signal),
        finalReportApi.getFinalReportHistory(detailGroupId, signal),
        milestoneApi.getMilestones(detailGroupId, signal),
      ]);
      const progressReports = await Promise.all(
        milestones.map(async (milestone) => ({
          milestone,
          report: await milestoneApi.getProgressReport(milestone.id, signal),
        })),
      );
      return { syllabus, midterm, finalReport, progressReports };
    },
    enabled: Boolean(user?.id && detailGroupId),
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

  const hasFilters = Boolean(draft.keyword || draft.batchId || draft.status);
  const activeBatchName = batches.find((b) => b.id === (filters.batchId || draft.batchId))?.batchName;

  function removeFilter(key: 'keyword' | 'batchId' | 'status') {
    const next = { ...filters, [key]: '' };
    setFilters(next);
    setDraft((prev) => ({ ...prev, [key]: '' }));
    setPage(1);
    setSelectedRowId(null);
  }

  return (
    <div className="space-y-6 animate-in fade-in-50 duration-300">
      {!embedded && (
        <PageHeader
          compact
          accent
          title="Đề tài hướng dẫn"
          description="Quản lý danh sách đề tài đề xuất, theo dõi nhóm sinh viên đăng ký và tiến độ hướng dẫn khóa luận tốt nghiệp"
          navigation={
            <NavigationTabs
              tabs={tabs.map((item) => ({
                id: item.id,
                label: item.label,
                icon:
                  item.id === 'topics'
                    ? BookOpen
                    : item.id === 'registrations'
                      ? Users
                      : GraduationCap,
              }))}
              activeId={tab}
              onSelect={(id) => changeTab(id as AdvisingTab)}
              ariaLabel="Đề tài hướng dẫn"
              continuityKey="topics-advising-standalone-tabs"
            />
          }
        />
      )}

      {/* Row 1: Search & Filter Card (8 cols) + Bento Guidelines & Stats Card (4 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        <div className="lg:col-span-8">
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs space-y-5 transition-all hover:border-slate-300">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-4">
              <div>
                <h2 className="text-base font-bold text-slate-900 tracking-tight">
                  {tab === 'registrations'
                    ? 'Tìm kiếm & Bộ lọc nhóm đăng ký'
                    : tab === 'advising'
                      ? 'Tìm kiếm & Bộ lọc nhóm hướng dẫn'
                      : 'Tìm kiếm & Bộ lọc đề tài'}
                </h2>
                <p className="text-xs text-slate-500 mt-0.5">
                  {tab === 'registrations'
                    ? 'Lọc danh sách nhóm sinh viên đăng ký đề tài theo từ khóa, đợt khóa luận và trạng thái'
                    : tab === 'advising'
                      ? 'Lọc danh sách nhóm đang hướng dẫn và theo dõi tiến độ khóa luận tốt nghiệp'
                      : 'Lọc danh sách đề tài theo từ khóa, đợt khóa luận và trạng thái'}
                </p>
              </div>
              {hasFilters && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  leftIcon={<FunnelX className="size-3.5" />}
                  onClick={resetFilters}
                  className="h-8 text-xs text-slate-500 hover:text-slate-800 cursor-pointer"
                >
                  Xóa bộ lọc
                </Button>
              )}
            </div>

            <form onSubmit={applyFilters} className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <label className="block space-y-1.5 text-xs font-semibold text-slate-700">
                  <span>Tìm kiếm nhóm hoặc sinh viên</span>
                  <Input
                    aria-label="Tìm đề tài hoặc sinh viên"
                    placeholder="Tên đề tài, sinh viên, MSSV hoặc mã nhóm"
                    leftIcon={<Search className="size-4" />}
                    className="h-10 rounded-xl border-slate-200/90 bg-slate-50/60 shadow-none focus-visible:bg-white text-xs"
                    value={draft.keyword}
                    onChange={(event) => setDraft({ ...draft, keyword: event.target.value })}
                  />
                </label>
                <div className="space-y-1.5 text-xs font-semibold text-slate-700">
                  <span>Đợt khóa luận</span>
                  <Select<string>
                    aria-label="Đợt KLTN"
                    triggerClassName="h-10 rounded-xl border-slate-200/90 bg-slate-50/60 shadow-none focus-visible:bg-white text-xs font-medium"
                    value={draft.batchId}
                    options={[
                      { value: '', label: 'Tất cả đợt khóa luận' },
                      ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
                    ]}
                    onChange={(batchId: string) => setDraft({ ...draft, batchId })}
                    searchable
                    disabled={!canReadBatches || batchesQuery.isPending}
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 items-end">
                <div className="space-y-1.5 text-xs font-semibold text-slate-700">
                  <span>Trạng thái tiến độ</span>
                  <Select<string>
                    aria-label="Trạng thái"
                    triggerClassName="h-10 rounded-xl border-slate-200/90 bg-slate-50/60 shadow-none focus-visible:bg-white text-xs font-medium"
                    value={draft.status}
                    options={[
                      { value: '', label: 'Tất cả trạng thái' },
                      ...statusOptions.map(([value, label]) => ({ value, label })),
                    ]}
                    onChange={(status: string) => setDraft({ ...draft, status })}
                  />
                </div>

                <div className="flex gap-2">
                  <Button
                    type="submit"
                    variant="soft"
                    leftIcon={<SlidersHorizontal className="size-3.5" />}
                    className="h-10 flex-1 rounded-xl text-xs font-semibold shadow-xs"
                  >
                    Áp dụng
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    leftIcon={<FunnelX className="size-3.5" />}
                    className="h-10 rounded-xl text-xs font-semibold border-slate-200 hover:bg-slate-50 px-3.5"
                    onClick={resetFilters}
                  >
                    Bỏ lọc
                  </Button>
                </div>
              </div>
            </form>

            {Boolean(filters.keyword || filters.batchId || filters.status) && (
              <div
                className="flex flex-wrap items-center gap-1.5 pt-3 border-t border-slate-100"
                aria-label="Bộ lọc đang áp dụng"
              >
                <span className="text-[11px] font-medium text-slate-400">Đang lọc theo:</span>
                {filters.keyword && (
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    rightIcon={<X className="size-3" />}
                    onClick={() => removeFilter('keyword')}
                    className="h-7 text-xs rounded-lg"
                    aria-label={`Bỏ lọc từ khóa ${filters.keyword}`}
                  >
                    Từ khóa: {filters.keyword}
                  </Button>
                )}
                {filters.batchId && (
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    rightIcon={<X className="size-3" />}
                    onClick={() => removeFilter('batchId')}
                    className="h-7 text-xs rounded-lg"
                    aria-label="Bỏ lọc đợt KLTN"
                  >
                    {batches.find((b) => b.id === filters.batchId)?.batchName ?? 'Đợt đã chọn'}
                  </Button>
                )}
                {filters.status && (
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    rightIcon={<X className="size-3" />}
                    onClick={() => removeFilter('status')}
                    className="h-7 text-xs rounded-lg"
                    aria-label="Bỏ lọc trạng thái"
                  >
                    {((tab === 'topics' ? TOPIC_STATUS_LABELS : GROUP_STATUS_LABELS) as Record<string, string>)[filters.status] ??
                      filters.status}
                  </Button>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Right Bento Card (4 cols) - Tổng quan & Quy định */}
        <div className="lg:col-span-4">
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs flex flex-col justify-between h-full transition-all hover:border-slate-300">
            <div className="space-y-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-base font-bold text-slate-900 tracking-tight">
                  Tổng quan hướng dẫn
                </h2>
                <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-700 shadow-2xs">
                  {activeBatchName || 'Tất cả đợt khóa luận'}
                </span>
              </div>

              {/* StatisticsBar */}
              <section aria-label="Thống kê hướng dẫn" aria-busy={busy}>
                <StatisticsBar
                  layout="card"
                  className="grid-cols-2 auto-cols-auto grid-flow-row gap-2.5"
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
                      label: 'Sinh viên',
                      value: confirmationStatistics.confirmed ?? unavailable,
                      icon: <CheckCircle2 />,
                      tone: 'muted',
                    },
                  ]}
                />
              </section>

              {/* Policy Rules Key-Value List */}
              <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3.5 space-y-2.5 text-xs">
                <div className="flex items-start justify-between gap-2">
                  <span className="text-slate-500 shrink-0 font-medium">Sinh viên / đề tài:</span>
                  <span className="font-semibold text-slate-800 text-right leading-snug">
                    Tối đa 2 sinh viên
                  </span>
                </div>
                <div className="flex items-start justify-between gap-2 pt-2 border-t border-slate-200/80">
                  <span className="text-slate-500 shrink-0 font-medium">Xác nhận lời mời:</span>
                  <span className="font-semibold text-slate-800 text-right leading-snug">
                    Sinh viên xác nhận trong đợt
                  </span>
                </div>
                <div className="flex items-start justify-between gap-2 pt-2 border-t border-slate-200/80">
                  <span className="text-slate-500 shrink-0 font-medium">Tiến độ khóa luận:</span>
                  <span className="font-semibold text-slate-800 text-right leading-snug">
                    Đề cương, giữa kỳ & báo cáo
                  </span>
                </div>
              </div>
            </div>

            <div className="pt-3 border-t border-slate-100 mt-4 text-[11px] text-slate-400">
              Giảng viên hướng dẫn phản hồi đề cương và duyệt tiến độ của nhóm sinh viên.
            </div>
          </div>
        </div>
      </div>

      {batchesQuery.isError && (
        <p role="alert" className="text-sm text-amber-700">
          Không thể tải danh sách đợt khóa luận.{' '}
          <Button variant="link" size="sm" onClick={() => void batchesQuery.refetch()}>
            Thử lại
          </Button>
        </p>
      )}

      {/* Row 2: Data Table Card */}
      <div className="rounded-2xl border border-slate-200 bg-white overflow-hidden shadow-xs transition-all hover:border-slate-300">
        <div className="p-5 sm:px-6 flex flex-wrap items-center justify-between gap-3 border-b border-slate-100">
          <div>
            <h3 className="text-base font-bold text-slate-900 tracking-tight">
              {tab === 'registrations'
                ? 'Danh sách nhóm đăng ký đề tài'
                : tab === 'advising'
                  ? 'Danh sách nhóm đang hướng dẫn'
                  : 'Danh sách đề tài'}
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              {tab === 'registrations'
                ? 'Theo dõi danh sách các nhóm sinh viên đã gửi đăng ký đề tài và trạng thái phê duyệt'
                : tab === 'advising'
                  ? 'Theo dõi nhóm thực hiện khóa luận, hồ sơ đề cương và các mốc đánh giá tiến độ'
                  : 'Quản lý danh sách các đề tài đã lưu nháp, chờ duyệt hoặc đã công bố'}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <StatusTabs
              ariaLabel="Chuyển chế độ xem đề tài hoặc nhóm"
              activeKey={tab}
              onSelect={(selectedTab) => changeTab(selectedTab as 'topics' | 'advising')}
              loading={query.isPending}
              tabs={[
                {
                  key: 'topics',
                  label: 'Đề tài của tôi',
                  count: topics?.length,
                  icon: <BookOpen className="size-3.5 text-slate-500" />,
                  tone: 'primary',
                },
                {
                  key: 'advising',
                  label: 'Nhóm hướng dẫn',
                  count: topics?.filter(isAdvisingGroup).length,
                  icon: <Users className="size-3.5 text-slate-500" />,
                  tone: 'primary',
                },
              ]}
            />
          </div>
        </div>

        <div aria-busy={busy} className="h-[min(60vh,40rem)] min-h-[20rem] overflow-auto">
          {query.isPending ? (
            <TableSkeleton
              className="min-w-[1120px]"
              columns={
                tab === 'topics'
                  ? [
                      { label: 'Đề tài', variant: 'topic' },
                      { label: 'Đợt khóa luận', width: 'w-40' },
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
              <thead className="sticky top-0 z-10 border-b border-slate-200/80 bg-slate-50/90 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                <tr>
                  {(tab === 'topics'
                    ? ['Đề tài', 'Đợt khóa luận', 'Sinh viên', 'Trạng thái', 'Thao tác']
                    : ['Nhóm', 'Đề tài', 'Sinh viên', 'Tiến độ khóa luận', 'Thao tác']
                  ).map((label) => (
                    <th
                      scope="col"
                      key={label}
                      className={`px-5 py-3.5 font-bold ${label === 'Thao tác' ? 'text-center' : ''}`}
                    >
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {visibleRows.map((topic, index) => (
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
                        <td className="px-5 py-4 tabular-nums text-xs font-semibold text-slate-700">
                          {topic.registeredStudentsCount}/{topic.studentNumber ?? '—'}
                        </td>
                        <td className="px-5 py-4">
                          <TopicStatusBadge status={topic.status} />
                        </td>
                      </>
                    ) : (
                      <>
                        <td className="px-5 py-4">
                          <div className="space-y-1">
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-blue-50 text-blue-700 text-xs font-bold border border-blue-100">
                              <Users className="size-3 text-blue-600" />
                              <span>Nhóm {String((currentPage - 1) * pageSize + index + 1).padStart(2, '0')}</span>
                            </span>
                            <p
                              className="text-[11px] font-mono text-slate-400 truncate max-w-[140px]"
                              title={`Mã nhóm: ${topic.group!.groupId}`}
                            >
                              #{topic.group!.groupId.slice(0, 8)}
                            </p>
                            <p className="text-[11px] text-slate-400">
                              {date(topic.group!.createdAt)}
                            </p>
                          </div>
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
                                titleClassName="font-medium text-xs text-slate-900"
                                descriptionClassName="text-[11px] text-slate-500 font-mono"
                                leading={
                                  <span
                                    aria-hidden="true"
                                    className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-blue-50 text-[10px] font-bold text-blue-600 ring-1 ring-inset ring-blue-100"
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
                        leftIcon={<Eye className="size-3.5" />}
                        disabled={busy}
                        className="h-8 w-28 rounded-xl font-semibold shadow-2xs"
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
            <select
              id="advising-page-size"
              className="sr-only"
              aria-hidden="true"
              tabIndex={-1}
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
            <TablePagination
              pageNumber={currentPage}
              pageSize={pageSize}
              totalCount={total}
              isLoading={busy}
              onPageChange={(next) => {
                setPage(next);
                setSelectedRowId(null);
              }}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPage(1);
                setSelectedRowId(null);
              }}
              itemLabel={tab === 'topics' ? 'đề tài' : 'nhóm'}
            />
          </>
        )}
      </div>
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
                    <dt className="text-xs text-slate-500">Đợt khóa luận</dt>
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
                        <h3 className="font-semibold text-slate-900">
                          Nhóm đăng ký #{detail.group.groupId.slice(0, 8).toUpperCase()}
                        </h3>
                        <p className="mt-1 break-all font-mono text-xs text-slate-400">
                          Mã định danh: {detail.group.groupId}
                        </p>
                        <p className="mt-1 text-xs text-slate-500">
                          Ngày tạo: {date(detail.group.createdAt)}
                        </p>
                      </div>
                      <GroupStatusBadge status={detail.group.status} />
                    </div>
                    {groupDetailQuery.isPending ? (
                      <p className="text-xs text-slate-500">Đang tải trạng thái xác nhận thành viên…</p>
                    ) : groupDetailQuery.isError ? (
                      <p role="alert" className="text-xs text-rose-700">
                        Không tải được trạng thái xác nhận thành viên: {getTopicErrorMessage(groupDetailQuery.error)}
                      </p>
                    ) : null}
                    {(groupDetailQuery.data?.members ?? detail.group.students).map((student) => (
                      <div key={student.studentId} className="rounded-lg bg-slate-50 p-3 text-sm">
                        <p className="font-medium">
                          {student.fullName}{' '}
                          <span className="font-normal text-slate-500">
                            - {student.userCode || 'Chưa có MSSV'}
                          </span>
                        </p>
                        <p className="mt-1 text-xs text-slate-600">
                          Xác nhận: {('confirmationStatus' in student && student.confirmationStatus) || 'Chưa có trạng thái xác nhận riêng'}
                        </p>
                        {'email' in student && <p className="mt-1 break-all text-xs text-slate-500">{student.email || 'Chưa có email'}</p>}
                        {'joinedAt' in student && <p className="mt-1 text-xs text-slate-500">Ghi nhận: {date(student.joinedAt)}</p>}
                      </div>
                    ))}
                    <div className="space-y-3 border-t border-slate-100 pt-4">
                      <h3 className="text-sm font-semibold">Tiến độ hồ sơ</h3>
                      {progressQuery.isPending ? (
                        <p className="text-xs text-slate-500">Đang tải tiến độ…</p>
                      ) : progressQuery.isError ? (
                        <p role="alert" className="text-xs text-rose-700">
                          Không tải được tiến độ nhóm: {getTopicErrorMessage(progressQuery.error)}
                        </p>
                      ) : (
                        <>
                          <div className="grid gap-2 text-sm sm:grid-cols-3">
                            <div className="rounded-lg bg-slate-50 p-3">Đề cương: <strong>{progressQuery.data.syllabus ? SYLLABUS_STATUS_LABELS[progressQuery.data.syllabus.currentStatus] ?? progressQuery.data.syllabus.currentStatus : 'Chưa nộp'}</strong></div>
                            <div className="rounded-lg bg-slate-50 p-3">Giữa kỳ: <strong>{progressQuery.data.midterm ? MIDTERM_STATUS_LABELS[progressQuery.data.midterm.currentStatus] ?? progressQuery.data.midterm.currentStatus : 'Chưa nộp'}</strong></div>
                            <div className="rounded-lg bg-slate-50 p-3">Báo cáo cuối: <strong>{progressQuery.data.finalReport ? FINAL_REPORT_STATUS_LABELS[progressQuery.data.finalReport.currentStatus as keyof typeof FINAL_REPORT_STATUS_LABELS] ?? progressQuery.data.finalReport.currentStatus : 'Chưa nộp'}</strong></div>
                          </div>
                          {progressQuery.data.finalReport && (
                            <SimilarityReportPanel
                              reportId={progressQuery.data.finalReport.finalReportId}
                              versionNo={progressQuery.data.finalReport.currentVersionNo}
                            />
                          )}
                          {progressQuery.data.progressReports.length > 0 && (
                            <div className="space-y-2">
                              <h4 className="text-xs font-semibold text-slate-600">Mốc tiến độ</h4>
                              {progressQuery.data.progressReports.map(({ milestone, report }) => (
                                <div key={milestone.id} className="flex flex-wrap justify-between gap-2 rounded-lg bg-slate-50 p-3 text-sm">
                                  <span>{milestone.title}</span>
                                  <span className="text-slate-600">
                                    {report
                                      ? PROGRESS_REPORT_STATUS_LABELS[report.currentStatus as keyof typeof PROGRESS_REPORT_STATUS_LABELS] ?? report.currentStatus
                                      : 'Chưa nộp'}
                                  </span>
                                </div>
                              ))}
                            </div>
                          )}
                        </>
                      )}
                    </div>
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
