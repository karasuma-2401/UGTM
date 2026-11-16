import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { similarityApi } from '../services/similarityApi';
import {
  buildSimilarityRowView,
  computeSimilarityStats,
  filterSimilarityRows,
  sortSimilarityRows,
  type SimilarityFilterKey,
  type SimilaritySortField,
  type SimilaritySortState,
} from '../utils/similarity';

export function useCouncilSimilarityReports() {
  const { user, hasRole } = useAuth();
  const canAccess = hasRole(['CouncilMember']);

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);

  const [selectedBatchId, setSelectedBatchId] = useState<string>('ALL');
  const [filterKey, setFilterKey] = useState<SimilarityFilterKey>('ALL');
  const [keyword, setKeyword] = useState('');
  const [sort, setSort] = useState<SimilaritySortState>({
    field: 'coverage',
    direction: 'desc',
  });
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [inspectedReportId, setInspectedReportId] = useState<string | null>(null);

  const listQuery = useQuery({
    queryKey: ['my-council-similarity-reports', user?.id],
    queryFn: ({ signal }) => similarityApi.getMyCouncilReports(signal),
    enabled: Boolean(user?.id && canAccess),
    retry: false,
    gcTime: 0,
    refetchOnMount: 'always',
  });

  const allRows = useMemo(
    () => (listQuery.data ?? []).map(buildSimilarityRowView),
    [listQuery.data],
  );

  const batchScopedRows = useMemo(
    () =>
      selectedBatchId === 'ALL'
        ? allRows
        : allRows.filter((row) => row.batchId === selectedBatchId),
    [allRows, selectedBatchId],
  );

  const stats = useMemo(() => computeSimilarityStats(batchScopedRows), [batchScopedRows]);

  const filteredRows = useMemo(() => {
    const matched = filterSimilarityRows(allRows, {
      batchId: selectedBatchId,
      filterKey,
      keyword,
    });
    return sortSimilarityRows(matched, sort);
  }, [allRows, selectedBatchId, filterKey, keyword, sort]);

  const totalCount = filteredRows.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const safePageNumber = Math.min(pageNumber, totalPages);

  const pagedRows = useMemo(() => {
    const start = (safePageNumber - 1) * pageSize;
    return filteredRows.slice(start, start + pageSize);
  }, [filteredRows, safePageNumber, pageSize]);

  const selectedRow = useMemo(() => {
    if (!selectedRowId) return null;
    return filteredRows.find((r) => r.finalReportId === selectedRowId) ?? null;
  }, [filteredRows, selectedRowId]);

  const inspectedIndex = useMemo(() => {
    if (!inspectedReportId) return -1;
    return filteredRows.findIndex((r) => r.finalReportId === inspectedReportId);
  }, [filteredRows, inspectedReportId]);

  const inspectedRow = inspectedIndex >= 0 ? filteredRows[inspectedIndex] : null;

  return {
    canAccess,
    batches,
    isLoadingBatches: batchesQuery.isLoading,
    selectedBatchId,
    filterKey,
    keyword,
    sort,
    pageNumber: safePageNumber,
    pageSize,
    totalCount,
    allRowsCount: batchScopedRows.length,
    stats,
    rows: pagedRows,
    selectedRowId,
    selectedRow,
    inspectedRow,
    inspectedIndex,
    filteredTotalCount: filteredRows.length,
    isLoadingList: listQuery.isPending,
    isFetchingList: listQuery.isFetching,
    listError: listQuery.isError,
    actions: {
      selectBatch: (batchId: string) => {
        setSelectedBatchId(batchId);
        setPageNumber(1);
        setSelectedRowId(null);
        setInspectedReportId(null);
      },
      changeFilter: (nextFilter: SimilarityFilterKey) => {
        setFilterKey(nextFilter);
        setPageNumber(1);
      },
      changeKeyword: (nextKeyword: string) => {
        setKeyword(nextKeyword);
        setPageNumber(1);
      },
      changeSort: (field: SimilaritySortField) => {
        setSort((prev) => {
          if (prev.field === field) {
            return { field, direction: prev.direction === 'asc' ? 'desc' : 'asc' };
          }
          const defaultDirection = field === 'coverage' || field === 'version' ? 'desc' : 'asc';
          return { field, direction: defaultDirection };
        });
        setPageNumber(1);
      },
      resetFilters: () => {
        setSelectedBatchId('ALL');
        setFilterKey('ALL');
        setKeyword('');
        setSort({ field: 'coverage', direction: 'desc' });
        setPageNumber(1);
      },
      changePage: (nextPage: number) => setPageNumber(nextPage),
      changePageSize: (nextSize: number) => {
        setPageSize(nextSize);
        setPageNumber(1);
      },
      selectRow: (reportId: string) => {
        setSelectedRowId((prev) => (prev === reportId ? null : reportId));
      },
      clearSelection: () => setSelectedRowId(null),
      openInspection: (reportId: string) => {
        setSelectedRowId(reportId);
        setInspectedReportId(reportId);
      },
      closeInspection: () => setInspectedReportId(null),
      navigateInspection: (delta: -1 | 1) => {
        if (inspectedIndex < 0) return;
        const target = filteredRows[inspectedIndex + delta];
        if (!target) return;
        setSelectedRowId(target.finalReportId);
        setInspectedReportId(target.finalReportId);
      },
      refresh: () => void listQuery.refetch(),
    },
  };
}
