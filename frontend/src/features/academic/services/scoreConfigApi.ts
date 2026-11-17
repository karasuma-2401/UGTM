import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { PagedResult } from '@/shared/api/types';
import type {
  ScoreConfigurationDto,
  CreateScoreConfigurationDraftRequest,
  UpdateScoreConfigurationDraftRequest,
  KltnBatchSummaryDto,
} from '../types/scoreConfig.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message);
  }
  return response.data;
}

export function getScoreConfigErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const response = error.response?.data;
    if (response?.success === false && response.error?.message) {
      return response.error.message;
    }
    if (error.response?.status === 403) {
      return 'Bạn không có quyền quản lý cấu hình điểm (Chỉ dành cho Admin và PĐT).';
    }
    if (error.response?.status === 404) {
      return 'Không tìm thấy dữ liệu đợt khóa luận hoặc cấu hình điểm.';
    }
    if (error.response?.status === 409) {
      return 'Đã có thay đổi mới từ người dùng khác hoặc đợt đã có bản nháp. Vui lòng tải lại.';
    }
    if (!error.response) {
      return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng rồi thử lại.';
    }
  }

  return error instanceof Error ? error.message : 'Đã có lỗi xảy ra khi xử lý cấu hình điểm.';
}

export const scoreConfigApi = {
  async getBatches(
    params: {
      pageNumber?: number;
      pageSize?: number;
      status?: string;
      academicYear?: string;
      semester?: number;
    } = {},
    signal?: AbortSignal,
  ): Promise<PagedResult<KltnBatchSummaryDto>> {
    const response = await httpClient.get<ApiResponse<PagedResult<KltnBatchSummaryDto>>>(
      '/api/academic-periods/batches',
      {
        params: {
          pageNumber: params.pageNumber ?? 1,
          pageSize: params.pageSize ?? 50,
          status: params.status,
          academicYear: params.academicYear,
          semester: params.semester,
        },
        signal,
      },
    );
    return unwrap(response.data);
  },

  async getActiveConfiguration(
    batchId: string,
    signal?: AbortSignal,
  ): Promise<ScoreConfigurationDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<ScoreConfigurationDto>>(
        `/api/academic-periods/batches/${batchId}/score-configurations/active`,
        { signal },
      );
      return unwrap(response.data);
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 404) {
        return null;
      }
      throw err;
    }
  },

  async getConfigurationHistory(
    batchId: string,
    signal?: AbortSignal,
  ): Promise<ScoreConfigurationDto[]> {
    const response = await httpClient.get<ApiResponse<ScoreConfigurationDto[]>>(
      `/api/academic-periods/batches/${batchId}/score-configurations/history`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getConfigurationVersion(
    batchId: string,
    configurationId: string,
    signal?: AbortSignal,
  ): Promise<ScoreConfigurationDto> {
    const response = await httpClient.get<ApiResponse<ScoreConfigurationDto>>(
      `/api/academic-periods/batches/${batchId}/score-configurations/${configurationId}`,
      { signal },
    );
    return unwrap(response.data);
  },

  async createDraft(
    batchId: string,
    data: CreateScoreConfigurationDraftRequest,
  ): Promise<ScoreConfigurationDto> {
    const response = await httpClient.post<ApiResponse<ScoreConfigurationDto>>(
      `/api/academic-periods/batches/${batchId}/score-configurations/drafts`,
      data,
    );
    return unwrap(response.data);
  },

  async updateDraft(
    batchId: string,
    configurationId: string,
    data: UpdateScoreConfigurationDraftRequest,
  ): Promise<ScoreConfigurationDto> {
    const response = await httpClient.put<ApiResponse<ScoreConfigurationDto>>(
      `/api/academic-periods/batches/${batchId}/score-configurations/drafts/${configurationId}`,
      data,
    );
    return unwrap(response.data);
  },

  async publishConfiguration(
    batchId: string,
    configurationId: string,
  ): Promise<ScoreConfigurationDto> {
    const response = await httpClient.post<ApiResponse<ScoreConfigurationDto>>(
      `/api/academic-periods/batches/${batchId}/score-configurations/${configurationId}/publish`,
    );
    return unwrap(response.data);
  },
};