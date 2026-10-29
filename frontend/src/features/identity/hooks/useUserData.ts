import { useQuery } from '@tanstack/react-query';
import type { AccountGroup } from '../types/user.types';
import type { UserFilters } from './useUserFilters';
import { userApi } from '../services/userApi';

export function useUserData(
  accountGroup: AccountGroup,
  currentUserId: string | undefined,
  filters: UserFilters,
  pageNumber: number,
  pageSize: number,
) {
  const usersQuery = useQuery({
    queryKey: ['admin-users', currentUserId, accountGroup, filters, pageNumber, pageSize],
    queryFn: ({ signal }) =>
      userApi.getUsers({ ...filters, accountGroup, pageNumber, pageSize }, signal),
    placeholderData: (previousData, previousQuery) =>
      previousQuery?.queryKey[1] === currentUserId && previousQuery?.queryKey[2] === accountGroup
        ? previousData
        : undefined,
  });

  const statisticsQuery = useQuery({
    queryKey: ['admin-users', currentUserId, accountGroup, 'statistics'],
    queryFn: ({ signal }) => userApi.getStatistics(accountGroup, signal),
  });

  const statistics = statisticsQuery.isError ? undefined : statisticsQuery.data;
  const formatCount = (count: number | undefined) =>
    count === undefined ? '—' : count.toLocaleString('vi-VN');

  const data = usersQuery.data;
  const isRefreshing = usersQuery.isFetching && !usersQuery.isPending;

  return {
    usersQuery,
    statisticsQuery,
    statistics,
    formatCount,
    data,
    isRefreshing,
  };
}