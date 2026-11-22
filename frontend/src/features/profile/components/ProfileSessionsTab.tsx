import { useState } from 'react';
import {
  Globe,
  LogOut,
  Monitor,
  ShieldCheck,
  Smartphone,
  Tablet,
  Trash2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { ProgressBar } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import type { AuditLogItem } from '@/features/audit/types/auditLog.types';
import type {
  ActivityCategory,
  SessionItem,
  UserActivitySummary,
} from '@/features/auth/types/auth.types';
import { cn } from '@/lib/utils';
import {
  computeSessionRemaining,
  formatVietnamDateTime,
  getProfileErrorMessage,
  parseUserAgent,
} from '../utils/profileFormatters';
import { ActivityContributionGrid } from './ActivityContributionGrid';
import { ActivityTimelineFeed } from './ActivityTimelineFeed';

interface ProfileSessionsTabProps {
  sessions: SessionItem[];
  isLoadingSessions: boolean;
  sessionsError: unknown;
  onRetrySessions: () => void;
  onRevokeSession: (session: SessionItem) => Promise<void>;
  onRevokeAllSessions: () => Promise<void>;
  revokingSessionId: string | null;
  isRevokingAll: boolean;
  activityLogs: AuditLogItem[];
  activityTotalCount: number;
  activityHasNextPage: boolean;
  isLoadingActivity: boolean;
  isFetchingActivity: boolean;
  activityError: unknown;
  activityCategory: ActivityCategory;
  onChangeActivityCategory: (category: ActivityCategory) => void;
  selectedDate: string | null;
  onSelectDate: (date: string | null) => void;
  onLoadMoreActivity: () => void;
  onRetryActivity: () => void;
  activitySummary: UserActivitySummary | null;
  isLoadingSummary: boolean;
}

const ACTIVITY_CATEGORIES: { id: ActivityCategory; label: string }[] = [
  { id: 'all', label: 'Tất cả' },
  { id: 'auth', label: 'Đăng nhập' },
  { id: 'password', label: 'Mật khẩu' },
  { id: 'session', label: 'Phiên' },
  { id: 'profile', label: 'Hồ sơ' },
];

function DeviceIcon({ type }: { type: ReturnType<typeof parseUserAgent>['deviceType'] }) {
  if (type === 'mobile') return <Smartphone className="size-4" aria-hidden="true" />;
  if (type === 'tablet') return <Tablet className="size-4" aria-hidden="true" />;
  if (type === 'desktop') return <Monitor className="size-4" aria-hidden="true" />;
  return <Globe className="size-4" aria-hidden="true" />;
}

export function ProfileSessionsTab({
  sessions,
  isLoadingSessions,
  sessionsError,
  onRetrySessions,
  onRevokeSession,
  onRevokeAllSessions,
  revokingSessionId,
  isRevokingAll,
  activityLogs,
  activityTotalCount,
  activityHasNextPage,
  isLoadingActivity,
  isFetchingActivity,
  activityError,
  activityCategory,
  onChangeActivityCategory,
  selectedDate,
  onSelectDate,
  onLoadMoreActivity,
  onRetryActivity,
  activitySummary,
  isLoadingSummary,
}: ProfileSessionsTabProps) {
  const [confirmSession, setConfirmSession] = useState<SessionItem | null>(null);
  const [confirmAllOpen, setConfirmAllOpen] = useState(false);

  const handleConfirmSingle = async () => {
    if (!confirmSession) return;
    try {
      await onRevokeSession(confirmSession);
      setConfirmSession(null);
    } catch {
      // Toast handled by mutation
    }
  };

  const handleConfirmAll = async () => {
    try {
      await onRevokeAllSessions();
      setConfirmAllOpen(false);
    } catch {
      // Toast handled by mutation
    }
  };

  return (
    <>
      <Card className="overflow-hidden divide-y divide-line-subtle">
        {/* Section 1: Active Sessions */}
        <section aria-label="Phiên đăng nhập đang hoạt động" className="p-5 sm:p-6 space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3">
            <div className="min-w-0 flex-1">
              <h2 className="text-base font-semibold text-ink-primary">
                Thiết bị và Phiên đăng nhập
              </h2>
              <p className="mt-0.5 text-xs text-ink-muted leading-relaxed">
                Tự hết hạn sau 7 ngày không hoạt động hoặc tối đa 30 ngày.
              </p>
            </div>

            {sessions.length > 0 && (
              <div className="flex items-center gap-2 shrink-0 self-end sm:self-start sm:ml-auto">
                <Button
                  type="button"
                  variant="dangerOutline"
                  size="sm"
                  disabled={isRevokingAll || revokingSessionId !== null}
                  leftIcon={<LogOut className="size-3.5" />}
                  onClick={() => setConfirmAllOpen(true)}
                >
                  Đăng xuất tất cả
                </Button>
              </div>
            )}
          </div>

          {sessionsError ? (
            <InlineAlert
              tone="error"
              title="Không tải được danh sách phiên"
              action={
                <Button type="button" size="sm" variant="outline" onClick={onRetrySessions}>
                  Thử lại
                </Button>
              }
            >
              {getProfileErrorMessage(
                sessionsError,
                'Không thể lấy danh sách phiên đăng nhập hiện tại.',
              )}
            </InlineAlert>
          ) : isLoadingSessions ? (
            <div className="space-y-3">
              <Skeleton className="h-24 w-full rounded-xl" />
              <Skeleton className="h-24 w-full rounded-xl" />
            </div>
          ) : sessions.length === 0 ? (
            <div className="rounded-xl border border-dashed border-line-input bg-surface-subtle/50 p-6 text-center space-y-1">
              <ShieldCheck className="size-6 mx-auto text-ink-subtle" aria-hidden="true" />
              <p className="text-sm font-medium text-ink-primary">
                Không ghi nhận phiên đăng nhập nào đang hoạt động
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {sessions.map((session) => {
                const device = parseUserAgent(session.userAgentSummary);
                const idle = computeSessionRemaining(session.idleExpiresAt, 7);
                const absolute = computeSessionRemaining(session.absoluteExpiresAt, 30);
                const isRevokingThis = revokingSessionId === session.id;

                return (
                  <div
                    key={session.id}
                    className={cn(
                      'rounded-xl border p-4 transition-colors space-y-3',
                      session.isCurrent
                        ? 'border-tone-info-border bg-tone-info-bg/35'
                        : 'border-line/80 bg-surface-subtle/40 hover:bg-surface-subtle/70',
                    )}
                  >
                    <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3">
                      <div className="flex items-start gap-3 min-w-0 flex-1">
                        <span
                          className={cn(
                            'flex size-10 shrink-0 items-center justify-center rounded-xl border',
                            session.isCurrent
                              ? 'border-tone-info-border bg-surface-card text-primary'
                              : 'border-line-input bg-surface-card text-ink-muted',
                          )}
                        >
                          <DeviceIcon type={device.deviceType} />
                        </span>

                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <h3 className="text-sm font-semibold text-ink-primary">
                              {device.title}
                            </h3>
                            {session.isCurrent && (
                              <span className="inline-flex items-center gap-1.5 rounded-full border border-tone-success-border bg-tone-success-bg px-2.5 py-0.5 text-[11px] font-semibold text-tone-success-strong">
                                <span className="relative flex size-2">
                                  <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
                                  <span className="relative inline-flex size-2 rounded-full bg-emerald-500" />
                                </span>
                                Phiên hiện tại
                              </span>
                            )}
                          </div>
                          <p className="mt-0.5 text-xs text-ink-muted tabular-nums">
                            Đăng nhập lúc {formatVietnamDateTime(session.authenticatedAt)} · Hoạt động cuối{' '}
                            {formatVietnamDateTime(session.lastSeenAt)}
                          </p>
                          {session.userAgentSummary && (
                            <p
                              className="mt-1 truncate max-w-xl text-[11px] text-ink-subtle"
                              title={session.userAgentSummary}
                            >
                              {session.userAgentSummary}
                            </p>
                          )}
                        </div>
                      </div>

                      <Button
                        type="button"
                        variant={session.isCurrent ? 'dangerOutline' : 'outline'}
                        size="sm"
                        className="shrink-0 self-end sm:self-start sm:ml-auto"
                        disabled={isRevokingAll || revokingSessionId !== null}
                        isLoading={isRevokingThis}
                        leftIcon={!isRevokingThis ? <Trash2 className="size-3.5" /> : undefined}
                        onClick={() => setConfirmSession(session)}
                      >
                        {session.isCurrent ? 'Đăng xuất phiên này' : 'Thu hồi'}
                      </Button>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 border-t border-line-subtle/80">
                      <ProgressBar
                        size="xs"
                        value={idle.percent}
                        variant={idle.isExpiringSoon ? 'warning' : 'default'}
                        label="Hạn nhàn rỗi (7 ngày)"
                        valueText={idle.remainingText}
                        labelClassName="text-[11px] font-medium text-ink-muted"
                        valueClassName="text-[11px] font-semibold tabular-nums text-ink-secondary"
                      />
                      <ProgressBar
                        size="xs"
                        value={absolute.percent}
                        variant={absolute.isExpiringSoon ? 'warning' : 'info'}
                        label="Hạn phiên tối đa (30 ngày)"
                        valueText={absolute.remainingText}
                        labelClassName="text-[11px] font-medium text-ink-muted"
                        valueClassName="text-[11px] font-semibold tabular-nums text-ink-secondary"
                      />
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </section>

        {/* Section 2: 16-Week Activity Contribution Heatmap + Filterable Security Activity Feed */}
        <section aria-label="Nhật ký bảo mật cá nhân" className="p-5 sm:p-6 space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <h2 className="text-base font-semibold text-ink-primary">
                  Nhật ký hoạt động bảo mật
                </h2>
                <Badge variant="outline" className="bg-surface-subtle tabular-nums">
                  {activityTotalCount} sự kiện
                </Badge>
              </div>
              <p className="mt-0.5 text-xs text-ink-muted">
                Biểu đồ tần suất 36 tuần gần nhất kết hợp dòng thời gian chi tiết theo tháng.
              </p>
            </div>
          </div>

          {activitySummary && activitySummary.recentAlertCount7d > 0 && (
            <InlineAlert
              tone="warning"
              title={`Có ${activitySummary.recentAlertCount7d} cảnh báo bảo mật trong 7 ngày qua`}
            >
              Hệ thống ghi nhận lần đăng nhập thất bại hoặc thao tác bị từ chối gần đây. Hãy kiểm tra các phiên đang hoạt động phía trên hoặc đổi mật khẩu nếu thấy bất thường.
            </InlineAlert>
          )}

          {isLoadingSummary ? (
            <Skeleton className="h-40 w-full rounded-xl" />
          ) : activitySummary ? (
            <ActivityContributionGrid
              summary={activitySummary}
              selectedDate={selectedDate}
              onSelectDate={onSelectDate}
            />
          ) : null}

          {/* Category Filter Chips */}
          <div
            role="group"
            aria-label="Lọc theo nhóm sự kiện"
            className="flex flex-wrap items-center gap-1.5"
          >
            {ACTIVITY_CATEGORIES.map((cat) => {
              const active = activityCategory === cat.id;
              return (
                <button
                  key={cat.id}
                  type="button"
                  aria-pressed={active}
                  onClick={() => onChangeActivityCategory(cat.id)}
                  className={cn(
                    'rounded-full border px-3 py-1 text-xs font-medium transition-colors cursor-pointer',
                    active
                      ? 'border-primary/40 bg-primary/10 text-primary dark:bg-primary/15'
                      : 'border-line-input bg-surface-card text-ink-muted hover:bg-surface-hover hover:text-ink-primary',
                  )}
                >
                  {cat.label}
                </button>
              );
            })}
          </div>

          {activityError ? (
            <InlineAlert
              tone="error"
              title="Không tải được nhật ký hoạt động"
              action={
                <Button type="button" size="sm" variant="outline" onClick={onRetryActivity}>
                  Thử lại
                </Button>
              }
            >
              {getProfileErrorMessage(
                activityError,
                'Không thể lấy lịch sử hoạt động bảo mật cá nhân.',
              )}
            </InlineAlert>
          ) : isLoadingActivity ? (
            <div className="space-y-2.5">
              <Skeleton className="h-12 w-full rounded-xl" />
              <Skeleton className="h-12 w-full rounded-xl" />
              <Skeleton className="h-12 w-full rounded-xl" />
            </div>
          ) : activityLogs.length === 0 ? (
            <p className="rounded-xl border border-dashed border-line-input bg-surface-subtle/50 p-5 text-center text-xs text-ink-muted">
              {selectedDate || activityCategory !== 'all'
                ? 'Không có sự kiện bảo mật nào khớp với bộ lọc đang chọn.'
                : 'Chưa có sự kiện bảo mật nào được ghi nhận cho tài khoản này.'}
            </p>
          ) : (
            <div className="space-y-4">
              <ActivityTimelineFeed logs={activityLogs} isFetching={isFetchingActivity} />

              {activityHasNextPage && (
                <div className="flex justify-center pt-1">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    isLoading={isFetchingActivity}
                    onClick={onLoadMoreActivity}
                  >
                    Xem thêm sự kiện
                  </Button>
                </div>
              )}
            </div>
          )}
        </section>
      </Card>

      <ConfirmDialog
        open={confirmSession !== null}
        onClose={() => setConfirmSession(null)}
        onConfirm={() => void handleConfirmSingle()}
        type={confirmSession?.isCurrent ? 'error' : 'warning'}
        title={confirmSession?.isCurrent ? 'Đăng xuất khỏi phiên hiện tại?' : 'Thu hồi phiên đăng nhập?'}
        description={
          confirmSession && (
            <p>
              {confirmSession.isCurrent
                ? 'Bạn đang thu hồi chính phiên trên thiết bị này. Sau khi xác nhận, hệ thống sẽ đăng xuất và chuyển bạn về màn hình đăng nhập.'
                : `Thiết bị "${parseUserAgent(confirmSession.userAgentSummary).title}" sẽ bị đăng xuất ngay lập tức ở yêu cầu kế tiếp.`}
            </p>
          )
        }
        confirmText={confirmSession?.isCurrent ? 'Đăng xuất ngay' : 'Thu hồi phiên'}
        cancelText="Hủy"
        isConfirming={revokingSessionId !== null}
      />

      <ConfirmDialog
        open={confirmAllOpen}
        onClose={() => setConfirmAllOpen(false)}
        onConfirm={() => void handleConfirmAll()}
        type="error"
        title="Đăng xuất khỏi tất cả thiết bị?"
        description="Toàn bộ các phiên đăng nhập đang hoạt động (bao gồm cả thiết bị hiện tại) sẽ bị thu hồi ngay lập tức. Bạn sẽ cần đăng nhập lại để tiếp tục sử dụng."
        confirmText="Đăng xuất mọi thiết bị"
        cancelText="Hủy"
        isConfirming={isRevokingAll}
      />
    </>
  );
}
