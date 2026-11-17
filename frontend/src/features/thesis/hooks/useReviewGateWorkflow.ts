import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import type { PagedResult } from '@/shared/api/types';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';
import { notify } from '@/shared/notifications';
import type {
  ReviewDecision,
  ReviewGateFilterKey,
  ReviewGateHistoryDto,
  ReviewGateItem,
  ReviewGateKind,
  ReviewGateSortField,
  ReviewGateSortState,
  ReviewGateStats,
  SubmissionFileDownloadDto,
  SubmissionReviewResultDto,
} from '../types/review.types';
import { triggerBrowserDownload } from '../utils/archival';
import {
  REVIEW_FEEDBACK_MAX_LENGTH,
  sortReviewGateItems,
} from '../utils/reviewGate';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';

const DEFAULT_PAGE_SIZE = 10;
const STATS_STALE_TIME_MS = 30_000;
const SEARCH_DEBOUNCE_MS = 250;

const STAT_FILTER_KEYS: readonly ReviewGateFilterKey[] = [
  'ALL',
  'Submitted',
  'NeedRevision',
  'Approved',
  'Rejected',
];

export interface ReviewGateApiAdapter<TItem extends ReviewGateItem> {
  queryKeyPrefix: string;
  historyQueryKeyPrefix: string;
  listErrorFallback: string;
  getReviewQueue: (
    query: {
      batchId?: string;
      status?: string;
      keyword?: string;
      pageNumber?: number;
      pageSize?: number;
    },
    signal?: AbortSignal,
  ) => Promise<PagedResult<TItem>>;
  getHistory: (groupId: string, signal?: AbortSignal) => Promise<ReviewGateHistoryDto | null>;
  getDownloadUrl: (fileId: string, signal?: AbortSignal) => Promise<SubmissionFileDownloadDto>;
  submitReview: (
    groupId: string,
    payload: { decision: ReviewDecision; feedback: string },
  ) => Promise<SubmissionReviewResultDto | unknown>;
}

export interface UseReviewGateWorkflowOptions<TItem extends ReviewGateItem> {
  gateKind: ReviewGateKind;
  knownGroupId?: string;
  adapter: ReviewGateApiAdapter<TItem>;
}

export function useReviewGateWorkflow<TItem extends ReviewGateItem>({
  gateKind,
  knownGroupId,
  adapter,
}: UseReviewGateWorkflowOptions<TItem>) {
  const queryClient = useQueryClient();
  const { user } = useAuth();

  const batchesQuery = useAcademicBatches();
  const batches = useMemo(() => batchesQuery.data ?? [], [batchesQuery.data]);

  const [explicitBatchId, setExplicitBatchId] = useState<string | null>(null);
  const defaultBatchId = useMemo(() => {
    const current = getCurrentBatches(batches);
    return current[0]?.id ?? batches[0]?.id ?? '';
  }, [batches]);
  const selectedBatchId = explicitBatchId ?? defaultBatchId;

  const [filterKey, setFilterKey] = useState<ReviewGateFilterKey>('ALL');
  const [keyword, setKeyword] = useState(knownGroupId || '');
  const [debouncedKeyword, setDebouncedKeyword] = useState(knownGroupId || '');
  const [sort, setSort] = useState<ReviewGateSortState>({
    field: 'submittedAt',
    direction: 'desc',
  });
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const [manualSelectedGroupId, setManualSelectedGroupId] = useState<string | null>(null);
  const [inspectedGroupIds, setInspectedGroupIds] = useState<Set<string>>(() => new Set());
  const [feedbackByGroup, setFeedbackByGroup] = useState<Record<string, string>>({});
  const [feedbackError, setFeedbackError] = useState<string | null>(null);
  const [pendingDecision, setPendingDecision] = useState<ReviewDecision | null>(null);

  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [downloadingFileId, setDownloadingFileId] = useState<string | null>(null);
  const [previewingFileId, setPreviewingFileId] = useState<string | null>(null);
  const [pdfPreviewState, setPdfPreviewState] = useState<{
    open: boolean;
    url: string;
    fileName?: string;
    fileSize?: number;
  }>({ open: false, url: '' });

  useEffect(() => {
    const trimmed = keyword.trim();
    if (!trimmed) return;
    const timer = window.setTimeout(() => {
      setDebouncedKeyword(trimmed);
    }, SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [keyword]);

  const canQueryQueue = Boolean(user?.id && !batchesQuery.isLoading);

  const queueQuery = useQuery({
    queryKey: [
      adapter.queryKeyPrefix,
      user?.id,
      'queue',
      selectedBatchId,
      filterKey,
      debouncedKeyword,
      pageNumber,
      pageSize,
    ],
    queryFn: ({ signal }) =>
      adapter.getReviewQueue(
        {
          batchId: selectedBatchId || undefined,
          status: filterKey === 'ALL' ? undefined : filterKey,
          keyword: debouncedKeyword || undefined,
          pageNumber,
          pageSize,
        },
        signal,
      ),
    enabled: canQueryQueue,
    refetchOnWindowFocus: false,
  });

  const queueErrorKind = queueQuery.isError ? classifyThesisError(queueQuery.error) : null;
  const queueErrorMessage = queueQuery.isError
    ? getThesisErrorMessage(queueQuery.error, adapter.listErrorFallback)
    : null;

  const statsQueries = useQueries({
    queries: STAT_FILTER_KEYS.map((statusKey) => ({
      queryKey: [adapter.queryKeyPrefix, user?.id, 'stats', selectedBatchId, statusKey],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        adapter.getReviewQueue(
          {
            batchId: selectedBatchId || undefined,
            status: statusKey === 'ALL' ? undefined : statusKey,
            pageNumber: 1,
            pageSize: 1,
          },
          signal,
        ),
      enabled: canQueryQueue && queueErrorKind !== 'forbidden',
      staleTime: STATS_STALE_TIME_MS,
      refetchOnWindowFocus: false,
      retry: false,
    })),
  });

  const stats: ReviewGateStats = useMemo(
    () => ({
      all: statsQueries[0]?.data?.totalCount ?? 0,
      submitted: statsQueries[1]?.data?.totalCount ?? 0,
      needRevision: statsQueries[2]?.data?.totalCount ?? 0,
      approved: statsQueries[3]?.data?.totalCount ?? 0,
      rejected: statsQueries[4]?.data?.totalCount ?? 0,
    }),
    [statsQueries],
  );
  const isLoadingStats = statsQueries.some((q) => q.isLoading);

  const rawItems = useMemo(() => queueQuery.data?.items ?? [], [queueQuery.data?.items]);
  const rows = useMemo(() => sortReviewGateItems(rawItems, sort), [rawItems, sort]);
  const totalCount = queueQuery.data?.totalCount ?? 0;

  const selectedRow = useMemo(() => {
    if (manualSelectedGroupId) {
      const matched = rows.find((item) => item.groupId === manualSelectedGroupId);
      if (matched) return matched;
    }
    return rows[0] ?? null;
  }, [rows, manualSelectedGroupId]);

  const selectedGroupId = selectedRow?.groupId ?? null;
  const isAlreadyReviewed = Boolean(selectedRow && selectedRow.currentStatus !== 'Submitted');
  const hasInspectedFile = Boolean(
    selectedRow && (!selectedRow.fileId || inspectedGroupIds.has(selectedRow.groupId)),
  );

  const historyQuery = useQuery({
    queryKey: [adapter.historyQueryKeyPrefix, user?.id, selectedGroupId],
    queryFn: ({ signal }) => adapter.getHistory(selectedGroupId!, signal),
    enabled: Boolean(
      user?.id &&
        selectedGroupId &&
        queueErrorKind !== 'forbidden' &&
        (isHistoryOpen || selectedRow?.currentStatus !== 'Submitted'),
    ),
    staleTime: STATS_STALE_TIME_MS,
    refetchOnWindowFocus: false,
  });

  const feedback = selectedGroupId ? (feedbackByGroup[selectedGroupId] ?? '') : '';

  const setFeedback = (value: string) => {
    if (!selectedGroupId) return;
    setFeedbackError(null);
    setFeedbackByGroup((prev) => ({
      ...prev,
      [selectedGroupId]: value,
    }));
  };

  const markGroupInspected = (groupId?: string | null) => {
    const targetId = groupId || selectedGroupId;
    if (!targetId) return;
    setInspectedGroupIds((prev) => {
      if (prev.has(targetId)) return prev;
      const next = new Set(prev);
      next.add(targetId);
      return next;
    });
  };

  const reviewMutation = useMutation({
    mutationFn: async ({
      groupId,
      decision,
      feedbackText,
    }: {
      groupId: string;
      decision: ReviewDecision;
      feedbackText: string;
    }) => {
      return await adapter.submitReview(groupId, {
        decision,
        feedback: feedbackText,
      });
    },
    onSuccess: async (_, variables) => {
      setPendingDecision(null);
      setFeedbackError(null);
      setFeedbackByGroup((prev) => {
        const next = { ...prev };
        delete next[variables.groupId];
        return next;
      });

      const decisionText =
        variables.decision === 'Approved'
          ? 'Đạt'
          : variables.decision === 'NeedRevision'
            ? 'Yêu cầu chỉnh sửa'
            : 'Không đạt';
      notify.success(`Đã ghi nhận kết quả thẩm định: ${decisionText}.`);

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: [adapter.queryKeyPrefix, user?.id] }),
        queryClient.invalidateQueries({
          queryKey: [adapter.historyQueryKeyPrefix, user?.id, variables.groupId],
        }),
      ]);
    },
    onError: (err: unknown) => {
      notify.error(getThesisErrorMessage(err, 'Thẩm định hồ sơ thất bại. Vui lòng thử lại.'));
    },
  });

  const openDecisionConfirm = (decision: ReviewDecision) => {
    if (!selectedRow) {
      setFeedbackError('Vui lòng chọn một hồ sơ để thẩm định.');
      return;
    }
    if (isAlreadyReviewed) {
      setFeedbackError('Hồ sơ này đã được thẩm định.');
      return;
    }
    if (!hasInspectedFile) {
      const docName = gateKind === 'syllabus' ? 'đề cương' : 'báo cáo giữa kỳ';
      setFeedbackError(
        `Vui lòng mở xem trước hoặc tải tệp ${docName} để đối chiếu trước khi thẩm định.`,
      );
      return;
    }

    const trimmed = feedback.trim();
    if ((decision === 'NeedRevision' || decision === 'Rejected') && !trimmed) {
      setFeedbackError('Vui lòng nhập nhận xét/lý do khi yêu cầu chỉnh sửa hoặc đánh giá không đạt.');
      return;
    }
    if (trimmed.length > REVIEW_FEEDBACK_MAX_LENGTH) {
      setFeedbackError(`Nhận xét không được vượt quá ${REVIEW_FEEDBACK_MAX_LENGTH} ký tự.`);
      return;
    }

    setFeedbackError(null);
    setPendingDecision(decision);
  };

  const confirmDecision = () => {
    if (!pendingDecision || !selectedRow) return;
    reviewMutation.mutate({
      groupId: selectedRow.groupId,
      decision: pendingDecision,
      feedbackText: feedback.trim(),
    });
  };

  const downloadFile = async (fileId: string, fileName: string, groupId?: string) => {
    setDownloadingFileId(fileId);
    try {
      const info = await adapter.getDownloadUrl(fileId);
      if (info?.downloadUrl) {
        triggerBrowserDownload(info.downloadUrl, fileName);
        markGroupInspected(groupId);
      }
    } catch (err: unknown) {
      notify.error(getThesisErrorMessage(err, 'Không thể lấy liên kết tải tệp.'));
    } finally {
      setDownloadingFileId(null);
    }
  };

  const previewPdf = async (
    fileId: string,
    fileName: string,
    fileSize?: number | null,
    groupId?: string,
  ) => {
    setPreviewingFileId(fileId);
    try {
      const info = await adapter.getDownloadUrl(fileId);
      if (info?.downloadUrl) {
        setPdfPreviewState({
          open: true,
          url: info.downloadUrl,
          fileName,
          fileSize: fileSize ?? undefined,
        });
        markGroupInspected(groupId);
      }
    } catch (err: unknown) {
      notify.error(getThesisErrorMessage(err, 'Không tạo được liên kết xem trước tệp PDF.'));
    } finally {
      setPreviewingFileId(null);
    }
  };

  return {
    gateKind,
    batches,
    selectedBatchId,
    isLoadingBatches: batchesQuery.isLoading,
    filterKey,
    keyword,
    sort,
    pageNumber,
    pageSize,
    totalCount,
    stats,
    isLoadingStats,
    rows,
    selectedRow,
    isAlreadyReviewed,
    hasInspectedFile,
    isLoadingList: batchesQuery.isLoading || queueQuery.isLoading,
    isFetchingList: queueQuery.isFetching,
    queueErrorKind,
    queueErrorMessage,
    historyData: historyQuery.data ?? null,
    isLoadingHistory: historyQuery.isLoading,
    historyError: historyQuery.isError
      ? getThesisErrorMessage(historyQuery.error, 'Không tải được lịch sử hồ sơ.')
      : null,
    feedback,
    feedbackError,
    pendingDecision,
    isSubmittingReview: reviewMutation.isPending,
    isHistoryOpen,
    downloadingFileId,
    previewingFileId,
    pdfPreviewState,
    actions: {
      selectBatch: (nextBatchId: string) => {
        setExplicitBatchId(nextBatchId);
        setPageNumber(1);
        setManualSelectedGroupId(null);
        setFeedbackError(null);
      },
      changeFilter: (nextFilter: ReviewGateFilterKey) => {
        setFilterKey(nextFilter);
        setPageNumber(1);
        setManualSelectedGroupId(null);
        setFeedbackError(null);
      },
      changeKeyword: (nextKeyword: string) => {
        setKeyword(nextKeyword);
        if (!nextKeyword.trim()) {
          setDebouncedKeyword('');
        }
        setPageNumber(1);
        setManualSelectedGroupId(null);
      },
      changeSort: (field: ReviewGateSortField) => {
        setSort((prev) => {
          if (prev.field === field) {
            return { field, direction: prev.direction === 'asc' ? 'desc' : 'asc' };
          }
          const defaultDirection = field === 'submittedAt' || field === 'version' ? 'desc' : 'asc';
          return { field, direction: defaultDirection };
        });
      },
      resetFilters: () => {
        setFilterKey('ALL');
        setKeyword('');
        setDebouncedKeyword('');
        setSort({ field: 'submittedAt', direction: 'desc' });
        setPageNumber(1);
        setManualSelectedGroupId(null);
        setFeedbackError(null);
      },
      changePage: (nextPage: number) => {
        setPageNumber(Math.max(1, nextPage));
        setManualSelectedGroupId(null);
        setFeedbackError(null);
      },
      changePageSize: (nextSize: number) => {
        setPageSize(nextSize);
        setPageNumber(1);
        setManualSelectedGroupId(null);
      },
      selectRow: (groupId: string) => {
        setManualSelectedGroupId(groupId);
        setFeedbackError(null);
      },
      setFeedback,
      openDecisionConfirm,
      closeDecisionConfirm: () => {
        if (!reviewMutation.isPending) setPendingDecision(null);
      },
      confirmDecision,
      openHistory: () => setIsHistoryOpen(true),
      closeHistory: () => setIsHistoryOpen(false),
      refetchHistory: () => void historyQuery.refetch(),
      downloadFile,
      previewPdf,
      closePdfPreview: () => setPdfPreviewState((prev) => ({ ...prev, open: false })),
      refresh: () => {
        void queryClient.invalidateQueries({ queryKey: [adapter.queryKeyPrefix, user?.id] });
      },
    },
  };
}
