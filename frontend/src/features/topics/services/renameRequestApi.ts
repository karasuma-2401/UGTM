import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { RenameRequest, RenameRequestStatus } from '../types/renameRequest.types';

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message);
  return response.data;
}

const root = '/api/thesis';

export const renameRequestApi = {
  async listByGroup(groupId: string, signal?: AbortSignal) {
    const response = await httpClient.get<ApiResponse<RenameRequest[]>>(
      `${root}/groups/${groupId}/rename-requests`,
      { signal },
    );
    return unwrap(response.data);
  },
  async list(status?: RenameRequestStatus | '', signal?: AbortSignal) {
    const response = await httpClient.get<ApiResponse<RenameRequest[]>>(`${root}/rename-requests`, {
      params: status ? { status } : undefined,
      signal,
    });
    return unwrap(response.data);
  },
  async create(
    groupId: string,
    input: { newTitleVi: string; newTitleEn: string | null; reason: string },
  ) {
    const response = await httpClient.post<ApiResponse<RenameRequest>>(
      `${root}/groups/${groupId}/rename-requests`,
      input,
    );
    return unwrap(response.data);
  },
  async review(
    requestId: string,
    level: 'advisor' | 'faculty' | 'academic-affairs',
    input: { decision: 'Approve' | 'Reject'; rejectionReason: string | null },
  ) {
    const response = await httpClient.post<ApiResponse<RenameRequest>>(
      `${root}/rename-requests/${requestId}/${level}-review`,
      input,
    );
    return unwrap(response.data);
  },
};
