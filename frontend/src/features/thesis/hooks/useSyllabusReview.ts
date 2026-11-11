import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { syllabusReviewApi } from '../services/syllabusReviewApi';
import { classifyThesisError, getThesisErrorMessage } from '../utils/thesisErrors';
import type { ReviewGroupItemDto, ReviewDecision } from '../types/review.types';

const DEFAULT_PAGE_SIZE = 10;

export function useSyllabusReview(knownGroupId?: string) {
  const queryClient = useQueryClient();
  const { user } = useAuth();

  const [batchId, setBatchId] = useState<string>('');
  const [appliedBatchId, setAppliedBatchId] = useState<string>('');
  const [searchTerm, setSearchTerm] = useState(knownGroupId || '');
  const [appliedSearchTerm, setAppliedSearchTerm] = useState(knownGroupId || '');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [appliedStatusFilter, setAppliedStatusFilter] = useState('ALL');
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(DEFAULT_PAGE_SIZE);

  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [downloadedGroupIds, setDownloadedGroupIds] = useState<Set<string>>(new Set());
  const [noticeMessage, setNoticeMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const queueQuery = useQuery({
    queryKey: [
      'syllabus-review-queue',
      user?.id,
      appliedBatchId,
      appliedStatusFilter,
      appliedSearchTerm,
      pageNumber,
      pageSize,
    ],
    queryFn: ({ signal }) =>
      syllabusReviewApi.getReviewQueue(
        {
          batchId: appliedBatchId || undefined,
          status: appliedStatusFilter === 'ALL' ? undefined : appliedStatusFilter,
          keyword: appliedSearchTerm || undefined,
          pageNumber,
          pageSize,
        },
        signal,
      ),
    enabled: Boolean(user?.id),
  });

  const pagedData = queueQuery.data;
  const filteredItems = useMemo(() => pagedData?.items || [], [pagedData?.items]);
  const totalCount = pagedData?.totalCount ?? 0;
  const totalPages = pagedData?.totalPages ?? 1;
  const hasPreviousPage = pagedData?.hasPreviousPage ?? pageNumber > 1;
  const hasNextPage = pagedData?.hasNextPage ?? false;

  const queueErrorKind = queueQuery.isError ? classifyThesisError(queueQuery.error) : null;
  const queueErrorMessage = queueQuery.isError
    ? getThesisErrorMessage(queueQuery.error, 'Không thể tải danh sách hồ sơ đề cương.')
    : null;

  const selectedGroup = useMemo(() => {
    if (selectedGroupId) {
      const found = filteredItems.find((i) => i.groupId === selectedGroupId);
      if (found) return found;
    }
    return filteredItems[0] || null;
  }, [filteredItems, selectedGroupId]);

  const isAlreadyReviewed = Boolean(
    selectedGroup && selectedGroup.currentStatus !== 'Submitted',
  );

  const hasDownloadedFile = Boolean(
    selectedGroup &&
      (!selectedGroup.fileId || downloadedGroupIds.has(selectedGroup.groupId)),
  );

  const reviewMutation = useMutation({
    mutationFn: async ({
      groupId,
      decision,
      feedback,
    }: {
      groupId: string;
      decision: ReviewDecision;
      feedback: string;
    }) => {
      setErrorMessage(null);
      setNoticeMessage(null);
      return await syllabusReviewApi.reviewSyllabus(groupId, { decision, feedback });
    },
    onSuccess: (_, variables) => {
      const decisionText =
        variables.decision === 'Approved'
          ? 'Đạt'
          : variables.decision === 'NeedRevision'
            ? 'Yêu cầu chỉnh sửa'
            : 'Không đạt';

      setNoticeMessage(`Đã ghi nhận kết quả thẩm định: ${decisionText}!`);
      void queryClient.invalidateQueries({ queryKey: ['syllabus-review-queue', user?.id] });
      void queryClient.invalidateQueries({
        queryKey: ['syllabus-history', user?.id, variables.groupId],
      });
    },
    onError: (err: Error) => {
      setErrorMessage(getThesisErrorMessage(err, 'Thẩm định hồ sơ thất bại. Vui lòng thử lại.'));
    },
  });

  const handleDownload = async (fileId: string, groupId?: string) => {
    try {
      setErrorMessage(null);
      const downloadInfo = await syllabusReviewApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        window.open(downloadInfo.downloadUrl, '_blank', 'noopener,noreferrer');
        const targetGroupId = groupId || selectedGroup?.groupId;
        if (targetGroupId) {
          setDownloadedGroupIds((prev) => new Set(prev).add(targetGroupId));
        }
      }
    } catch (err: unknown) {
      setErrorMessage(getThesisErrorMessage(err, 'Không thể lấy liên kết tải tệp.'));
    }
  };

  const handleApplyFilter = () => {
    setAppliedBatchId(batchId);
    setAppliedSearchTerm(searchTerm.trim());
    setAppliedStatusFilter(statusFilter);
    setPageNumber(1);
    setSelectedGroupId(null);
  };

  const handleResetFilter = () => {
    setBatchId('');
    setAppliedBatchId('');
    setSearchTerm('');
    setAppliedSearchTerm('');
    setStatusFilter('ALL');
    setAppliedStatusFilter('ALL');
    setPageNumber(1);
    setSelectedGroupId(null);
  };

  const handlePageChange = (nextPage: number) => {
    setPageNumber(Math.max(1, nextPage));
    setSelectedGroupId(null);
  };

  return {
    queueQuery,
    queueErrorKind,
    queueErrorMessage,
    filteredItems,
    selectedGroup,
    setSelectedGroup: (g: ReviewGroupItemDto | null) => setSelectedGroupId(g?.groupId || null),
    batchId,
    setBatchId,
    searchTerm,
    setSearchTerm,
    statusFilter,
    setStatusFilter,
    pageNumber,
    pageSize,
    setPageSize,
    totalCount,
    totalPages,
    hasPreviousPage,
    hasNextPage,
    handlePageChange,
    handleApplyFilter,
    handleResetFilter,
    reviewMutation,
    handleDownload,
    hasDownloadedFile,
    isAlreadyReviewed,
    noticeMessage,
    setNoticeMessage,
    errorMessage,
    setErrorMessage,
  };
}
