import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';

export type CouncilRole = 'Chairman' | 'Secretary' | 'Member';
export interface CouncilCandidate {
  userId: string;
  fullName: string;
  email: string;
  userCode: string | null;
  isAlreadyInCouncil: boolean;
  currentRoleInCouncil?: CouncilRole | null;
  isScheduleConflict?: boolean;
  isAdvisorConflict?: boolean;
  isReviewerForCouncilGroups?: boolean;
  isEligible: boolean;
  allowedRoles: CouncilRole[];
  conflictReason: string | null;
}
export interface MemberInput {
  userId: string;
  role: CouncilRole;
}
/** `CouncilMemberResponseDto` của backend. */
export interface CouncilMemberResponse {
  id: string;
  councilId: string;
  userId: string;
  role: CouncilRole;
  fullName?: string | null;
  email?: string | null;
  userCode?: string | null;
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message || 'Thao tác không thành công.');
  return response.data;
}

/** Thông báo lỗi nói rõ nguyên nhân và cách khôi phục, thay vì một câu chung chung. */
export function memberError(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const serverMessage = error.response?.data?.error?.message;
    const status = error.response?.status;
    if (status === 401) return 'Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại rồi thử lại.';
    if (status === 403) {
      return serverMessage || 'Tài khoản của bạn không có quyền quản lý thành viên hội đồng.';
    }
    if (serverMessage) return serverMessage;
    if (!error.response) {
      return 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại; dữ liệu bạn nhập vẫn được giữ nguyên.';
    }
    if (status === 404) return 'Hội đồng này không còn tồn tại. Hãy bấm Làm mới để cập nhật danh sách.';
    if (status === 409) {
      return 'Dữ liệu hội đồng vừa được thay đổi bởi người khác. Hãy bấm Làm mới rồi thử lại.';
    }
    return 'Máy chủ gặp lỗi khi xử lý. Hãy thử lại sau ít phút.';
  }
  return error instanceof Error && error.message ? error.message : 'Không thể thực hiện thao tác.';
}

export const councilMembersApi = {
  async candidates(id: string, keyword: string, signal?: AbortSignal) {
    const response = await httpClient.get<ApiResponse<CouncilCandidate[]>>(
      `/api/councils/${id}/member-candidates`,
      { params: { keyword: keyword.trim() || undefined }, signal },
    );
    return unwrap(response.data);
  },
  async add(id: string, member: MemberInput) {
    return unwrap(
      (
        await httpClient.post<ApiResponse<CouncilMemberResponse[]>>(`/api/councils/${id}/members`, {
          members: [member],
        })
      ).data,
    );
  },
  async replace(id: string, members: MemberInput[]) {
    return unwrap(
      (await httpClient.put<ApiResponse<CouncilMemberResponse[]>>(`/api/councils/${id}/members`, { members }))
        .data,
    );
  },
  async remove(id: string, userId: string) {
    return unwrap(
      (await httpClient.delete<ApiResponse<CouncilMemberResponse[]>>(`/api/councils/${id}/members/${userId}`))
        .data,
    );
  },
  async submit(id: string, notes: string) {
    return unwrap(
      (
        await httpClient.post<ApiResponse<unknown>>(`/api/councils/${id}/submit`, {
          notes: notes.trim() || null,
        })
      ).data,
    );
  },
};
