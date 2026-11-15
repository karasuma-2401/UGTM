import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import { buildUploadProgressHandler } from './submissionUploadHelper';
import type {
  ProgressMilestoneDto,
  CreateProgressMilestoneRequest,
  ProgressReportHistoryDto,
  ProgressReportReviewDto,
  ProgressReportSubmissionDto,
} from '../types/milestone.types';
import type {
  SubmissionFileDownloadDto,
  SubmissionProductLinkInput,
} from '../types/syllabus.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const milestoneApi = {
  async getMilestones(
    groupId: string,
    signal?: AbortSignal
  ): Promise<ProgressMilestoneDto[]> {
    const response = await httpClient.get<ApiResponse<ProgressMilestoneDto[]>>(
      `/api/thesis/groups/${groupId}/progress-milestones`,
      { signal }
    );
    return unwrap(response.data);
  },

  async createMilestone(
    groupId: string,
    data: CreateProgressMilestoneRequest
  ): Promise<ProgressMilestoneDto> {
    const response = await httpClient.post<ApiResponse<ProgressMilestoneDto>>(
      `/api/thesis/groups/${groupId}/progress-milestones`,
      data
    );
    return unwrap(response.data);
  },

  async getProgressReport(
    milestoneId: string,
    signal?: AbortSignal
  ): Promise<ProgressReportHistoryDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<ProgressReportHistoryDto>>(
        `/api/thesis/progress-milestones/${milestoneId}/report`,
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

  async submitProgressReport(
    milestoneId: string,
    data: {
      file: File;
      gitUrl?: string;
      studentNote?: string;
      productLinks?: SubmissionProductLinkInput[];
      images?: File[];
      onUploadProgress?: (percent: number) => void;
    }
  ): Promise<ProgressReportSubmissionDto> {
    const formData = new FormData();
    formData.append('file', data.file);
    const primaryUrl = data.gitUrl || data.productLinks?.[0]?.url;
    if (primaryUrl) formData.append('gitUrl', primaryUrl);
    if (data.studentNote) formData.append('studentNote', data.studentNote);
    if (data.productLinks && data.productLinks.length > 0) {
      formData.append('productLinks', JSON.stringify(data.productLinks));
    }
    if (data.images && data.images.length > 0) {
      for (const image of data.images) {
        formData.append('images', image);
      }
    }

    const response = await httpClient.post<ApiResponse<ProgressReportSubmissionDto>>(
      `/api/thesis/progress-milestones/${milestoneId}/report`,
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        onUploadProgress: buildUploadProgressHandler(data.onUploadProgress),
      }
    );
    return unwrap(response.data);
  },

  async reviewProgressReport(
    milestoneId: string,
    data: { decision: 'Accepted' | 'NeedRevision'; feedback?: string }
  ): Promise<ProgressReportReviewDto> {
    const response = await httpClient.post<ApiResponse<ProgressReportReviewDto>>(
      `/api/thesis/progress-milestones/${milestoneId}/report/review`,
      data
    );
    return unwrap(response.data);
  },

  async getDownloadUrl(fileId: string): Promise<SubmissionFileDownloadDto> {
    const response = await httpClient.get<ApiResponse<SubmissionFileDownloadDto>>(
      `/api/thesis/progress-report-files/${fileId}/download`
    );
    return unwrap(response.data);
  },

  async getAttachmentUrl(attachmentId: string): Promise<SubmissionFileDownloadDto> {
    const response = await httpClient.get<ApiResponse<SubmissionFileDownloadDto>>(
      `/api/thesis/progress-report-attachments/${attachmentId}/download`
    );
    return unwrap(response.data);
  },
};