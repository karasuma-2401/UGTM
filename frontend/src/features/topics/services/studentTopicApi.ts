import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { PagedResult } from '../types/topic.types';
import type {
  CatalogAvailabilityFilter,
  CatalogSortField,
  PublishedTopic,
  StudentRegistrationStatus,
  RegistrationResult,
  ParticipationResult,
  RegistrationRequest,
  RegistrationTimeline,
  StudentTopicGroupDetail,
} from '../types/studentTopic.types';
import { validateStudentRegistration } from '../studentRegistration';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message);
  return response.data;
}

export const studentTopicApi = {
  async getTopics(
    query: {
      keyword: string;
      batchId: string;
      availability?: CatalogAvailabilityFilter;
      sortBy?: CatalogSortField;
      sortOrder?: 'asc' | 'desc';
      pageNumber: number;
      pageSize: number;
    },
    signal?: AbortSignal,
  ): Promise<PagedResult<PublishedTopic>> {
    const response = await httpClient.get<ApiResponse<PagedResult<PublishedTopic>>>(
      '/api/topics/all-topics',
      {
        params: {
          pageNumber: query.pageNumber,
          pageSize: query.pageSize,
          keyword: query.keyword.trim() || undefined,
          batchId: query.batchId || undefined,
          ...(query.availability ? { availability: query.availability } : {}),
          ...(query.sortBy ? { sortBy: query.sortBy } : {}),
          ...(query.sortOrder ? { sortOrder: query.sortOrder } : {}),
        },
        signal,
      },
    );
    const page = unwrap(response.data);
    if (page.items.some((topic) => topic.status !== 'Published' || !topic.isPublished)) {
      throw new Error('Danh mục có dữ liệu chưa được công bố. Vui lòng tải lại.');
    }
    return page;
  },
  async getMyStatus(signal?: AbortSignal): Promise<StudentRegistrationStatus> {
    const response = await httpClient.get<ApiResponse<StudentRegistrationStatus>>(
      '/api/thesis/topic-registrations/my-status',
      { signal },
    );
    return unwrap(response.data);
  },
  async getGroupDetail(groupId: string, signal?: AbortSignal): Promise<StudentTopicGroupDetail> {
    const response = await httpClient.get<ApiResponse<StudentTopicGroupDetail>>(
      `/api/thesis/groups/${groupId}/detail`,
      { signal },
    );
    return unwrap(response.data);
  },
  async getTimeline(batchId: string, signal?: AbortSignal): Promise<RegistrationTimeline> {
    const response = await httpClient.get<ApiResponse<RegistrationTimeline>>(
      `/api/academic-periods/batches/${batchId}/timeline`,
      { signal },
    );
    return unwrap(response.data);
  },
  async register(
    request: RegistrationRequest,
    ownCode?: string | null,
    signal?: AbortSignal,
  ): Promise<RegistrationResult> {
    signal?.throwIfAborted();
    const partnerUserCode = request.partnerUserCode.trim().toUpperCase();
    const inputError = validateStudentRegistration(request.topic, partnerUserCode, ownCode);
    if (inputError) throw new Error(inputError);
    const status = await studentTopicApi.getMyStatus(signal);
    signal?.throwIfAborted();
    const error = validateStudentRegistration(request.topic, partnerUserCode, ownCode, status);
    if (error) throw new Error(error);
    const response = await httpClient.post<ApiResponse<RegistrationResult>>(
      '/api/thesis/topic-registrations/',
      {
        topicId: request.topic.id,
        ...(partnerUserCode ? { partnerUserCode } : {}),
      },
      { signal },
    );
    return unwrap(response.data);
  },
  async confirmParticipation(token: string, signal?: AbortSignal): Promise<ParticipationResult> {
    signal?.throwIfAborted();
    if (!token.trim()) throw new Error('Vui lòng nhập mã xác nhận trong email.');
    const response = await httpClient.post<ApiResponse<ParticipationResult>>(
      '/api/thesis/topic-registrations/confirm',
      { token: token.trim() },
      { signal },
    );
    return unwrap(response.data);
  },
};
