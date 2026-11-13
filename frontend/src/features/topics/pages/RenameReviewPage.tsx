import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  CheckCircle2,
  Clock3,
  FileEdit,
  FilterX,
  Layers,
  RefreshCw,
  Search,
  XCircle,
} from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/data-table';
import { Input } from '@/components/ui/input';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { NavigationTabs, type TabItem } from '@/components/ui/navigation-tabs';
import { PageHeader } from '@/components/ui/page-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { StatusBadge } from '@/components/ui/status-badge';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { useAuth } from '@/shared/auth';
import { OperationToast } from '@/shared/notifications';
import {
  RenameReviewDialog,
  type RenameDecisionPayload,
} from '../components/RenameReviewDialog';
import {
  RENAME_LEVEL_CONFIGS,
  RENAME_SCOPE_OPTIONS,
  computeRenameQueueStats,
  filterAndSortRenameRequests,
  formatRenameDate,
  type RenameQueueScope,
  type RenameReviewLevel,
  type RenameSortField,
} from '../renameReview';
import { renameRequestApi } from '../services/renameRequestApi';
import { getTopicErrorMessage } from '../services/topicApi';
import {
  renameStatusLabels,
  renameStatusTones,
  type RenameRequest,
} from '../types/renameRequest.types';

const VALID_LEVELS: RenameReviewLevel[] = ['advisor', 'faculty', 'academic-affairs'];

function isRenameReviewLevel(value: string | null): value is RenameReviewLevel {
  return value !== null && (VALID_LEVELS as string[]).includes(value);
}

function getStepSummary(item: RenameRequest): string {
  switch (item.status) {
    case 'Created':
      return 'Bước 1/3 · Chờ GVHD';
    case 'AdvisorConfirmed':
      return 'Bước 2/3 · Đã qua GVHD';
    case 'FacultyApproved':
      return 'Bước 3/3 · Đã qua Khoa';
    case 'Completed':
      return 'Hoàn tất cả 3 cấp';
    case 'RejectedByAdvisor':
      return 'Dừng ở cấp 1';
    case 'RejectedByFaculty':
      return 'Dừng ở cấp 2';
    case 'RejectedByAA':
      return 'Dừng ở cấp 3';
    default:
      return 'Đã đóng';
  }
}

export function RenameReviewEntryPage() {
  const { hasRole } = useAuth();
  const [params, setParams] = useSearchParams();

  if (hasRole('Admin')) {
    const paramLevel = params.get('level');
    const activeLevel: RenameReviewLevel = isRenameReviewLevel(paramLevel)
      ? paramLevel
      : 'advisor';

    return (
      <RenameReviewView
        level={activeLevel}
        showAdminTabs
        onLevelChange={(nextLevel) => {
          setParams(
            nextLevel === 'advisor' ? {} : { level: nextLevel },
            { flushSync: true },
          );
        }}
      />
    );
  }

  if (hasRole('Lecturer')) return <RenameReviewPage level="advisor" />;
  if (hasRole('Faculty')) return <RenameReviewPage level="faculty" />;
  if (hasRole('AcademicAffairs')) return <RenameReviewPage level="academic-affairs" />;

  return (
    <p role="alert" className="p-6 text-sm text-rose-700">
      Tài khoản của bạn không có quyền duyệt yêu cầu đổi tên đề tài.
    </p>
  );
}

export function RenameReviewPage({ level }: { level: RenameReviewLevel }) {
  const { hasRole } = useAuth();
  const navigate = useNavigate();

  return (
    <RenameReviewView
      level={level}
      showAdminTabs={hasRole('Admin')}
      onLevelChange={(nextLevel) => navigate(`/topics/rename/review/${nextLevel}`)}
      useRouteLinks
    />
  );
}

interface RenameReviewViewProps {
  level: RenameReviewLevel;
  showAdminTabs: boolean;
  onLevelChange?: (level: RenameReviewLevel) => void;
  useRouteLinks?: boolean;
}

function RenameReviewView({
  level,
  showAdminTabs,
  onLevelChange,
  useRouteLinks = false,
}: RenameReviewViewProps) {
  const config = RENAME_LEVEL_CONFIGS[level];
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const [scope, setScope] = useState<RenameQueueScope>('pending');
  const [keyword, setKeyword] = useState('');
  const [batchId, setBatchId] = useState('');
  const [sortBy, setSortBy] = useState<RenameSortField>('createdAt');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [openRequestId, setOpenRequestId] = useState<string | null>(null);
  const [notice, setNotice] = useState('');

  const query = useQuery({
    queryKey: ['rename-requests-review', user?.id],
    queryFn: ({ signal }) => renameRequestApi.list('', signal),
    enabled: Boolean(user?.id),
    staleTime: 0,
  });

  const allItems = useMemo(() => query.data ?? [], [query.data]);
  const stats = useMemo(
    () => computeRenameQueueStats(allItems, config.status),
    [allItems, config.status],
  );

  const batchOptions = useMemo(() => {
    const seen = new Map<string, string>();
    for (const item of allItems) {
      if (item.batchId && item.batchName && !seen.has(item.batchId)) {
        seen.set(item.batchId, item.batchName);
      }
    }
    return [
      { value: '', label: 'Tất cả đợt KLTN' },
      ...Array.from(seen.entries()).map(([id, name]) => ({ value: id, label: name })),
    ];
  }, [allItems]);

  const filteredItems = useMemo(
    () =>
      filterAndSortRenameRequests(allItems, {
        activeStatus: config.status,
        scope,
        keyword,
        batchId,
        sortBy,
        sortOrder,
      }),
    [allItems, batchId, config.status, keyword, scope, sortBy, sortOrder],
  );

  const totalCount = filteredItems.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const safePage = Math.min(page, totalPages);
  const pagedItems = useMemo(() => {
    const start = (safePage - 1) * pageSize;
    return filteredItems.slice(start, start + pageSize);
  }, [filteredItems, pageSize, safePage]);

  const selectedRow = useMemo(
    () => pagedItems.find((item) => item.id === selectedRowId) ?? null,
    [pagedItems, selectedRowId],
  );
  const openRequest = useMemo(
    () => allItems.find((item) => item.id === openRequestId) ?? null,
    [allItems, openRequestId],
  );

  const reviewMutation = useMutation({
    mutationFn: (payload: RenameDecisionPayload) =>
      renameRequestApi.review(payload.request.id, level, {
        decision: payload.decision,
        rejectionReason: payload.rejectionReason ?? null,
      }),
    onSuccess: async (_, payload) => {
      setNotice(
        payload.decision === 'Approve'
          ? level === 'academic-affairs'
            ? 'Đã phê duyệt hoàn tất: tên đề tài mới đã được cập nhật chính thức.'
            : `Đã phê duyệt cấp ${config.number} và chuyển yêu cầu sang bước tiếp theo.`
          : `Đã từ chối yêu cầu đổi tên ở cấp ${config.number}.`,
      );
      setOpenRequestId(null);
      setSelectedRowId(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['rename-requests-review'] }),
        queryClient.invalidateQueries({ queryKey: ['rename-requests-group'] }),
        queryClient.invalidateQueries({ queryKey: ['student-topic-group'] }),
        queryClient.invalidateQueries({ queryKey: ['topics'] }),
        queryClient.invalidateQueries({ queryKey: ['topic-review'] }),
      ]);
    },
  });

  function switchLevel(nextLevel: RenameReviewLevel) {
    setPage(1);
    setSelectedRowId(null);
    reviewMutation.reset();
    if (nextLevel !== level) {
      setScope('pending');
      onLevelChange?.(nextLevel);
    }
  }

  function handleScopeChange(nextScope: RenameQueueScope) {
    setScope(nextScope);
    setPage(1);
    setSelectedRowId(null);
  }

  function handleSortChange(field: RenameSortField) {
    if (sortBy === field) {
      setSortOrder((prev) => (prev === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(field);
      setSortOrder(field === 'newTitleVi' ? 'asc' : 'desc');
    }
    setPage(1);
    setSelectedRowId(null);
  }

  function handleResetFilters() {
    setScope('pending');
    setKeyword('');
    setBatchId('');
    setPage(1);
    setSelectedRowId(null);
  }

  function openDialog(item: RenameRequest) {
    reviewMutation.reset();
    setSelectedRowId(item.id);
    setOpenRequestId(item.id);
  }

  const adminTabs: TabItem[] = VALID_LEVELS.map((lvl) => ({
    id: lvl,
    label: RENAME_LEVEL_CONFIGS[lvl].tabLabel,
    count: stats.levelCounts[lvl],
    ...(useRouteLinks ? { href: `/topics/rename/review/${lvl}` } : {}),
  }));

  const navigation = showAdminTabs ? (
    <NavigationTabs
      tabs={adminTabs}
      activeId={level}
      onSelect={(id) => {
        if (isRenameReviewLevel(id)) switchLevel(id);
      }}
      ariaLabel="Chọn cấp phê duyệt đổi tên đề tài"
      continuityKey="rename-review-level-tabs"
    />
  ) : undefined;

  const hasCustomFilters = Boolean(keyword.trim() || batchId || scope !== 'pending');
  const scopeDescription =
    scope === 'pending'
      ? `Đơn chờ xử lý ở ${config.tabLabel} (${config.priorHint})`
      : scope === 'completed'
        ? 'Các yêu cầu đổi tên đã được phê duyệt hoàn tất cả 3 cấp'
        : scope === 'rejected'
          ? 'Các yêu cầu đổi tên đã bị từ chối trong quy trình'
          : 'Toàn bộ yêu cầu đổi tên thuộc phạm vi theo dõi của bạn';

  const columns: DataTableColumn<RenameRequest, RenameSortField>[] = [
    {
      id: 'stt',
      header: 'STT',
      widthClassName: 'w-14',
      align: 'center',
      headerClassName: 'px-3 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-4',
      skeleton: { variant: 'number' },
      cell: (_, index) => (
        <span className="text-xs font-medium tabular-nums text-slate-400">
          {(safePage - 1) * pageSize + index + 1}
        </span>
      ),
    },
    {
      id: 'titleDiff',
      header: 'Tên đề tài (Cũ → Mới)',
      sortKey: 'newTitleVi',
      title: 'Sắp xếp theo tên đề tài mới',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'topic' },
      cell: (item) => (
        <div className="min-w-[260px] space-y-1.5">
          <div className="space-y-0.5">
            <span
              className="line-clamp-2 block text-sm font-semibold leading-snug text-slate-900"
              title={item.newTitleVi}
            >
              {item.newTitleVi}
            </span>
            {item.newTitleEn && (
              <span
                className="block truncate text-[11px] font-normal text-slate-500"
                title={item.newTitleEn}
              >
                {item.newTitleEn}
              </span>
            )}
          </div>
          <div className="flex items-start gap-1.5 text-xs text-slate-600">
            <span className="mt-0.5 shrink-0 rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold text-slate-600">
              Cũ
            </span>
            <span className="line-clamp-1 leading-relaxed" title={item.oldTitleVi}>
              {item.oldTitleVi}
            </span>
          </div>
        </div>
      ),
    },
    {
      id: 'groupAndAdvisor',
      header: 'Nhóm & GVHD',
      widthClassName: 'w-72',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'person' },
      cell: (item) => {
        const members = item.members ?? [];
        const avatarItems = members.map((m) => ({
          id: m.studentId,
          name: m.fullName || m.userCode || 'Sinh viên',
          userCode: m.userCode,
        }));
        const memberSummary = members
          .map((m) => m.fullName || m.userCode)
          .filter(Boolean)
          .join(', ');

        return (
          <div className="min-w-0 space-y-1.5">
            {avatarItems.length > 0 ? (
              <div className="flex min-w-0 items-center gap-2.5">
                <AvatarGroup items={avatarItems} max={2} size="sm" className="shrink-0" />
                <span
                  className="min-w-0 flex-1 truncate text-xs font-semibold text-slate-800"
                  title={memberSummary}
                >
                  {memberSummary}
                </span>
              </div>
            ) : (
              <span className="block text-xs font-medium text-slate-700">
                Nhóm thực hiện KLTN
              </span>
            )}
            <div className="truncate text-[11px] text-slate-500">
              <span>GVHD: {item.advisorName || 'Theo phân công đề tài'}</span>
              {item.batchName && <span className="text-slate-400"> · {item.batchName}</span>}
            </div>
          </div>
        );
      },
    },
    {
      id: 'status',
      header: 'Trạng thái',
      widthClassName: 'w-44',
      sortKey: 'status',
      title: 'Sắp xếp theo trạng thái phê duyệt',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'badge' },
      cell: (item) => (
        <div className="space-y-1">
          <StatusBadge tone={renameStatusTones[item.status]}>
            {renameStatusLabels[item.status] ?? item.status}
          </StatusBadge>
          <p className="text-[11px] text-slate-500">{getStepSummary(item)}</p>
        </div>
      ),
    },
    {
      id: 'createdAt',
      header: 'Ngày gửi',
      widthClassName: 'w-32',
      sortKey: 'createdAt',
      title: 'Sắp xếp theo thời gian gửi đơn',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-4 py-4',
      skeleton: { variant: 'text' },
      cell: (item) => (
        <span className="text-xs font-medium tabular-nums text-slate-700">
          {formatRenameDate(item.createdAt)}
        </span>
      ),
    },
    {
      id: 'actions',
      header: 'Thao tác',
      widthClassName: 'w-24',
      align: 'center',
      headerClassName: 'px-4 py-3.5 whitespace-nowrap',
      cellClassName: 'px-3 py-4',
      skeleton: { variant: 'actions', actionCount: 1 },
      cell: (item) => (
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-label={`Xem đơn đổi tên ${item.newTitleVi}`}
          onClick={(event) => {
            event.stopPropagation();
            openDialog(item);
          }}
          className="h-8 rounded-md border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 shadow-sm hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900"
        >
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div className="min-w-0 space-y-5 pb-24">
      <PageHeader
        accent
        compact={showAdminTabs}
        title="Duyệt đổi tên đề tài"
        description={config.description}
        navigation={navigation}
      >
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={query.isFetching}
          leftIcon={
            <RefreshCw className={query.isFetching ? 'animate-spin' : undefined} />
          }
          onClick={() => void query.refetch()}
          className="h-9 rounded-xl border-slate-200 bg-white shadow-2xs hover:border-slate-300"
        >
          Làm mới
        </Button>
      </PageHeader>

      <OperationToast message={notice} onConsumed={() => setNotice('')} />

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-[0_1px_3px_rgba(15,23,42,0.03),0_1px_2px_rgba(15,23,42,0.02)]">
        <ListCardHeader
          title="Danh sách yêu cầu đổi tên"
          icon={<FileEdit />}
          description={scopeDescription}
          className="min-h-0 lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]"
        >
          <div className="flex w-full items-center lg:justify-end">
            <StatisticsBar
              loading={query.isPending}
              items={[
                {
                  key: 'pending',
                  label: `Chờ cấp ${config.number}`,
                  value: stats.pendingCount,
                  icon: <Clock3 />,
                  tone: 'primary',
                  active: scope === 'pending',
                  actionLabel: `Lọc đơn chờ phê duyệt cấp ${config.number}`,
                  onClick: () => handleScopeChange('pending'),
                },
                {
                  key: 'completed',
                  label: 'Đã hoàn tất',
                  value: stats.completedCount,
                  icon: <CheckCircle2 />,
                  tone: 'success',
                  active: scope === 'completed',
                  actionLabel: 'Lọc các đơn đã hoàn tất',
                  onClick: () => handleScopeChange('completed'),
                },
                {
                  key: 'rejected',
                  label: 'Đã từ chối',
                  value: stats.rejectedCount,
                  icon: <XCircle />,
                  tone: 'danger',
                  active: scope === 'rejected',
                  actionLabel: 'Lọc các đơn đã từ chối',
                  onClick: () => handleScopeChange('rejected'),
                },
                {
                  key: 'all',
                  label: 'Tất cả đơn',
                  value: stats.totalCount,
                  icon: <Layers />,
                  tone: 'muted',
                  active: scope === 'all',
                  actionLabel: 'Hiển thị tất cả yêu cầu đổi tên',
                  onClick: () => handleScopeChange('all'),
                },
              ]}
            />
          </div>
        </ListCardHeader>

        <div className="flex flex-wrap items-center gap-3 border-b border-slate-100 bg-slate-50/40 px-4 py-3.5 sm:px-6">
          <div className="min-w-60 flex-1">
            <Input
              value={keyword}
              aria-label="Tìm yêu cầu đổi tên đề tài"
              placeholder="Tìm tên đề tài cũ/mới, sinh viên, giảng viên hoặc lý do..."
              leftIcon={<Search />}
              onChange={(event) => {
                setKeyword(event.target.value);
                setPage(1);
                setSelectedRowId(null);
              }}
              className="h-10 rounded-xl shadow-none"
            />
          </div>

          <Select<RenameQueueScope>
            aria-label="Phạm vi trạng thái"
            options={RENAME_SCOPE_OPTIONS}
            value={scope}
            onChange={(nextScope) => handleScopeChange(nextScope as RenameQueueScope)}
            containerClassName="w-max"
            triggerClassName="h-10 w-max rounded-xl"
          />

          {batchOptions.length > 1 && (
            <Select<string>
              aria-label="Lọc theo đợt KLTN"
              options={batchOptions}
              value={batchId}
              onChange={(nextBatchId) => {
                setBatchId(String(nextBatchId ?? ''));
                setPage(1);
                setSelectedRowId(null);
              }}
              containerClassName="w-max"
              triggerClassName="h-10 w-max rounded-xl"
            />
          )}

          {hasCustomFilters && (
            <Button
              type="button"
              variant="outline"
              leftIcon={<FilterX />}
              onClick={handleResetFilters}
              className="h-10 rounded-xl border-slate-200 bg-white text-slate-600 hover:text-slate-900"
            >
              Bỏ lọc
            </Button>
          )}
        </div>

        <DataTable
          data={pagedItems}
          columns={columns}
          getRowId={(item) => item.id}
          ariaLabel="Danh sách yêu cầu đổi tên đề tài"
          selectedRowId={selectedRowId}
          onRowSelect={(id) =>
            setSelectedRowId((prev) => (prev === id ? null : id))
          }
          onRowOpen={openDialog}
          sort={{ field: sortBy, direction: sortOrder }}
          onSortChange={handleSortChange}
          loading={query.isPending}
          busy={query.isFetching && !query.isPending}
          error={
            query.isError
              ? {
                  message: getTopicErrorMessage(query.error),
                  onRetry: () => void query.refetch(),
                }
              : null
          }
          empty={{
            title:
              scope === 'pending' && !keyword.trim() && !batchId
                ? `Chưa có đơn chờ duyệt ở ${config.tabLabel}`
                : 'Không tìm thấy yêu cầu phù hợp',
            description:
              scope === 'pending' && !keyword.trim() && !batchId
                ? 'Khi sinh viên gửi đơn mới hoặc cấp trước hoàn tất xác nhận, yêu cầu sẽ tự động xuất hiện tại đây.'
                : 'Thử thay đổi từ khóa tìm kiếm hoặc chọn phạm vi trạng thái khác để xem thêm đơn.',
            action: hasCustomFilters ? (
              <Button type="button" variant="outline" size="sm" onClick={handleResetFilters}>
                Xem hàng chờ cấp {config.number}
              </Button>
            ) : undefined,
          }}
          pagination={{
            pageNumber: safePage,
            pageSize,
            totalCount,
            onPageChange: (nextPage) => {
              setPage(nextPage);
              setSelectedRowId(null);
            },
            onPageSizeChange: (nextSize) => {
              setPageSize(nextSize);
              setPage(1);
              setSelectedRowId(null);
            },
            isLoading: query.isFetching,
            itemLabel: 'yêu cầu',
          }}
          renderMobileItem={(item) => (
            <div className="space-y-3 rounded-xl border border-slate-200/80 bg-white p-4">
              <div className="flex items-start justify-between gap-2">
                <StatusBadge tone={renameStatusTones[item.status]}>
                  {renameStatusLabels[item.status] ?? item.status}
                </StatusBadge>
                <span className="text-xs font-medium tabular-nums text-slate-500">
                  {formatRenameDate(item.createdAt)}
                </span>
              </div>
              <div className="space-y-1">
                <p className="text-sm font-semibold leading-snug text-slate-900">
                  {item.newTitleVi}
                </p>
                <p className="text-xs text-slate-600 line-clamp-2">
                  Cũ: {item.oldTitleVi}
                </p>
              </div>
              <div className="flex items-center justify-between gap-2 border-t border-slate-100 pt-2.5">
                <span className="truncate text-xs text-slate-500">
                  GVHD: {item.advisorName || 'Theo phân công'}
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => openDialog(item)}
                  className="h-8 rounded-md border-slate-200 px-3 text-xs font-medium"
                >
                  Xem
                </Button>
              </div>
            </div>
          )}
        />
      </Card>

      <TableFloatingBar
        selectedCount={selectedRow && !openRequest ? 1 : 0}
        onClearSelection={() => setSelectedRowId(null)}
      >
        {selectedRow && (
          <Button
            type="button"
            size="sm"
            onClick={() => openDialog(selectedRow)}
            className="h-8 rounded-lg px-3 text-xs font-semibold"
          >
            {selectedRow.status === config.status
              ? `Mở đối chiếu & duyệt cấp ${config.number}`
              : 'Xem chi tiết'}
          </Button>
        )}
      </TableFloatingBar>

      {openRequest && (
        <RenameReviewDialog
          key={openRequest.id}
          request={openRequest}
          levelConfig={config}
          busy={reviewMutation.isPending}
          error={
            reviewMutation.isError ? getTopicErrorMessage(reviewMutation.error) : ''
          }
          onClose={() => {
            if (!reviewMutation.isPending) setOpenRequestId(null);
          }}
          onSubmitDecision={(payload) => reviewMutation.mutate(payload)}
          onSwitchLevel={
            showAdminTabs
              ? (targetLevel) => {
                  switchLevel(targetLevel);
                }
              : undefined
          }
        />
      )}
    </div>
  );
}
