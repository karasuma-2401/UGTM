import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { Badge } from '@/components/ui/badge';
import { AuditLogFilterBar } from '../components/AuditLogFilterBar';
import { AuditLogTable } from '../components/AuditLogTable';
import { AuditLogDiffCard } from '../components/AuditLogDiffCard';
import { AuditLogDetailCard } from '../components/AuditLogDetailCard';
import { auditLogApi, getAuditLogErrorMessage } from '../services/auditLogApi';
import type {
  AuditLogItem,
  AuditLogFilterState,
} from '../types/auditLog.types';

const INITIAL_FILTER: AuditLogFilterState = {
  searchTerm: '',
  action: '',
  fromUtc: '',
  toUtc: '',
  academicPeriod: '2026-1',
};

const PAGE_SIZE = 10;

export const AuditLogPage: React.FC = () => {
  const [filter, setFilter] = useState<AuditLogFilterState>(INITIAL_FILTER);
  const [appliedFilter, setAppliedFilter] = useState<AuditLogFilterState>(INITIAL_FILTER);

  const [logs, setLogs] = useState<AuditLogItem[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [pageNumber, setPageNumber] = useState<number>(1);

  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const detailSectionRef = useRef<HTMLDivElement>(null);

  // Fetch dữ liệu từ backend
  const fetchLogs = useCallback(
    async (page = 1, currentFilter = appliedFilter, signal?: AbortSignal) => {
      setIsLoading(true);
      setError(null);

      try {
        const result = await auditLogApi.getAuditLogs(
          {
            action: currentFilter.action || undefined,
            fromUtc: currentFilter.fromUtc || undefined,
            toUtc: currentFilter.toUtc || undefined,
            pageNumber: page,
            pageSize: PAGE_SIZE,
          },
          signal,
        );

        setLogs(result.items);
        setTotalCount(result.totalCount);
        setPageNumber(result.pageNumber);

        if (result.items.length > 0) {
          setSelectedId((prev) => {
            const stillExists = result.items.some((item) => item.id === prev);
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
    [appliedFilter],
  );

  useEffect(() => {
    const controller = new AbortController();
    fetchLogs(pageNumber, appliedFilter, controller.signal);
    return () => controller.abort();
  }, [fetchLogs, pageNumber, appliedFilter]);

  const filteredLogs = useMemo(() => {
    const term = appliedFilter.searchTerm.trim().toLowerCase();
    if (!term) return logs;

    return logs.filter((item) => {
      const actor = `${item.actorFullName || ''} ${item.actorEmail || ''}`.toLowerCase();
      const target = `${item.targetType || ''} ${item.targetId || ''}`.toLowerCase();
      const action = (item.action || '').toLowerCase();
      const meta = item.metadata ? JSON.stringify(item.metadata).toLowerCase() : '';

      return (
        actor.includes(term) ||
        target.includes(term) ||
        action.includes(term) ||
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

  const handleViewDetails = (log: AuditLogItem) => {
    setSelectedId(log.id);
    setTimeout(() => {
      detailSectionRef.current?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
    }, 50);
  };

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              Nhật ký thao tác
            </h1>
          </div>

          <div>
            <Badge variant="secondary" className="text-xs font-semibold px-2.5 py-1">
              Nhật ký chỉ đọc
            </Badge>
          </div>
        </div>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-xl border border-rose-200 bg-rose-50/80 p-4 text-xs sm:text-sm text-rose-700 font-medium shadow-xs"
        >
          {error}
        </div>
      )}

      <AuditLogFilterBar
        filter={filter}
        onFilterChange={setFilter}
        onApply={handleApplyFilter}
        onReset={handleResetFilter}
        isLoading={isLoading}
      />

      <AuditLogTable
        logs={filteredLogs}
        selectedId={selectedId}
        onSelect={(log) => setSelectedId(log.id)}
        onViewDetails={handleViewDetails}
        pageNumber={pageNumber}
        pageSize={PAGE_SIZE}
        totalCount={totalCount}
        onPageChange={(page) => setPageNumber(page)}
        isLoading={isLoading}
      />

      <div
        ref={detailSectionRef}
        id="audit-log-details"
        className="scroll-mt-24 grid grid-cols-1 lg:grid-cols-2 gap-5 sm:gap-6 pt-1"
      >
        <AuditLogDiffCard log={selectedLog} />
        <AuditLogDetailCard log={selectedLog} />
      </div>
    </div>
  );
};