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
  computeArchivalStats,
  filterArchivalRows,
  mergeAdvisorArchivalRows,
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
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [manualSelectedId, setManualSelectedId] = useState<string | null>(null);
  const [decisionBySubmission, setDecisionBySubmission] = useState<
    Record<string, AdvisorArchivalDecision>
  >({});
  const [reasonsBySubmission, setReasonsBySubmission] = useState<Record<string, string>>({});
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<AdvisorArchivalDecision | null>(null);
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [isMetadataOpen, setIsMetadataOpen] = useState(false);
  const [downloadingKey, setDownloadingKey] = useState<string | null>(null);

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
    const filtered = filterArchivalRows(baseMerged, filterKey, keyword);
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize).map((item) => item.groupId);
  }, [listQuery.data, filterKey, keyword, pageNumber, pageSize]);

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
    () => filterArchivalRows(allMergedRows, filterKey, keyword),
    [allMergedRows, filterKey, keyword],
  );

  const totalCount = filteredRows.length;
  const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
  const safePageNumber = Math.min(pageNumber, maxPage);
  const pagedRows = useMemo(() => {
    const start = (safePageNumber - 1) * pageSize;
    return filteredRows.slice(start, start + pageSize);
  }, [filteredRows, safePageNumber, pageSize]);

  const selectedRow = useMemo(() => {
    if (manualSelectedId) {
      const matched = filteredRows.find((r) => r.submissionId === manualSelectedId);
      if (matched) return matched;
    }
    return pagedRows[0] ?? filteredRows[0] ?? null;
  }, [filteredRows, pagedRows, manualSelectedId]);

  const selectedSubmissionId = selectedRow?.submissionId ?? null;
  const selectedGroupId = selectedRow?.groupId ?? null;

  const submissionDetailQuery = useQuery({
    queryKey: ['advisor-archival-detail', selectedSubmissionId],
    queryFn: ({ signal }) => archivalApi.getDetail(selectedSubmissionId!, signal),
    enabled: Boolean(selectedSubmissionId && canAccess),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const selectedGroupDetailQuery = useQuery({
    queryKey: ['advisor-archival-group-detail', selectedGroupId],
    queryFn: ({ signal }) =>
      syllabusApi.getGroupDetail(selectedGroupId!, { batchName: selectedBatchName }, signal),
    enabled: Boolean(selectedGroupId && canAccess),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const packagesQuery = useQuery({
    queryKey: ['advisor-archival-packages', selectedSubmissionId],
    queryFn: ({ signal }) => archivalApi.getPackages(selectedSubmissionId!, signal),
    enabled: Boolean(isHistoryOpen && selectedSubmissionId && canAccess),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const decision: AdvisorArchivalDecision = selectedSubmissionId
    ? (decisionBySubmission[selectedSubmissionId] ?? 'AdvisorConfirm')
    : 'AdvisorConfirm';

  const revisionReason = selectedSubmissionId
    ? (reasonsBySubmission[selectedSubmissionId] ?? '')
    : '';

  const selectDecision = (nextDecision: AdvisorArchivalDecision) => {
    if (!selectedSubmissionId) return;
    setReasonError(null);
    setDecisionBySubmission((prev) => ({
      ...prev,
      [selectedSubmissionId]: nextDecision,
    }));
  };

  const setRevisionReason = (value: string) => {
    if (!selectedSubmissionId) return;
    setReasonError(null);
    setReasonsBySubmission((prev) => ({
      ...prev,
      [selectedSubmissionId]: value,
    }));
  };

  const confirmMutation = useMutation({
    mutationFn: async (action: AdvisorArchivalDecision) => {
      const detail = submissionDetailQuery.data;
      if (!selectedRow || !detail) {
        throw new Error('Không tìm thấy thông tin phiên bản hồ sơ hiện tại.');
      }
      const trimmedReason = revisionReason.trim();
      return archivalApi.confirmArchive(selectedRow.submissionId, {
        action,
        expectedSubmissionVersion: detail.submissionVersion,
        expectedPackageVersion: detail.currentVersionNo,
        reason: action === 'RequestRevision' ? trimmedReason : null,
      });
    },
    onSuccess: async (result) => {
      setPendingAction(null);
      setReasonError(null);
      if (result.status === 'AdvisorConfirmed') {
        notify.success(
          `Đã xác nhận bản chỉnh sửa của nhóm ${selectedRow?.groupCode ?? ''} và chuyển Khoa nghiệm thu.`,
        );
      } else {
        notify.success(
          `Đã gửi yêu cầu chỉnh sửa hồ sơ lưu chiểu tới nhóm ${selectedRow?.groupCode ?? ''}.`,
        );
        setReasonsBySubmission((prev) => {
          const next = { ...prev };
          delete next[result.submissionId];
          return next;
        });
      }
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
    if (!selectedSubmissionId) return;
    const key = `${packageVersion}-${fileRole}`;
    setDownloadingKey(key);
    try {
      const download = await archivalApi.getDownloadUrl(
        selectedSubmissionId,
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

  return {
    canAccess,
    batches,
    selectedBatchId,
    isLoadingBatches: batchesQuery.isLoading,
    filterKey,
    keyword,
    pageNumber: safePageNumber,
    pageSize,
    totalCount,
    stats,
    allRowsCount: allMergedRows.length,
    rows: pagedRows,
    selectedRow,
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
    isMetadataOpen,
    downloadingKey,
    actions: {
      selectBatch: (batchId: string) => {
        setExplicitBatchId(batchId);
        setPageNumber(1);
        setManualSelectedId(null);
      },
      changeFilter: (nextFilter: ArchivalFilterKey) => {
        setFilterKey(nextFilter);
        setPageNumber(1);
      },
      changeKeyword: (nextKeyword: string) => {
        setKeyword(nextKeyword);
        setPageNumber(1);
      },
      resetFilters: () => {
        setFilterKey('Submitted');
        setKeyword('');
        setPageNumber(1);
      },
      changePage: (nextPage: number) => setPageNumber(nextPage),
      changePageSize: (nextSize: number) => {
        setPageSize(nextSize);
        setPageNumber(1);
      },
      selectRow: (submissionId: string) => {
        setManualSelectedId(submissionId);
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
      openMetadata: () => setIsMetadataOpen(true),
      closeMetadata: () => setIsMetadataOpen(false),
      downloadFile: handleDownloadFile,
      refresh: () => void listQuery.refetch(),
      refetchDetail: () => void submissionDetailQuery.refetch(),
    },
  };
}
