import { InlineAlert } from '@/components/ui/inline-alert';
import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { ScrollText, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PageHeader } from '@/components/ui/page-header';
import { ListCardHeader } from '@/components/ui/list-card-header';
import {
  AuditLogStatusTabs,
  type AuditOutcomeStats,
} from '../components/AuditLogStatusTabs';
import { AuditLogFilterBar } from '../components/AuditLogFilterBar';
import { AuditLogTable } from '../components/AuditLogTable';
import { AuditLogDetailDrawer } from '../components/AuditLogDetailDrawer';
import { auditLogApi, getAuditLogErrorMessage } from '../services/auditLogApi';
import type {
  AuditLogItem,
  AuditLogFilterState,
} from '../types/auditLog.types';

const INITIAL_FILTER: AuditLogFilterState = {
  searchTerm: '',
  action: '',
  outcome: '',
  fromUtc: '',
  toUtc: '',
  timePreset: 'all',
  academicPeriod: '2026-1',
};

export const AuditLogPage: React.FC = () => {
  const [filter, setFilter] = useState<AuditLogFilterState>(INITIAL_FILTER);
  const [appliedFilter, setAppliedFilter] = useState<AuditLogFilterState>(INITIAL_FILTER);

  const [logs, setLogs] = useState<AuditLogItem[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);

  const [stats, setStats] = useState<AuditOutcomeStats>({
    total: 0,
    success: 0,
    denied: 0,
    failed: 0,
  });
  const [isLoadingStats, setIsLoadingStats] = useState<boolean>(false);

  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [isDrawerOpen, setIsDrawerOpen] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchLogs = useCallback(
    async (
      page: number,
      size: number,
      currentFilter: AuditLogFilterState,
      signal?: AbortSignal,
    ) => {
      setIsLoading(true);
      setError(null);

      try {
        const result = await auditLogApi.getAuditLogs(
          {
            action: currentFilter.action || undefined,
            outcome: currentFilter.outcome || undefined,
            fromUtc: currentFilter.fromUtc || undefined,
            toUtc: currentFilter.toUtc || undefined,
            pageNumber: page,
            pageSize: size,
          },
          signal,
        );

        setLogs(result.items);
        setTotalCount(result.totalCount);
        setPageNumber(result.pageNumber);
        setPageSize(result.pageSize);

        if (result.items.length > 0) {
          setSelectedId((prev) => {
            const stillExists = result.items.some((item: AuditLogItem) => item.id === prev);
            return stillExists ? prev : result.items[0].id;
          });
        } else {
          setSelectedId(null);
        }
      } catch (err) {
        if (signal?.aborted) return;
        const msg = getAuditLogErrorMessage(err);
        setError(msg);
        setLogs([]);
        setTotalCount(0);
        setSelectedId(null);
      } finally {
        if (!signal?.aborted) {
          setIsLoading(false);
        }
      }
    },
    [],
  );

  const fetchStats = useCallback(
    async (currentFilter: AuditLogFilterState, signal?: AbortSignal) => {
      setIsLoadingStats(true);
      try {
        const [allRes, successRes, deniedRes, failedRes] = await Promise.all([
          auditLogApi.getAuditLogs(
            {
              fromUtc: currentFilter.fromUtc || undefined,
              toUtc: currentFilter.toUtc || undefined,
              action: currentFilter.action || undefined,
              pageSize: 1,
            },
            signal,
          ),
          auditLogApi.getAuditLogs(
            {
              fromUtc: currentFilter.fromUtc || undefined,
              toUtc: currentFilter.toUtc || undefined,
              action: currentFilter.action || undefined,
              outcome: 'Success',
              pageSize: 1,
            },
            signal,
          ),
          auditLogApi.getAuditLogs(
            {
              fromUtc: currentFilter.fromUtc || undefined,
              toUtc: currentFilter.toUtc || undefined,
              action: currentFilter.action || undefined,
              outcome: 'Denied',
              pageSize: 1,
            },
            signal,
          ),
          auditLogApi.getAuditLogs(
            {
              fromUtc: currentFilter.fromUtc || undefined,
              toUtc: currentFilter.toUtc || undefined,
              action: currentFilter.action || undefined,
              outcome: 'Failed',
              pageSize: 1,
            },
            signal,
          ),
        ]);

        setStats({
          total: allRes.totalCount,
          success: successRes.totalCount,
          denied: deniedRes.totalCount,
          failed: failedRes.totalCount,
        });
      } catch {
        // Fallback
      } finally {
        if (!signal?.aborted) {
          setIsLoadingStats(false);
        }
      }
    },
    [],
  );

  useEffect(() => {
    const controller = new AbortController();
    fetchLogs(pageNumber, pageSize, appliedFilter, controller.signal);
    return () => controller.abort();
  }, [fetchLogs, pageNumber, pageSize, appliedFilter]);

  const statsFilterKey = `${appliedFilter.fromUtc}:${appliedFilter.toUtc}:${appliedFilter.action}`;

  useEffect(() => {
    const controller = new AbortController();
    fetchStats(appliedFilter, controller.signal);
    return () => controller.abort();
    // Chỉ tải lại breakdown thống kê khi khoảng ngày hoặc thao tác thay đổi, không chạy lại khi đổi tab outcome
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fetchStats, statsFilterKey]);

  const filteredLogs = useMemo(() => {
    const term = appliedFilter.searchTerm.trim().toLowerCase();
    if (!term) return logs;

    return logs.filter((item) => {
      const actor = `${item.actorFullName || ''} ${item.actorEmail || ''}`.toLowerCase();
      const target = `${item.targetType || ''} ${item.targetId || ''}`.toLowerCase();
      const action = (item.action || '').toLowerCase();
      const reason = (item.reasonCode || '').toLowerCase();
      const reqId = (item.requestId || '').toLowerCase();
      const meta = item.metadata ? JSON.stringify(item.metadata).toLowerCase() : '';

      return (
        actor.includes(term) ||
        target.includes(term) ||
        action.includes(term) ||
        reason.includes(term) ||
        reqId.includes(term) ||
        meta.includes(term)
      );
    });
  }, [logs, appliedFilter.searchTerm]);

  const selectedLog = useMemo(() => {
    return logs.find((l) => l.id === selectedId) || null;
  }, [logs, selectedId]);

  const handleApplyFilter = () => {
    setAppliedFilter({ ...filter });
    setPageNumber(1);
  };

  const handleResetFilter = () => {
    setFilter(INITIAL_FILTER);
    setAppliedFilter(INITIAL_FILTER);
    setPageNumber(1);
  };

  const handleQuickOutcome = (outcomeValue: string) => {
    const nextOutcome = appliedFilter.outcome === outcomeValue ? '' : outcomeValue;
    const nextFilter: AuditLogFilterState = {
      ...filter,
      outcome: nextOutcome,
    };
    setFilter(nextFilter);
    setAppliedFilter({
      ...appliedFilter,
      outcome: nextOutcome,
    });
    setPageNumber(1);
  };

  const handleViewDetails = (log: AuditLogItem) => {
    setSelectedId(log.id);
    setIsDrawerOpen(true);
  };

  const handlePageSizeChange = (newSize: number) => {
    setPageSize(newSize);
    setPageNumber(1);
  };

  const handleRefresh = () => {
    fetchLogs(pageNumber, pageSize, appliedFilter);
    fetchStats(appliedFilter);
  };

  return (
    <div className="min-w-0 space-y-5">
      {/* Tiêu đề trang */}
      <PageHeader
        compact
        accent
        title="Nhật ký thao tác"
        icon={<ScrollText />}
      >
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={isLoading || isLoadingStats}
          onClick={handleRefresh}
          leftIcon={
            <RefreshCw
              className={isLoading || isLoadingStats ? 'animate-spin size-3.5' : 'size-3.5'}
            />
          }
          className="h-8 px-3 text-xs"
        >
          Làm mới
        </Button>
      </PageHeader>

      {/* Thông báo lỗi nếu có */}
      {error && <InlineAlert tone="error">{error}</InlineAlert>}

      {/* Card bao bọc toàn bộ Danh sách & Bộ lọc */}
      <Card className="min-w-0 overflow-hidden rounded-2xl border-slate-200/80 shadow-sm">
        {/* Header thẻ với Status Tabs hiện đại */}
        <ListCardHeader
          title="Sự kiện hệ thống"
          icon={<ScrollText />}
          description={`${totalCount.toLocaleString('vi-VN')} sự kiện ghi nhận`}
          className="min-h-0 lg:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_auto]"
        >
          <div className="flex items-center justify-end w-full">
            <AuditLogStatusTabs
              currentOutcome={appliedFilter.outcome}
              onSelectOutcome={handleQuickOutcome}
              stats={stats}
              loading={isLoadingStats}
            />
          </div>
        </ListCardHeader>

        {/* Thanh công cụ lọc hiện đại */}
        <AuditLogFilterBar
          filter={filter}
          onFilterChange={setFilter}
          onApply={handleApplyFilter}
          onReset={handleResetFilter}
          isLoading={isLoading}
        />

        {/* Bảng dữ liệu sự kiện theo chuẩn UX Tài khoản & Phân quyền */}
        <AuditLogTable
          logs={filteredLogs}
          selectedId={selectedId}
          onSelect={(log) => setSelectedId(log.id)}
          onViewDetails={handleViewDetails}
          pageNumber={pageNumber}
          pageSize={pageSize}
          totalCount={totalCount}
          onPageChange={(page) => setPageNumber(page)}
          onPageSizeChange={handlePageSizeChange}
          isLoading={isLoading}
        />
      </Card>

      {/* Drawer chi tiết sự kiện trượt từ bên phải */}
      <AuditLogDetailDrawer
        log={selectedLog}
        open={isDrawerOpen}
        onClose={() => setIsDrawerOpen(false)}
      />
    </div>
  );
};
