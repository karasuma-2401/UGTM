import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '../services/syllabusApi';
import type { SyllabusStatus } from '../types/syllabus.types';

const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx'];

export function useSyllabusSubmission() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
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
    (b) => b.id === statusQuery.data?.batchId || b.status === 'Open',
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

  const historyQuery = useQuery({
    queryKey: ['syllabus-history', groupId],
    queryFn: ({ signal }) => syllabusApi.getSyllabusHistory(groupId!, signal),
    enabled: Boolean(groupId),
  });

  const submitMutation = useMutation({
    mutationFn: async (file: File) => {
      setActionError(null);
      setActionSuccess(null);

      if (!groupId) {
        throw new Error('Không tìm thấy thông tin nhóm của bạn.');
      }

      const fileNameLower = file.name.toLowerCase();
      const hasValidExt = ALLOWED_EXTENSIONS.some((ext) => fileNameLower.endsWith(ext));
      if (!hasValidExt) {
        throw new Error('Chỉ chấp nhận tệp định dạng .pdf, .doc hoặc .docx.');
      }

      if (file.size > MAX_FILE_SIZE_BYTES) {
        throw new Error('Dung lượng tệp không được vượt quá 20 MB.');
      }

      return await syllabusApi.submitSyllabus(groupId, file);
    },
    onSuccess: (data) => {
      setSelectedFile(null);
      setActionSuccess(`Nộp đề cương phiên bản v${data.versionNo} thành công!`);
      void queryClient.invalidateQueries({ queryKey: ['syllabus-history', groupId] });
      void queryClient.invalidateQueries({ queryKey: ['student-registration-status'] });
    },
    onError: (err: Error) => {
      setActionError(err.message || 'Nộp đề cương thất bại. Vui lòng thử lại.');
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setActionError(null);
      const downloadInfo = await syllabusApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        window.open(downloadInfo.downloadUrl, '_blank', 'noopener,noreferrer');
      }
    } catch (err: unknown) {
      const errorMsg =
        err instanceof Error ? err.message : 'Không thể lấy liên kết tải tệp.';
      setActionError(errorMsg);
    }
  };

  const currentStatus: SyllabusStatus =
    historyQuery.data?.currentStatus || (historyQuery.data ? 'Submitted' : 'Draft');

  const canSubmit = currentStatus === 'Draft' || currentStatus === 'NeedRevision';

  return {
    statusQuery,
    groupDetailQuery,
    historyQuery,
    selectedFile,
    setSelectedFile,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    submitMutation,
    handleDownload,
    currentStatus,
    canSubmit,
    groupId,
  };
}