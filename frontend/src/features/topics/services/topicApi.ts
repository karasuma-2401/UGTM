import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import { normalizeStudentCodes } from '../proposalEditing';
import type {
  PagedResult,
  Topic,
  TopicFilters,
  TopicFormValues,
  TopicStatistics,
} from '../types/topic.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message);
  }
  return response.data;
}

export function getTopicErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const body = error.response?.data;

    if (body?.success === false) return body.error.message;
    if (error.response?.status === 403) {
      return 'Bạn không có quyền thực hiện thao tác này.';
    }
    if (!error.response) {
      return 'Không thể kết nối máy chủ. Vui lòng thử lại.';
    }
  }

  return error instanceof Error ? error.message : 'Không thể thực hiện thao tác. Vui lòng thử lại.';
}

export const topicApi = {
  async getMyTopics(
    query: Partial<TopicFilters> & {
      advisorId?: string;
      pageNumber: number;
      pageSize: number;
    },
    signal?: AbortSignal,
  ): Promise<PagedResult<Topic>> {
    const response = await httpClient.get<ApiResponse<PagedResult<Topic>>>(
      '/api/topics/my-topics',
      {
        params: {
          ...query,
          keyword: query.keyword?.trim() || undefined,
          batchId: query.batchId || undefined,
          status: query.status || undefined,
        },
        signal,
      },
    );

    return unwrap(response.data);
  },

  async getStatistics(
    advisorId: string,
    batchId?: string,
    signal?: AbortSignal,
  ): Promise<TopicStatistics> {
    const [all, submitted, needRevision] = await Promise.all(
      (['', 'Submitted', 'NeedRevision'] as const).map((status) =>
        topicApi.getMyTopics({ advisorId, batchId, status, pageNumber: 1, pageSize: 1 }, signal),
      ),
    );
    return {
      total: all.totalCount,
      submitted: submitted.totalCount,
      needRevision: needRevision.totalCount,
    };
  },

  async getDetail(id: string, signal?: AbortSignal): Promise<Topic> {
    const response = await httpClient.get<ApiResponse<Topic>>(`/api/topics/${id}/tracking`, {
      signal,
    });
    return unwrap(response.data);
  },

  async submit(id: string): Promise<Topic> {
    const response = await httpClient.post<ApiResponse<Topic>>(`/api/topics/${id}/submit`);
    return unwrap(response.data);
  },

  async save(
    id: string | null,
    values: TopicFormValues,
    submitImmediately: boolean,
    updateAssignedStudents = false,
  ): Promise<Topic> {
    const studentCodes = normalizeStudentCodes(values.studentCodes);
    if (
      studentCodes.length > values.studentNumber ||
      new Set(studentCodes).size !== studentCodes.length
    ) {
      throw new Error('Danh sách sinh viên bị trùng hoặc vượt quá chỉ tiêu.');
    }
    const payload = {
      titleVi: values.titleVi.trim(),
      titleEn: values.titleEn.trim() || null,
      description: values.description.trim() || null,
      requirements: values.requirements.trim() || null,
      studentNumber: values.studentNumber,
      submitImmediately,
      ...(!id || updateAssignedStudents
        ? studentCodes.length
          ? { studentCodes }
          : id
            ? { clearAssignedStudents: true }
            : {}
        : {}),
    };

    if (id) {
      const response = await httpClient.put<ApiResponse<Topic>>(`/api/topics/${id}`, payload);
      return unwrap(response.data);
    }

    const response = await httpClient.post<ApiResponse<Topic>>('/api/topics', {
      ...payload,
      batchId: values.batchId,
    });

    return unwrap(response.data);
  },
};
