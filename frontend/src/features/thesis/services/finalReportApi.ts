import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  FinalReportSubmissionDto,
  FinalReportHistoryDto,
  ReviewerFeedbackDto,
  SubmissionFileDownloadDto,
} from '../types/finalReport.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const finalReportApi = {
  async submitFinalReport(
    groupId: string,
    file: File,
    signal?: AbortSignal,
  ): Promise<FinalReportSubmissionDto> {
    const formData = new FormData();
    formData.append('groupId', groupId);
    formData.append('file', file);

    const response = await httpClient.post<ApiResponse<FinalReportSubmissionDto>>(
      '/api/thesis/final-reports',
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        signal,
      },
    );

    return unwrap(response.data);
  },

  async getFinalReportHistory(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<FinalReportHistoryDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<FinalReportHistoryDto>>(
        `/api/thesis/groups/${groupId}/final-report`,
        { signal },
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

  async getDownloadUrl(
    fileId: string,
    signal?: AbortSignal,
  ): Promise<SubmissionFileDownloadDto> {
    const response = await httpClient.get<ApiResponse<SubmissionFileDownloadDto>>(
      `/api/thesis/final-report-files/${fileId}/download`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getReviewerFeedback(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<ReviewerFeedbackDto> {
    const response = await httpClient.get<ApiResponse<ReviewerFeedbackDto>>(
      `/api/thesis/groups/${groupId}/feedback`,
      { signal },
    );
    return unwrap(response.data);
  },
};