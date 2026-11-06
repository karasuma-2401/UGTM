import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { AssignedCouncilReport, SimilarityResult } from '../types/similarity.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message || 'Thao tác không thành công.');
  return response.data;
}

export const similarityApi = {
  async getReport(
    reportId: string,
    versionNo?: number,
    signal?: AbortSignal,
  ): Promise<SimilarityResult> {
    const response = await httpClient.get<ApiResponse<SimilarityResult>>(
      `/api/thesis/final-reports/${encodeURIComponent(reportId)}/similarity-report`,
      { params: versionNo == null ? undefined : { versionNo }, signal },
    );
    return unwrap(response.data);
  },
  async retry(reportId: string, versionNo?: number): Promise<SimilarityResult> {
    const response = await httpClient.post<ApiResponse<SimilarityResult>>(
      `/api/thesis/final-reports/${encodeURIComponent(reportId)}/similarity-scan/retry`,
      null,
      { params: versionNo == null ? undefined : { versionNo } },
    );
    return unwrap(response.data);
  },
  async getMyCouncilReports(signal?: AbortSignal): Promise<AssignedCouncilReport[]> {
    const response = await httpClient.get<ApiResponse<AssignedCouncilReport[]>>(
      '/api/thesis/my-council-similarity-reports',
      { signal },
    );
    return unwrap(response.data);
  },
};
