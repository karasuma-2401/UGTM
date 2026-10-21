import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { AcademicBatch } from './types';

interface BatchPage {
  items: AcademicBatch[];
  hasNextPage: boolean;
}

export const academicBatchApi = {
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
