import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  CouncilDraftItemDto,
  ApproveCouncilRequest,
  CouncilApprovalResponseDto,
} from '../types/councilApproval.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const councilApprovalApi = {
  async getCouncilDrafts(
    _params?: {
      batchId?: string;
      keyword?: string;
      status?: string;
    },
    _signal?: AbortSignal,
  ): Promise<CouncilDraftItemDto[]> {
    return [];
  },

  async approveCouncil(
    id: string,
    request: ApproveCouncilRequest,
  ): Promise<CouncilApprovalResponseDto> {
    const response = await httpClient.put<ApiResponse<CouncilApprovalResponseDto>>(
      `/api/councils/${id}/approve`,
      request,
    );
    return unwrap(response.data);
  },
};
