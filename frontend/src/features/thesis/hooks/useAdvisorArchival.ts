import { useMemo, useState } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { advisingApi } from '@/features/topics/services/advisingApi';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import { archivalApi } from '../services/archivalApi';
import { syllabusApi } from '../services/syllabusApi';
import type { ArchivalFilterKey } from '../types/archival.types';
import type { StudentGroupDetailDto } from '../types/syllabus.types';
import {
  ARCHIVAL_REASON_MAX_LENGTH,
  type ArchivalSortField,
  type ArchivalSortState,
  computeArchivalStats,
  filterArchivalRows,
  mergeAdvisorArchivalRows,
  sortArchivalRows,
  triggerBrowserDownload,
} from '../utils/archival';
import { getThesisErrorMessage } from '../utils/thesisErrors';

const STALE_TIME_MS = 60_000;

export type AdvisorArchivalDecision = 'AdvisorConfirm' | 'RequestRevision';

export function useAdvisorArchival() {
  const queryClient = useQueryClient();
  const { user, hasRole } = useAuth();

  const canAccess = hasRole(['Lecturer', 'Admin']);

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);

  const [explicitBatchId, setExplicitBatchId] = useState<string | null>(null);
  const defaultBatchId = useMemo(() => {
    const current = getCurrentBatches(batches);
    return current[0]?.id ?? batches[0]?.id ?? '';
  }, [batches]);
  const selectedBatchId = explicitBatchId ?? defaultBatchId;

  const selectedBatchName = useMemo(() => {
    const batch = batches.find((b) => b.id === selectedBatchId);
    return batch ? `${batch.batchName} (${batch.academicYear} - HK${batch.semester})` : undefined;
  }, [batches, selectedBatchId]);

  const [filterKey, setFilterKey] = useState<ArchivalFilterKey>('Submitted');
  const [keyword, setKeyword] = useState('');
  const [sort, setSort] = useState<ArchivalSortState>({ field: 'submittedAt', direction: 'desc' });
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const [inspectedSubmissionId, setInspectedSubmissionId] = useState<string | null>(null);
  const [decisionBySubmission, setDecisionBySubmission] = useState<
    Record<string, AdvisorArchivalDecision>
  >({});
  const [reasonsBySubmission, setReasonsBySubmission] = useState<Record<string, string>>({});
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<AdvisorArchivalDecision | null>(null);
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [downloadingKey, setDownloadingKey] = useState<string | null>(null);
  const [previewingKey, setPreviewingKey] = useState<string | null>(null);
  const [pdfPreviewState, setPdfPreviewState] = useState<{
    open: boolean;
    url: string;
    fileName?: string;
    fileSize?: number;
  }>({ open: false, url: '' });

  const listQuery = useQuery({
    queryKey: ['advisor-archival-list', user?.id, selectedBatchId],
    queryFn: async ({ signal }) => {
      const [submissions, advisingPage] = await Promise.all([
        archivalApi.getAllSubmissionsByBatch(selectedBatchId, signal),
        advisingApi
          .getGroups(
            {
              batchId: selectedBatchId,
              keyword: '',
              scope: 'all',
              status: '',
              confirmation: '',
              pageNumber: 1,
              pageSize: 100,
              sortBy: 'createdAt',
              sortOrder: 'desc',
            },
            signal,
          )
          .catch(() => null),
      ]);
      return {
        submissions,
        advisingGroups: advisingPage?.page.items ?? [],
      };
    },
    enabled: Boolean(user?.id && selectedBatchId && canAccess),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const stats = useMemo(
    () => computeArchivalStats(listQuery.data?.submissions ?? []),
    [listQuery.data?.submissions],
  );

  const visibleGroupIds = useMemo(() => {
    const submissions = listQuery.data?.submissions ?? [];
    const advisingGroups = listQuery.data?.advisingGroups ?? [];
    const baseMerged = mergeAdvisorArchivalRows(submissions, advisingGroups, {});
    const filtered = sortArchivalRows(filterArchivalRows(baseMerged, filterKey, keyword), sort);
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize).map((item) => item.groupId);
  }, [listQuery.data, filterKey, keyword, sort, pageNumber, pageSize]);

  const groupDetailQueries = useQueries({
    queries: visibleGroupIds.map((groupId) => ({
      queryKey: ['advisor-archival-group-detail', groupId],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        syllabusApi.getGroupDetail(groupId, { batchName: selectedBatchName }, signal),
      enabled: Boolean(groupId && canAccess),
      staleTime: STALE_TIME_MS,
      refetchOnWindowFocus: false,
      retry: false,
    })),
  });

  const detailByGroupId = useMemo(() => {
    const map: Record<string, StudentGroupDetailDto | undefined> = {};
    visibleGroupIds.forEach((groupId, index) => {
      const data = groupDetailQueries[index]?.data;
      if (data) {
        map[groupId] = data;
      }
    });
    return map;
  }, [visibleGroupIds, groupDetailQueries]);

  const allMergedRows = useMemo(() => {
    const submissions = listQuery.data?.submissions ?? [];
    const advisingGroups = listQuery.data?.advisingGroups ?? [];
    return mergeAdvisorArchivalRows(submissions, advisingGroups, detailByGroupId);
  }, [listQuery.data, detailByGroupId]);

  const filteredRows = useMemo(
    () => sortArchivalRows(filterArchivalRows(allMergedRows, filterKey, keyword), sort),
    [allMergedRows, filterKey, keyword, sort],
  );

  const totalCount = filteredRows.length;
  const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
  const safePageNumber = Math.min(pageNumber, maxPage);
  const pagedRows = useMemo(() => {
    const start = (safePageNumber - 1) * pageSize;
    return filteredRows.slice(start, start + pageSize);
  }, [filteredRows, safePageNumber, pageSize]);

  const selectedRow = useMemo(() => {
    if (!selectedRowId) return null;
    return filteredRows.find((r) => r.submissionId === selectedRowId) ?? null;
  }, [filteredRows, selectedRowId]);

  const inspectedIndex = useMemo(() => {
    if (!inspectedSubmissionId) return -1;
    return filteredRows.findIndex((r) => r.submissionId === inspectedSubmissionId);
  }, [filteredRows, inspectedSubmissionId]);

  const inspectedRow = inspectedIndex >= 0 ? filteredRows[inspectedIndex] : null;
  const activeSubmissionId = inspectedRow?.submissionId ?? null;
  const activeGroupId = inspectedRow?.groupId ?? null;
  const historySubmissionId = activeSubmissionId ?? selectedRow?.submissionId ?? null;

  const submissionDetailQuery = useQuery({
    queryKey: ['advisor-archival-detail', activeSubmissionId],
    queryFn: ({ signal }) => archivalApi.getDetail(activeSubmissionId!, signal),
    enabled: Boolean(activeSubmissionId && canAccess),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const selectedGroupDetailQuery = useQuery({
    queryKey: ['advisor-archival-group-detail', activeGroupId],
    queryFn: ({ signal }) =>
      syllabusApi.getGroupDetail(activeGroupId!, { batchName: selectedBatchName }, signal),
    enabled: Boolean(activeGroupId && canAccess),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const packagesQuery = useQuery({
    queryKey: ['advisor-archival-packages', historySubmissionId],
    queryFn: ({ signal }) => archivalApi.getPackages(historySubmissionId!, signal),
    enabled: Boolean(isHistoryOpen && historySubmissionId && canAccess),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const decision: AdvisorArchivalDecision = activeSubmissionId
    ? (decisionBySubmission[activeSubmissionId] ?? 'AdvisorConfirm')
    : 'AdvisorConfirm';

  const revisionReason = activeSubmissionId
    ? (reasonsBySubmission[activeSubmissionId] ?? '')
    : '';

  const selectDecision = (nextDecision: AdvisorArchivalDecision) => {
    if (!activeSubmissionId) return;
    setReasonError(null);
    setDecisionBySubmission((prev) => ({
      ...prev,
      [activeSubmissionId]: nextDecision,
    }));
  };

  const setRevisionReason = (value: string) => {
    if (!activeSubmissionId) return;
    setReasonError(null);
    setReasonsBySubmission((prev) => ({
      ...prev,
      [activeSubmissionId]: value,
    }));
  };

  const confirmMutation = useMutation({
    mutationFn: async (action: AdvisorArchivalDecision) => {
      const detail = submissionDetailQuery.data;
      if (!inspectedRow || !detail) {
        throw new Error('Không tìm thấy thông tin phiên bản hồ sơ hiện tại.');
      }
      const trimmedReason = revisionReason.trim();
      return archivalApi.confirmArchive(inspectedRow.submissionId, {
        action,
        expectedSubmissionVersion: detail.submissionVersion,
        expectedPackageVersion: detail.currentVersionNo,
        reason: action === 'RequestRevision' ? trimmedReason : null,
      });
    },
    onSuccess: async (result) => {
      setPendingAction(null);
      setReasonError(null);
      const groupLabel = inspectedRow?.groupCode ?? '';
      if (result.status === 'AdvisorConfirmed') {
        notify.success(
          `Đã xác nhận bản chỉnh sửa của nhóm ${groupLabel} và chuyển Khoa nghiệm thu.`,
        );
      } else {
        notify.success(
          `Đã gửi yêu cầu chỉnh sửa hồ sơ lưu chiểu tới nhóm ${groupLabel}.`,
        );
        setReasonsBySubmission((prev) => {
          const next = { ...prev };
          delete next[result.submissionId];
          return next;
        });
      }
      setInspectedSubmissionId(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['advisor-archival-list'] }),
        queryClient.invalidateQueries({ queryKey: ['advisor-archival-detail', result.submissionId] }),
        queryClient.invalidateQueries({
          queryKey: ['advisor-archival-packages', result.submissionId],
        }),
        queryClient.invalidateQueries({ queryKey: ['archival-review-list'] }),
        queryClient.invalidateQueries({
          queryKey: ['archival-submission-detail', result.submissionId],
        }),
      ]);
    },
    onError: (error) => {
      notify.error(getThesisErrorMessage(error, 'Xác nhận hồ sơ lưu chiểu chưa thành công.'));
    },
  });

  const handleOpenDecisionConfirm = () => {
    if (decision === 'RequestRevision') {
      const trimmed = revisionReason.trim();
      if (!trimmed) {
        setReasonError('Vui lòng nhập lý do yêu cầu chỉnh sửa để sinh viên khắc phục.');
        return;
      }
      if (trimmed.length > ARCHIVAL_REASON_MAX_LENGTH) {
        setReasonError(`Lý do không được vượt quá ${ARCHIVAL_REASON_MAX_LENGTH} ký tự.`);
        return;
      }
    }
    setReasonError(null);
    setPendingAction(decision);
  };

  const handleDownloadFile = async (
    packageVersion: number,
    fileRole: string,
    fileName: string,
  ) => {
    const downloadSubmissionId = activeSubmissionId ?? selectedRow?.submissionId ?? null;
    if (!downloadSubmissionId) return;
    const key = `${packageVersion}-${fileRole}`;
    setDownloadingKey(key);
    try {
      const download = await archivalApi.getDownloadUrl(
        downloadSubmissionId,
        packageVersion,
        fileRole,
      );
      triggerBrowserDownload(download.downloadUrl, download.fileName || fileName);
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không tạo được liên kết tải tệp.'));
    } finally {
      setDownloadingKey(null);
    }
  };

  const handlePreviewPdf = async (
    packageVersion: number,
    fileName: string,
    fileSize?: number,
  ) => {
    const targetSubmissionId =
      historySubmissionId ?? activeSubmissionId ?? selectedRow?.submissionId ?? null;
    if (!targetSubmissionId) return;
    const key = `${packageVersion}-ThesisPdf`;
    setPreviewingKey(key);
    try {
      const download = await archivalApi.getDownloadUrl(
        targetSubmissionId,
        packageVersion,
        'ThesisPdf',
      );
      setPdfPreviewState({
        open: true,
        url: download.downloadUrl,
        fileName: download.fileName || fileName,
        fileSize: download.fileSize ?? fileSize,
      });
    } catch (error) {
      notify.error(getThesisErrorMessage(error, 'Không tạo được liên kết xem trước tệp PDF.'));
    } finally {
      setPreviewingKey(null);
    }
  };

  return {
    canAccess,
    batches,
    selectedBatchId,
    isLoadingBatches: batchesQuery.isLoading,
    filterKey,
    keyword,
    sort,
    pageNumber: safePageNumber,
    pageSize,
    totalCount,
    stats,
    allRowsCount: allMergedRows.length,
    rows: pagedRows,
    selectedRowId,
    selectedRow,
    inspectedRow,
    inspectedIndex,
    filteredTotalCount: filteredRows.length,
    submissionDetail: submissionDetailQuery.data ?? null,
    isLoadingDetail: submissionDetailQuery.isLoading,
    detailError: submissionDetailQuery.isError
      ? getThesisErrorMessage(submissionDetailQuery.error, 'Không tải được chi tiết hồ sơ.')
      : null,
    groupDetail: selectedGroupDetailQuery.data ?? null,
    packagesQuery,
    isLoadingList: listQuery.isLoading,
    isFetchingList: listQuery.isFetching,
    listError: listQuery.isError
      ? getThesisErrorMessage(listQuery.error, 'Không tải được danh sách hồ sơ lưu chiểu.')
      : null,
    decision,
    revisionReason,
    reasonError,
    pendingAction,
    isConfirming: confirmMutation.isPending,
    isHistoryOpen,
    downloadingKey,
    previewingKey,
    pdfPreviewState,
    actions: {
      selectBatch: (batchId: string) => {
        setExplicitBatchId(batchId);
        setPageNumber(1);
        setSelectedRowId(null);
        setInspectedSubmissionId(null);
      },
      changeFilter: (nextFilter: ArchivalFilterKey) => {
        setFilterKey(nextFilter);
        setPageNumber(1);
      },
      changeKeyword: (nextKeyword: string) => {
        setKeyword(nextKeyword);
        setPageNumber(1);
      },
      changeSort: (field: ArchivalSortField) => {
        setSort((prev) => {
          if (prev.field === field) {
            return { field, direction: prev.direction === 'asc' ? 'desc' : 'asc' };
          }
          const defaultDirection = field === 'submittedAt' || field === 'version' ? 'desc' : 'asc';
          return { field, direction: defaultDirection };
        });
        setPageNumber(1);
      },
      resetFilters: () => {
        setFilterKey('Submitted');
        setKeyword('');
        setSort({ field: 'submittedAt', direction: 'desc' });
        setPageNumber(1);
      },
      changePage: (nextPage: number) => setPageNumber(nextPage),
      changePageSize: (nextSize: number) => {
        setPageSize(nextSize);
        setPageNumber(1);
      },
      selectRow: (submissionId: string) => {
        setSelectedRowId((prev) => (prev === submissionId ? null : submissionId));
      },
      clearSelection: () => setSelectedRowId(null),
      openInspection: (submissionId: string) => {
        setSelectedRowId(submissionId);
        setInspectedSubmissionId(submissionId);
        setReasonError(null);
      },
      closeInspection: () => {
        if (!confirmMutation.isPending) {
          setInspectedSubmissionId(null);
          setReasonError(null);
        }
      },
      navigateInspection: (delta: -1 | 1) => {
        if (inspectedIndex < 0) return;
        const target = filteredRows[inspectedIndex + delta];
        if (!target) return;
        setSelectedRowId(target.submissionId);
        setInspectedSubmissionId(target.submissionId);
        setReasonError(null);
      },
      selectDecision,
      setRevisionReason,
      openDecisionConfirm: handleOpenDecisionConfirm,
      closeDecisionConfirm: () => {
        if (!confirmMutation.isPending) setPendingAction(null);
      },
      confirmDecision: () => {
        if (pendingAction) confirmMutation.mutate(pendingAction);
      },
      openHistory: () => setIsHistoryOpen(true),
      closeHistory: () => setIsHistoryOpen(false),
      downloadFile: handleDownloadFile,
      previewPdf: handlePreviewPdf,
      closePdfPreview: () => setPdfPreviewState((prev) => ({ ...prev, open: false })),
      refresh: () => void listQuery.refetch(),
      refetchDetail: () => void submissionDetailQuery.refetch(),
    },
  };
}
