import { useState, useEffect, useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '../services/syllabusApi';
import { milestoneApi } from '../services/milestoneApi';
import { validateReportFile } from '../services/submissionUploadHelper';
import {
  validateProductLinksDraft,
  type ProductLinkDraft,
} from '../components/shared/SubmissionProductLinksEditor';
import type { ProgressReportStatus } from '../types/milestone.types';

export function useStudentMilestones() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [selectedMilestoneId, setSelectedMilestoneId] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [productLinks, setProductLinks] = useState<ProductLinkDraft[]>([]);
  const [selectedImages, setSelectedImages] = useState<File[]>([]);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [studentNote, setStudentNote] = useState('');
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
    (b) => b.id === statusQuery.data?.batchId || b.status === 'Open'
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
        signal
      ),
    enabled: Boolean(user?.id && groupId),
  });

  const milestonesQuery = useQuery({
    queryKey: ['student-milestones', user?.id, groupId],
    queryFn: ({ signal }) => (groupId ? milestoneApi.getMilestones(groupId, signal) : []),
    enabled: Boolean(user?.id && groupId),
  });

  const milestones = useMemo(() => milestonesQuery.data || [], [milestonesQuery.data]);

  useEffect(() => {
    if (!selectedMilestoneId && milestones.length > 0) {
      setSelectedMilestoneId(milestones[0].id);
    }
  }, [selectedMilestoneId, milestones]);

  const selectedMilestoneIndex = useMemo(() => {
    return milestones.findIndex((m) => m.id === selectedMilestoneId);
  }, [milestones, selectedMilestoneId]);

  const selectedMilestone = useMemo(() => {
    return milestones.find((m) => m.id === selectedMilestoneId) || null;
  }, [milestones, selectedMilestoneId]);

  const reportQuery = useQuery({
    queryKey: ['student-milestone-report', user?.id, selectedMilestoneId],
    queryFn: ({ signal }) =>
      selectedMilestoneId ? milestoneApi.getProgressReport(selectedMilestoneId, signal) : null,
    enabled: Boolean(user?.id && selectedMilestoneId),
    retry: false,
  });

  const report = reportQuery.data;
  const versions = report?.versions || [];
  const currentStatus: ProgressReportStatus =
    (report?.currentStatus as ProgressReportStatus) || 'NotSubmitted';
  const nextVersionNo = (report?.currentVersionNo || 0) + 1;

  const canSubmit = currentStatus === 'NotSubmitted' || currentStatus === 'NeedRevision';

  const submitMutation = useMutation({
    mutationFn: async () => {
      setActionError(null);
      setActionSuccess(null);

      if (!selectedMilestoneId) {
        throw new Error('Vui lòng chọn mốc báo cáo.');
      }

      if (!selectedFile) {
        throw new Error('Vui lòng chọn tệp báo cáo tiến độ.');
      }

      const fileError = validateReportFile(selectedFile);
      if (fileError) {
        throw new Error(fileError);
      }

      const { validLinks, error: linksError } = validateProductLinksDraft(productLinks, true);
      if (linksError) {
        throw new Error(linksError);
      }

      setUploadProgress(1);
      return await milestoneApi.submitProgressReport(selectedMilestoneId, {
        file: selectedFile,
        gitUrl: validLinks[0]?.url,
        productLinks: validLinks,
        images: selectedImages,
        studentNote: studentNote.trim() || undefined,
        onUploadProgress: (percent) => setUploadProgress(percent),
      });
    },
    onSuccess: (data) => {
      setSelectedFile(null);
      setProductLinks([]);
      setSelectedImages([]);
      setStudentNote('');
      setUploadProgress(null);
      setActionSuccess(`Nộp sản phẩm phiên bản v${data.versionNo} thành công!`);
      void queryClient.invalidateQueries({
        queryKey: ['student-milestone-report', user?.id, selectedMilestoneId],
      });
      void queryClient.invalidateQueries({
        queryKey: ['student-milestones', user?.id, groupId],
      });
    },
    onError: (err: Error) => {
      setUploadProgress(null);
      setActionError(err.message || 'Nộp sản phẩm thất bại. Vui lòng thử lại.');
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setActionError(null);
      const downloadInfo = await milestoneApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        window.open(downloadInfo.downloadUrl, '_blank', 'noopener,noreferrer');
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
    milestonesQuery,
    milestones,
    selectedMilestoneId,
    setSelectedMilestoneId,
    selectedMilestone,
    selectedMilestoneIndex,
    reportQuery,
    report,
    versions,
    currentStatus,
    nextVersionNo,
    canSubmit,
    selectedFile,
    setSelectedFile,
    productLinks,
    setProductLinks,
    selectedImages,
    setSelectedImages,
    uploadProgress,
    studentNote,
    setStudentNote,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    submitMutation,
    handleDownload,
  };
}
