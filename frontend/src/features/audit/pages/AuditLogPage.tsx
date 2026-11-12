import React, { useState, useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ScrollText, RefreshCw, Download } from 'lucide-react';
import { InlineAlert } from '@/components/ui/inline-alert';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import { AuditLogStatusTabs, type AuditOutcomeStats } from '../components/AuditLogStatusTabs';
import { AuditLogFilterBar } from '../components/AuditLogFilterBar';
import { AuditLogTable } from '../components/AuditLogTable';
import { AuditLogDetailDrawer } from '../components/AuditLogDetailDrawer';
import { auditLogApi, getAuditLogErrorMessage } from '../services/auditLogApi';
import type {
  AuditLogItem,
  AuditLogFilterState,
  AuditLogQueryParams,
  AuditTimePreset,
} from '../types/auditLog.types';

const INITIAL_FILTER: AuditLogFilterState = {
  searchTerm: '',
  action: '',
  outcome: '',
  fromUtc: '',
  toUtc: '',
  timePreset: 'all',
  requestId: '',
  targetId: '',
  actorSessionId: '',
};
const EMPTY_STATS: AuditOutcomeStats = { total: 0, success: 0, denied: 0, failed: 0 };
const validDate = (date: string) =>
  /^\d{4}-\d{2}-\d{2}$/.test(date) &&
  !Number.isNaN(Date.parse(`${date}T00:00:00Z`)) &&
  new Date(`${date}T00:00:00Z`).toISOString().slice(0, 10) === date;
const validGuid = (value: string) =>
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
function filterFromUrl(params: URLSearchParams): AuditLogFilterState {
  const from = params.get('from') || '';
  const to = params.get('to') || '';
  const preset = params.get('preset');
  const outcome = params.get('outcome') || '';
  const session = params.get('actorSessionId') || '';
  return {
    searchTerm: (params.get('search') || '').slice(0, 200),
    action: (params.get('action') || '').slice(0, 100),
    outcome: ['Success', 'Denied', 'Failed'].includes(outcome) ? outcome : '',
    fromUtc: validDate(from) ? from : '',
    toUtc: validDate(to) ? to : '',
    timePreset: (['all', 'today', '7d', '30d', 'custom'].includes(preset || '')
      ? preset
      : from || to
        ? 'custom'
        : 'all') as AuditTimePreset,
    requestId: (params.get('requestId') || '').slice(0, 100),
    targetId: (params.get('targetId') || '').slice(0, 100),
    actorSessionId: validGuid(session) ? session : '',
  };
}
function queryFor(filter: AuditLogFilterState): AuditLogQueryParams {
  return {
    searchTerm: filter.searchTerm || undefined,
    action: filter.action || undefined,
    outcome: filter.outcome || undefined,
    fromUtc: filter.fromUtc || undefined,
    toUtc: filter.toUtc || undefined,
    requestId: filter.requestId || undefined,
    targetId: filter.targetId || undefined,
    actorSessionId: filter.actorSessionId || undefined,
  };
}
function writeFilter(params: URLSearchParams, filter: AuditLogFilterState) {
  const values: Record<string, string | undefined> = {
    search: filter.searchTerm.trim(),
    action: filter.action,
    outcome: filter.outcome,
    from: filter.fromUtc,
    to: filter.toUtc,
    preset: filter.timePreset === 'all' ? '' : filter.timePreset,
    requestId: filter.requestId?.trim(),
    targetId: filter.targetId?.trim(),
    actorSessionId: filter.actorSessionId?.trim(),
  };
  for (const [key, value] of Object.entries(values)) {
    if (value) params.set(key, value);
    else params.delete(key);
  }
}

export const AuditLogPage: React.FC = () => {
  const [params, setParams] = useSearchParams();
  const paramKey = params.toString();
  const applied = useMemo(() => filterFromUrl(new URLSearchParams(paramKey)), [paramKey]);
  const filterKey = JSON.stringify(applied);
  const [filter, setFilter] = useState<AuditLogFilterState>(applied);
  const [logs, setLogs] = useState<AuditLogItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [stats, setStats] = useState<AuditOutcomeStats>(EMPTY_STATS);
  const [actions, setActions] = useState<string[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<AuditLogItem | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [listError, setListError] = useState<string | null>(null);
  const [statsError, setStatsError] = useState<string | null>(null);
  const [actionsError, setActionsError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingStats, setLoadingStats] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const pageParam = Number(params.get('page'));
  const page =
    Number.isSafeInteger(pageParam) && pageParam > 0 && pageParam <= 1_000_000 ? pageParam : 1;
  const size = [10, 20, 50].includes(Number(params.get('pageSize')))
    ? Number(params.get('pageSize'))
    : 10;
  const eventId = /^\d+$/.test(params.get('event') || '') ? Number(params.get('event')) : 0;

  useEffect(() => {
    setFilter(JSON.parse(filterKey));
  }, [filterKey]);
  useEffect(() => {
    const controller = new AbortController();
    auditLogApi
      .getActions(controller.signal)
      .then((items) => {
        setActions(items);
        setActionsError(false);
      })
      .catch(() => {
        if (!controller.signal.aborted) setActionsError(true);
      });
    return () => controller.abort();
  }, [refresh]);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setListError(null);
    auditLogApi
      .getAuditLogs(
        { ...queryFor(JSON.parse(filterKey)), pageNumber: page, pageSize: size },
        controller.signal,
      )
      .then((result) => {
        if (!controller.signal.aborted) {
          setLogs(result.items);
          setTotalCount(result.totalCount);
        }
      })
      .catch((err) => {
        if (!controller.signal.aborted) {
          setListError(getAuditLogErrorMessage(err));
          setLogs([]);
          setTotalCount(0);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [filterKey, page, size, refresh]);
  const statsKey = JSON.stringify({ ...applied, outcome: '' });
  useEffect(() => {
    const controller = new AbortController();
    setLoadingStats(true);
    setStatsError(null);
    auditLogApi
      .getStatistics(queryFor(JSON.parse(statsKey)), controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setStats(result);
      })
      .catch((err) => {
        if (!controller.signal.aborted) setStatsError(getAuditLogErrorMessage(err));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoadingStats(false);
      });
    return () => controller.abort();
  }, [statsKey, refresh]);
  useEffect(() => {
    if (!eventId || !Number.isSafeInteger(eventId)) {
      setDetail(null);
      setDetailError(null);
      return;
    }
    const controller = new AbortController();
    setDetailError(null);
    auditLogApi
      .getById(eventId, controller.signal)
      .then((item) => {
        if (!controller.signal.aborted) {
          setDetail(item);
          setSelectedId(item.id);
        }
      })
      .catch((err) => {
        if (!controller.signal.aborted) {
          setDetail(null);
          setDetailError(getAuditLogErrorMessage(err));
        }
      });
    return () => controller.abort();
  }, [eventId]);
  const update = (change: (next: URLSearchParams) => void) => {
    const next = new URLSearchParams(params);
    change(next);
    setParams(next);
  };
  const apply = () => {
    if (
      (filter.fromUtc && !validDate(filter.fromUtc)) ||
      (filter.toUtc && !validDate(filter.toUtc)) ||
      (filter.fromUtc && filter.toUtc && filter.fromUtc > filter.toUtc)
    ) {
      setError('Khoảng ngày không hợp lệ.');
      return;
    }
    if (filter.actorSessionId && !validGuid(filter.actorSessionId.trim())) {
      setError('Session ID phải là GUID hợp lệ.');
      return;
    }
    setError(null);
    update((next) => {
      writeFilter(next, filter);
      next.delete('page');
      next.delete('event');
    });
  };
  const setEvent = (id?: number) =>
    update((next) => {
      if (id) next.set('event', String(id));
      else next.delete('event');
    });
  const exportCsv = async () => {
    setExporting(true);
    setError(null);
    try {
      await auditLogApi.exportCsv(queryFor(applied));
    } catch (err) {
      setError(getAuditLogErrorMessage(err));
    } finally {
      setExporting(false);
    }
  };
  return (
    <div className="min-w-0 space-y-5">
      <PageHeader compact accent title="Nhật ký thao tác" icon={<ScrollText />}>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={exporting}
          onClick={exportCsv}
          leftIcon={<Download className="size-3.5" />}
        >
          {exporting ? 'Đang xuất...' : 'Xuất CSV'}
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={loading || loadingStats}
          onClick={() => setRefresh((v) => v + 1)}
          leftIcon={
            <RefreshCw className={loading || loadingStats ? 'animate-spin size-3.5' : 'size-3.5'} />
          }
        >
          Làm mới
        </Button>
      </PageHeader>
      {error && <InlineAlert tone="error">{error}</InlineAlert>}
      {listError && (
        <InlineAlert tone="error">
          {listError}{' '}
          <button type="button" className="underline" onClick={() => setRefresh((v) => v + 1)}>
            Thử lại
          </button>
        </InlineAlert>
      )}
      {statsError && (
        <InlineAlert tone="error">
          Không tải được thống kê.{' '}
          <button type="button" className="underline" onClick={() => setRefresh((v) => v + 1)}>
            Thử lại
          </button>
        </InlineAlert>
      )}
      {actionsError && (
        <InlineAlert tone="warning">
          Không tải được toàn bộ danh mục thao tác.{' '}
          <button type="button" className="underline" onClick={() => setRefresh((v) => v + 1)}>
            Thử lại
          </button>
        </InlineAlert>
      )}
      {detailError && (
        <InlineAlert tone="error">
          Không tải được sự kiện. {detailError}{' '}
          <button type="button" className="underline" onClick={() => setEvent()}>
            Đóng
          </button>
        </InlineAlert>
      )}
      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        <ListCardHeader
          title="Sự kiện hệ thống"
          icon={<ScrollText />}
          description={
            listError
              ? 'Không tải được số lượng'
              : `${totalCount.toLocaleString('vi-VN')} sự kiện ghi nhận`
          }
          className="min-h-0 lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]"
        >
          <div className="flex items-center justify-end w-full">
            <AuditLogStatusTabs
              currentOutcome={applied.outcome}
              onSelectOutcome={(outcome) =>
                update((next) => {
                  if (outcome) next.set('outcome', outcome);
                  else next.delete('outcome');
                  next.delete('page');
                  next.delete('event');
                })
              }
              stats={stats}
              loading={loadingStats}
              error={Boolean(statsError)}
            />
          </div>
        </ListCardHeader>
        <AuditLogFilterBar
          filter={filter}
          onFilterChange={setFilter}
          onApply={apply}
          onReset={() => {
            setFilter(INITIAL_FILTER);
            setError(null);
            update((next) => {
              writeFilter(next, INITIAL_FILTER);
              next.delete('page');
              next.delete('event');
            });
          }}
          isLoading={loading}
          actions={actions}
        />
        {!listError && (
          <AuditLogTable
            logs={logs}
            selectedId={selectedId}
            onSelect={(log) => setSelectedId(log.id)}
            onViewDetails={(log) => setEvent(log.id)}
            pageNumber={page}
            pageSize={size}
            totalCount={totalCount}
            onPageChange={(n) =>
              update((next) => {
                next.set('page', String(n));
                next.delete('event');
              })
            }
            onPageSizeChange={(n) =>
              update((next) => {
                next.set('pageSize', String(n));
                next.delete('page');
                next.delete('event');
              })
            }
            isLoading={loading}
          />
        )}
      </Card>
      <AuditLogDetailDrawer
        log={detail}
        open={Boolean(detail && eventId)}
        onClose={() => setEvent()}
        onViewRequest={(requestId) =>
          update((next) => {
            writeFilter(next, { ...INITIAL_FILTER, requestId });
            next.delete('page');
            next.delete('event');
          })
        }
      />
    </div>
  );
};
