import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { PagedResult } from '../types/user.types';
import type {
  KltnBatchSummaryDto,
  KltnBatchDetailDto,
  CreateKltnBatchRequest,
  UpdateKltnBatchScheduleRequest,
  ReviewGateConfigurationDto,
  ReviewGateType,
  UpsertReviewGateConfigurationRequest,
} from '../types/period.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message);
  }
  return response.data;
}

export function getPeriodErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const response = error.response?.data;
    if (response?.success === false && response.error?.message) {
      return response.error.message;
    }
    if (error.response?.status === 400) {
      return 'Dữ liệu không hợp lệ. Vui lòng kiểm tra lại thông tin và thứ tự các mốc thời gian.';
    }
    if (error.response?.status === 403) {
      return 'Bạn không có quyền quản lý đợt KLTN hoặc deadline (Chỉ dành cho Admin và PĐT).';
    }
    if (error.response?.status === 404) {
      return 'Không tìm thấy đợt khóa luận tương ứng.';
    }
    if (error.response?.status === 409) {
      return 'Đợt KLTN cho năm học và học kỳ này đã tồn tại hoặc trạng thái đợt không cho phép cập nhật.';
    }
    if (!error.response) {
      return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng rồi thử lại.';
    }
  }

  return error instanceof Error ? error.message : 'Đã có lỗi xảy ra khi xử lý đợt khóa luận.';
}

export const periodApi = {
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

  async getBatchById(
    batchId: string,
    signal?: AbortSignal,
  ): Promise<KltnBatchDetailDto> {
    const response = await httpClient.get<ApiResponse<KltnBatchDetailDto>>(
      `/api/academic-periods/batches/${batchId}`,
      { signal },
    );
    return unwrap(response.data);
  },

  async createBatch(
    data: CreateKltnBatchRequest,
  ): Promise<KltnBatchDetailDto> {
    const response = await httpClient.post<ApiResponse<KltnBatchDetailDto>>(
      '/api/academic-periods/batches',
      data,
    );
    return unwrap(response.data);
  },

  async updateSchedule(
    batchId: string,
    data: UpdateKltnBatchScheduleRequest,
  ): Promise<KltnBatchDetailDto> {
    const response = await httpClient.put<ApiResponse<KltnBatchDetailDto>>(
      `/api/academic-periods/batches/${batchId}/schedule`,
      data,
    );
    return unwrap(response.data);
  },

  async openBatch(batchId: string): Promise<KltnBatchDetailDto> {
    const response = await httpClient.post<ApiResponse<KltnBatchDetailDto>>(
      `/api/academic-periods/batches/${batchId}/open`,
    );
    return unwrap(response.data);
  },

  async closeBatch(batchId: string): Promise<KltnBatchDetailDto> {
    const response = await httpClient.post<ApiResponse<KltnBatchDetailDto>>(
      `/api/academic-periods/batches/${batchId}/close`,
    );
    return unwrap(response.data);
  },

  async archiveBatch(batchId: string): Promise<KltnBatchDetailDto> {
    const response = await httpClient.post<ApiResponse<KltnBatchDetailDto>>(
      `/api/academic-periods/batches/${batchId}/archive`,
    );
    return unwrap(response.data);
  },

  async getReviewGateConfigurations(
    batchId: string,
    signal?: AbortSignal,
  ): Promise<ReviewGateConfigurationDto[]> {
    try {
      const response = await httpClient.get<ApiResponse<ReviewGateConfigurationDto[]>>(
        `/api/thesis/review-gate-configurations/${batchId}`,
        { signal },
      );
      return unwrap(response.data);
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 404) {
        return [];
      }
      throw err;
    }
  },

  async upsertReviewGateConfiguration(
    batchId: string,
    gateType: ReviewGateType,
    data: UpsertReviewGateConfigurationRequest,
  ): Promise<ReviewGateConfigurationDto> {
    const response = await httpClient.put<ApiResponse<ReviewGateConfigurationDto>>(
      `/api/thesis/review-gate-configurations/${batchId}/${gateType}`,
      data,
    );
    return unwrap(response.data);
  },
};

