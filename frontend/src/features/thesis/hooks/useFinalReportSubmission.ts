import { useState, useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { academicBatchApi } from '@/shared/academic-periods/academicBatchApi';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '../services/syllabusApi';
import { midtermApi } from '../services/midtermApi';
import { finalReportApi } from '../services/finalReportApi';
import { archivalApi } from '../services/archivalApi';
import { getPostDefenseProgress } from '../utils/postDefenseProgress';
import { validateReportFile } from '../services/submissionUploadHelper';
import { triggerBrowserDownload } from '../utils/archival';
import { evaluateSubmissionStageWindow } from '../utils/submissionStageWindow';
import { getThesisErrorMessage } from '../utils/thesisErrors';
import {
  validateProductLinksDraft,
  type ProductLinkDraft,
} from '../components/shared/SubmissionProductLinksEditor';

export function useFinalReportSubmission() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [selectedRevisedFile, setSelectedRevisedFile] = useState<File | null>(null);
  const [productLinks, setProductLinks] = useState<ProductLinkDraft[]>([]);
  const [selectedImages, setSelectedImages] = useState<File[]>([]);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const statusQuery = useQuery({
    queryKey: ['student-registration-status', user?.id],
    queryFn: ({ signal }) => syllabusApi.getMyStatus(signal),
    enabled: Boolean(user?.id),
  });

  const groupId = statusQuery.data?.groupId;
  const defenseProgressQuery = useQuery({
    queryKey: ['final-report-defense-progress', user?.id, groupId],
    queryFn: ({ signal }) => archivalApi.getEligibility(groupId!, signal),
    enabled: Boolean(user?.id && groupId),
    retry: false,
    refetchInterval: 30_000,
  });
  const postDefenseProgress = defenseProgressQuery.isError
    ? null : getPostDefenseProgress(defenseProgressQuery.data);
  const topicId = statusQuery.data?.topicId;
  const topicTitleVi = statusQuery.data?.topicTitleVi;

  const currentBatch = useMemo(
    () =>
      batchesQuery.data?.find(
        (b) => b.id === statusQuery.data?.batchId || b.status === 'Open',
      ),
    [batchesQuery.data, statusQuery.data?.batchId],
  );

  const groupDetailQuery = useQuery({
    queryKey: ['student-group-detail', user?.id, groupId, currentBatch?.id],
    queryFn: ({ signal }) =>
      syllabusApi.getGroupDetail(
        groupId!,
        {
          topicId: topicId ?? undefined,
          topicTitle: topicTitleVi ?? undefined,
          currentUser: user,
          batchName: currentBatch?.batchName,
        },
        signal,
      ),
    enabled: Boolean(user?.id && groupId),
  });

  const midtermHistoryQuery = useQuery({
    queryKey: ['midterm-history', user?.id, groupId],
    queryFn: ({ signal }) => midtermApi.getMidtermHistory(groupId!, signal),
    enabled: Boolean(user?.id && groupId),
    retry: false,
  });

  const historyQuery = useQuery({
    queryKey: ['final-report-history', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getFinalReportHistory(groupId!, signal),
    enabled: Boolean(user?.id && groupId),
    retry: false,
  });

  const feedbackQuery = useQuery({
    queryKey: ['final-report-feedback', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getReviewerFeedback(groupId!, signal),
    enabled: Boolean(user?.id && groupId),
    retry: false,
  });

  const revisedEligibilityQuery = useQuery({
    queryKey: ['revised-final-report-eligibility', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getRevisedFinalReportEligibility(groupId!, signal),
    enabled: Boolean(user?.id && groupId),
    retry: false,
  });

  const revisedHistoryQuery = useQuery({
    queryKey: ['revised-final-report-history', user?.id, groupId],
    queryFn: ({ signal }) => finalReportApi.getRevisedFinalReportHistory(groupId!, signal),
    enabled: Boolean(user?.id && groupId),
    retry: false,
  });

  const resolvedBatchId =
    statusQuery.data?.batchId ?? groupDetailQuery.data?.batchId ?? currentBatch?.id;

  const timelineQuery = useQuery({
    queryKey: ['academic-batch-timeline', user?.id, resolvedBatchId],
    queryFn: ({ signal }) => academicBatchApi.getTimeline(resolvedBatchId!, signal),
    enabled: Boolean(user?.id && resolvedBatchId),
    retry: false,
  });

  const currentStatus = historyQuery.data?.currentStatus || 'Draft';
  const versions = useMemo(
    () => historyQuery.data?.versions || [],
    [historyQuery.data?.versions],
  );
  const latestVersion = useMemo(
    () => (versions.length > 0 ? versions[versions.length - 1] : null),
    [versions],
  );

  const midtermApproved = midtermHistoryQuery.data?.currentStatus === 'Approved';
  const stageWindow = evaluateSubmissionStageWindow(
    timelineQuery.isError ? undefined : timelineQuery.data,
    'final-report',
  );
  const isStageBlocked = stageWindow.state === 'closed';
  const stageBlockedReason = isStageBlocked ? stageWindow.message : null;

  const notEligibleReason = useMemo(() => {
    if (midtermHistoryQuery.isLoading) return null;
    if (midtermHistoryQuery.isError) return null;
    if (!midtermApproved) {
      return 'Báo cáo giữa kỳ (Cổng 2) chưa được GVHD phê duyệt hoặc đang chờ đánh giá. Sinh viên chỉ được nộp báo cáo cuối kỳ sau khi GVHD cho phép tiếp tục ở Cổng 2.';
    }
    if (currentStatus === 'UnderEvaluation') {
      return 'Báo cáo cuối kỳ đang trong quá trình đánh giá và đã khóa chỉnh sửa.';
    }
    if (currentStatus === 'Approved' || currentStatus === 'Rejected') {
      return 'Báo cáo đã chuyển sang giai đoạn đánh giá và không thể nộp lại qua cổng báo cáo cuối kỳ.';
    }
    if (isStageBlocked) {
      return stageBlockedReason;
    }
    return null;
  }, [
    midtermHistoryQuery.isLoading,
    midtermHistoryQuery.isError,
    midtermApproved,
    currentStatus,
    isStageBlocked,
    stageBlockedReason,
  ]);

  const canSubmit = Boolean(
    groupId && !postDefenseProgress &&
      midtermApproved &&
      !isStageBlocked &&
      historyQuery.isSuccess &&
      (currentStatus === 'Draft' || currentStatus === 'Submitted'),
  );
  const canSubmitRevision = Boolean(
    groupId && !postDefenseProgress &&
      feedbackQuery.data?.comment &&
      revisedEligibilityQuery.data?.canSubmit &&
      revisedHistoryQuery.isSuccess,
  );

  const submitRevisedMutation = useMutation({
    mutationFn: async (file: File) => {
      setActionError(null);
      setActionSuccess(null);
      if (!groupId || !canSubmitRevision) {
        throw new Error('Nhóm chưa được mở quyền nộp bản chỉnh sửa sau phản biện. Vui lòng tải lại trạng thái.');
      }
      const fileError = validateReportFile(file);
      if (fileError) throw new Error(fileError);
      return finalReportApi.submitRevisedFinalReport(groupId, file);
    },
    onSuccess: (data) => {
      setSelectedRevisedFile(null);
      setActionSuccess(`Đã nộp bản chỉnh sửa sau phản biện v${data.versionNo}.`);
      void queryClient.invalidateQueries({ queryKey: ['revised-final-report-history', user?.id, groupId] });
      void queryClient.invalidateQueries({ queryKey: ['revised-final-report-eligibility', user?.id, groupId] });
    },
    onError: (err: unknown) =>
      setActionError(getThesisErrorMessage(err, 'Không thể nộp bản chỉnh sửa.')),
  });

  const submitMutation = useMutation({
    mutationFn: async (file: File) => {
      setActionError(null);
      setActionSuccess(null);

      if (!groupId) {
        throw new Error('Không tìm thấy thông tin nhóm của bạn.');
      }

      if (!midtermApproved) {
        throw new Error('Bạn cần được GVHD duyệt đạt báo cáo giữa kỳ trước khi nộp báo cáo cuối kỳ.');
      }

      if (isStageBlocked && stageBlockedReason) {
        throw new Error(stageBlockedReason);
      }

      if (!canSubmit) {
        throw new Error('Phiên bản báo cáo hiện tại không còn cho phép nộp lại. Vui lòng tải lại trang để kiểm tra trạng thái.');
      }

      const fileError = validateReportFile(file);
      if (fileError) {
        throw new Error(fileError);
      }

      const { validLinks, error: linksError } = validateProductLinksDraft(productLinks, false);
      if (linksError) {
        throw new Error(linksError);
      }

      setUploadProgress(1);
      return await finalReportApi.submitFinalReport(groupId, file, {
        productLinks: validLinks,
        images: selectedImages,
        onUploadProgress: (percent) => setUploadProgress(percent),
      });
    },
    onSuccess: (data) => {
      setSelectedFile(null);
      setProductLinks([]);
      setSelectedImages([]);
      setUploadProgress(null);
      setActionSuccess(`Nộp báo cáo cuối kỳ phiên bản v${data.versionNo} thành công!`);
      void queryClient.invalidateQueries({ queryKey: ['final-report-history', user?.id, groupId] });
      void queryClient.invalidateQueries({ queryKey: ['student-registration-status', user?.id] });
    },
    onError: (err: unknown) => {
      setUploadProgress(null);
      setActionError(getThesisErrorMessage(err, 'Nộp báo cáo cuối kỳ thất bại. Vui lòng thử lại.'));
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setActionError(null);
      const downloadInfo = await finalReportApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        triggerBrowserDownload(downloadInfo.downloadUrl);
      }
    } catch (err: unknown) {
      setActionError(getThesisErrorMessage(err, 'Không thể lấy liên kết tải tệp.'));
    }
  };

  const handleFileSelect = (file: File) => {
    setActionError(null);
    setActionSuccess(null);
    const fileError = validateReportFile(file);
    if (fileError) {
      setActionError(fileError);
      return;
    }
    setSelectedFile(file);
  };

  const handleRemoveFile = () => {
    setSelectedFile(null);
    setActionError(null);
  };

  return {
    defenseProgressQuery,
    postDefenseProgress,
    statusQuery,
    groupDetailQuery,
    midtermHistoryQuery,
    historyQuery,
    feedbackQuery,
    revisedEligibilityQuery,
    revisedHistoryQuery,
    selectedRevisedFile,
    setSelectedRevisedFile,
    canSubmitRevision,
    submitRevisedMutation,
    selectedFile,
    productLinks,
    setProductLinks,
    selectedImages,
    setSelectedImages,
    uploadProgress,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    currentStatus,
    versions,
    latestVersion,
    canSubmit,
    notEligibleReason,
    stageWindow,
    stageBlockedReason,
    timelineQuery,
    submitMutation,
    handleDownload,
    handleFileSelect,
    handleRemoveFile,
  };
}
