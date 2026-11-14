import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiError, ApiResponse, PagedResult } from '@/shared/api/types';
import type {
  AcademicResultDetailDto,
  AcademicResultListItemDto,
  CalculateScoreSummaryRequest,
  PublishScoreSummaryRequest,
  ResultsExportFormat,
  ScoreListItemDto,
  ScorePageDto,
  ScorePublicationDto,
  ScoreReadinessDto,
  ScoreSummaryDto,
} from '../types/results.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export function extractApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const payload = error.response?.data as Partial<ApiResponse<unknown>> | undefined;
    if (payload && payload.error) {
      return payload.error;
    }
    if (error.message) {
      return { code: 'RequestFailed', message: error.message, errors: null };
    }
  }
  if (error instanceof Error) {
    return { code: 'UnknownError', message: error.message, errors: null };
  }
  return { code: 'UnknownError', message: 'Đã xảy ra lỗi không xác định.', errors: null };
}

function parseFilename(contentDisposition: string | undefined, fallback: string): string {
  if (!contentDisposition) {
    return fallback;
  }
  const utf8Match = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8Match?.[1]) {
    try {
      return decodeURIComponent(utf8Match[1].trim());
    } catch {
      return utf8Match[1].trim();
    }
  }
  const plainMatch = contentDisposition.match(/filename="?([^";]+)"?/i);
  return plainMatch?.[1]?.trim() || fallback;
}

export const academicResultsApi = {
  async getAllScoreSummaries(batchId: string, signal?: AbortSignal): Promise<ScoreListItemDto[]> {
    const items: ScoreListItemDto[] = [];
    let pageNumber = 1;
    const pageSize = 100;

    while (true) {
      const response = await httpClient.get<ApiResponse<ScorePageDto>>('/api/thesis/scores', {
        params: { batchId, pageNumber, pageSize },
        signal,
      });
      const page = unwrap(response.data);
      items.push(...page.items);
      if (items.length >= page.totalCount || page.items.length < pageSize) {
        return items;
      }
      pageNumber += 1;
    }
  },

  async getAllAcademicResults(
    batchId: string,
    signal?: AbortSignal,
  ): Promise<AcademicResultListItemDto[]> {
    const items: AcademicResultListItemDto[] = [];
    let pageIndex = 1;
    const pageSize = 100;

    while (true) {
      const response = await httpClient.get<ApiResponse<PagedResult<AcademicResultListItemDto>>>(
        '/api/academic-affairs/results',
        {
          params: { batchId, phase: 'Final', pageIndex, pageSize },
          signal,
        },
      );
      const page = unwrap(response.data);
      items.push(...page.items);
      if (!page.hasNextPage || items.length >= page.totalCount || page.items.length < pageSize) {
        return items;
      }
      pageIndex += 1;
    }
  },

  async getScoreSummaryDetail(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<ScoreSummaryDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<ScoreSummaryDto>>(
        `/api/thesis/scores/group/${groupId}`,
        { signal },
      );
      return unwrap(response.data);
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  },

  async getScoreReadiness(groupId: string, signal?: AbortSignal): Promise<ScoreReadinessDto> {
    const response = await httpClient.get<ApiResponse<ScoreReadinessDto>>(
      `/api/thesis/scores/readiness/${groupId}`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getAcademicResultDetail(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<AcademicResultDetailDto> {
    const response = await httpClient.get<ApiResponse<AcademicResultDetailDto>>(
      `/api/academic-affairs/results/${groupId}`,
      { params: { phase: 'Final' }, signal },
    );
    return unwrap(response.data);
  },

  async calculateScore(
    groupId: string,
    payload: CalculateScoreSummaryRequest,
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<ScoreSummaryDto> {
    const response = await httpClient.post<ApiResponse<ScoreSummaryDto>>(
      `/api/thesis/scores/calculate/${groupId}`,
      payload,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },

  async publishScores(
    payload: PublishScoreSummaryRequest,
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<ScorePublicationDto> {
    const response = await httpClient.put<ApiResponse<ScorePublicationDto>>(
      '/api/thesis/scores/publish',
      payload,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },

  async exportResultsFile(params: {
    batchId: string;
    format: ResultsExportFormat;
    keyword?: string;
    status?: string;
  }): Promise<string> {
    try {
      const response = await httpClient.get<Blob>('/api/academic-affairs/results/export', {
        params: {
          batchId: params.batchId,
          format: params.format,
          phase: 'Final',
          keyword: params.keyword?.trim() || undefined,
          status: params.status || undefined,
        },
        responseType: 'blob',
      });

      const disposition = response.headers['content-disposition'] as string | undefined;
      const fallbackName = `KetQuaKLTN_Final.${params.format}`;
      const filename = parseFilename(disposition, fallbackName);

      const blobUrl = URL.createObjectURL(response.data);
      const link = document.createElement('a');
      link.href = blobUrl;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(blobUrl);

      return filename;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.data instanceof Blob) {
        const text = await error.response.data.text();
        try {
          const parsed = JSON.parse(text) as Partial<ApiResponse<unknown>>;
          if (parsed.error?.message) {
            throw new Error(parsed.error.message);
          }
        } catch (parseError) {
          if (parseError instanceof Error && !(parseError instanceof SyntaxError)) {
            throw parseError;
          }
        }
      }
      throw new Error(extractApiError(error).message);
    }
  },
};
