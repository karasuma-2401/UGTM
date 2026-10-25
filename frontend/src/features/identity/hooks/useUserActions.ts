import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { UserSummary, UserFormValues } from '../types/user.types';
import { userApi } from '../services/userApi';
import { resendActivationApi } from '@/features/auth/services/authApi';

interface UserActionsParams {
  canManage: boolean;
  currentUserId?: string;
  onFormClose: () => void;
  onSetNotice: (msg: string) => void;
  onResetPage: () => void;
  onSetEditingUser: (user: UserSummary | null) => void;
  onOpenForm: () => void;
}

export function useUserActions({
  canManage,
  currentUserId,
  onFormClose,
  onSetNotice,
  onResetPage,
  onSetEditingUser,
  onOpenForm,
}: UserActionsParams) {
  const queryClient = useQueryClient();

  const saveMutation = useMutation({
    mutationFn: async ({ user, values }: { user: UserSummary | null; values: UserFormValues }) => {
      if (user) {
        return userApi.updateUser(user.id, values);
      }
      return userApi.createUser(values);
    },
    onSuccess: async () => {
      onFormClose();
      onSetNotice('Đã lưu tài khoản thành công.');
      await queryClient.invalidateQueries({
        queryKey: ['admin-users'],
      });
    },
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: 'Active' | 'Disabled' }) =>
      userApi.updateStatus(id, status),
    onSuccess: async () => {
      onSetNotice('Đã cập nhật trạng thái tài khoản.');
      onResetPage();
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
    onSuccess: () => onSetNotice('Đã gửi email kích hoạt tài khoản.'),
  });

  const handleOpenForm = (user: UserSummary | null) => {
    saveMutation.reset();
    statusMutation.reset();
    activationMutation.reset();
    onSetNotice('');
    onSetEditingUser(user);
    onOpenForm();
  };

  const handleChangeStatus = (user: UserSummary) => {
    if (!canManage || statusMutation.isPending) return;
    if (user.id === currentUserId) return;
    if (user.status === 'PendingActivation') return;

    const status: 'Active' | 'Disabled' = user.status === 'Active' ? 'Disabled' : 'Active';
    const action = status === 'Active' ? 'khôi phục truy cập cho' : 'vô hiệu hóa';
    const confirmed = window.confirm(`Bạn có muốn ${action} tài khoản "${user.fullName}"?`);
    if (!confirmed) return;

    saveMutation.reset();
    onSetNotice('');
    statusMutation.mutate({
      id: user.id,
      status,
    });
  };

  const handleResendActivation = (user: UserSummary) => {
    onSetNotice('');
    activationMutation.mutate(user);
  };

  const mutationError = saveMutation.error || statusMutation.error || activationMutation.error;
  const isUpdating =
    saveMutation.isPending || statusMutation.isPending || activationMutation.isPending;

  return {
    saveMutation,
    statusMutation,
    activationMutation,
    handleOpenForm,
    handleChangeStatus,
    handleResendActivation,
    mutationError,
    isUpdating,
  };
}