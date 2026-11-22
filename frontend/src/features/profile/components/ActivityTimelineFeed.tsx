import { useMemo, useState } from 'react';
import {
  Activity,
  ChevronDown,
  ChevronsUpDown,
  KeyRound,
  LogIn,
  LogOut,
  MonitorOff,
  RefreshCw,
  ShieldAlert,
  UserCheck,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { StatusBadge } from '@/components/ui/status-badge';
import {
  formatAuditAction,
  formatAuditRelativeTime,
  formatAuditShortTime,
} from '@/features/audit/utils/parseAuditDiff';
import type { AuditLogItem } from '@/features/audit/types/auditLog.types';
import { cn } from '@/lib/utils';
import { groupActivityByMonthAndAction } from '../utils/profileFormatters';

interface ActivityTimelineFeedProps {
  logs: AuditLogItem[];
  isFetching?: boolean;
}

function ClusterIcon({ action }: { action: string }) {
  if (action === 'Identity.Authentication.Login') {
    return <LogIn className="size-3.5" aria-hidden="true" />;
  }
  if (action === 'Identity.Session.Refreshed') {
    return <RefreshCw className="size-3.5" aria-hidden="true" />;
  }
  if (action === 'Identity.Session.LoggedOut') {
    return <LogOut className="size-3.5" aria-hidden="true" />;
  }
  if (
    action === 'Identity.Session.Revoked' ||
    action === 'Identity.User.SessionsRevoked'
  ) {
    return <MonitorOff className="size-3.5" aria-hidden="true" />;
  }
  if (action === 'Identity.Session.RefreshTokenReuseDetected') {
    return <ShieldAlert className="size-3.5" aria-hidden="true" />;
  }
  if (
    action.includes('Password') ||
    action === 'Identity.Account.Activated' ||
    action === 'Identity.Account.RecoveryOtpVerified'
  ) {
    return <KeyRound className="size-3.5" aria-hidden="true" />;
  }
  if (action === 'Identity.Profile.Updated' || action === 'Identity.User.Updated') {
    return <UserCheck className="size-3.5" aria-hidden="true" />;
  }
  return <Activity className="size-3.5" aria-hidden="true" />;
}

function OutcomeBadge({ outcome }: { outcome: string }) {
  if (outcome === 'Success') return <StatusBadge tone="success">Thành công</StatusBadge>;
  if (outcome === 'Denied') return <StatusBadge tone="warning">Từ chối</StatusBadge>;
  if (outcome === 'Failed') return <StatusBadge tone="error">Thất bại</StatusBadge>;
  return <StatusBadge tone="neutral">{outcome}</StatusBadge>;
}

function OutcomeDot({ outcome }: { outcome: string }) {
  const color =
    outcome === 'Success'
      ? 'bg-emerald-500'
      : outcome === 'Denied'
        ? 'bg-amber-500'
        : outcome === 'Failed'
          ? 'bg-rose-500'
          : 'bg-slate-400';
  return <span className={cn('size-2 rounded-full shrink-0', color)} aria-hidden="true" />;
}

function formatDayShort(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit',
    month: '2-digit',
  }).format(date);
}

export function ActivityTimelineFeed({ logs, isFetching = false }: ActivityTimelineFeedProps) {
  const monthGroups = useMemo(() => groupActivityByMonthAndAction(logs), [logs]);
  const [toggledIds, setToggledIds] = useState<Record<string, boolean>>({});

  const toggleCluster = (id: string, currentlyExpanded: boolean) => {
    setToggledIds((prev) => ({ ...prev, [id]: !currentlyExpanded }));
  };

  return (
    <div className={cn('space-y-6 transition-opacity', isFetching && 'opacity-65')}>
      {monthGroups.map((month) => (
        <div key={month.monthKey} className="space-y-3">
          {/* Month + Year Header with horizontal rule */}
          <div className="flex items-center gap-3">
            <h3 className="text-xs font-semibold text-ink-primary whitespace-nowrap">
              {month.monthTitle}{' '}
              <span className="font-normal text-ink-muted tabular-nums">{month.yearTitle}</span>
            </h3>
            <div className="h-px flex-1 bg-line-subtle" aria-hidden="true" />
          </div>

          {/* Vertical GitHub-style timeline */}
          <div className="relative ml-3.5 pl-6 border-l border-line space-y-4 pb-1">
            {month.clusters.map((cluster, clusterIndex) => {
              const hasAlerts = cluster.alertCount > 0;
              const isMulti = cluster.items.length > 1;
              const defaultExpanded = clusterIndex === 0 || hasAlerts || !isMulti;
              const isExpanded = toggledIds[cluster.id] ?? defaultExpanded;
              const actionLabel = formatAuditAction(cluster.action);

              return (
                <div key={cluster.id} className="relative">
                  {/* Node icon sitting on the vertical timeline rail */}
                  <span
                    className={cn(
                      'absolute -left-[39px] top-0.5 flex size-7 items-center justify-center rounded-full border ring-4 ring-surface-card',
                      hasAlerts
                        ? 'border-tone-warning-border bg-tone-warning-bg text-tone-warning-icon'
                        : 'border-line bg-surface-subtle text-ink-muted',
                    )}
                  >
                    <ClusterIcon action={cluster.action} />
                  </span>

                  {/* Cluster summary row */}
                  <button
                    type="button"
                    aria-expanded={isExpanded}
                    onClick={() => toggleCluster(cluster.id, isExpanded)}
                    className="group flex w-full items-center justify-between gap-3 py-1 text-left cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary rounded-lg"
                  >
                    <div className="flex flex-wrap items-center gap-2 min-w-0">
                      <span className="text-sm font-semibold text-ink-primary group-hover:text-primary transition-colors">
                        {actionLabel}
                      </span>
                      {isMulti && (
                        <Badge variant="outline" className="bg-surface-subtle text-[11px] tabular-nums">
                          {cluster.items.length} lần
                        </Badge>
                      )}
                      {hasAlerts && (
                        <Badge variant="warning" className="text-[11px] tabular-nums">
                          {cluster.alertCount} cảnh báo
                        </Badge>
                      )}
                    </div>

                    <span className="flex items-center gap-2 shrink-0 text-xs text-ink-muted tabular-nums">
                      <span>{formatDayShort(cluster.latestTimestamp)}</span>
                      {isExpanded ? (
                        <ChevronDown className="size-3.5 text-ink-subtle" aria-hidden="true" />
                      ) : (
                        <ChevronsUpDown className="size-3.5 text-ink-subtle" aria-hidden="true" />
                      )}
                    </span>
                  </button>

                  {/* Nested individual events */}
                  {isExpanded && (
                    <div className="mt-2 rounded-xl border border-line/80 bg-surface-subtle/40 divide-y divide-line-subtle overflow-hidden">
                      {cluster.items.map((item) => {
                        const relative = formatAuditRelativeTime(item.timestamp);
                        return (
                          <div
                            key={item.id}
                            className="flex flex-wrap items-center justify-between gap-3 px-3.5 py-2.5 text-xs bg-surface-card/70 hover:bg-surface-subtle/60 transition-colors"
                          >
                            <div className="flex items-center gap-2.5 min-w-0">
                              <OutcomeDot outcome={item.outcome} />
                              <div className="min-w-0">
                                <span className="font-medium text-ink-primary">
                                  {formatAuditShortTime(item.timestamp)}
                                </span>
                                {relative && (
                                  <span className="text-ink-muted"> · {relative}</span>
                                )}
                                {item.reasonCode && (
                                  <span className="ml-1.5 rounded bg-surface-muted px-1.5 py-0.5 font-mono text-[10px] text-ink-secondary">
                                    {item.reasonCode}
                                  </span>
                                )}
                              </div>
                            </div>
                            <OutcomeBadge outcome={item.outcome} />
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
