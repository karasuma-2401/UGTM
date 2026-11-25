import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { notificationApi } from './notificationApi';

export type NotificationInboxFilter = 'all' | 'unread';

export function useNotificationInbox(options: {
  open: boolean;
  filter: NotificationInboxFilter;
}) {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const userId = user?.id;

  const unreadCountQuery = useQuery({
    queryKey: ['notifications-unread-count', userId],
    queryFn: ({ signal }) => notificationApi.getUnreadCount(signal),
    enabled: Boolean(userId),
    refetchInterval: 60_000,
    retry: false,
  });

  const listQuery = useQuery({
    queryKey: ['notifications-inbox', userId, options.filter],
    queryFn: ({ signal }) =>
      notificationApi.getNotifications(
        {
          pageNumber: 1,
          pageSize: 20,
          unreadOnly: options.filter === 'unread',
        },
        signal,
      ),
    enabled: Boolean(userId && options.open),
    retry: false,
  });

  const markReadMutation = useMutation({
    mutationFn: (id: string) => notificationApi.markAsRead(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications-inbox', userId] });
      void queryClient.invalidateQueries({ queryKey: ['notifications-unread-count', userId] });
    },
  });

  const markAllReadMutation = useMutation({
    mutationFn: () => notificationApi.markAllAsRead(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications-inbox', userId] });
      void queryClient.invalidateQueries({ queryKey: ['notifications-unread-count', userId] });
    },
  });

  return {
    unreadCount: unreadCountQuery.data ?? 0,
    unreadCountQuery,
    listQuery,
    items: listQuery.data?.items ?? [],
    totalCount: listQuery.data?.totalCount ?? 0,
    markReadMutation,
    markAllReadMutation,
  };
}
