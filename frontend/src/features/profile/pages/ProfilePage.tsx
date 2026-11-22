import { useEffect, useMemo, useState } from 'react';
import { useBlocker, useNavigate, useSearchParams } from 'react-router-dom';
import { RefreshCw, ShieldAlert } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { NavigationTabs, type TabItem } from '@/components/ui/navigation-tabs';
import { PageHeader } from '@/components/ui/page-header';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/ui/status-badge';
import {
  changePasswordApi,
  getMyActivityApi,
  getMyActivitySummaryApi,
  getMySessionsApi,
  revokeAllMySessionsApi,
  revokeMySessionApi,
  updateMyProfileApi,
} from '@/features/auth/services/authApi';
import type { ActivityCategory, SessionItem } from '@/features/auth/types/auth.types';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import { ProfileOverviewTab } from '../components/ProfileOverviewTab';
import { ProfileSecuritySidebar } from '../components/ProfileSecuritySidebar';
import { ProfileSecurityTab } from '../components/ProfileSecurityTab';
import { ProfileSessionsTab } from '../components/ProfileSessionsTab';
import {
  ACCOUNT_STATUS_DISPLAY,
  getProfileErrorMessage,
  normalizePhoneValue,
} from '../utils/profileFormatters';

type ProfileTabId = 'overview' | 'security' | 'sessions';

function resolveTabId(raw: string | null): ProfileTabId {
  if (raw === 'security' || raw === 'sessions') return raw;
  return 'overview';
}

export function ProfilePage() {
  const { user, refreshProfile, updateUserProfile, completeServerLogout } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const activeTab = resolveTabId(searchParams.get('tab'));
  const setActiveTab = (tab: ProfileTabId) => {
    setSearchParams(tab === 'overview' ? {} : { tab }, { replace: true });
  };

  // Editable phone state
  const [phoneDraft, setPhoneDraft] = useState<string | null>(null);
  const initialPhone = normalizePhoneValue(user?.phoneNumber);
  const currentPhone = phoneDraft ?? initialPhone;
  const isPhoneDirty = normalizePhoneValue(currentPhone) !== initialPhone;

  // Password form state
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const hasPasswordDraft = Boolean(currentPassword || newPassword || confirmPassword);

  const [isRefreshingAll, setIsRefreshingAll] = useState(false);
  const [revokingSessionId, setRevokingSessionId] = useState<string | null>(null);
  const [activityCategory, setActivityCategory] = useState<ActivityCategory>('all');
  const [selectedActivityDate, setSelectedActivityDate] = useState<string | null>(null);
  const [activityLimit, setActivityLimit] = useState(15);

  const profileQuery = useQuery({
    queryKey: ['identity-my-profile'],
    queryFn: refreshProfile,
    staleTime: 30_000,
  });

  const activeUser = profileQuery.data ?? user;

  const sessionsQuery = useQuery({
    queryKey: ['identity-my-sessions'],
    queryFn: async () => {
      const response = await getMySessionsApi();
      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Không thể tải danh sách phiên.');
      }
      return response.data;
    },
    enabled: Boolean(activeUser) && !activeUser?.mustChangePassword,
  });

  const activityQuery = useQuery({
    queryKey: ['identity-my-activity', activityCategory, selectedActivityDate, activityLimit],
    queryFn: async () => {
      const response = await getMyActivityApi({
        pageNumber: 1,
        pageSize: activityLimit,
        category: activityCategory,
        date: selectedActivityDate,
      });
      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Không thể tải nhật ký hoạt động.');
      }
      return response.data;
    },
    enabled: Boolean(activeUser) && !activeUser?.mustChangePassword,
  });

  const activitySummaryQuery = useQuery({
    queryKey: ['identity-my-activity-summary'],
    queryFn: async () => {
      const response = await getMyActivitySummaryApi(252);
      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Không thể tải biểu đồ hoạt động.');
      }
      return response.data;
    },
    enabled: Boolean(activeUser) && !activeUser?.mustChangePassword,
  });

  const updatePhoneMutation = useMutation({
    mutationFn: async (nextPhone: string) => {
      const trimmed = nextPhone.trim();
      const response = await updateMyProfileApi({ phoneNumber: trimmed || null });
      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Không thể cập nhật số điện thoại.');
      }
      return response.data;
    },
    onSuccess: (updated) => {
      updateUserProfile(updated);
      queryClient.setQueryData(['identity-my-profile'], updated);
      setPhoneDraft(null);
      void queryClient.invalidateQueries({ queryKey: ['identity-my-activity'] });
      void queryClient.invalidateQueries({ queryKey: ['identity-my-activity-summary'] });
      notify.success('Đã cập nhật số điện thoại liên hệ.');
    },
    onError: (error) => {
      notify.error(getProfileErrorMessage(error, 'Không thể cập nhật số điện thoại.'));
    },
  });

  const changePasswordMutation = useMutation({
    mutationFn: async () => {
      const response = await changePasswordApi({
        currentPassword,
        newPassword,
      });
      if (!response.success) {
        throw new Error(response.error?.message || 'Không thể đổi mật khẩu.');
      }
    },
    onSuccess: async () => {
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setPhoneDraft(null);
      notify.success('Đổi mật khẩu thành công. Vui lòng đăng nhập lại bằng mật khẩu mới.');
      await completeServerLogout();
      navigate('/login', { replace: true });
    },
    onError: (error) => {
      notify.error(getProfileErrorMessage(error, 'Không thể đổi mật khẩu.'));
    },
  });

  const revokeSessionMutation = useMutation({
    mutationFn: async (session: SessionItem) => {
      setRevokingSessionId(session.id);
      const response = await revokeMySessionApi(session.id);
      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Không thể thu hồi phiên đăng nhập.');
      }
      return response.data;
    },
    onSuccess: async (result) => {
      if (result.currentSessionRevoked) {
        notify.info('Phiên hiện tại đã được thu hồi. Vui lòng đăng nhập lại.');
        await completeServerLogout();
        navigate('/login', { replace: true });
        return;
      }
      notify.success('Đã thu hồi phiên đăng nhập thành công.');
      await queryClient.invalidateQueries({ queryKey: ['identity-my-sessions'] });
      await queryClient.invalidateQueries({ queryKey: ['identity-my-activity'] });
      await queryClient.invalidateQueries({ queryKey: ['identity-my-activity-summary'] });
    },
    onError: (error) => {
      notify.error(getProfileErrorMessage(error, 'Không thể thu hồi phiên đăng nhập.'));
    },
    onSettled: () => {
      setRevokingSessionId(null);
    },
  });

  const revokeAllMutation = useMutation({
    mutationFn: async () => {
      const response = await revokeAllMySessionsApi();
      if (!response.success || !response.data) {
        throw new Error(response.error?.message || 'Không thể thu hồi tất cả phiên.');
      }
      return response.data;
    },
    onSuccess: async (result) => {
      notify.success(
        `Đã thu hồi ${result.revokedCount} phiên đăng nhập. Vui lòng đăng nhập lại.`,
      );
      await completeServerLogout();
      navigate('/login', { replace: true });
    },
    onError: (error) => {
      notify.error(getProfileErrorMessage(error, 'Không thể đăng xuất khỏi tất cả thiết bị.'));
    },
  });

  // Protect unsaved changes against route transitions and tab closes
  const hasUnsavedWork =
    (isPhoneDirty && !updatePhoneMutation.isPending) ||
    (hasPasswordDraft && !changePasswordMutation.isPending);
  const blocker = useBlocker(hasUnsavedWork);

  useEffect(() => {
    if (!hasUnsavedWork) return;
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [hasUnsavedWork]);

  const handleRefreshAll = async () => {
    setIsRefreshingAll(true);
    try {
      await Promise.all([
        profileQuery.refetch(),
        !activeUser?.mustChangePassword ? sessionsQuery.refetch() : Promise.resolve(),
        !activeUser?.mustChangePassword ? activityQuery.refetch() : Promise.resolve(),
        !activeUser?.mustChangePassword ? activitySummaryQuery.refetch() : Promise.resolve(),
      ]);
    } finally {
      setIsRefreshingAll(false);
    }
  };

  const tabs: TabItem[] = useMemo(
    () => [
      {
        id: 'overview',
        label: 'Hồ sơ và Quyền hạn',
      },
      {
        id: 'security',
        label: 'Mật khẩu và Bảo mật',
        badge: activeUser?.mustChangePassword ? (
          <span className="ml-1 inline-flex size-2 rounded-full bg-amber-500" aria-hidden="true" />
        ) : undefined,
      },
      {
        id: 'sessions',
        label: 'Phiên và Nhật ký',
        count: sessionsQuery.data?.length,
      },
    ],
    [activeUser?.mustChangePassword, sessionsQuery.data?.length],
  );

  if (!activeUser && profileQuery.isLoading) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-32 w-full rounded-2xl" />
        <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_340px] gap-6 items-start">
          <Skeleton className="h-96 w-full rounded-2xl" />
          <Skeleton className="h-80 w-full rounded-2xl" />
        </div>
      </div>
    );
  }

  if (!activeUser) {
    return (
      <InlineAlert
        tone="error"
        title="Không tải được hồ sơ cá nhân"
        action={
          <Button type="button" size="sm" variant="outline" onClick={() => void profileQuery.refetch()}>
            Thử lại
          </Button>
        }
      >
        {getProfileErrorMessage(profileQuery.error, 'Vui lòng thử làm mới trang.')}
      </InlineAlert>
    );
  }

  const statusMeta =
    ACCOUNT_STATUS_DISPLAY[activeUser.status ?? 'Active'] ?? ACCOUNT_STATUS_DISPLAY.Active;

  return (
    <div className="min-w-0 space-y-5 pb-8">
      <PageHeader
        compact
        accent
        title={activeUser.fullName}
        description={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
            <span>{activeUser.email}</span>
            {activeUser.userCode && (
              <>
                <span aria-hidden="true" className="text-ink-faint">
                  ·
                </span>
                <span className="font-medium tabular-nums text-ink-secondary">
                  {activeUser.userCode}
                </span>
              </>
            )}
          </span>
        }
        navigation={
          <NavigationTabs
            tabs={tabs}
            activeId={activeTab}
            continuityKey="profile-tabs"
            ariaLabel="Danh mục hồ sơ cá nhân"
            onSelect={(id) => setActiveTab(resolveTabId(id))}
          />
        }
      >
        <StatusBadge tone={statusMeta.tone}>{statusMeta.label}</StatusBadge>
        {activeUser.mustChangePassword && (
          <Badge variant="warning" dot>
            Cần đổi mật khẩu
          </Badge>
        )}
        {activeUser.mustChangePassword && activeTab !== 'security' && (
          <Button
            type="button"
            size="sm"
            leftIcon={<ShieldAlert className="size-3.5" />}
            onClick={() => setActiveTab('security')}
          >
            Đổi mật khẩu ngay
          </Button>
        )}
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="bg-surface-card"
          isLoading={isRefreshingAll || profileQuery.isFetching}
          leftIcon={
            !(isRefreshingAll || profileQuery.isFetching) ? (
              <RefreshCw className="size-3.5" />
            ) : undefined
          }
          onClick={() => void handleRefreshAll()}
        >
          Làm mới
        </Button>
      </PageHeader>

      {profileQuery.isError && (
        <InlineAlert
          tone="warning"
          title="Đang hiển thị dữ liệu hồ sơ lưu tạm"
          action={
            <Button type="button" size="sm" variant="outline" onClick={() => void profileQuery.refetch()}>
              Đồng bộ lại
            </Button>
          }
        >
          {getProfileErrorMessage(
            profileQuery.error,
            'Không thể làm mới dữ liệu mới nhất từ máy chủ.',
          )}
        </InlineAlert>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_340px] gap-6 items-start">
        <div className="min-w-0 fluent-tab-enter" key={activeTab}>
          {activeTab === 'overview' && (
            <ProfileOverviewTab
              user={activeUser}
              phoneInput={currentPhone}
              onPhoneInputChange={(val) => {
                updatePhoneMutation.reset();
                setPhoneDraft(val);
              }}
              isDirty={isPhoneDirty}
              isSavingPhone={updatePhoneMutation.isPending}
              phoneServerError={
                updatePhoneMutation.isError
                  ? getProfileErrorMessage(
                      updatePhoneMutation.error,
                      'Không thể cập nhật số điện thoại.',
                    )
                  : null
              }
              onSavePhone={() => updatePhoneMutation.mutate(currentPhone)}
              onResetPhone={() => {
                updatePhoneMutation.reset();
                setPhoneDraft(null);
              }}
            />
          )}

          {activeTab === 'security' && (
            <ProfileSecurityTab
              user={activeUser}
              currentPassword={currentPassword}
              newPassword={newPassword}
              confirmPassword={confirmPassword}
              onCurrentPasswordChange={(val) => {
                changePasswordMutation.reset();
                setCurrentPassword(val);
              }}
              onNewPasswordChange={(val) => {
                changePasswordMutation.reset();
                setNewPassword(val);
              }}
              onConfirmPasswordChange={(val) => {
                changePasswordMutation.reset();
                setConfirmPassword(val);
              }}
              isSubmitting={changePasswordMutation.isPending}
              serverError={
                changePasswordMutation.isError
                  ? getProfileErrorMessage(changePasswordMutation.error, 'Không thể đổi mật khẩu.')
                  : null
              }
              onSubmitChangePassword={() => changePasswordMutation.mutate()}
              onResetForm={() => {
                changePasswordMutation.reset();
                setCurrentPassword('');
                setNewPassword('');
                setConfirmPassword('');
              }}
            />
          )}

          {activeTab === 'sessions' && (
            activeUser.mustChangePassword ? (
              <InlineAlert
                tone="warning"
                title="Tạm khóa danh sách phiên và nhật ký"
                action={
                  <Button type="button" size="sm" onClick={() => setActiveTab('security')}>
                    Đi tới Đổi mật khẩu
                  </Button>
                }
              >
                Vui lòng hoàn tất đổi mật khẩu bắt buộc trước khi xem và quản lý các phiên đăng nhập khác.
              </InlineAlert>
            ) : (
              <ProfileSessionsTab
                sessions={sessionsQuery.data ?? []}
                isLoadingSessions={sessionsQuery.isLoading}
                sessionsError={sessionsQuery.error}
                onRetrySessions={() => void sessionsQuery.refetch()}
                onRevokeSession={async (session) => {
                  await revokeSessionMutation.mutateAsync(session);
                }}
                onRevokeAllSessions={async () => {
                  await revokeAllMutation.mutateAsync();
                }}
                revokingSessionId={revokingSessionId}
                isRevokingAll={revokeAllMutation.isPending}
                activityLogs={activityQuery.data?.items ?? []}
                activityTotalCount={activityQuery.data?.totalCount ?? 0}
                activityHasNextPage={Boolean(activityQuery.data?.hasNextPage) && activityLimit < 30}
                isLoadingActivity={activityQuery.isLoading}
                isFetchingActivity={activityQuery.isFetching}
                activityError={activityQuery.error}
                activityCategory={activityCategory}
                onChangeActivityCategory={(category) => {
                  setActivityCategory(category);
                  setActivityLimit(15);
                }}
                selectedDate={selectedActivityDate}
                onSelectDate={(date) => {
                  setSelectedActivityDate(date);
                  setActivityLimit(15);
                }}
                onLoadMoreActivity={() => setActivityLimit((prev) => Math.min(30, prev + 15))}
                onRetryActivity={() => void activityQuery.refetch()}
                activitySummary={activitySummaryQuery.data ?? null}
                isLoadingSummary={activitySummaryQuery.isLoading}
              />
            )
          )}
        </div>

        <ProfileSecuritySidebar
          user={activeUser}
          activeSessionCount={sessionsQuery.data?.length ?? null}
          onJumpToSecurity={() => setActiveTab('security')}
        />
      </div>

      <ConfirmDialog
        open={blocker.state === 'blocked'}
        type="warning"
        title="Bạn có thay đổi chưa lưu"
        description="Rời khỏi trang hồ sơ sẽ bỏ các thay đổi số điện thoại hoặc mật khẩu bạn đang nhập."
        confirmText="Rời trang và bỏ thay đổi"
        cancelText="Ở lại chỉnh sửa"
        onClose={() => blocker.reset?.()}
        onConfirm={() => {
          setPhoneDraft(null);
          setCurrentPassword('');
          setNewPassword('');
          setConfirmPassword('');
          blocker.proceed?.();
        }}
      />
    </div>
  );
}
