import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/shared/auth';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { syllabusApi } from '../services/syllabusApi';
import { midtermApi } from '../services/midtermApi';
import type { MidtermReportStatus } from '../types/midterm.types';

const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx'];

export function useMidtermSubmission() {
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

  const syllabusHistoryQuery = useQuery({
    queryKey: ['syllabus-history', groupId],
    queryFn: ({ signal }) => syllabusApi.getSyllabusHistory(groupId!, signal),
    enabled: Boolean(groupId),
    retry: false,
  });

  const historyQuery = useQuery({
    queryKey: ['midterm-history', groupId],
    queryFn: ({ signal }) => midtermApi.getMidtermHistory(groupId!, signal),
    enabled: Boolean(groupId),
    retry: false,
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

      return await midtermApi.submitMidtermReport(groupId, file);
    },
    onSuccess: (data) => {
      setSelectedFile(null);
      setActionSuccess(`Nộp báo cáo giữa kỳ phiên bản v${data.versionNo} thành công!`);
      void queryClient.invalidateQueries({ queryKey: ['midterm-history', groupId] });
      void queryClient.invalidateQueries({ queryKey: ['student-registration-status'] });
    },
    onError: (err: Error) => {
      setActionError(err.message || 'Nộp báo cáo giữa kỳ thất bại. Vui lòng thử lại.');
    },
  });

  const handleDownload = async (fileId: string) => {
    try {
      setActionError(null);
      const downloadInfo = await midtermApi.getDownloadUrl(fileId);
      if (downloadInfo?.downloadUrl) {
        window.open(downloadInfo.downloadUrl, '_blank', 'noopener,noreferrer');
      }
    } catch (err: unknown) {
      const errorMsg =
        err instanceof Error ? err.message : 'Không thể lấy liên kết tải tệp.';
      setActionError(errorMsg);
    }
  };

  const syllabusStatus = syllabusHistoryQuery.data?.currentStatus;
  const isSyllabusApproved = syllabusStatus === 'Approved';

  let notEligibleReason: string | null = null;
  if (!syllabusHistoryQuery.isLoading && !isSyllabusApproved) {
    if (!syllabusHistoryQuery.data) {
      notEligibleReason =
        'Nhóm của bạn chưa nộp Đề cương chi tiết (Cổng 1). Vui lòng hoàn thành và chờ Khoa phê duyệt Đề cương trước khi nộp Báo cáo giữa kỳ.';
    } else if (syllabusStatus === 'Submitted') {
      notEligibleReason =
        'Đề cương chi tiết (Cổng 1) của nhóm đang chờ Văn phòng Khoa thẩm định. Báo cáo giữa kỳ sẽ được mở ngay sau khi Đề cương được phê duyệt.';
    } else if (syllabusStatus === 'NeedRevision') {
      notEligibleReason =
        'Đề cương chi tiết (Cổng 1) của nhóm đang có yêu cầu chỉnh sửa từ Khoa. Vui lòng hoàn tất nộp lại Đề cương ở Cổng 1 trước.';
    } else if (syllabusStatus === 'Rejected') {
      notEligibleReason =
        'Đề cương chi tiết (Cổng 1) của nhóm không đạt yêu cầu. Nhóm đã dừng thực hiện đề tài theo quy định.';
    } else {
      notEligibleReason =
        'Nhóm chưa hoàn thành Đề cương chi tiết (Cổng 1). Cần có phê duyệt Đề cương từ Khoa để mở Cổng 2.';
    }
  }

  const isEligibleForMidterm = Boolean(isSyllabusApproved);

  const currentStatus: MidtermReportStatus =
    historyQuery.data?.currentStatus || 'Draft';

  const canSubmit =
    isEligibleForMidterm &&
    (currentStatus === 'Draft' || currentStatus === 'NeedRevision');

  return {
    statusQuery,
    groupDetailQuery,
    syllabusHistoryQuery,
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
    isEligibleForMidterm,
    notEligibleReason,
  };
}
