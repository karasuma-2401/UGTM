import { InlineAlert } from '@/components/ui/inline-alert';
import { OperationToast } from '@/shared/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { UsersRound, GraduationCap, UserPlus, Upload, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { StatisticsBar } from '@/components/ui/statistics-bar';
import { TablePagination } from '@/components/ui/table-pagination';
import { TableFloatingBar } from '@/components/ui/table-floating-bar';
import { TableSkeleton } from '@/components/ui/table-skeleton';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

import {
  UserFormDialog,
  StudentImportDialog,
  UsersTable,
  IdentityNavigation,
  UserFilterBar,
  UserStatusTabs,
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
  const selectedUser = users?.items.find((item) => item.id === modals.selectedRowId) ?? null;
  const isBusy = actions.isUpdating || data.usersQuery.isFetching;

  return (
    <div className="min-w-0 space-y-5">
      <PageHeader
        compact
        accent
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
            className="h-10 rounded-xl px-4 shadow-2xs"
          >
            {isStudent ? 'Thêm sinh viên' : 'Thêm giảng viên / cán bộ'}
          </Button>
        )}
        {isStudent && canImport && (
          <Button
            type="button"
            variant="success"
            disabled={actions.isUpdating}
            onClick={() => modals.setIsImportOpen(true)}
            leftIcon={<Upload />}
            className="h-10 rounded-xl px-4 shadow-2xs"
          >
            Nhập Excel
          </Button>
        )}
      </PageHeader>

      <OperationToast message={modals.notice} onConsumed={() => modals.setNotice('')} />

      {actions.mutationError && <InlineAlert tone="error">{getUserErrorMessage(actions.mutationError)}</InlineAlert>}

      {modals.isFormOpen && canManage && (
        <UserFormDialog
          accountGroup={accountGroup}
          key={modals.editingUser?.id || 'create'}
          user={modals.editingUser}
          isSaving={actions.saveMutation.isPending}
          error={
            actions.saveMutation.error ? getUserErrorMessage(actions.saveMutation.error) : null
          }
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

      <ConfirmDialog
        open={Boolean(actions.pendingStatusUser)}
        onClose={actions.cancelChangeStatus}
        onConfirm={actions.confirmChangeStatus}
        type={actions.pendingStatusUser?.status === 'Active' ? 'error' : 'success'}
        title={
          actions.pendingStatusUser?.status === 'Active'
            ? 'Vô hiệu hóa tài khoản?'
            : 'Khôi phục tài khoản?'
        }
        description={
          actions.pendingStatusUser && (
            <div className="space-y-2">
              <p className="text-sm text-slate-800">
                Bạn sắp {actions.pendingStatusUser.status === 'Active' ? 'vô hiệu hóa' : 'khôi phục'} tài khoản{' '}
                <strong className="font-semibold text-slate-950">
                  {actions.pendingStatusUser.fullName}
                </strong>
                {actions.pendingStatusUser.userCode ? (
                  <span className="text-slate-500"> ({actions.pendingStatusUser.userCode})</span>
                ) : null}
                .
              </p>
              <p className="text-xs text-slate-500 leading-relaxed">
                {actions.pendingStatusUser.status === 'Active'
                  ? 'Tài khoản này sẽ bị thu hồi phiên đăng nhập và tạm ngưng quyền truy cập hệ thống.'
                  : 'Tài khoản sẽ được mở khóa và người dùng có thể đăng nhập lại vào hệ thống.'}
              </p>
            </div>
          )
        }
        confirmText={actions.pendingStatusUser?.status === 'Active' ? 'Vô hiệu hóa' : 'Khôi phục'}
        isConfirming={actions.statusMutation.isPending}
      />

      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-xs">
        <ListCardHeader
          title="Danh sách tài khoản"
          icon={isStudent ? <GraduationCap /> : <UsersRound />}
          description={`${data.formatCount(users?.totalCount)} ${isStudent ? 'sinh viên' : 'giảng viên / cán bộ'}`}
          className="min-h-0 lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]"
        >
          <section
            className="flex items-center justify-end w-full"
            aria-label="Thống kê tài khoản"
            aria-busy={data.statisticsQuery.isFetching}
          >
            <StatisticsBar
              compact
              className="sr-only"
              loading={data.statisticsQuery.isPending}
              items={[
                {
                  key: 'total',
                  onClick: () => filters.quickFilter(''),
                  active:
                    !filters.filters.status && !filters.filters.keyword && !filters.filters.role,
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
                  icon: <UsersRound />,
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
                  icon: <UsersRound />,
                  tone: 'danger',
                },
              ]}
            />
            <UserStatusTabs
              activeKey={
                !filters.filters.status && !filters.filters.keyword && !filters.filters.role
                  ? ''
                  : filters.filters.status === 'PendingActivation' &&
                      !filters.filters.keyword &&
                      !filters.filters.role
                    ? 'PendingActivation'
                    : filters.filters.status === 'Disabled' &&
                        !filters.filters.keyword &&
                        !filters.filters.role
                      ? 'Disabled'
                      : 'none'
              }
              onSelect={(status) => filters.quickFilter(status)}
              stats={data.statistics}
              loading={data.statisticsQuery.isPending}
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
        <div className="border-b border-slate-100 bg-slate-50/40 p-4 sm:px-5">
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
        </div>

        <div
          aria-busy={data.usersQuery.isFetching}
          className={`min-h-[12rem] overflow-x-auto transition-opacity ${data.isRefreshing ? 'opacity-60' : ''}`}
        >
          {data.usersQuery.isPending ? (
            <TableSkeleton
              className="min-w-[900px]"
              columns={[
                { label: 'STT', width: 'w-14', variant: 'number' },
                { label: isStudent ? 'Sinh viên' : 'Giảng viên / Cán bộ', variant: 'person' },
                {
                  label: isStudent ? 'MSSV' : 'Vai trò / Mã cán bộ',
                  width: 'w-52',
                },
                { label: 'Lần truy cập', width: 'w-36' },
                { label: 'Trạng thái', width: 'w-36', variant: 'badge' },
                { label: 'Thao tác', width: 'w-[176px]', variant: 'actions', actionCount: 2 },
              ]}
            />
          ) : data.usersQuery.isError ? (
            <div
              role="alert"
              className="flex h-full flex-col items-center justify-center gap-3 p-6 text-center"
            >
              <p className="text-sm text-rose-600">{getUserErrorMessage(data.usersQuery.error)}</p>
              <Button
                type="button"
                variant="outline"
                onClick={() => void data.usersQuery.refetch()}
              >
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
              isChangingStatus={isBusy}
              onEdit={actions.handleOpenForm}
              selectedRowId={modals.selectedRowId}
              onSelectRow={(id) => modals.setSelectedRowId((prev) => (prev === id ? null : id))}
              sort={filters.sort}
              onSortChange={(field) => {
                modals.setSelectedRowId(null);
                filters.handleSortChange(field);
              }}
              onChangeStatus={actions.handleChangeStatus}
              onResendActivation={actions.handleResendActivation}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center text-sm text-slate-500">
              <p>Không có tài khoản phù hợp.</p>
              {filters.hasFilters && (
                <Button
                  type="button"
                  variant="outline"
                  leftIcon={<X />}
                  onClick={filters.clearFilters}
                >
                  Bỏ lọc
                </Button>
              )}
            </div>
          )}
        </div>

        {users && !data.usersQuery.isError && (
          <TablePagination
            pageNumber={users.pageNumber}
            pageSize={users.pageSize}
            totalCount={users.totalCount}
            hasPreviousPage={users.hasPreviousPage}
            hasNextPage={users.hasNextPage}
            isLoading={data.usersQuery.isFetching || actions.isUpdating}
            itemLabel={isStudent ? 'sinh viên' : 'giảng viên / cán bộ'}
            onPageChange={(page) => {
              modals.setSelectedRowId(null);
              filters.setPageNumber(page);
            }}
            onPageSizeChange={(size) => {
              modals.setSelectedRowId(null);
              filters.setPageSize(size);
              filters.setPageNumber(1);
            }}
          />
        )}
      </Card>

      {canManage && selectedUser && (
        <TableFloatingBar
          aria-label="Thao tác với tài khoản đã chọn"
          selectedCount={1}
          totalCount={users?.totalCount ?? 0}
          onClearSelection={() => modals.setSelectedRowId(null)}
        >
          <Button
            type="button"
            variant="default"
            size="sm"
            className="h-8 rounded-xl text-xs font-semibold shadow-xs"
            disabled={isBusy}
            onClick={() => actions.handleOpenForm(selectedUser)}
          >
            Xem
          </Button>
          {selectedUser.status === 'PendingActivation' ? (
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 rounded-xl text-xs font-semibold"
              disabled={isBusy}
              onClick={() => actions.handleResendActivation(selectedUser)}
            >
              Gửi email kích hoạt
            </Button>
          ) : (
            selectedUser.id !== currentUserId && (
              <Button
                type="button"
                variant={selectedUser.status === 'Active' ? 'dangerOutline' : 'successOutline'}
                size="sm"
                className="h-8 rounded-xl text-xs font-semibold"
                disabled={isBusy}
                onClick={() => actions.handleChangeStatus(selectedUser)}
              >
                {selectedUser.status === 'Active' ? 'Vô hiệu hóa' : 'Khôi phục'}
              </Button>
            )
          )}
        </TableFloatingBar>
      )}
    </div>
  );
}
