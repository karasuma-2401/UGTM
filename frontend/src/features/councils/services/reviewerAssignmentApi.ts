import { httpClient } from '@/shared/api';
import type { ApiResponse, PagedResult } from '@/shared/api/types';
import type {
  ReviewerAssignmentEligibilityItemDto,
  ReviewerCandidateContractDto,
  ReviewerAssignmentDto,
  CreateReviewerAssignmentRequest,
  CancelReviewerAssignmentRequest,
} from '../types/reviewerAssignment.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const reviewerAssignmentApi = {
  async getEligibilityList(
    params: {
      batchId: string;
      includeAssigned?: boolean;
      keyword?: string;
      pageNumber?: number;
      pageSize?: number;
    },
    signal?: AbortSignal,
  ): Promise<PagedResult<ReviewerAssignmentEligibilityItemDto>> {
    const response = await httpClient.get<
      ApiResponse<PagedResult<ReviewerAssignmentEligibilityItemDto>>
    >('/api/thesis/reviewer-assignment-eligibility', {
      params: {
        batchId: params.batchId,
        includeAssigned: params.includeAssigned ?? true,
        keyword: params.keyword?.trim() || undefined,
        pageNumber: params.pageNumber ?? 1,
        pageSize: params.pageSize ?? 50,
      },
      signal,
    });
    return unwrap(response.data);
  },

  async getCandidates(
    groupId: string,
    params?: {
      keyword?: string;
      pageNumber?: number;
      pageSize?: number;
    },
    signal?: AbortSignal,
  ): Promise<PagedResult<ReviewerCandidateContractDto>> {
    const response = await httpClient.get<
      ApiResponse<PagedResult<ReviewerCandidateContractDto>>
    >('/api/thesis/reviewer-candidates', {
      params: {
        groupId,
        keyword: params?.keyword?.trim() || undefined,
        pageNumber: params?.pageNumber ?? 1,
        pageSize: params?.pageSize ?? 100,
      },
      signal,
    });
    return unwrap(response.data);
  },

  async createAssignment(
    request: CreateReviewerAssignmentRequest,
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<ReviewerAssignmentDto> {
    const response = await httpClient.post<ApiResponse<ReviewerAssignmentDto>>(
      '/api/thesis/reviewer-assignments',
      request,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },

  async cancelAssignment(
    assignmentId: string,
    request: CancelReviewerAssignmentRequest,
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<ReviewerAssignmentDto> {
    const response = await httpClient.delete<ApiResponse<ReviewerAssignmentDto>>(
      `/api/thesis/reviewer-assignments/${assignmentId}`,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
        data: request,
      },
    );
    return unwrap(response.data);
  },

  async getAssignmentsList(
    params: {
      batchId?: string;
      groupId?: string;
      reviewerId?: string;
      isActive?: boolean;
      includeHistory?: boolean;
      keyword?: string;
      pageNumber?: number;
      pageSize?: number;
    },
    signal?: AbortSignal,
  ): Promise<PagedResult<ReviewerAssignmentDto>> {
    const response = await httpClient.get<ApiResponse<PagedResult<ReviewerAssignmentDto>>>(
      '/api/thesis/reviewer-assignments',
      {
        params,
        signal,
      },
    );
    return unwrap(response.data);
  },
};