import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import { buildDownloadRequestConfig, type DownloadUrlOptions } from '../utils/archival';
import { buildUploadProgressHandler } from './submissionUploadHelper';
import type {
  FinalReportSubmissionDto,
  FinalReportHistoryDto,
  ReviewerFeedbackDto,
  RevisedFinalReportEligibilityDto,
  SubmissionFileDownloadDto,
} from '../types/finalReport.types';
import type { SubmissionProductLinkInput } from '../types/syllabus.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const finalReportApi = {
  async getRevisedFinalReportEligibility(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<RevisedFinalReportEligibilityDto> {
    const response = await httpClient.get<ApiResponse<RevisedFinalReportEligibilityDto>>(
      `/api/thesis/groups/${groupId}/revised-final-report/eligibility`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getRevisedFinalReportHistory(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<FinalReportHistoryDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<FinalReportHistoryDto>>(
        `/api/thesis/groups/${groupId}/revised-final-report`,
        { signal },
      );
      return unwrap(response.data);
    } catch (error: unknown) {
      const axiosError = error as { response?: { status?: number } };
      if (axiosError?.response?.status === 404) return null;
      throw error;
    }
  },

  async submitRevisedFinalReport(groupId: string, file: File): Promise<FinalReportSubmissionDto> {
    const formData = new FormData();
    formData.append('groupId', groupId);
    formData.append('file', file);
    const response = await httpClient.post<ApiResponse<FinalReportSubmissionDto>>(
      '/api/thesis/revised-final-reports',
      formData,
    );
    return unwrap(response.data);
  },

  async submitFinalReport(
    groupId: string,
    file: File,
    options?: {
      productLinks?: SubmissionProductLinkInput[];
      images?: File[];
      onUploadProgress?: (percent: number) => void;
      signal?: AbortSignal;
    } | AbortSignal,
  ): Promise<FinalReportSubmissionDto> {
    const resolved =
      options && typeof options === 'object' && 'aborted' in options
        ? { signal: options as AbortSignal }
        : (options as {
            productLinks?: SubmissionProductLinkInput[];
            images?: File[];
            onUploadProgress?: (percent: number) => void;
            signal?: AbortSignal;
          } | undefined);

    const formData = new FormData();
    formData.append('groupId', groupId);
    formData.append('file', file);
    if (resolved?.productLinks && resolved.productLinks.length > 0) {
      formData.append('productLinks', JSON.stringify(resolved.productLinks));
    }
    if (resolved?.images && resolved.images.length > 0) {
      for (const image of resolved.images) {
        formData.append('images', image);
      }
    }

    const response = await httpClient.post<ApiResponse<FinalReportSubmissionDto>>(
      '/api/thesis/final-reports',
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        signal: resolved?.signal,
        onUploadProgress: buildUploadProgressHandler(resolved?.onUploadProgress),
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
    optionsOrSignal?: DownloadUrlOptions | AbortSignal,
  ): Promise<SubmissionFileDownloadDto> {
    const response = await httpClient.get<ApiResponse<SubmissionFileDownloadDto>>(
      `/api/thesis/final-report-files/${fileId}/download`,
      buildDownloadRequestConfig(optionsOrSignal),
    );
    return unwrap(response.data);
  },

  async getAttachmentUrl(
    attachmentId: string,
    optionsOrSignal?: DownloadUrlOptions | AbortSignal,
  ): Promise<SubmissionFileDownloadDto> {
    const response = await httpClient.get<ApiResponse<SubmissionFileDownloadDto>>(
      `/api/thesis/final-report-attachments/${attachmentId}/download`,
      buildDownloadRequestConfig(optionsOrSignal),
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
