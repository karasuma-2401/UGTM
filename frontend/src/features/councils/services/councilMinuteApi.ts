import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  CouncilDiscrepancyResponseDto,
  CouncilMinuteLockResponseDto,
  CouncilMinuteWorkspaceResponseDto,
  LockCouncilMinuteRequest,
  SaveCouncilMinuteRequest,
  SaveCouncilMinuteResponseDto,
  UnlockCouncilMinuteRequest,
} from '../types/councilMinute.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const councilMinuteApi = {
  async getMinuteWorkspace(
    assignmentId: string,
    signal?: AbortSignal,
  ): Promise<CouncilMinuteWorkspaceResponseDto> {
    const response = await httpClient.get<ApiResponse<CouncilMinuteWorkspaceResponseDto>>(
      `/api/councils/assignments/${assignmentId}/minute-workspace`,
      { signal },
    );
    return unwrap(response.data);
  },

  async getDiscrepancyCheck(
    assignmentId: string,
    signal?: AbortSignal,
  ): Promise<CouncilDiscrepancyResponseDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<CouncilDiscrepancyResponseDto>>(
        `/api/councils/assignments/${assignmentId}/discrepancy-check`,
        { signal },
      );
      return unwrap(response.data);
    } catch (error) {
      if (axios.isAxiosError(error) && (error.response?.status === 403 || error.response?.status === 404)) {
        return null;
      }
      throw error;
    }
  },

  async previewMinute(
    assignmentId: string,
    payload: SaveCouncilMinuteRequest,
  ): Promise<SaveCouncilMinuteResponseDto> {
    const response = await httpClient.put<ApiResponse<SaveCouncilMinuteResponseDto>>(
      `/api/councils/assignments/${assignmentId}/minute`,
      payload,
      { params: { preview: true } },
    );
    return unwrap(response.data);
  },

  async saveMinute(
    assignmentId: string,
    payload: SaveCouncilMinuteRequest,
  ): Promise<SaveCouncilMinuteResponseDto> {
    const response = await httpClient.put<ApiResponse<SaveCouncilMinuteResponseDto>>(
      `/api/councils/assignments/${assignmentId}/minute`,
      payload,
      { params: { preview: false } },
    );
    return unwrap(response.data);
  },

  async lockMinute(
    assignmentId: string,
    payload: LockCouncilMinuteRequest,
  ): Promise<CouncilMinuteLockResponseDto> {
    const response = await httpClient.post<ApiResponse<CouncilMinuteLockResponseDto>>(
      `/api/councils/assignments/${assignmentId}/minute/lock`,
      payload,
    );
    return unwrap(response.data);
  },

  async unlockMinute(
    assignmentId: string,
    payload: UnlockCouncilMinuteRequest,
  ): Promise<CouncilMinuteLockResponseDto> {
    const response = await httpClient.post<ApiResponse<CouncilMinuteLockResponseDto>>(
      `/api/councils/assignments/${assignmentId}/minute/unlock`,
      payload,
    );
    return unwrap(response.data);
  },
};
