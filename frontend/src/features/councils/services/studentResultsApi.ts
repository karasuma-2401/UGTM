import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  StudentFinalSubmissionDto,
  StudentScoreLookupResult,
  StudentScoreSummaryDto,
} from '../types/studentResults.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const studentResultsApi = {
  async getMyScoreSummary(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<StudentScoreLookupResult> {
    try {
      const response = await httpClient.get<ApiResponse<StudentScoreSummaryDto>>(
        '/api/thesis/scores/my-group',
        {
          params: { groupId },
          signal,
        },
      );
      return {
        kind: 'published',
        data: unwrap(response.data),
      };
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 403) {
        const payload = error.response.data as Partial<ApiResponse<unknown>> | undefined;
        const code = payload?.error?.code || 'ScoreSummary.NotYetPublished';
        const message =
          payload?.error?.message ||
          'Kết quả chưa đủ điều kiện mở công bố hoặc hồ sơ lưu chiểu chưa được nghiệm thu.';
        if (
          code === 'ScoreSummary.NotYetPublished' ||
          code === 'ScoreSummary.ArchivalNotConfirmed'
        ) {
          return { kind: 'locked', code, message };
        }
      }
      throw error;
    }
  },

  async getMyFinalSubmission(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<StudentFinalSubmissionDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<StudentFinalSubmissionDto>>(
        '/api/thesis/final-submissions/my-group',
        {
          params: { groupId },
          signal,
        },
      );
      return unwrap(response.data);
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  },
};
