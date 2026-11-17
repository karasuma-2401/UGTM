import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { AcademicBatch, AcademicBatchTimeline } from './types';

interface BatchPage {
  items: AcademicBatch[];
  hasNextPage: boolean;
}

export const academicBatchApi = {
  async getTimeline(id: string, signal?: AbortSignal): Promise<AcademicBatchTimeline> {
    const response = await httpClient.get<ApiResponse<AcademicBatchTimeline>>(
      `/api/academic-periods/batches/${id}/timeline`,
      { signal },
    );
    if (!response.data.success) throw new Error(response.data.error.message);
    return response.data.data;
  },
  async getAll(signal?: AbortSignal): Promise<AcademicBatch[]> {
    const batches: AcademicBatch[] = [];
    let pageNumber = 1;

    while (true) {
      const response = await httpClient.get<ApiResponse<BatchPage>>(
        '/api/academic-periods/batches',
        { params: { pageNumber, pageSize: 100 }, signal },
      );
      if (!response.data.success) throw new Error(response.data.error.message);

      const page = response.data.data;
      batches.push(...page.items);
      if (!page.hasNextPage) return batches;
      pageNumber += 1;
    }
  },
};
