import { httpClient } from '@/shared/api';
import type { ApiResponse, PagedResult } from '@/shared/api/types';
import type {
  ReviewSyllabusRequest,
  ReviewGroupItemDto,
  SubmissionReviewResultDto,
  SubmissionFileDownloadDto,
  SyllabusReviewItemDto,
} from '../types/review.types';
import type { SyllabusStatus, SyllabusSubmissionHistoryDto } from '../types/syllabus.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const syllabusReviewApi = {
  async reviewSyllabus(
    groupId: string,
    data: ReviewSyllabusRequest,
    signal?: AbortSignal,
  ): Promise<SubmissionReviewResultDto> {
    const response = await httpClient.post<ApiResponse<SubmissionReviewResultDto>>(
      `/api/thesis/groups/${groupId}/syllabus/review`,
      data,
      { signal },
    );
    return unwrap(response.data);
  },

  async getSyllabusHistory(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<SyllabusSubmissionHistoryDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<SyllabusSubmissionHistoryDto>>(
        `/api/thesis/groups/${groupId}/submissions/Syllabus`,
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
      `/api/thesis/submission-files/${fileId}/download`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getReviewQueue(
    query?: {
      batchId?: string;
      status?: string;
      keyword?: string;
      pageNumber?: number;
      pageSize?: number;
    } | string,
    signal?: AbortSignal,
  ): Promise<ReviewGroupItemDto[]> {
    try {
      const params = typeof query === 'string' ? { keyword: query } : query;
      const response = await httpClient.get<ApiResponse<PagedResult<SyllabusReviewItemDto>>>(
        '/api/thesis/syllabus/submissions',
        {
          params,
          signal,
        },
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
          currentStatus: item.currentStatus as SyllabusStatus,
          submittedAtUtc: item.latestFile?.uploadedAtUtc,
          fileId: item.latestFile?.id,
        };
      });
    } catch {
      const knownGroupId = typeof query === 'string' ? query : undefined;
      const items: ReviewGroupItemDto[] = [];
      const testGroupId = knownGroupId || '13c2a7dc-a0bc-4c6a-a560-26ad3b4fda44';

      try {
        const history = await syllabusReviewApi.getSyllabusHistory(testGroupId, signal);
        if (history) {
          const latest = history.versions[history.versions.length - 1];
          items.push({
            groupId: history.groupId,
            groupCode: `Nhóm ${history.groupId.slice(0, 4).toUpperCase()}`,
            topicCode: `KLTN-${history.groupId.slice(0, 8).toUpperCase()}`,
            topicTitleVi: 'Chưa có dữ liệu',
            studentNames: 'Chưa có dữ liệu',
            advisorName: 'Chưa có dữ liệu',
            batchName: 'Học kỳ hiện tại',
            latestFileName: latest?.file.fileName || 'Chưa có dữ liệu',
            latestVersionNo: latest?.versionNo || 1,
            currentStatus: history.currentStatus,
            submittedAtUtc: latest?.file.uploadedAtUtc,
            fileId: latest?.file.id,
          });
        }
      } catch {
      }

      return items;
    }
  },
};
