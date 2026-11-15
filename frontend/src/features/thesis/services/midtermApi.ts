import { httpClient } from '@/shared/api';
import type { ApiResponse, PagedResult } from '@/shared/api/types';
import { buildUploadProgressHandler } from './submissionUploadHelper';
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
    file: File,
    onUploadProgress?: (percent: number) => void
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
        onUploadProgress: buildUploadProgressHandler(onUploadProgress),
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
    query?:
      | {
          batchId?: string;
          status?: string;
          keyword?: string;
          pageNumber?: number;
          pageSize?: number;
        }
      | string,
    signal?: AbortSignal
  ): Promise<PagedResult<ReviewMidtermGroupItemDto>> {
    const raw = typeof query === 'string' ? { keyword: query } : query || {};
    const pageNumber = raw.pageNumber && raw.pageNumber > 0 ? raw.pageNumber : 1;
    const pageSize = raw.pageSize && raw.pageSize > 0 ? raw.pageSize : 20;

    const params: Record<string, string | number> = {
      pageNumber,
      pageSize,
    };

    if (raw.batchId && raw.batchId !== 'ALL' && raw.batchId !== 'all') {
      params.batchId = raw.batchId;
    }
    if (raw.status && raw.status !== 'ALL' && raw.status !== 'all') {
      params.status = raw.status;
    }
    if (raw.keyword && raw.keyword.trim()) {
      params.keyword = raw.keyword.trim();
    }

    const response = await httpClient.get<ApiResponse<PagedResult<MidtermReportReviewItemDto>>>(
      '/api/thesis/midterm-report/submissions',
      {
        params,
        signal,
      }
    );
    const paged = unwrap(response.data);
    const rawItems = paged?.items || [];
    const items = rawItems.map((item) => {
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

    const totalCount = paged?.totalCount ?? items.length;
    const resolvedPageNumber = paged?.pageNumber ?? pageNumber;
    const resolvedPageSize = paged?.pageSize ?? pageSize;
    const totalPages =
      paged?.totalPages ?? Math.max(1, Math.ceil(totalCount / Math.max(1, resolvedPageSize)));

    return {
      items,
      totalCount,
      pageNumber: resolvedPageNumber,
      pageSize: resolvedPageSize,
      totalPages,
      hasPreviousPage: paged?.hasPreviousPage ?? resolvedPageNumber > 1,
      hasNextPage: paged?.hasNextPage ?? resolvedPageNumber < totalPages,
    };
  },
};
