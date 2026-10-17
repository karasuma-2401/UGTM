import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { UsersRound, UserPlus, Clock3, ShieldOff, Search, SlidersHorizontal } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { useAuth } from '@/shared/auth';

import { UserFormDialog } from '../components/UserFormDialog';
import { UsersTable } from '../components/UsersTable';
import { STATUS_LABELS } from '../types/user.types';
import { SYSTEM_ROLES } from '../constants/systemRoles';
import { getUserErrorMessage, userApi } from '../services/userApi';

import type {
  AccountStatus,
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

const PAGE_SIZE = 10;

export function UsersPage() {
  const queryClient = useQueryClient();
  const { user: currentUser, hasPermission } = useAuth();

  const canManage = hasPermission('identity.users.manage');

  const [draftFilters, setDraftFilters] = useState<UserFilters>(INITIAL_FILTERS);
  const [filters, setFilters] = useState<UserFilters>(INITIAL_FILTERS);
  const [pageNumber, setPageNumber] = useState(1);

  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserSummary | null>(null);
  const [notice, setNotice] = useState('');

  const usersQuery = useQuery({
    queryKey: ['admin-users', filters, pageNumber],
    queryFn: ({ signal }) =>
      userApi.getUsers({ ...filters, pageNumber, pageSize: PAGE_SIZE }, signal),
  });

  const statisticsQuery = useQuery({
    queryKey: ['admin-users', 'statistics'],
    queryFn: ({ signal }) => userApi.getStatistics(signal),
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

      await queryClient.invalidateQueries({
        queryKey: ['admin-users'],
      });
    },
  });

  function handleApplyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFilters({
      ...draftFilters,
      keyword: draftFilters.keyword.trim(),
    });
    setPageNumber(1);
  }

  function handleOpenForm(user: UserSummary | null) {
    saveMutation.reset();
    statusMutation.reset();
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
  const mutationError = saveMutation.error || statusMutation.error;
  const isUpdating = saveMutation.isPending || statusMutation.isPending;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-start gap-3.5">
          <div className="hidden size-12 shrink-0 items-center justify-center rounded-2xl border border-primary/15 bg-primary/10 text-primary sm:flex">
            <UsersRound className="size-6" aria-hidden="true" />
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              Tài khoản & quyền truy cập
            </h1>
            <p className="mt-2 text-sm text-slate-500">
              Quản lý tài khoản và vai trò người dùng trong hệ thống.
            </p>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          {canManage && (
            <Button
              type="button"
              disabled={isUpdating}
              onClick={() => handleOpenForm(null)}
              leftIcon={<UserPlus />}
              className="h-10 rounded-lg px-4 shadow-sm"
            >
              Tạo tài khoản
            </Button>
          )}
        </div>
      </div>

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

      <form
        onSubmit={handleApplyFilters}
        className="grid items-end gap-4 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm md:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr_auto]"
      >
        <label className="block space-y-2 text-sm text-slate-700">
          <span>Tìm tài khoản</span>
          <Input
            leftIcon={<Search className="size-4" />}
            className="h-10 rounded-lg bg-slate-50/70 shadow-none focus-visible:bg-white"
            placeholder="Họ tên, mã người dùng hoặc email"
            value={draftFilters.keyword}
            onChange={(event) =>
              setDraftFilters({
                ...draftFilters,
                keyword: event.target.value,
              })
            }
          />
        </label>

        <div className="space-y-2 text-sm text-slate-700">
          <span>Vai trò</span>
          <Select<SystemRole | ''>
            triggerClassName="h-10 rounded-lg bg-slate-50/70 shadow-none"
            aria-label="Lọc theo vai trò"
            placeholder="Tất cả vai trò"
            value={draftFilters.role}
            options={[
              { value: '', label: 'Tất cả vai trò' },
              ...SYSTEM_ROLES.map((role) => ({
                value: role.roleName,
                label: role.title,
              })),
            ]}
            onChange={(value: SystemRole | '') => setDraftFilters({ ...draftFilters, role: value })}
          />
        </div>

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

        <Button
          type="submit"
          variant="soft"
          leftIcon={<SlidersHorizontal />}
          className="h-10 rounded-lg px-5"
        >
          Áp dụng
        </Button>
      </form>

      <Card className="overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <CardHeader className="flex-row flex-wrap items-center justify-between gap-3 space-y-0 border-b border-slate-100 bg-gradient-to-r from-slate-50/80 to-white px-4 py-3">
          <div className="space-y-1">
            <CardTitle className="text-base text-slate-900">Danh sách tài khoản</CardTitle>
            <p className="text-xs text-slate-500">Thông tin người dùng và trạng thái truy cập</p>
          </div>
          <section
            className="w-full sm:w-auto"
            aria-label="Thống kê tài khoản"
            aria-busy={statisticsQuery.isFetching}
          >
            <StatisticsBar
              items={[
                {
                  key: 'total',
                  label: 'Tổng tài khoản',
                  value: formatCount(statistics?.total),
                  icon: <UsersRound />,
                  tone: 'primary',
                },
                {
                  key: 'pending',
                  label: 'Chờ kích hoạt',
                  value: formatCount(statistics?.pendingActivation),
                  icon: <Clock3 />,
                  tone: 'warning',
                },
                {
                  key: 'disabled',
                  label: 'Vô hiệu hóa',
                  value: formatCount(statistics?.disabled),
                  icon: <ShieldOff />,
                  tone: 'muted',
                },
              ]}
            />
            {statisticsQuery.isPending && (
              <p role="status" className="mt-2 text-xs text-muted-foreground">
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
        </CardHeader>

        {usersQuery.isPending ? (
          <p role="status" className="p-8 text-center text-sm text-slate-500">
            Đang tải danh sách...
          </p>
        ) : usersQuery.isError ? (
          <div role="alert" className="space-y-3 p-6 text-center">
            <p className="text-sm text-rose-600">{getUserErrorMessage(usersQuery.error)}</p>
            <Button type="button" variant="outline" onClick={() => void usersQuery.refetch()}>
              Thử lại
            </Button>
          </div>
        ) : data && data.items.length > 0 ? (
          <UsersTable
            users={data.items}
            pageNumber={data.pageNumber}
            pageSize={data.pageSize}
            currentUserId={currentUser?.id}
            canManage={canManage}
            isChangingStatus={isUpdating}
            onEdit={handleOpenForm}
            onChangeStatus={handleChangeStatus}
          />
        ) : (
          <p className="p-8 text-center text-sm text-slate-500">Không có tài khoản phù hợp.</p>
        )}

        {data && !usersQuery.isError && (
          <TablePagination
            pageNumber={data.pageNumber}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            hasPreviousPage={data.hasPreviousPage}
            hasNextPage={data.hasNextPage}
            isLoading={usersQuery.isFetching}
            itemLabel="tài khoản"
            onPageChange={setPageNumber}
          />
        )}
      </Card>
    </div>
  );
}
