import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import { userApi } from '@/features/admin/services/userApi';
import {
  REVIEW_TOPIC_STATUSES,
  type Topic,
  type TopicFilters,
  type PagedResult,
} from '../types/topic.types';
import { topicApi } from './topicApi';

export type ReviewDecision = 'Approved' | 'NeedRevision' | 'Rejected';
export type ReviewStatistics = Record<'total' | 'Submitted' | 'Approved' | 'Published', number>;

export interface PublishTopicsResult {
  batchId: string;
  publishedCount: number;
  publishedTopicIds: string[];
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message);
  return response.data;
}

export const topicReviewApi = {
  // Faculty/Admin omit advisorId to see the department queue, including other lecturers.
  async getTopics(
    query: TopicFilters & { pageNumber: number; pageSize: number },
    signal?: AbortSignal,
  ): Promise<PagedResult<Topic>> {
    if (query.status && query.status !== 'Draft') return topicApi.getMyTopics(query, signal);
    const pageNumber = Math.max(1, query.pageNumber);
    const pageSize = Math.max(1, Math.min(100, query.pageSize));
    const end = pageNumber * pageSize;
    // The API supports one status at a time. Merge non-draft streams before
    // paginating, rather than removing drafts from an already paginated page.
    const streams =
      query.status === 'Draft'
        ? []
        : await Promise.all(
            REVIEW_TOPIC_STATUSES.map(async (status) => {
              const requestSize = Math.min(100, end);
              const first = await topicApi.getMyTopics(
                { ...query, status, pageNumber: 1, pageSize: requestSize },
                signal,
              );
              const items = [...first.items];
              for (
                let page = 2;
                (page - 1) * requestSize < Math.min(end, first.totalCount);
                page++
              ) {
                const next = await topicApi.getMyTopics(
                  { ...query, status, pageNumber: page, pageSize: requestSize },
                  signal,
                );
                items.push(...next.items);
              }
              return { items, totalCount: first.totalCount };
            }),
          );
    const totalCount = streams.reduce((sum, stream) => sum + stream.totalCount, 0);
    const items = streams
      .flatMap((stream) => stream.items)
      .sort(
        (a, b) =>
          Date.parse(b.createdAt) - Date.parse(a.createdAt) ||
          (a.id === b.id ? 0 : a.id > b.id ? -1 : 1),
      )
      .slice((pageNumber - 1) * pageSize, end);
    return {
      items,
      totalCount,
      pageNumber,
      pageSize,
      totalPages: Math.ceil(totalCount / pageSize),
      hasPreviousPage: pageNumber > 1,
      hasNextPage: end < totalCount,
    };
  },

  async getDetail(id: string, signal?: AbortSignal): Promise<Topic> {
    const topic = await topicApi.getDetail(id, signal);
    if (topic.status === 'Draft')
      throw new Error('Đề tài là bản nháp của giảng viên, chưa được gửi thẩm định.');
    return topic;
  },

  async getStatistics(batchId: string, signal?: AbortSignal): Promise<ReviewStatistics> {
    const counts = await Promise.all(
      REVIEW_TOPIC_STATUSES.map(async (status) => {
        const result = await topicReviewApi.getTopics(
          { keyword: '', batchId, status, pageNumber: 1, pageSize: 1 },
          signal,
        );
        return [status, result.totalCount] as const;
      }),
    );
    const byStatus = Object.fromEntries(counts);
    return {
      total: counts.reduce((sum, [, count]) => sum + count, 0),
      Submitted: byStatus.Submitted,
      Approved: byStatus.Approved,
      Published: byStatus.Published,
    };
  },

  async getAdvisorNames(signal?: AbortSignal): Promise<Record<string, string>> {
    const names: Record<string, string> = {};
    let pageNumber = 1;
    let hasNextPage: boolean;
    do {
      const page = await userApi.getUsers(
        { accountGroup: 'Staff', keyword: '', role: '', status: '', pageNumber, pageSize: 100 },
        signal,
      );
      for (const user of page.items) names[user.id] = user.fullName;
      hasNextPage = page.hasNextPage;
      pageNumber++;
    } while (hasNextPage);
    return names;
  },

  async review(id: string, decision: ReviewDecision): Promise<Topic> {
    if (!(['Approved', 'NeedRevision', 'Rejected'] as string[]).includes(decision)) {
      throw new Error('Kết luận thẩm định không hợp lệ.');
    }
    const fresh = await topicApi.getDetail(id);
    if (fresh.status !== 'Submitted') {
      throw new Error('Trạng thái đề tài đã thay đổi. Chỉ thẩm định đề tài đang chờ duyệt.');
    }
    const response = await httpClient.post<ApiResponse<Topic>>(`/api/topics/${id}/review`, {
      decision,
    });
    return unwrap(response.data);
  },

  async publish(batchId: string, topicIds: string[]): Promise<PublishTopicsResult> {
    // An empty list means "publish the entire batch" to the backend: never send one here.
    const ids = [...new Set(topicIds)];
    if (!batchId || !ids.length || ids.some((id) => !id)) {
      throw new Error('Chọn ít nhất một đề tài đã duyệt trong cùng một đợt để công bố.');
    }
    const topics = await Promise.all(ids.map((id) => topicApi.getDetail(id)));
    if (topics.some((topic) => topic.batchId !== batchId || topic.status !== 'Approved')) {
      throw new Error('Danh sách đã thay đổi. Chỉ công bố các đề tài đã duyệt trong cùng một đợt.');
    }
    const response = await httpClient.post<ApiResponse<PublishTopicsResult>>(
      '/api/topics/publish',
      {
        batchId,
        topicIds: ids,
      },
    );
    return unwrap(response.data);
  },
};
