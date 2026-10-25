import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  UsersRound,
  GraduationCap,
  UserPlus,
  Clock3,
  ShieldOff,
  Search,
  SlidersHorizontal,
  Upload,
  X,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { Select } from '@/components/ui/select';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { useAuth } from '@/shared/auth';
import { TableSkeleton } from '@/components/ui/table-skeleton';

import { UserFormDialog } from '../components/users/UserFormDialog';
import { StudentImportDialog } from '../components/users/StudentImportDialog';
import { UsersTable } from '../components/users/UsersTable';
import { IdentityNavigation } from '../components/users/IdentityNavigation';
import { STATUS_LABELS } from '../types/user.types';
import { SYSTEM_ROLES } from '../constants/systemRoles';
import { getUserErrorMessage, userApi } from '../services/userApi';
import { resendActivationApi } from '@/features/auth/services/authApi';

import type {
  AccountStatus,
  AccountGroup,
  SystemRole,
  UsersQuery,
  UserFormValues,
  UserSummary,
} from '../types/user.types';

type UserFilters = Pick<UsersQuery, 'keyword' | 'role' | 'status'>;

const INITIAL_FILTERS: UserFilters = {
  keyword: '',
  role: '',
  status: '',
};

interface UsersPageProps {
  accountGroup: AccountGroup;
}

export function UsersPage({ accountGroup }: UsersPageProps) {
  const isStudent = accountGroup === 'Student';
  const queryClient = useQueryClient();
  const { user: currentUser, hasPermission } = useAuth();

  const canManage = hasPermission('identity.users.manage');
  const canImport = hasPermission('identity.users.import');

  const [draftFilters, setDraftFilters] = useState<UserFilters>(INITIAL_FILTERS);
  const [filters, setFilters] = useState<UserFilters>(INITIAL_FILTERS);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [isImportOpen, setIsImportOpen] = useState(false);

  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserSummary | null>(null);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [notice, setNotice] = useState('');

  const usersQuery = useQuery({
    queryKey: ['admin-users', currentUser?.id, accountGroup, filters, pageNumber, pageSize],
    queryFn: ({ signal }) =>
      userApi.getUsers({ ...filters, accountGroup, pageNumber, pageSize }, signal),
    placeholderData: (previousData, previousQuery) =>
      previousQuery?.queryKey[1] === currentUser?.id && previousQuery?.queryKey[2] === accountGroup
        ? previousData
        : undefined,
  });

  const statisticsQuery = useQuery({
    queryKey: ['admin-users', currentUser?.id, accountGroup, 'statistics'],
    queryFn: ({ signal }) => userApi.getStatistics(accountGroup, signal),
  });

  const statistics = statisticsQuery.isError ? undefined : statisticsQuery.data;
  const formatCount = (count: number | undefined) =>
    count === undefined ? '—' : count.toLocaleString('vi-VN');

  const saveMutation = useMutation({
    mutationFn: async ({ user, values }: { user: UserSummary | null; values: UserFormValues }) => {
      if (user) {
        return userApi.updateUser(user.id, values);
      }

      return userApi.createUser(values);
    },
    onSuccess: async () => {
      setIsFormOpen(false);
      setNotice('Đã lưu tài khoản thành công.');

      await queryClient.invalidateQueries({
        queryKey: ['admin-users'],
      });
    },
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: 'Active' | 'Disabled' }) =>
      userApi.updateStatus(id, status),
    onSuccess: async () => {
      setNotice('Đã cập nhật trạng thái tài khoản.');
      setPageNumber(1);

      await queryClient.invalidateQueries({
        queryKey: ['admin-users'],
      });
    },
  });

  const activationMutation = useMutation({
    mutationFn: async (user: UserSummary) => {
      const response = await resendActivationApi(user.email);
      if (!response.success) throw new Error(response.error.message);
    },
    onSuccess: () => setNotice('Đã gửi email kích hoạt tài khoản.'),
  });

  function quickFilter(status: AccountStatus | '') {
    const next = { ...INITIAL_FILTERS, status };
    setFilters(next);
    setDraftFilters(next);
    setPageNumber(1);
  }
  function handleApplyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFilters({
      ...draftFilters,
      keyword: draftFilters.keyword.trim(),
    });
    setPageNumber(1);
  }

  function clearFilters() {
    setDraftFilters({ ...INITIAL_FILTERS });
    setFilters({ ...INITIAL_FILTERS });
    setPageNumber(1);
  }

  function removeFilter(key: keyof UserFilters) {
    const next = { ...filters, [key]: '' };
    setDraftFilters(next);
    setFilters(next);
    setPageNumber(1);
  }

  function handleOpenForm(user: UserSummary | null) {
    saveMutation.reset();
    statusMutation.reset();
    activationMutation.reset();
    setNotice('');
    setEditingUser(user);
    setIsFormOpen(true);
  }

  function handleChangeStatus(user: UserSummary) {
    if (!canManage || statusMutation.isPending) return;
    if (user.id === currentUser?.id) return;

    // Tài khoản chờ kích hoạt phải hoàn thành luồng kích hoạt.
    if (user.status === 'PendingActivation') return;

    const status: 'Active' | 'Disabled' = user.status === 'Active' ? 'Disabled' : 'Active';

    const action = status === 'Active' ? 'khôi phục truy cập cho' : 'vô hiệu hóa';

    const confirmed = window.confirm(`Bạn có muốn ${action} tài khoản "${user.fullName}"?`);

    if (!confirmed) return;

    saveMutation.reset();
    setNotice('');
    statusMutation.mutate({
      id: user.id,
      status,
    });
  }

  const data = usersQuery.data;
  const isRefreshing = usersQuery.isFetching && !usersQuery.isPending;
  const mutationError = saveMutation.error || statusMutation.error || activationMutation.error;
  const isUpdating =
    saveMutation.isPending || statusMutation.isPending || activationMutation.isPending;
  const hasFilters = Boolean(
    filters.keyword ||
    filters.role ||
    filters.status ||
    draftFilters.keyword ||
    draftFilters.role ||
    draftFilters.status,
  );

  return (
    <div className="min-w-0 space-y-5">
      <PageHeader
        title="Tài khoản và phân quyền"
        icon={<UsersRound />}
        navigation={<IdentityNavigation />}
      >
        {canManage && (
          <Button
            type="button"
            variant="outline"
            disabled={isUpdating}
            onClick={() => handleOpenForm(null)}
            leftIcon={<UserPlus />}
            className="h-10 rounded-lg px-4 shadow-sm"
          >
            {isStudent ? 'Thêm sinh viên' : 'Thêm giảng viên / cán bộ'}
          </Button>
        )}
        {isStudent && canImport && (
          <Button
            type="button"
            disabled={isUpdating}
            onClick={() => setIsImportOpen(true)}
            leftIcon={<Upload />}
            className="h-10 rounded-lg px-4 shadow-sm"
          >
            Nhập Excel
          </Button>
        )}
      </PageHeader>

      {notice && (
        <p role="status" className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700">
          {notice}
        </p>
      )}

      {mutationError && (
        <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
          {getUserErrorMessage(mutationError)}
        </p>
      )}

      {isFormOpen && canManage && (
        <UserFormDialog
          accountGroup={accountGroup}
          key={editingUser?.id || 'create'}
          user={editingUser}
          isSaving={saveMutation.isPending}
          error={saveMutation.error ? getUserErrorMessage(saveMutation.error) : null}
          onClose={() => setIsFormOpen(false)}
          onSave={(values) => {
            saveMutation.mutate({
              user: editingUser,
              values,
            });
          }}
        />
      )}

      {isImportOpen && isStudent && canImport && (
        <StudentImportDialog
          onClose={() => setIsImportOpen(false)}
          onImported={async (count) => {
            setNotice(
              `Đã tạo ${count.toLocaleString('vi-VN')} tài khoản sinh viên chờ kích hoạt. Sinh viên có thể yêu cầu link tại trang kích hoạt.`,
            );
            setPageNumber(1);
            await queryClient.invalidateQueries({ queryKey: ['admin-users'] });
            setIsImportOpen(false);
          }}
        />
      )}

      <form
        onSubmit={handleApplyFilters}
        className={`grid items-end gap-4 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm md:grid-cols-2 ${isStudent ? 'xl:grid-cols-[2fr_1fr_auto]' : 'xl:grid-cols-[2fr_1fr_1fr_auto]'}`}
      >
        <label className="block space-y-2 text-sm text-slate-700">
          <span>Tìm tài khoản</span>
          <Input
            leftIcon={<Search className="size-4" />}
            className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
            placeholder={isStudent ? 'MSSV, họ tên hoặc email' : 'Mã cán bộ, họ tên hoặc email'}
            value={draftFilters.keyword}
            onChange={(event) =>
              setDraftFilters({
                ...draftFilters,
                keyword: event.target.value,
              })
            }
          />
        </label>

        {!isStudent && (
          <div className="space-y-2 text-sm text-slate-700">
            <span>Vai trò</span>
            <Select<SystemRole | ''>
              triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
              aria-label="Lọc theo vai trò"
              placeholder="Tất cả vai trò"
              value={draftFilters.role}
              options={[
                { value: '', label: 'Tất cả vai trò' },
                ...SYSTEM_ROLES.filter((role) => role.roleName !== 'Student').map((role) => ({
                  value: role.roleName,
                  label: role.title,
                })),
              ]}
              onChange={(value: SystemRole | '') =>
                setDraftFilters({ ...draftFilters, role: value })
              }
            />
          </div>
        )}

        <div className="space-y-2 text-sm text-slate-700">
          <span>Trạng thái</span>
          <Select<AccountStatus | ''>
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            aria-label="Lọc theo trạng thái"
            placeholder="Tất cả trạng thái"
            value={draftFilters.status}
            options={[
              { value: '', label: 'Tất cả trạng thái' },
              ...Object.entries(STATUS_LABELS).map(([value, label]) => ({
                value: value as AccountStatus,
                label,
              })),
            ]}
            onChange={(value: AccountStatus | '') =>
              setDraftFilters({ ...draftFilters, status: value })
            }
          />
        </div>

        <div className="flex flex-wrap gap-2">
          <Button
            type="submit"
            variant="soft"
            leftIcon={<SlidersHorizontal />}
            className="h-10 rounded-lg px-5"
          >
            Áp dụng
          </Button>
          <Button
            type="button"
            variant="outline"
            leftIcon={<X />}
            className="h-10 rounded-lg px-4"
            disabled={!hasFilters}
            onClick={clearFilters}
          >
            Bỏ lọc
          </Button>
        </div>
      </form>

      {Boolean(filters.keyword || filters.role || filters.status) && (
        <div className="flex flex-wrap items-center gap-2" aria-label="Bộ lọc đang áp dụng">
          <span className="text-xs text-slate-500">Đang lọc:</span>
          {filters.keyword && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => removeFilter('keyword')}
              aria-label={`Bỏ lọc từ khóa ${filters.keyword}`}
            >
              Từ khóa: {filters.keyword}
            </Button>
          )}
          {filters.role && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => removeFilter('role')}
              aria-label="Bỏ lọc vai trò"
            >
              {SYSTEM_ROLES.find((role) => role.roleName === filters.role)?.title ?? filters.role}
            </Button>
          )}
          {filters.status && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              rightIcon={<X />}
              onClick={() => removeFilter('status')}
              aria-label="Bỏ lọc trạng thái"
            >
              {STATUS_LABELS[filters.status]}
            </Button>
          )}
        </div>
      )}

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <ListCardHeader
          title="Danh sách tài khoản"
          icon={isStudent ? <GraduationCap /> : <UsersRound />}
          description={`${formatCount(data?.totalCount)} ${isStudent ? 'sinh viên' : 'giảng viên / cán bộ'}`}
          className="xl:grid-cols-[minmax(0,1fr)_420px]"
        >
          <section
            className="w-full"
            aria-label="Thống kê tài khoản"
            aria-busy={statisticsQuery.isFetching}
          >
            <StatisticsBar
              loading={statisticsQuery.isPending}
              items={[
                {
                  key: 'total',
                  onClick: () => quickFilter(''),
                  active: !filters.status && !filters.keyword && !filters.role,
                  label: 'Tổng tài khoản',
                  value: formatCount(statistics?.total),
                  icon: <UsersRound />,
                  tone: 'primary',
                },
                {
                  key: 'pending',
                  onClick: () => quickFilter('PendingActivation'),
                  active:
                    filters.status === 'PendingActivation' && !filters.keyword && !filters.role,
                  label: 'Chờ kích hoạt',
                  value: formatCount(statistics?.pendingActivation),
                  icon: <Clock3 />,
                  tone: 'warning',
                },
                {
                  key: 'disabled',
                  onClick: () => quickFilter('Disabled'),
                  active: filters.status === 'Disabled' && !filters.keyword && !filters.role,
                  label: 'Vô hiệu hóa',
                  value: formatCount(statistics?.disabled),
                  icon: <ShieldOff />,
                  tone: 'muted',
                },
              ]}
            />
            {statisticsQuery.isPending && (
              <p role="status" className="sr-only">
                Đang tải thống kê...
              </p>
            )}
            {statisticsQuery.isError && (
              <div role="alert" className="mt-2 flex flex-wrap items-center gap-2">
                <p className="text-xs text-rose-700">Không thể tải thống kê tài khoản.</p>
                <Button
                  type="button"
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
        </ListCardHeader>

        <div
          aria-busy={usersQuery.isFetching}
          className={`h-[min(60vh,40rem)] min-h-[20rem] overflow-auto transition-opacity ${isRefreshing ? 'opacity-60' : ''}`}
        >
          {usersQuery.isPending ? (
            <TableSkeleton
              className="min-w-[1120px]"
              columns={[
                { label: 'STT', width: 'w-14', variant: 'number' },
                { label: isStudent ? 'Sinh viên' : 'Giảng viên / Cán bộ', variant: 'person' },
                {
                  label: isStudent ? 'MSSV' : 'Vai trò / Mã cán bộ',
                  width: 'w-52',
                },
                { label: 'Lần truy cập', width: 'w-36' },
                { label: 'Trạng thái', width: 'w-36', variant: 'badge' },
                { label: 'Thao tác', width: 'w-[280px]', variant: 'actions', actionCount: 2 },
              ]}
            />
          ) : usersQuery.isError ? (
            <div
              role="alert"
              className="flex h-full flex-col items-center justify-center gap-3 p-6 text-center"
            >
              <p className="text-sm text-rose-600">{getUserErrorMessage(usersQuery.error)}</p>
              <Button type="button" variant="outline" onClick={() => void usersQuery.refetch()}>
                Thử lại
              </Button>
            </div>
          ) : data && data.items.length > 0 ? (
            <UsersTable
              accountGroup={accountGroup}
              users={data.items}
              pageNumber={data.pageNumber}
              pageSize={data.pageSize}
              currentUserId={currentUser?.id}
              canManage={canManage}
              isChangingStatus={isUpdating || usersQuery.isFetching}
              onEdit={handleOpenForm}
              selectedRowId={selectedRowId}
              onSelectRow={setSelectedRowId}
              onChangeStatus={handleChangeStatus}
              onResendActivation={(user) => {
                setNotice('');
                activationMutation.mutate(user);
              }}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center text-sm text-slate-500">
              <p>Không có tài khoản phù hợp.</p>
              {hasFilters && (
                <Button type="button" variant="outline" leftIcon={<X />} onClick={clearFilters}>
                  Bỏ lọc
                </Button>
              )}
            </div>
          )}
        </div>

        {data && !usersQuery.isError && (
          <>
            <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-3 text-sm">
              <label htmlFor={`page-size-${accountGroup}`}>Số tài khoản mỗi trang</label>
              <select
                id={`page-size-${accountGroup}`}
                value={pageSize}
                disabled={isUpdating || usersQuery.isFetching}
                className="rounded-md border border-slate-200 bg-white p-2"
                onChange={(event) => {
                  setPageSize(Number(event.target.value));
                  setPageNumber(1);
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
              hasPreviousPage={data.hasPreviousPage}
              hasNextPage={data.hasNextPage}
              isLoading={usersQuery.isFetching || isUpdating}
              itemLabel={isStudent ? 'sinh viên' : 'giảng viên / cán bộ'}
              onPageChange={setPageNumber}
            />
          </>
        )}
      </Card>
    </div>
  );
}
