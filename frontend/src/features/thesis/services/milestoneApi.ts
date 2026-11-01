import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  ProgressMilestoneDto,
  CreateProgressMilestoneRequest,
  ProgressReportHistoryDto,
  ProgressReportReviewDto,
  ProgressReportSubmissionDto,
} from '../types/milestone.types';
import type { SubmissionFileDownloadDto } from '../types/syllabus.types';

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
    data: { file: File; gitUrl?: string; studentNote?: string }
  ): Promise<ProgressReportSubmissionDto> {
    const formData = new FormData();
    formData.append('file', data.file);
    if (data.gitUrl) formData.append('gitUrl', data.gitUrl);
    if (data.studentNote) formData.append('studentNote', data.studentNote);

    const response = await httpClient.post<ApiResponse<ProgressReportSubmissionDto>>(
      `/api/thesis/progress-milestones/${milestoneId}/report`,
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
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
};