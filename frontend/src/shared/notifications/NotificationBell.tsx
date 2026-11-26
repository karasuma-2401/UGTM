import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Bell, CheckCheck, RefreshCw } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import type { NotificationItem } from './notificationApi';
import {
  useNotificationInbox,
  type NotificationInboxFilter,
} from './useNotificationInbox';

export function formatNotificationRelativeTime(
  isoUtc: string,
  nowMs = Date.now(),
): string {
  const parsed = Date.parse(isoUtc);
  if (!Number.isFinite(parsed)) return '';
  const diffSeconds = Math.max(0, Math.floor((nowMs - parsed) / 1000));
  if (diffSeconds < 60) return 'Vừa xong';
  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) return `${diffMinutes} phút trước`;
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours} giờ trước`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays} ngày trước`;
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date(parsed));
}

const CONTENT_PREVIEW_LIMIT = 20;

function formatNotificationPreviewContent(content: string, expanded: boolean): {
  text: string;
  canExpand: boolean;
} {
  const normalized = content.trim();
  if (normalized.length <= CONTENT_PREVIEW_LIMIT) {
    return { text: normalized, canExpand: false };
  }

  if (expanded) {
    return { text: normalized, canExpand: true };
  }

  return {
    text: `${normalized.slice(0, CONTENT_PREVIEW_LIMIT).trimEnd()}...`,
    canExpand: true,
  };
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState<NotificationInboxFilter>('all');
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => new Set());
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const navigate = useNavigate();

  const {
    unreadCount,
    listQuery,
    items,
    markReadMutation,
    markAllReadMutation,
  } = useNotificationInbox({ open, filter });

  useEffect(() => {
    if (!open) return;
    function handlePointerDown(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    }
    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  function handleSelectItem(item: NotificationItem) {
    if (!item.isRead) {
      markReadMutation.mutate(item.id);
    }
    setOpen(false);
    if (item.actionPath && item.actionPath.startsWith('/')) {
      void navigate(item.actionPath);
    }
  }

  function toggleExpanded(id: string) {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  const badgeLabel = unreadCount > 99 ? '99+' : String(unreadCount);

  return (
    <div ref={containerRef} className="relative">
      <button
        ref={triggerRef}
        type="button"
        onClick={() => setOpen((prev) => !prev)}
        aria-label={
          unreadCount > 0
            ? `Mở thông báo (${unreadCount} chưa đọc)`
            : 'Mở thông báo'
        }
        aria-expanded={open}
        aria-haspopup="dialog"
        className={cn(
          'relative flex size-9 items-center justify-center rounded-xl border border-line-input/90 bg-surface-muted/70 text-ink-muted transition-colors hover:bg-surface-hover hover:text-ink-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary cursor-pointer',
          open && 'bg-surface-hover text-ink-primary border-line-strong',
        )}
      >
        <Bell
          className={cn(
            'size-4',
            unreadCount > 0 && !open && 'notification-bell-ring',
          )}
          aria-hidden="true"
        />
        {unreadCount > 0 && (
          <span
            aria-hidden="true"
            className="absolute -right-1 -top-1 flex min-w-[18px] h-[18px] items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-bold leading-none text-white ring-2 ring-surface-card"
          >
            {badgeLabel}
          </span>
        )}
      </button>

      {open && (
        <div
          role="dialog"
          aria-label="Hộp thư thông báo"
          className="absolute right-0 top-full z-50 mt-2 w-[min(24rem,calc(100vw-1.5rem))] overflow-hidden rounded-2xl border border-line-input/90 bg-surface-card shadow-xl animate-in fade-in-0 zoom-in-95 duration-150 dark:shadow-[0_20px_48px_rgba(0,0,0,0.6)]"
        >
          {/* Header */}
          <div className="flex items-center justify-between gap-2 border-b border-line-subtle px-4 py-3">
            <div className="flex items-center gap-2 min-w-0">
              <h2 className="text-sm font-semibold text-ink-primary">Thông báo</h2>
              {unreadCount > 0 && (
                <span className="inline-flex items-center rounded-full bg-primary/10 px-2 py-0.5 text-[11px] font-semibold text-primary">
                  {unreadCount} mới
                </span>
              )}
            </div>
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={() => markAllReadMutation.mutate()}
                disabled={markAllReadMutation.isPending}
                className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-medium text-primary transition-colors hover:bg-primary/10 disabled:opacity-50 cursor-pointer"
              >
                <CheckCheck className="size-3.5" aria-hidden="true" />
                Đọc tất cả
              </button>
            )}
          </div>

          {/* Filter Tabs */}
          <div className="flex items-center gap-1 border-b border-line-subtle bg-surface-subtle/50 px-3 py-2">
            {(
              [
                { id: 'all', label: 'Tất cả' },
                { id: 'unread', label: 'Chưa đọc' },
              ] as const
            ).map((tab) => {
              const active = filter === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setFilter(tab.id)}
                  aria-pressed={active}
                  className={cn(
                    'rounded-lg px-2.5 py-1 text-xs font-medium transition-colors cursor-pointer',
                    active
                      ? 'bg-surface-card text-primary shadow-2xs ring-1 ring-line-input/80'
                      : 'text-ink-muted hover:text-ink-primary hover:bg-surface-muted/70',
                  )}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>

          {/* Body */}
          <div className="max-h-[26rem] overflow-y-auto divide-y divide-line-subtle/70">
            {listQuery.isLoading && (
              <div className="space-y-3 p-4">
                <Skeleton className="h-14 w-full rounded-xl" />
                <Skeleton className="h-14 w-full rounded-xl" />
                <Skeleton className="h-14 w-full rounded-xl" />
              </div>
            )}

            {listQuery.isError && !listQuery.isLoading && (
              <div className="flex flex-col items-center gap-2.5 px-4 py-8 text-center">
                <AlertCircle className="size-6 text-tone-danger-icon" aria-hidden="true" />
                <p className="text-xs text-ink-muted">
                  Không thể tải danh sách thông báo. Vui lòng thử lại.
                </p>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={() => void listQuery.refetch()}
                >
                  <RefreshCw className="size-3.5 mr-1.5" />
                  Thử lại
                </Button>
              </div>
            )}

            {!listQuery.isLoading && !listQuery.isError && items.length === 0 && (
              <div className="flex flex-col items-center gap-2 px-4 py-10 text-center">
                <Bell className="size-7 text-ink-faint" aria-hidden="true" />
                <p className="text-sm font-medium text-ink-heading">
                  {filter === 'unread'
                    ? 'Bạn đã đọc hết thông báo'
                    : 'Chưa có thông báo nào'}
                </p>
                <p className="text-xs text-ink-muted">
                  Các cập nhật quan trọng về khóa luận sẽ hiển thị tại đây.
                </p>
              </div>
            )}

            {!listQuery.isLoading &&
              !listQuery.isError &&
              items.map((item) => {
                const isCritical = item.priority === 'Critical';
                const isExpanded = expandedIds.has(item.id);
                const { text: displayContent, canExpand } = formatNotificationPreviewContent(
                  item.content,
                  isExpanded,
                );
                return (
                  <div
                    key={item.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => handleSelectItem(item)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        handleSelectItem(item);
                      }
                    }}
                    className={cn(
                      'w-full px-4 py-3 text-left transition-colors hover:bg-surface-hover/80 focus-visible:outline focus-visible:outline-2 focus-visible:outline-inset focus-visible:outline-primary cursor-pointer',
                      !item.isRead && 'bg-primary/[0.04] dark:bg-primary/[0.08]',
                    )}
                  >
                    <div className="flex items-start gap-2.5">
                      <span
                        aria-hidden="true"
                        className={cn(
                          'mt-1.5 size-2 shrink-0 rounded-full',
                          item.isRead
                            ? 'bg-transparent'
                            : isCritical
                              ? 'bg-red-500'
                              : 'bg-primary',
                        )}
                      />
                      <div className="min-w-0 flex-1 space-y-1">
                        <div className="flex items-start justify-between gap-2">
                          <p
                            className={cn(
                              'text-xs leading-snug line-clamp-2',
                              item.isRead
                                ? 'font-medium text-ink-secondary'
                                : 'font-semibold text-ink-primary',
                            )}
                          >
                            {item.title}
                          </p>
                          {isCritical && (
                            <span className="shrink-0 rounded-md bg-tone-danger-bg px-1.5 py-0.5 text-[10px] font-semibold text-tone-danger-text">
                              Quan trọng
                            </span>
                          )}
                        </div>
                        <p
                          className={cn(
                            'text-xs leading-relaxed text-ink-muted',
                            isExpanded ? 'whitespace-pre-line break-words' : 'break-words',
                          )}
                        >
                          {displayContent}
                        </p>
                        <div className="flex items-center justify-between gap-2 pt-0.5">
                          <p className="text-[11px] text-ink-subtle">
                            {formatNotificationRelativeTime(item.createdAt)}
                          </p>
                          {canExpand && (
                            <button
                              type="button"
                              aria-expanded={isExpanded}
                              onClick={(event) => {
                                event.stopPropagation();
                                toggleExpanded(item.id);
                              }}
                              onKeyDown={(event) => {
                                if (event.key === 'Enter' || event.key === ' ') {
                                  event.stopPropagation();
                                }
                              }}
                              className="ml-auto shrink-0 rounded px-1.5 py-0.5 text-[11px] font-medium text-primary transition-colors hover:bg-primary/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary cursor-pointer"
                            >
                              {isExpanded ? 'Ẩn bớt' : 'Chi tiết'}
                            </button>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
          </div>
        </div>
      )}
    </div>
  );
}
