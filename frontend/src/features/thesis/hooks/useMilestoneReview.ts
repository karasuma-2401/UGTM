import { useState, useMemo, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { milestoneApi } from '../services/milestoneApi';
import { syllabusApi } from '../services/syllabusApi';
import type { ProgressReportStatus } from '../types/milestone.types';

export function useMilestoneReview() {
  const queryClient = useQueryClient();
  const { milestoneId } = useParams<{ milestoneId: string }>();
  const batchesQuery = useAcademicBatches();

  const [feedback, setFeedback] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [isDownloading, setIsDownloading] = useState(false);

  const reportQuery = useQuery({
    queryKey: ['advisor-milestone-report', milestoneId],
    queryFn: ({ signal }) =>
      milestoneId ? milestoneApi.getProgressReport(milestoneId, signal) : null,
    enabled: Boolean(milestoneId),
  });

  const report = reportQuery.data;
  const groupId = report?.groupId;

  const currentBatch = batchesQuery.data?.find((b) => b.status === 'Open');

  const groupDetailQuery = useQuery({
    queryKey: ['advisor-group-detail', groupId],
    queryFn: () =>
      groupId
        ? syllabusApi.getGroupDetail(groupId, {
            batchName: currentBatch?.batchName,
          })
        : null,
    enabled: Boolean(groupId),
  });

  const milestonesQuery = useQuery({
    queryKey: ['advisor-milestones', groupId],
    queryFn: ({ signal }) =>
      groupId ? milestoneApi.getMilestones(groupId, signal) : [],
    enabled: Boolean(groupId),
  });

  const milestones = useMemo(
    () => milestonesQuery.data || [],
    [milestonesQuery.data]
  );

  const selectedMilestoneIndex = useMemo(() => {
    if (!milestones.length || !milestoneId) return 0;
    const idx = milestones.findIndex((m) => m.id === milestoneId);
    return idx >= 0 ? idx : 0;
  }, [milestones, milestoneId]);

  const selectedMilestone = useMemo(() => {
    return milestones.find((m) => m.id === milestoneId) || null;
  }, [milestones, milestoneId]);

  const versions = useMemo(() => report?.versions || [], [report?.versions]);
  const latestVersion = useMemo(() => {
    if (!versions.length) return null;
    return (
      versions.find((v) => v.versionNo === report?.currentVersionNo) ||
      versions[versions.length - 1]
    );
  }, [versions, report?.currentVersionNo]);

  useEffect(() => {
    if (latestVersion?.review?.feedback && !feedback) {
      setFeedback(latestVersion.review.feedback);
    }
  }, [latestVersion, feedback]);

  const currentStatus: ProgressReportStatus =
    (report?.currentStatus as ProgressReportStatus) || 'NotSubmitted';

  const canReview = currentStatus === 'Submitted';

  const reviewMutation = useMutation({
    mutationFn: async (decision: 'Accepted' | 'NeedRevision') => {
      setActionError(null);
      setActionSuccess(null);

      if (!milestoneId) {
        throw new Error('Không tìm thấy thông tin mốc tiến độ.');
      }

      if (decision === 'NeedRevision' && !feedback.trim()) {
        throw new Error(
          'Vui lòng nhập nhận xét/hướng dẫn khi yêu cầu nhóm sinh viên chỉnh sửa.'
        );
      }

      return await milestoneApi.reviewProgressReport(milestoneId, {
        decision,
        feedback: feedback.trim() || undefined,
      });
    },
    onSuccess: (data) => {
      const msg =
        data.decision === 'Accepted'
          ? 'Đã đánh giá Đạt cho mốc tiến độ này.'
          : 'Đã gửi yêu cầu chỉnh sửa đến nhóm sinh viên.';
      setActionSuccess(msg);
      void queryClient.invalidateQueries({
        queryKey: ['advisor-milestone-report', milestoneId],
      });
      if (groupId) {
        void queryClient.invalidateQueries({
          queryKey: ['advisor-milestones', groupId],
        });
      }
    },
    onError: (err: Error) => {
      setActionError(
        err.message || 'Đánh giá báo cáo tiến độ thất bại. Vui lòng thử lại.'
      );
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setIsDownloading(true);
      setActionError(null);
      const downloadInfo = await milestoneApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        window.open(downloadInfo.downloadUrl, '_blank', 'noopener,noreferrer');
      }
    } catch (err: unknown) {
      const errorMsg =
        err instanceof Error ? err.message : 'Không thể lấy liên kết tải tệp.';
      setActionError(errorMsg);
    } finally {
      setIsDownloading(false);
    }
  };

  const getDisabledReason = () => {
    if (!report && !reportQuery.isLoading) {
      return 'Nhóm sinh viên chưa nộp báo cáo cho mốc tiến độ này.';
    }
    if (currentStatus === 'Accepted') {
      return 'Báo cáo mốc tiến độ này đã được đánh giá Đạt.';
    }
    if (currentStatus === 'NeedRevision') {
      return 'Bạn đã yêu cầu nhóm chỉnh sửa. Vui lòng chờ sinh viên nộp phiên bản tiếp theo.';
    }
    if (currentStatus === 'NotSubmitted') {
      return 'Nhóm sinh viên chưa nộp sản phẩm cho mốc này.';
    }
    return null;
  };

  return {
    milestoneId,
    reportQuery,
    report,
    groupDetailQuery,
    groupDetail: groupDetailQuery.data,
    milestonesQuery,
    milestones,
    selectedMilestone,
    selectedMilestoneIndex,
    latestVersion,
    currentStatus,
    feedback,
    setFeedback,
    actionError,
    actionSuccess,
    canReview,
    isSubmitting: reviewMutation.isPending,
    isDownloading,
    disabledReason: getDisabledReason(),
    handleAccept: () => reviewMutation.mutate('Accepted'),
    handleNeedRevision: () => reviewMutation.mutate('NeedRevision'),
    handleDownload,
  };
}
