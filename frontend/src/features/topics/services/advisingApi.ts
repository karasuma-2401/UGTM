import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  AdvisingFilters,
  AdvisingGroupDetail,
  AdvisingGroupsResult,
} from '../types/advising.types';
import type { TrackingTopic } from '../types/advising.types';
import type { PagedResult } from '../types/topic.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message);
  return response.data;
}
export const advisingApi = {
  // Kept for the existing milestone selector; guidance lists use getGroups.
  async getTopics(
    advisorId: string,
    batchId: string,
    signal?: AbortSignal,
  ): Promise<TrackingTopic[]> {
    if (!advisorId) throw new Error('Không xác định được giảng viên đang đăng nhập.');
    const topics = new Map<string, TrackingTopic>();
    let received = 0;
    for (let pageNumber = 1; ; pageNumber++) {
      const response = await httpClient.get<ApiResponse<PagedResult<TrackingTopic>>>(
        '/api/topics/my-topics',
        {
          params: { advisorId, batchId: batchId || undefined, pageNumber, pageSize: 100 },
          signal,
        },
      );
      const page = unwrap(response.data);
      for (const topic of page.items) {
        if (topic.advisorId !== advisorId)
          throw new Error('Dữ liệu đề tài không thuộc giảng viên hiện tại.');
        topics.set(topic.id, topic);
      }
      received += page.items.length;
      if (!page.hasNextPage && received >= page.totalCount) return [...topics.values()];
      if (!page.items.length) throw new Error('Danh sách đề tài đã thay đổi. Vui lòng tải lại.');
    }
  },
  async getGroups(
    filters: AdvisingFilters & {
      pageNumber: number;
      pageSize: number;
      sortBy: 'createdAt' | 'title' | 'studentCount';
      sortOrder: 'asc' | 'desc';
    },
    signal?: AbortSignal,
  ): Promise<AdvisingGroupsResult> {
    const response = await httpClient.get<ApiResponse<AdvisingGroupsResult>>(
      '/api/thesis/advising/groups',
      {
        params: {
          ...filters,
          keyword: filters.keyword.trim() || undefined,
          batchId: filters.batchId || undefined,
          status: filters.status || undefined,
          confirmation: filters.confirmation || undefined,
        },
        signal,
      },
    );
    return unwrap(response.data);
  },
  async getGroupDetail(groupId: string, signal?: AbortSignal): Promise<AdvisingGroupDetail> {
    const response = await httpClient.get<ApiResponse<AdvisingGroupDetail>>(
      `/api/thesis/groups/${groupId}/detail`,
      { signal },
    );
    const group = unwrap(response.data);
    if (group.groupId !== groupId) throw new Error('Dữ liệu trả về không khớp nhóm cần xem.');
    return group;
  },
};
