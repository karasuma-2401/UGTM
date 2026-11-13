import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, Users, CheckCircle2, Mail, Ban } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TableActiveFilters } from '@/components/ui/table';
import { DataTable } from '@/components/ui/data-table';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { advisingApi } from '../services/advisingApi';
import { getTopicErrorMessage } from '../services/topicApi';
import {
  GROUP_STATUS_LABELS,
  type AdvisingFilters,
  type AdvisingGroup,
  type GroupScope,
} from '../types/advising.types';
import { GROUP_SCOPE_LABELS, confirmationLabel, formatAdvisingDate, groupLabel } from '../advising';
import { AdvisingGroupDetail, GroupStatusBadge } from '../components/AdvisingGroupDetail';

const DEFAULT_ADVISING_FILTERS: AdvisingFilters = {
  batchId: '',
  keyword: '',
  scope: 'active',
  status: '',
  confirmation: '',
};

function GroupStudents({ group }: { group: AdvisingGroup }) {
  return (
    <div className="flex items-start gap-3">
      <AvatarGroup
        items={group.students.map((student) => ({
          id: student.studentId,
          name: student.fullName?.trim() || student.userCode || 'Chưa có tên sinh viên',
          userCode: student.userCode,
        }))}
        size="md"
        className="shrink-0"
        ariaLabel="Thành viên nhóm hướng dẫn"
      />
      <div className="min-w-0 space-y-2 break-words">
        {group.students.map((student) => (
          <div key={student.studentId} className="text-sm">
            <p className="font-medium text-slate-800">
              {student.fullName ?? 'Chưa có tên sinh viên'}{' '}
              <span className="text-xs font-normal text-slate-500">
                {student.userCode ?? 'Chưa có MSSV'}
              </span>
            </p>
            {student.confirmationStatus !== 'Confirmed' && (
              <p
                className={
                  student.confirmationStatus === 'Expired'
                    ? 'text-xs text-rose-700'
                    : 'text-xs text-amber-700'
                }
              >
                {confirmationLabel(student.confirmationStatus, student.confirmationExpiresAtUtc)}
              </p>
            )}
          </div>
        ))}
        <p className="text-xs text-slate-500">
          {group.confirmedStudentsCount} đã xác nhận
          {group.pendingConfirmationsCount > 0
            ? ` · ${group.pendingConfirmationsCount} chờ xác nhận`
            : ''}
          {group.expiredInvitationsCount > 0
            ? ` · ${group.expiredInvitationsCount} lời mời hết hạn`
            : ''}
        </p>
      </div>
    </div>
  );
}

export function TopicAdvisingPage({
  initialFilters = DEFAULT_ADVISING_FILTERS,
  onFiltersChange,
}: {
  initialFilters?: AdvisingFilters;
  onFiltersChange?: (filters: AdvisingFilters) => void;
}) {
  const { user, hasPermission } = useAuth();
  const [filters, setFilters] = useState(initialFilters);
  const [draft, setDraft] = useState(initialFilters);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [sortBy, setSortBy] = useState<'createdAt' | 'title' | 'studentCount'>('createdAt');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [detailGroupId, setDetailGroupId] = useState<string | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const batchesQuery = useAcademicBatches();
  const canReadBatches = hasPermission('academic.batches.read');
  const query = useQuery({
    queryKey: ['topic-advising', user?.id, filters, pageNumber, pageSize, sortBy, sortOrder],
    queryFn: ({ signal }) =>
      advisingApi.getGroups({ ...filters, pageNumber, pageSize, sortBy, sortOrder }, signal),
    enabled: Boolean(user?.id),
    refetchInterval: 60_000,
  });
  const data = query.isError ? undefined : query.data;
  const page = data?.page;
  const statistics = data?.statistics;
  const busy = query.isFetching;
  const batches = batchesQuery.data ?? [];
  const batchName =
    batches.find((batch) => batch.id === filters.batchId)?.batchName ??
    page?.items.find((group) => group.batchId === filters.batchId)?.batchName ??
    'Đợt đã chọn';
  const count = (value: number | undefined) =>
    value === undefined ? '—' : value.toLocaleString('vi-VN');

  function apply(next: AdvisingFilters) {
    const normalized = { ...next, keyword: next.keyword.trim() };
    setFilters(normalized);
    setDraft(normalized);
    setPageNumber(1);
    setSelectedRowId(null);
    onFiltersChange?.(normalized);
  }
  function submit(event: FormEvent) {
    event.preventDefault();
    apply(draft);
  }
  function clear() {
    apply({ ...DEFAULT_ADVISING_FILTERS });
  }
  function sort(field: typeof sortBy) {
    setSortBy(field);
    setSortOrder(sortBy === field && sortOrder === 'asc' ? 'desc' : 'asc');
    setPageNumber(1);
    setSelectedRowId(null);
  }
  function open(group: AdvisingGroup) {
    if (busy) return;
    setSelectedRowId(group.groupId);
    setDetailGroupId(group.groupId);
  }
  const hasFilters = Boolean(
    filters.keyword ||
    filters.batchId ||
    filters.status ||
    filters.confirmation ||
    filters.scope !== 'active',
  );
  const statuses = Object.entries(GROUP_STATUS_LABELS).filter(
    ([status]) =>
      draft.scope === 'all' ||
      (draft.scope === 'completed'
        ? status === 'Completed'
        : draft.scope === 'stopped'
          ? status === 'Stopped'
          : !['Stopped', 'Completed'].includes(status)),
  );

  return (
    <div className="space-y-4">
      <section aria-label="Thống kê nhóm theo đợt" className="space-y-2">
        <div className="flex flex-wrap justify-between gap-2 text-sm text-slate-600">
          <p>
            {count(statistics?.total)} nhóm · {filters.batchId ? batchName : 'Tất cả đợt khóa luận'}
          </p>
          <p>
            {count(statistics?.activeConfirmedStudents)} sinh viên đã xác nhận trong nhóm đang thực
            hiện
          </p>
        </div>
        <StatisticsBar
          layout="card"
          className="grid-cols-2 sm:grid-cols-4"
          loading={query.isPending || query.isFetching}
          items={[
            {
              key: 'active',
              label: 'Đang thực hiện',
              value: count(statistics?.active),
              icon: <Users />,
              tone: 'primary',
              active: filters.scope === 'active' && !filters.confirmation,
              onClick: () =>
                apply({ ...filters, scope: 'active', status: '', confirmation: '', keyword: '' }),
            },
            {
              key: 'completed',
              label: 'Đã hoàn thành',
              value: count(statistics?.completed),
              icon: <CheckCircle2 />,
              tone: 'success',
              active: filters.scope === 'completed',
              onClick: () =>
                apply({
                  ...filters,
                  scope: 'completed',
                  status: '',
                  confirmation: '',
                  keyword: '',
                }),
            },
            {
              key: 'stopped',
              label: 'Đã dừng',
              value: count(statistics?.stopped),
              icon: <Ban />,
              tone: 'danger',
              active: filters.scope === 'stopped',
              onClick: () =>
                apply({ ...filters, scope: 'stopped', status: '', confirmation: '', keyword: '' }),
            },
            {
              key: 'pending',
              label: 'Lời mời đang chờ',
              value: count(statistics?.pendingInvitations),
              icon: <Mail />,
              tone: 'warning',
              active: filters.confirmation === 'pending',
              actionLabel: 'Lọc nhóm đang thực hiện có lời mời chờ xác nhận',
              onClick: () =>
                apply({
                  ...filters,
                  scope: 'active',
                  status: '',
                  confirmation: 'pending',
                  keyword: '',
                }),
            },
          ]}
        />
      </section>

      <section className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <form
          onSubmit={submit}
          className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 lg:grid-cols-4"
          aria-label="Bộ lọc nhóm"
        >
          <label className="block space-y-1.5 sm:col-span-2">
            <span className="text-xs font-medium text-slate-700">Tìm nhóm hoặc sinh viên</span>
            <Input
              aria-label="Tìm đề tài hoặc sinh viên"
              maxLength={200}
              placeholder="Tên đề tài, sinh viên, MSSV hoặc mã nhóm"
              leftIcon={<Search className="size-4" />}
              value={draft.keyword}
              onChange={(event) => setDraft({ ...draft, keyword: event.target.value })}
            />
          </label>
          <div className="space-y-1.5">
            <span className="text-xs font-medium text-slate-700">Đợt khóa luận</span>
            <Select<string>
              aria-label="Đợt khóa luận"
              placeholder="Tất cả đợt khóa luận"
              value={draft.batchId}
              options={[
                { value: '', label: 'Tất cả đợt khóa luận' },
                ...batches.map((batch) => ({ value: batch.id, label: batch.batchName })),
              ]}
              onChange={(batchId) => setDraft({ ...draft, batchId })}
              searchable
              disabled={!canReadBatches || batchesQuery.isPending || batchesQuery.isError}
            />
          </div>
          <div className="space-y-1.5">
            <span className="text-xs font-medium text-slate-700">Phạm vi nhóm</span>
            <Select<GroupScope>
              aria-label="Phạm vi nhóm"
              value={draft.scope}
              options={Object.entries(GROUP_SCOPE_LABELS).map(([value, label]) => ({
                value: value as GroupScope,
                label,
              }))}
              onChange={(scope) => setDraft({ ...draft, scope, status: '' })}
            />
          </div>
          <div className="space-y-1.5">
            <span className="text-xs font-medium text-slate-700">Tiến độ khóa luận</span>
            <Select<AdvisingFilters['status']>
              aria-label="Tiến độ khóa luận"
              placeholder="Tất cả tiến độ"
              value={draft.status}
              options={[
                { value: '', label: 'Tất cả tiến độ' },
                ...statuses.map(([value, label]) => ({
                  value: value as AdvisingFilters['status'],
                  label,
                })),
              ]}
              onChange={(status) => setDraft({ ...draft, status })}
            />
          </div>
          <div className="space-y-1.5">
            <span className="text-xs font-medium text-slate-700">Lời mời thành viên</span>
            <Select<AdvisingFilters['confirmation']>
              aria-label="Lời mời thành viên"
              placeholder="Tất cả lời mời"
              value={draft.confirmation}
              options={[
                { value: '', label: 'Tất cả lời mời' },
                { value: 'pending', label: 'Chờ xác nhận' },
                { value: 'expired', label: 'Đã hết hạn' },
              ]}
              onChange={(confirmation) => setDraft({ ...draft, confirmation })}
            />
          </div>
          <div className="flex items-end gap-2 sm:col-span-2">
            <Button type="submit">Áp dụng bộ lọc</Button>
            <Button type="button" variant="outline" onClick={clear}>
              Bỏ lọc
            </Button>
          </div>
        </form>
        {batchesQuery.isError && (
          <div role="alert" className="px-4 pb-3 text-sm text-amber-700">
            Không thể tải danh sách đợt.{' '}
            <Button variant="link" size="sm" onClick={() => void batchesQuery.refetch()}>
              Thử lại danh sách đợt
            </Button>
          </div>
        )}
        {!canReadBatches && (
          <p className="px-4 pb-3 text-xs text-slate-500">
            Bạn chưa có quyền đọc danh sách đợt; vẫn có thể xem các nhóm của mình.
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
                    onRemove: () => apply({ ...filters, keyword: '' }),
                  },
                ]
              : []),
            ...(filters.batchId
              ? [
                  {
                    key: 'batch',
                    label: 'Đợt',
                    value: batchName,
                    onRemove: () => apply({ ...filters, batchId: '' }),
                  },
                ]
              : []),
            ...(filters.scope !== 'active'
              ? [
                  {
                    key: 'scope',
                    label: 'Phạm vi',
                    value: GROUP_SCOPE_LABELS[filters.scope],
                    onRemove: () => apply({ ...filters, scope: 'active', status: '' }),
                  },
                ]
              : []),
            ...(filters.status
              ? [
                  {
                    key: 'status',
                    label: 'Tiến độ',
                    value: GROUP_STATUS_LABELS[filters.status],
                    onRemove: () => apply({ ...filters, status: '' }),
                  },
                ]
              : []),
            ...(filters.confirmation
              ? [
                  {
                    key: 'confirmation',
                    label: 'Lời mời',
                    value: filters.confirmation === 'pending' ? 'Chờ xác nhận' : 'Đã hết hạn',
                    onRemove: () => apply({ ...filters, confirmation: '' }),
                  },
                ]
              : []),
          ]}
          onClearAll={clear}
        />

        <DataTable<AdvisingGroup, typeof sortBy>
          ariaLabel="Danh sách nhóm"
          data={page?.items ?? []}
          getRowId={(group) => group.groupId}
          selectedRowId={selectedRowId}
          onRowSelect={setSelectedRowId}
          onRowOpen={open}
          busy={busy}
          loading={query.isPending}
          error={
            query.isError
              ? { message: getTopicErrorMessage(query.error), onRetry: () => void query.refetch() }
              : null
          }
          sort={{ field: sortBy, direction: sortOrder }}
          onSortChange={sort}
          columns={[
            {
              id: 'title',
              header: 'Đề tài & nhóm',
              sortKey: 'title',
              skeleton: { variant: 'topic' },
              cellClassName: 'px-4 py-4',
              cell: (group) => (
                <>
                  <p className="text-sm font-semibold text-slate-900">{group.titleVi}</p>
                  <p className="mt-1 text-xs text-slate-500">
                    {group.batchName ?? 'Chưa có tên đợt'}
                  </p>
                  <p title={group.groupId} className="mt-1 text-xs text-slate-500">
                    {groupLabel(group.groupId)} · {formatAdvisingDate(group.createdAt)}
                  </p>
                </>
              ),
            },
            {
              id: 'students',
              header: 'Thành viên',
              widthClassName: 'w-[28%]',
              sortKey: 'studentCount',
              title: 'Sắp xếp theo số sinh viên đã xác nhận',
              skeleton: { variant: 'person' },
              cellClassName: 'px-4 py-4',
              cell: (group) => <GroupStudents group={group} />,
            },
            {
              id: 'status',
              header: 'Tiến độ khóa luận',
              widthClassName: 'w-[24%]',
              skeleton: { variant: 'badge' },
              cellClassName: 'px-4 py-4',
              cell: (group) => <GroupStatusBadge status={group.status} />,
            },
            {
              id: 'actions',
              header: 'Thao tác',
              widthClassName: 'w-28',
              headerClassName: 'whitespace-nowrap',
              align: 'center',
              skeleton: { variant: 'actions' },
              cellClassName: 'px-3 py-4',
              cell: (group) => (
                <Button
                  variant="outline"
                  size="sm"
                  className="rounded-md shadow-sm"
                  disabled={busy}
                  onClick={() => open(group)}
                  aria-label={`Xem ${groupLabel(group.groupId)}: ${group.titleVi}`}
                >
                  Xem
                </Button>
              ),
            },
          ]}
          empty={{
            icon: <Users className="size-8" />,
            title:
              pageNumber > 1 && page?.totalCount
                ? 'Trang này không còn nhóm'
                : 'Không có nhóm phù hợp',
            description: hasFilters
              ? 'Thử đổi phạm vi hoặc bỏ các bộ lọc đang áp dụng.'
              : 'Các nhóm thực hiện đề tài của bạn sẽ xuất hiện tại đây.',
            action:
              pageNumber > 1 ? (
                <Button variant="outline" onClick={() => setPageNumber(1)}>
                  Về trang đầu
                </Button>
              ) : hasFilters ? (
                <Button variant="outline" onClick={clear}>
                  Bỏ lọc
                </Button>
              ) : undefined,
          }}
          pagination={
            page
              ? {
                  pageNumber: page.pageNumber,
                  pageSize: page.pageSize,
                  totalCount: page.totalCount,
                  onPageChange: (next) => {
                    setPageNumber(next);
                    setSelectedRowId(null);
                  },
                  onPageSizeChange: (size) => {
                    setPageSize(size);
                    setPageNumber(1);
                    setSelectedRowId(null);
                  },
                  itemLabel: 'nhóm',
                }
              : undefined
          }
          renderMobileItem={(group) => (
            <>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span title={group.groupId} className="text-xs font-medium text-slate-600">
                  {groupLabel(group.groupId)}
                </span>
                <GroupStatusBadge status={group.status} />
              </div>
              <p className="text-sm font-semibold leading-6 text-slate-900">{group.titleVi}</p>
              <p className="text-xs text-slate-500">{group.batchName ?? 'Chưa có tên đợt'}</p>
              <GroupStudents group={group} />
              <div className="flex items-center justify-between gap-3">
                <span className="text-xs text-slate-500">
                  {formatAdvisingDate(group.createdAt)}
                </span>
                <Button
                  variant="outline"
                  className="min-h-10 rounded-md shadow-sm"
                  disabled={busy}
                  onClick={() => open(group)}
                  aria-label={`Xem ${groupLabel(group.groupId)}: ${group.titleVi}`}
                >
                  Xem
                </Button>
              </div>
            </>
          )}
        />
      </section>
      {detailGroupId && (
        <AdvisingGroupDetail
          key={`${user?.id}:${detailGroupId}`}
          groupId={detailGroupId}
          onClose={() => setDetailGroupId(null)}
        />
      )}
    </div>
  );
}
