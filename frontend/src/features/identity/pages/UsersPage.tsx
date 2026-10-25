import { useQueryClient } from '@tanstack/react-query';
import {
  UsersRound,
  GraduationCap,
  UserPlus,
  Clock3,
  ShieldOff,
  Upload,
  X,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { TableSkeleton } from '@/components/ui/table-skeleton';

import {
  UserFormDialog,
  StudentImportDialog,
  UsersTable,
  IdentityNavigation,
  UserFilterBar,
} from '../components/users';
import { getUserErrorMessage } from '../services/userApi';
import { useUsers } from '../hooks';
import type { AccountGroup } from '../types/user.types';

interface UsersPageProps {
  accountGroup: AccountGroup;
}

export function UsersPage({ accountGroup }: UsersPageProps) {
  const queryClient = useQueryClient();
  const { permissions, filters, data, modals, actions } = useUsers(accountGroup);
  const { isStudent, canManage, canImport, currentUserId } = permissions;

  const users = data.data;

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
            disabled={actions.isUpdating}
            onClick={() => actions.handleOpenForm(null)}
            leftIcon={<UserPlus />}
            className="h-10 rounded-lg px-4 shadow-sm"
          >
            {isStudent ? 'Thêm sinh viên' : 'Thêm giảng viên / cán bộ'}
          </Button>
        )}
        {isStudent && canImport && (
          <Button
            type="button"
            disabled={actions.isUpdating}
            onClick={() => modals.setIsImportOpen(true)}
            leftIcon={<Upload />}
            className="h-10 rounded-lg px-4 shadow-sm"
          >
            Nhập Excel
          </Button>
        )}
      </PageHeader>

      {modals.notice && (
        <p role="status" className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700">
          {modals.notice}
        </p>
      )}

      {actions.mutationError && (
        <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
          {getUserErrorMessage(actions.mutationError)}
        </p>
      )}

      {modals.isFormOpen && canManage && (
        <UserFormDialog
          accountGroup={accountGroup}
          key={modals.editingUser?.id || 'create'}
          user={modals.editingUser}
          isSaving={actions.saveMutation.isPending}
          error={actions.saveMutation.error ? getUserErrorMessage(actions.saveMutation.error) : null}
          onClose={() => modals.setIsFormOpen(false)}
          onSave={(values) => {
            actions.saveMutation.mutate({
              user: modals.editingUser,
              values,
            });
          }}
        />
      )}

      {modals.isImportOpen && isStudent && canImport && (
        <StudentImportDialog
          onClose={() => modals.setIsImportOpen(false)}
          onImported={async (count) => {
            modals.setNotice(
              `Đã tạo ${count.toLocaleString('vi-VN')} tài khoản sinh viên chờ kích hoạt. Sinh viên có thể yêu cầu link tại trang kích hoạt.`,
            );
            filters.setPageNumber(1);
            await queryClient.invalidateQueries({ queryKey: ['admin-users'] });
            modals.setIsImportOpen(false);
          }}
        />
      )}

      <UserFilterBar
        isStudent={isStudent}
        draftFilters={filters.draftFilters}
        filters={filters.filters}
        hasFilters={filters.hasFilters}
        onDraftChange={filters.setDraftFilters}
        onApply={filters.handleApplyFilters}
        onClear={filters.clearFilters}
        onRemoveFilter={filters.removeFilter}
      />

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <ListCardHeader
          title="Danh sách tài khoản"
          icon={isStudent ? <GraduationCap /> : <UsersRound />}
          description={`${data.formatCount(users?.totalCount)} ${isStudent ? 'sinh viên' : 'giảng viên / cán bộ'}`}
          className="xl:grid-cols-[minmax(0,1fr)_420px]"
        >
          <section
            className="w-full"
            aria-label="Thống kê tài khoản"
            aria-busy={data.statisticsQuery.isFetching}
          >
            <StatisticsBar
              loading={data.statisticsQuery.isPending}
              items={[
                {
                  key: 'total',
                  onClick: () => filters.quickFilter(''),
                  active: !filters.filters.status && !filters.filters.keyword && !filters.filters.role,
                  label: 'Tổng tài khoản',
                  value: data.formatCount(data.statistics?.total),
                  icon: <UsersRound />,
                  tone: 'primary',
                },
                {
                  key: 'pending',
                  onClick: () => filters.quickFilter('PendingActivation'),
                  active:
                    filters.filters.status === 'PendingActivation' &&
                    !filters.filters.keyword &&
                    !filters.filters.role,
                  label: 'Chờ kích hoạt',
                  value: data.formatCount(data.statistics?.pendingActivation),
                  icon: <Clock3 />,
                  tone: 'warning',
                },
                {
                  key: 'disabled',
                  onClick: () => filters.quickFilter('Disabled'),
                  active:
                    filters.filters.status === 'Disabled' &&
                    !filters.filters.keyword &&
                    !filters.filters.role,
                  label: 'Vô hiệu hóa',
                  value: data.formatCount(data.statistics?.disabled),
                  icon: <ShieldOff />,
                  tone: 'muted',
                },
              ]}
            />
            {data.statisticsQuery.isPending && (
              <p role="status" className="sr-only">
                Đang tải thống kê...
              </p>
            )}
            {data.statisticsQuery.isError && (
              <div role="alert" className="mt-2 flex flex-wrap items-center gap-2">
                <p className="text-xs text-rose-700">Không thể tải thống kê tài khoản.</p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={data.statisticsQuery.isFetching}
                  onClick={() => void data.statisticsQuery.refetch()}
                >
                  Thử lại
                </Button>
              </div>
            )}
          </section>
        </ListCardHeader>

        <div
          aria-busy={data.usersQuery.isFetching}
          className={`h-[min(60vh,40rem)] min-h-[20rem] overflow-auto transition-opacity ${data.isRefreshing ? 'opacity-60' : ''}`}
        >
          {data.usersQuery.isPending ? (
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
          ) : data.usersQuery.isError ? (
            <div
              role="alert"
              className="flex h-full flex-col items-center justify-center gap-3 p-6 text-center"
            >
              <p className="text-sm text-rose-600">{getUserErrorMessage(data.usersQuery.error)}</p>
              <Button type="button" variant="outline" onClick={() => void data.usersQuery.refetch()}>
                Thử lại
              </Button>
            </div>
          ) : users && users.items.length > 0 ? (
            <UsersTable
              accountGroup={accountGroup}
              users={users.items}
              pageNumber={users.pageNumber}
              pageSize={users.pageSize}
              currentUserId={currentUserId}
              canManage={canManage}
              isChangingStatus={actions.isUpdating || data.usersQuery.isFetching}
              onEdit={actions.handleOpenForm}
              selectedRowId={modals.selectedRowId}
              onSelectRow={modals.setSelectedRowId}
              onChangeStatus={actions.handleChangeStatus}
              onResendActivation={actions.handleResendActivation}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center text-sm text-slate-500">
              <p>Không có tài khoản phù hợp.</p>
              {filters.hasFilters && (
                <Button type="button" variant="outline" leftIcon={<X />} onClick={filters.clearFilters}>
                  Bỏ lọc
                </Button>
              )}
            </div>
          )}
        </div>

        {users && !data.usersQuery.isError && (
          <>
            <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-3 text-sm">
              <label htmlFor={`page-size-${accountGroup}`}>Số tài khoản mỗi trang</label>
              <select
                id={`page-size-${accountGroup}`}
                value={filters.pageSize}
                disabled={actions.isUpdating || data.usersQuery.isFetching}
                className="rounded-md border border-slate-200 bg-white p-2"
                onChange={(event) => {
                  filters.setPageSize(Number(event.target.value));
                  filters.setPageNumber(1);
                }}
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
            <TablePagination
              pageNumber={users.pageNumber}
              pageSize={users.pageSize}
              totalCount={users.totalCount}
              hasPreviousPage={users.hasPreviousPage}
              hasNextPage={users.hasNextPage}
              isLoading={data.usersQuery.isFetching || actions.isUpdating}
              itemLabel={isStudent ? 'sinh viên' : 'giảng viên / cán bộ'}
              onPageChange={filters.setPageNumber}
            />
          </>
        )}
      </Card>
    </div>
  );
}
