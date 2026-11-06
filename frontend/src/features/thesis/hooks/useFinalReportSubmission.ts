import { useState, useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '../services/syllabusApi';
import { midtermApi } from '../services/midtermApi';
import { finalReportApi } from '../services/finalReportApi';

const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx'];

export function useFinalReportSubmission() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const batchesQuery = useAcademicBatches();

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [productNote, setProductNote] = useState<string>('Kho mã nguồn và kết quả thực nghiệm');
  const [isFeedbackOpen, setIsFeedbackOpen] = useState<boolean>(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const statusQuery = useQuery({
    queryKey: ['student-registration-status'],
    queryFn: ({ signal }) => syllabusApi.getMyStatus(signal),
  });

  const groupId = statusQuery.data?.groupId;
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

  const midtermHistoryQuery = useQuery({
    queryKey: ['midterm-history', groupId],
    queryFn: ({ signal }) => midtermApi.getMidtermHistory(groupId!, signal),
    enabled: Boolean(groupId),
    retry: false,
  });

  const historyQuery = useQuery({
    queryKey: ['final-report-history', groupId],
    queryFn: ({ signal }) => finalReportApi.getFinalReportHistory(groupId!, signal),
    enabled: Boolean(groupId),
    retry: false,
  });

  const feedbackQuery = useQuery({
    queryKey: ['final-report-feedback', groupId],
    queryFn: ({ signal }) => finalReportApi.getReviewerFeedback(groupId!, signal),
    enabled: Boolean(groupId),
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

  const notEligibleReason = useMemo(() => {
    if (midtermHistoryQuery.isLoading) return null;
    if (!midtermApproved) {
      return 'Báo cáo giữa kỳ (Cổng 2) chưa được GVHD phê duyệt hoặc đang chờ đánh giá. Sinh viên chỉ được nộp báo cáo cuối kỳ sau khi GVHD cho phép tiếp tục ở Cổng 2.';
    }
    if (currentStatus === 'UnderEvaluation') {
      return 'Báo cáo cuối kỳ đang được GVHD chấm điểm và khóa chỉnh sửa.';
    }
    return null;
  }, [midtermHistoryQuery.isLoading, midtermApproved, currentStatus]);

  const canSubmit = Boolean(groupId && midtermApproved && currentStatus !== 'UnderEvaluation');

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

      const fileNameLower = file.name.toLowerCase();
      const hasValidExt = ALLOWED_EXTENSIONS.some((ext) => fileNameLower.endsWith(ext));
      if (!hasValidExt) {
        throw new Error('Chỉ chấp nhận tệp định dạng .pdf, .doc hoặc .docx.');
      }

      if (file.size > MAX_FILE_SIZE_BYTES) {
        throw new Error('Dung lượng tệp không được vượt quá 20 MB.');
      }

      return await finalReportApi.submitFinalReport(groupId, file);
    },
    onSuccess: (data) => {
      setSelectedFile(null);
      setActionSuccess(`Nộp báo cáo cuối kỳ phiên bản v${data.versionNo} thành công!`);
      void queryClient.invalidateQueries({ queryKey: ['final-report-history', groupId] });
      void queryClient.invalidateQueries({ queryKey: ['student-registration-status'] });
    },
    onError: (err: Error) => {
      setActionError(err.message || 'Nộp báo cáo cuối kỳ thất bại. Vui lòng thử lại.');
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setActionError(null);
      const downloadInfo = await finalReportApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        window.open(downloadInfo.downloadUrl, '_blank', 'noopener,noreferrer');
      }
    } catch (err: unknown) {
      const errorMsg =
        err instanceof Error ? err.message : 'Không thể lấy liên kết tải tệp.';
      setActionError(errorMsg);
    }
  };

  const handleFileSelect = (file: File) => {
    setActionError(null);
    setActionSuccess(null);
    const fileNameLower = file.name.toLowerCase();
    const hasValidExt = ALLOWED_EXTENSIONS.some((ext) => fileNameLower.endsWith(ext));
    if (!hasValidExt) {
      setActionError('Chỉ chấp nhận tệp định dạng .pdf, .doc hoặc .docx.');
      return;
    }
    if (file.size > MAX_FILE_SIZE_BYTES) {
      setActionError('Dung lượng tệp không được vượt quá 20 MB.');
      return;
    }
    setSelectedFile(file);
  };

  const handleRemoveFile = () => {
    setSelectedFile(null);
    setActionError(null);
  };

  return {
    statusQuery,
    groupDetailQuery,
    midtermHistoryQuery,
    historyQuery,
    feedbackQuery,
    selectedFile,
    productNote,
    setProductNote,
    isFeedbackOpen,
    setIsFeedbackOpen,
    actionError,
    setActionError,
    actionSuccess,
    setActionSuccess,
    currentStatus,
    versions,
    latestVersion,
    canSubmit,
    notEligibleReason,
    submitMutation,
    handleDownload,
    handleFileSelect,
    handleRemoveFile,
  };
}