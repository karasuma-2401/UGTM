import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
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

const PAGE_SIZE = 8;

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

  const firstItem = data && data.totalCount > 0 ? (data.pageNumber - 1) * data.pageSize + 1 : 0;

  const lastItem = data ? Math.min(data.pageNumber * data.pageSize, data.totalCount) : 0;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-800">
            Tài khoản & quyền truy cập
          </h1>
          <p className="mt-2 text-sm text-slate-500">
            Quản lý tài khoản và vai trò người dùng trong hệ thống.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          {canManage && (
            <Button type="button" disabled={isUpdating} onClick={() => handleOpenForm(null)}>
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
        className="grid items-end gap-4 md:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr_auto]"
      >
        <label className="block space-y-2 text-sm text-slate-700">
          <span>Tìm tài khoản</span>
          <Input
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

        <Button type="submit">Áp dụng</Button>
      </form>

      <Card className="overflow-hidden">
        <CardHeader className="flex-row flex-wrap items-center justify-between gap-3 space-y-0 px-4 py-5">
          <CardTitle className="text-base">Danh sách tài khoản</CardTitle>
          <section aria-label="Thống kê tài khoản" aria-busy={statisticsQuery.isFetching}>
            <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-xs">
              <p className="font-medium text-slate-800">
                {formatCount(statistics?.total)} tài khoản
              </p>
              <p className="font-medium text-amber-700">
                {formatCount(statistics?.pendingActivation)} chờ kích hoạt
              </p>
              <p className="text-slate-500">{formatCount(statistics?.disabled)} vô hiệu hóa</p>
            </div>
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
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 p-4">
            <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
              <p className="text-xs text-slate-500">
                Hiển thị {firstItem}–{lastItem} / {data.totalCount} tài khoản
              </p>
            </div>

            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={!data.hasPreviousPage || usersQuery.isFetching}
                onClick={() => setPageNumber((page) => page - 1)}
              >
                Trước
              </Button>

              <span className="text-xs text-slate-500">Trang {data.pageNumber}</span>

              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={!data.hasNextPage || usersQuery.isFetching}
                onClick={() => setPageNumber((page) => page + 1)}
              >
                Sau
              </Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}
