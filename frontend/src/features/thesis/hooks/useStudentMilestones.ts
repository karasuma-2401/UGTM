import { useState, useEffect, useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '../services/syllabusApi';
import { milestoneApi } from '../services/milestoneApi';
import type { ProgressReportStatus } from '../types/milestone.types';

const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx'];

export function useStudentMilestones() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [selectedMilestoneId, setSelectedMilestoneId] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [gitUrl, setGitUrl] = useState('');
  const [studentNote, setStudentNote] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const statusQuery = useQuery({
    queryKey: ['student-registration-status'],
    queryFn: ({ signal }) => syllabusApi.getMyStatus(signal),
  });

  const groupId = statusQuery.data?.groupId;
  const topicId = statusQuery.data?.topicId;
  const topicTitleVi = statusQuery.data?.topicTitleVi;

  const currentBatch = batchesQuery.data?.find(
    (b) => b.id === statusQuery.data?.batchId || b.status === 'Open'
  );

  const groupDetailQuery = useQuery({
    queryKey: ['student-group-detail', groupId, user?.id, currentBatch?.id],
    queryFn: () =>
      syllabusApi.getGroupDetail(groupId!, {
        topicId: topicId ?? undefined,
        topicTitle: topicTitleVi ?? undefined,
        currentUser: user,
        batchName: currentBatch?.batchName,
      }),
    enabled: Boolean(groupId),
  });

  const milestonesQuery = useQuery({
    queryKey: ['student-milestones', groupId],
    queryFn: ({ signal }) => (groupId ? milestoneApi.getMilestones(groupId, signal) : []),
    enabled: Boolean(groupId),
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
    queryKey: ['student-milestone-report', selectedMilestoneId],
    queryFn: ({ signal }) =>
      selectedMilestoneId ? milestoneApi.getProgressReport(selectedMilestoneId, signal) : null,
    enabled: Boolean(selectedMilestoneId),
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

      const fileNameLower = selectedFile.name.toLowerCase();
      const hasValidExt = ALLOWED_EXTENSIONS.some((ext) => fileNameLower.endsWith(ext));
      if (!hasValidExt) {
        throw new Error('Chỉ chấp nhận tệp định dạng .pdf, .doc hoặc .docx.');
      }

      if (selectedFile.size > MAX_FILE_SIZE_BYTES) {
        throw new Error('Dung lượng tệp không được vượt quá 20 MB.');
      }

      if (!gitUrl.trim()) {
        throw new Error('Vui lòng cung cấp đường dẫn kho mã nguồn hoặc sản phẩm (Git URL).');
      }

      const trimmedGitUrl = gitUrl.trim();
      if (!trimmedGitUrl.startsWith('http://') && !trimmedGitUrl.startsWith('https://')) {
        throw new Error('Đường dẫn kho mã nguồn phải bắt đầu bằng http:// hoặc https://');
      }

      return await milestoneApi.submitProgressReport(selectedMilestoneId, {
        file: selectedFile,
        gitUrl: trimmedGitUrl,
        studentNote: studentNote.trim() || undefined,
      });
    },
    onSuccess: (data) => {
      setSelectedFile(null);
      setStudentNote('');
      setActionSuccess(`Nộp sản phẩm phiên bản v${data.versionNo} thành công!`);
      void queryClient.invalidateQueries({
        queryKey: ['student-milestone-report', selectedMilestoneId],
      });
      void queryClient.invalidateQueries({
        queryKey: ['student-milestones', groupId],
      });
    },
    onError: (err: Error) => {
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
    gitUrl,
    setGitUrl,
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
