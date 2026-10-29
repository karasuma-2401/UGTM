import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { syllabusReviewApi } from '../services/syllabusReviewApi';
import type { ReviewGroupItemDto, ReviewDecision } from '../types/review.types';

export function useSyllabusReview(knownGroupId?: string) {
  const queryClient = useQueryClient();
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [noticeMessage, setNoticeMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const queueQuery = useQuery({
    queryKey: ['syllabus-review-queue', knownGroupId],
    queryFn: ({ signal }) => syllabusReviewApi.getReviewQueue(knownGroupId, signal),
  });

  const allItems = useMemo(() => queueQuery.data || [], [queueQuery.data]);

  const selectedGroup = useMemo(() => {
    if (selectedGroupId) {
      const found = allItems.find((i) => i.groupId === selectedGroupId);
      if (found) return found;
    }
    return allItems[0] || null;
  }, [allItems, selectedGroupId]);

  const filteredItems = useMemo(() => {
    return allItems.filter((item) => {
      const matchSearch =
        !searchTerm.trim() ||
        item.groupCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.topicCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.topicTitleVi.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.studentNames.toLowerCase().includes(searchTerm.toLowerCase());

      const matchStatus = statusFilter === 'ALL' || item.currentStatus === statusFilter;

      return matchSearch && matchStatus;
    });
  }, [allItems, searchTerm, statusFilter]);

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
      void queryClient.invalidateQueries({ queryKey: ['syllabus-review-queue'] });
      void queryClient.invalidateQueries({ queryKey: ['syllabus-history', variables.groupId] });
    },
    onError: (err: Error) => {
      setErrorMessage(err.message || 'Thẩm định hồ sơ thất bại. Vui lòng thử lại.');
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setErrorMessage(null);
      const downloadInfo = await syllabusReviewApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        window.open(downloadInfo.downloadUrl, '_blank', 'noopener,noreferrer');
      }
    } catch (err: unknown) {
      const errorMsg =
        err instanceof Error ? err.message : 'Không thể lấy liên kết tải tệp.';
      setErrorMessage(errorMsg);
    }
  };

  const handleApplyFilter = () => {
    if (filteredItems.length > 0) {
      setSelectedGroupId(filteredItems[0].groupId);
    }
  };

  const handleResetFilter = () => {
    setSearchTerm('');
    setStatusFilter('ALL');
  };

  return {
    queueQuery,
    filteredItems,
    selectedGroup,
    setSelectedGroup: (g: ReviewGroupItemDto | null) => setSelectedGroupId(g?.groupId || null),
    searchTerm,
    setSearchTerm,
    statusFilter,
    setStatusFilter,
    handleApplyFilter,
    handleResetFilter,
    reviewMutation,
    handleDownload,
    noticeMessage,
    setNoticeMessage,
    errorMessage,
    setErrorMessage,
  };
}