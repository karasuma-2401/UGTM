import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { PagedResult } from '../types/topic.types';
import type { TrackingTopic } from '../types/advising.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message);
  return response.data;
}

export const advisingApi = {
  // No group-list or statistics endpoint yet: read all topic pages in scope
  // before filtering/counting groups. Never treat page totals as group totals.
  async getTopics(
    advisorId: string,
    batchId: string,
    signal?: AbortSignal,
  ): Promise<TrackingTopic[]> {
    if (!advisorId) throw new Error('Không xác định được giảng viên đang đăng nhập.');
    const topics = new Map<string, TrackingTopic>();
    let pageNumber = 1;
    let received = 0;
    while (true) {
      const response = await httpClient.get<ApiResponse<PagedResult<TrackingTopic>>>(
        '/api/topics/my-topics',
        { params: { advisorId, batchId: batchId || undefined, pageNumber, pageSize: 100 }, signal },
      );
      const page = unwrap(response.data);
      for (const topic of page.items) {
        if (topic.advisorId !== advisorId)
          throw new Error('Dữ liệu đề tài không thuộc giảng viên hiện tại.');
        topics.set(topic.id, topic);
      }
      received += page.items.length;
      if (!page.hasNextPage && received >= page.totalCount) return [...topics.values()];
      if (page.items.length === 0)
        throw new Error('Danh sách đề tài đã thay đổi. Vui lòng tải lại.');
      pageNumber++;
    }
  },

  async getDetail(advisorId: string, id: string, signal?: AbortSignal): Promise<TrackingTopic> {
    if (!advisorId || !id) throw new Error('Không xác định được đề tài cần xem.');
    const response = await httpClient.get<ApiResponse<TrackingTopic>>(
      `/api/topics/${id}/tracking`,
      { signal },
    );
    const topic = unwrap(response.data);
    if (topic.id !== id || topic.advisorId !== advisorId) {
      throw new Error('Dữ liệu đề tài không thuộc giảng viên hiện tại.');
    }
    return topic;
  },
};
