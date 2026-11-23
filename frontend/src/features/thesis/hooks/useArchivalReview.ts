import { useMemo, useState } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { academicResultsApi } from '@/features/councils/services/academicResultsApi';
import type { AcademicResultDetailDto } from '@/features/councils/types/results.types';
import { getPreferredDefaultBatch } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import { archivalApi } from '../services/archivalApi';
import type { ArchivalFilterKey, FinalSubmissionReviewAction } from '../types/archival.types';
import {
  ARCHIVAL_REASON_MAX_LENGTH,
  type ArchivalSortField,
  type ArchivalSortState,
  computeArchivalStats,
  filterArchivalRows,
  mergeArchivalSummaryRows,
  sortArchivalRows,
  triggerBrowserDownload,
} from '../utils/archival';
import { getThesisErrorMessage } from '../utils/thesisErrors';

const STALE_TIME_MS = 60_000;

export function useArchivalReview() {
  const queryClient = useQueryClient();
  const { user, hasRole, hasPermission } = useAuth();

  const canRead =
    hasRole('Admin') ||
    (hasRole(['Faculty', 'AcademicAffairs']) && hasPermission('thesis.final-submissions.read'));
  const canManage =
    hasRole('Admin') || (hasRole('Faculty') && hasPermission('thesis.final-submissions.manage'));

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);

  const [explicitBatchId, setExplicitBatchId] = useState<string | null>(null);
  const defaultBatchId = useMemo(
    () => getPreferredDefaultBatch(batches)?.id ?? '',
    [batches],
  );
  const selectedBatchId = explicitBatchId ?? defaultBatchId;

  const [filterKey, setFilterKey] = useState<ArchivalFilterKey>('AdvisorConfirmed');
  const [keyword, setKeyword] = useState('');
  const [sort, setSort] = useState<ArchivalSortState>({ field: 'submittedAt', direction: 'desc' });
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [manualSelectedId, setManualSelectedId] = useState<string | null>(null);
  const [commentsBySubmission, setCommentsBySubmission] = useState<Record<string, string>>({});
  const [pendingAction, setPendingAction] = useState<FinalSubmissionReviewAction | null>(null);
  const [commentError, setCommentError] = useState<string | null>(null);
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
    queryKey: ['archival-review-list', user?.id, selectedBatchId],
    queryFn: async ({ signal }) => {
      const [submissions, academicItems] = await Promise.all([
        archivalApi.getAllSubmissionsByBatch(selectedBatchId, signal),
        academicResultsApi.getAllAcademicResults(selectedBatchId, signal).catch(() => []),
      ]);
      return { submissions, academicItems };
    },
    enabled: Boolean(user?.id && selectedBatchId && canRead),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const stats = useMemo(
    () => computeArchivalStats(listQuery.data?.submissions ?? []),
    [listQuery.data?.submissions],
  );

  const visibleGroupIds = useMemo(() => {
    const submissions = listQuery.data?.submissions ?? [];
    const academicItems = listQuery.data?.academicItems ?? [];
    const baseMerged = mergeArchivalSummaryRows(submissions, academicItems, {});
    const filtered = sortArchivalRows(filterArchivalRows(baseMerged, filterKey, keyword), sort);
    const start = (pageNumber - 1) * pageSize;
    return filtered.slice(start, start + pageSize).map((item) => item.groupId);
  }, [listQuery.data, filterKey, keyword, sort, pageNumber, pageSize]);

  const groupDetailQueries = useQueries({
    queries: visibleGroupIds.map((groupId) => ({
      queryKey: ['archival-group-academic-detail', groupId],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        academicResultsApi.getAcademicResultDetail(groupId, signal),
      enabled: Boolean(groupId && canRead),
      staleTime: STALE_TIME_MS,
      refetchOnWindowFocus: false,
      retry: false,
    })),
  });

  const detailByGroupId = useMemo(() => {
    const map: Record<string, AcademicResultDetailDto | undefined> = {};
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
    const academicItems = listQuery.data?.academicItems ?? [];
    return mergeArchivalSummaryRows(submissions, academicItems, detailByGroupId);
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
    if (manualSelectedId) {
      const matched = filteredRows.find((r) => r.submissionId === manualSelectedId);
      if (matched) return matched;
    }
    return pagedRows[0] ?? filteredRows[0] ?? null;
  }, [filteredRows, pagedRows, manualSelectedId]);

  const selectedSubmissionId = selectedRow?.submissionId ?? null;
  const selectedGroupId = selectedRow?.groupId ?? null;

  const submissionDetailQuery = useQuery({
    queryKey: ['archival-submission-detail', selectedSubmissionId],
    queryFn: ({ signal }) => archivalApi.getDetail(selectedSubmissionId!, signal),
    enabled: Boolean(selectedSubmissionId && canRead),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const selectedAcademicDetailQuery = useQuery({
    queryKey: ['archival-group-academic-detail', selectedGroupId],
    queryFn: ({ signal }) => academicResultsApi.getAcademicResultDetail(selectedGroupId!, signal),
    enabled: Boolean(selectedGroupId && canRead),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const packagesQuery = useQuery({
    queryKey: ['archival-packages', selectedSubmissionId],
    queryFn: ({ signal }) => archivalApi.getPackages(selectedSubmissionId!, signal),
    enabled: Boolean(isHistoryOpen && selectedSubmissionId && canRead),
    staleTime: STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const facultyComment = selectedSubmissionId
    ? (commentsBySubmission[selectedSubmissionId] ?? '')
    : '';

  const setFacultyComment = (value: string) => {
    if (!selectedSubmissionId) return;
    setCommentError(null);
    setCommentsBySubmission((prev) => ({
      ...prev,
      [selectedSubmissionId]: value,
    }));
  };

  const confirmMutation = useMutation({
    mutationFn: async (action: FinalSubmissionReviewAction) => {
      const detail = submissionDetailQuery.data;
      if (!selectedRow || !detail) {
        throw new Error('Không tìm thấy thông tin phiên bản hồ sơ hiện tại.');
      }
      const trimmedReason = facultyComment.trim();
      return archivalApi.confirmArchive(selectedRow.submissionId, {
        action,
        expectedSubmissionVersion: detail.submissionVersion,
        expectedPackageVersion: detail.currentVersionNo,
        reason: action === 'RequestRevision' ? trimmedReason : null,
      });
    },
    onSuccess: async (result) => {
      setPendingAction(null);
      setCommentError(null);
      if (result.status === 'ArchiveConfirmed') {
        notify.success(`Đã nghiệm thu lưu chiểu cho nhóm ${selectedRow?.groupCode ?? ''}.`);
      } else {
        notify.success(`Đã gửi yêu cầu chỉnh sửa tới nhóm ${selectedRow?.groupCode ?? ''}.`);
        setCommentsBySubmission((prev) => {
          const next = { ...prev };
          delete next[result.submissionId];
          return next;
        });
      }
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['archival-review-list'] }),
        queryClient.invalidateQueries({
          queryKey: ['archival-submission-detail', result.submissionId],
        }),
        queryClient.invalidateQueries({ queryKey: ['archival-packages', result.submissionId] }),
        queryClient.invalidateQueries({ queryKey: ['advisor-archival-list'] }),
        queryClient.invalidateQueries({
          queryKey: ['advisor-archival-detail', result.submissionId],
        }),
      ]);
    },
    onError: (error) => {
      notify.error(getThesisErrorMessage(error, 'Xử lý hồ sơ lưu chiểu chưa thành công.'));
    },
  });

  const handleOpenDecisionConfirm = (action: FinalSubmissionReviewAction) => {
    if (action === 'RequestRevision') {
      const trimmed = facultyComment.trim();
      if (!trimmed) {
        setCommentError('Vui lòng nhập nhận xét / lý do yêu cầu chỉnh sửa.');
        return;
      }
      if (trimmed.length > ARCHIVAL_REASON_MAX_LENGTH) {
        setCommentError(`Nhận xét không được vượt quá ${ARCHIVAL_REASON_MAX_LENGTH} ký tự.`);
        return;
      }
    }
    setCommentError(null);
    setPendingAction(action);
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

  const handlePreviewPdf = async (
    packageVersion: number,
    fileName: string,
    fileSize?: number,
  ) => {
    if (!selectedSubmissionId) return;
    const key = `${packageVersion}-ThesisPdf`;
    setPreviewingKey(key);
    try {
      const download = await archivalApi.getDownloadUrl(
        selectedSubmissionId,
        packageVersion,
        'ThesisPdf',
        { inline: true },
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
    permissions: { canRead, canManage },
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
    selectedRow,
    submissionDetail: submissionDetailQuery.data ?? null,
    isLoadingDetail: submissionDetailQuery.isLoading,
    detailError: submissionDetailQuery.isError
      ? getThesisErrorMessage(submissionDetailQuery.error, 'Không tải được chi tiết hồ sơ.')
      : null,
    academicDetail: selectedAcademicDetailQuery.data ?? null,
    packagesQuery,
    isLoadingList: listQuery.isLoading,
    isFetchingList: listQuery.isFetching,
    listError: listQuery.isError
      ? getThesisErrorMessage(listQuery.error, 'Không tải được danh sách hồ sơ lưu chiểu.')
      : null,
    facultyComment,
    commentError,
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
        setFilterKey('AdvisorConfirmed');
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
        setManualSelectedId(submissionId);
        setCommentError(null);
      },
      setFacultyComment,
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
