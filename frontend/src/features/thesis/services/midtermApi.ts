import { httpClient } from '@/shared/api';
import type { ApiResponse, PagedResult } from '@/shared/api/types';
import type {
  MidtermReportSubmissionDto,
  MidtermReportSubmissionHistoryDto,
  MidtermReportReviewItemDto,
  ReviewMidtermGroupItemDto,
  MidtermReportStatus,
} from '../types/midterm.types';
import type {
  SubmissionFileDownloadDto,
  SubmissionReviewDto,
  ReviewDecision,
} from '../types/syllabus.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const midtermApi = {
  async submitMidtermReport(
    groupId: string,
    file: File
  ): Promise<MidtermReportSubmissionDto> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await httpClient.post<ApiResponse<MidtermReportSubmissionDto>>(
      `/api/thesis/groups/${groupId}/midterm-report/submissions`,
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );

    return unwrap(response.data);
  },

  async getMidtermHistory(
    groupId: string,
    signal?: AbortSignal
  ): Promise<MidtermReportSubmissionHistoryDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<MidtermReportSubmissionHistoryDto>>(
        `/api/thesis/groups/${groupId}/submissions/Midterm`,
        { signal }
      );

      return unwrap(response.data);
    } catch (err: unknown) {
      const axiosError = err as { response?: { status?: number } };
      if (axiosError?.response?.status === 404) {
        return null;
      }
      throw err;
    }
  },

  async getDownloadUrl(fileId: string): Promise<SubmissionFileDownloadDto> {
    const response = await httpClient.get<ApiResponse<SubmissionFileDownloadDto>>(
      `/api/thesis/submission-files/${fileId}/download`
    );

    return unwrap(response.data);
  },

  async reviewMidtermReport(
    groupId: string,
    data: { decision: ReviewDecision; feedback: string }
  ): Promise<SubmissionReviewDto> {
    const response = await httpClient.post<ApiResponse<SubmissionReviewDto>>(
      `/api/thesis/groups/${groupId}/midterm-report/review`,
      data
    );

    return unwrap(response.data);
  },

  async getMidtermReviewQueue(
    query?: {
      batchId?: string;
      status?: string;
      keyword?: string;
      pageNumber?: number;
      pageSize?: number;
    } | string,
    signal?: AbortSignal
  ): Promise<ReviewMidtermGroupItemDto[]> {
    try {
      const params = typeof query === 'string' ? { keyword: query } : query;
      const response = await httpClient.get<ApiResponse<PagedResult<MidtermReportReviewItemDto>>>(
        '/api/thesis/midterm-report/submissions',
        {
          params,
          signal,
        }
      );
      const paged = unwrap(response.data);
      const items = paged?.items || [];
      return items.map((item) => {
        const studentNames =
          (item.students || [])
            .map((s) => s.fullName || s.userCode || 'Sinh viên')
            .join(', ') || 'Chưa có thành viên';
        const advisorName = item.advisor?.fullName || 'Chưa phân công';

        return {
          groupId: item.groupId,
          groupCode: `Nhóm ${item.groupId.slice(0, 4).toUpperCase()}`,
          topicCode: `KLTN-${item.topicId.slice(0, 8).toUpperCase()}`,
          topicTitleVi: item.topicTitleVi,
          studentNames,
          advisorName,
          batchName: item.batchName || 'Học kỳ hiện tại',
          latestFileName: item.latestFile?.fileName || 'Chưa nộp tệp',
          latestVersionNo: item.currentVersionNo || 1,
          currentStatus: item.currentStatus as MidtermReportStatus,
          submittedAtUtc: item.latestFile?.uploadedAtUtc || null,
          fileId: item.latestFile?.id || null,
        };
      });
    } catch {
      const targetGroupId = typeof query === 'string' ? query : '13c2a7dc-a0bc-4c6a-a560-26ad3b4fda44';
      try {
        const history = await midtermApi.getMidtermHistory(targetGroupId, signal);
        if (!history || !history.versions || history.versions.length === 0) {
          return [];
        }
        const latest = history.versions[history.versions.length - 1];

        return [
          {
            groupId: history.groupId,
            groupCode: `Nhóm ${history.groupId.slice(0, 4).toUpperCase()}`,
            topicCode: `KLTN-${history.groupId.slice(0, 8).toUpperCase()}`,
            topicTitleVi: 'Chưa có dữ liệu',
            studentNames: 'Chưa có dữ liệu',
            advisorName: 'Chưa có dữ liệu',
            batchName: 'Đợt KLTN',
            currentStatus: history.currentStatus,
            latestVersionNo: history.currentVersionNo,
            latestFileName: latest?.file?.fileName || 'Chưa nộp tệp',
            fileId: latest?.file?.id || null,
            submittedAtUtc: latest?.file?.uploadedAtUtc || null,
          },
        ];
      } catch {
        return [];
      }
    }
  },
};
