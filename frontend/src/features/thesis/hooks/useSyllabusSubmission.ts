import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { academicBatchApi } from '@/shared/academic-periods/academicBatchApi';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '../services/syllabusApi';
import { validateReportFile } from '../services/submissionUploadHelper';
import { triggerBrowserDownload } from '../utils/archival';
import { evaluateSubmissionStageWindow } from '../utils/submissionStageWindow';
import type { SyllabusStatus } from '../types/syllabus.types';

export function useSyllabusSubmission() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const statusQuery = useQuery({
    queryKey: ['student-registration-status', user?.id],
    queryFn: ({ signal }) => syllabusApi.getMyStatus(signal),
    enabled: Boolean(user?.id),
  });

  const groupId = statusQuery.data?.groupId;
  const topicId = statusQuery.data?.topicId;
  const topicTitleVi = statusQuery.data?.topicTitleVi;

  const currentBatch = batchesQuery.data?.find(
    (b) => b.id === statusQuery.data?.batchId || b.status === 'Open',
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

  const historyQuery = useQuery({
    queryKey: ['syllabus-history', user?.id, groupId],
    queryFn: ({ signal }) => syllabusApi.getSyllabusHistory(groupId!, signal),
    enabled: Boolean(user?.id && groupId),
  });

  const resolvedBatchId =
    statusQuery.data?.batchId ?? groupDetailQuery.data?.batchId ?? currentBatch?.id;

  const timelineQuery = useQuery({
    queryKey: ['academic-batch-timeline', user?.id, resolvedBatchId],
    queryFn: ({ signal }) => academicBatchApi.getTimeline(resolvedBatchId!, signal),
    enabled: Boolean(user?.id && resolvedBatchId),
    retry: false,
  });

  const currentStatus: SyllabusStatus =
    historyQuery.data?.currentStatus || (historyQuery.data ? 'Submitted' : 'Draft');

  const isRenameResubmission = Boolean(groupDetailQuery.data?.allowSyllabusResubmission);
  const stageWindow = evaluateSubmissionStageWindow(
    timelineQuery.isError ? undefined : timelineQuery.data,
    'syllabus',
  );
  const isStageBlocked = !isRenameResubmission && stageWindow.state === 'closed';
  const stageBlockedReason = isStageBlocked ? stageWindow.message : null;

  const canSubmit =
    isRenameResubmission ||
    (!isStageBlocked && (currentStatus === 'Draft' || currentStatus === 'NeedRevision'));

  const submitMutation = useMutation({
    mutationFn: async (file: File) => {
      setActionError(null);
      setActionSuccess(null);

      if (!groupId) {
        throw new Error('Không tìm thấy thông tin nhóm của bạn.');
      }

      if (isStageBlocked && stageBlockedReason) {
        throw new Error(stageBlockedReason);
      }

      const fileError = validateReportFile(file);
      if (fileError) {
        throw new Error(fileError);
      }

      setUploadProgress(1);
      return await syllabusApi.submitSyllabus(groupId, file, {
        onUploadProgress: (percent) => setUploadProgress(percent),
      });
    },
    onSuccess: (data) => {
      setSelectedFile(null);
      setUploadProgress(null);
      setActionSuccess(`Nộp đề cương phiên bản v${data.versionNo} thành công!`);
      void queryClient.invalidateQueries({ queryKey: ['syllabus-history', user?.id, groupId] });
      void queryClient.invalidateQueries({ queryKey: ['student-group-detail', user?.id, groupId] });
      void queryClient.invalidateQueries({ queryKey: ['student-registration-status', user?.id] });
    },
    onError: (err: Error) => {
      setUploadProgress(null);
      setActionError(err.message || 'Nộp đề cương thất bại. Vui lòng thử lại.');
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setActionError(null);
      const downloadInfo = await syllabusApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        triggerBrowserDownload(downloadInfo.downloadUrl);
      }
    } catch (err: unknown) {
      const errorMsg =
        err instanceof Error ? err.message : 'Không thể lấy liên kết tải tệp.';
      setActionError(errorMsg);
    }
  };

  return {
    statusQuery,
    groupDetailQuery,
    historyQuery,
    timelineQuery,
    selectedFile,
    setSelectedFile,
    uploadProgress,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    submitMutation,
    handleDownload,
    currentStatus,
    canSubmit,
    isRenameResubmission,
    stageWindow,
    stageBlockedReason,
    groupId,
  };
}