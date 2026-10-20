import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  UsersRound,
  UserPlus,
  Clock3,
  ShieldOff,
  Search,
  SlidersHorizontal,
  Upload,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { useAuth } from '@/shared/auth';

import { UserFormDialog } from '../components/UserFormDialog';
import { StudentImportDialog } from '../components/StudentImportDialog';
import { UsersTable } from '../components/UsersTable';
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
  const [notice, setNotice] = useState('');

  const usersQuery = useQuery({
    queryKey: ['admin-users', accountGroup, filters, pageNumber, pageSize],
    queryFn: ({ signal }) =>
      userApi.getUsers({ ...filters, accountGroup, pageNumber, pageSize }, signal),
  });

  const statisticsQuery = useQuery({
    queryKey: ['admin-users', accountGroup, 'statistics'],
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
  const mutationError = saveMutation.error || statusMutation.error || activationMutation.error;
  const isUpdating =
    saveMutation.isPending || statusMutation.isPending || activationMutation.isPending;

  return (
    <div className="min-w-0 space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-start gap-3.5">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              {isStudent ? 'Sinh viên' : 'Giảng viên & cán bộ'}
            </h1>
            <p className="mt-2 text-sm text-slate-500">
              {isStudent
                ? 'Quản lý tài khoản sinh viên, nhập danh sách và theo dõi kích hoạt.'
                : 'Quản lý tài khoản, vai trò và trạng thái truy cập của giảng viên, cán bộ.'}
            </p>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          {isStudent && canImport && (
            <Button
              type="button"
              variant="outline"
              disabled={isUpdating}
              onClick={() => setIsImportOpen(true)}
              leftIcon={<Upload />}
            >
              Nhập Excel
            </Button>
          )}
          {canManage && (
            <Button
              type="button"
              disabled={isUpdating}
              onClick={() => handleOpenForm(null)}
              leftIcon={<UserPlus />}
              className="h-10 rounded-lg px-4 shadow-sm"
            >
              {isStudent ? 'Thêm sinh viên' : 'Thêm giảng viên / cán bộ'}
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

        <Button
          type="submit"
          variant="soft"
          leftIcon={<SlidersHorizontal />}
          className="h-10 rounded-lg px-5"
        >
          Áp dụng
        </Button>
      </form>

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <CardHeader className="grid min-h-[104px] grid-cols-1 items-center gap-4 space-y-0 border-b border-slate-100 bg-gradient-to-r from-slate-50/80 to-white px-5 py-4 xl:grid-cols-[minmax(0,1fr)_420px]">
          <div className="space-y-1">
            <CardTitle className="text-base text-slate-900">Danh sách tài khoản</CardTitle>
            <p className="text-xs text-slate-500">Thông tin người dùng và trạng thái truy cập</p>
          </div>
          <section
            className="w-full"
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

        <div className="h-[min(60vh,40rem)] min-h-[20rem] overflow-auto">
          {usersQuery.isPending ? (
            <p
              role="status"
              className="flex h-full items-center justify-center p-8 text-center text-sm text-slate-500"
            >
              Đang tải danh sách...
            </p>
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
              isChangingStatus={isUpdating}
              onEdit={handleOpenForm}
              onChangeStatus={handleChangeStatus}
              onResendActivation={(user) => {
                setNotice('');
                activationMutation.mutate(user);
              }}
            />
          ) : (
            <p className="flex h-full items-center justify-center p-8 text-center text-sm text-slate-500">
              Không có tài khoản phù hợp.
            </p>
          )}
        </div>

        {data && !usersQuery.isError && (
          <>
            <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-3 text-sm">
              <label htmlFor={`page-size-${accountGroup}`}>Số tài khoản mỗi trang</label>
              <select
                id={`page-size-${accountGroup}`}
                value={pageSize}
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
              isLoading={usersQuery.isFetching}
              itemLabel={isStudent ? 'sinh viên' : 'giảng viên / cán bộ'}
              onPageChange={setPageNumber}
            />
          </>
        )}
      </Card>
    </div>
  );
}
