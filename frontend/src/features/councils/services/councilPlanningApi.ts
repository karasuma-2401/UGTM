import { httpClient } from '@/shared/api';
import type { ApiResponse, PagedResult } from '@/shared/api/types';
import type { CouncilScheduleResponseDto } from '../types/councilApproval.types';

export interface CouncilScheduleInput {
  defenseDate: string;
  timeSlot: string;
  room: string;
}

export interface CreateCouncilInput extends CouncilScheduleInput {
  batchId: string;
  councilCode: string;
}

export interface CouncilWriteResponse extends CreateCouncilInput {
  id: string;
  status: string;
}

export interface DefenseEligibility {
  groupId: string;
  canProceed: boolean;
  reasonCode: string | null;
}

export interface CouncilGroupCandidate {
  groupId: string;
  topicTitleVi: string;
  reviewerFullName: string | null;
  hasEvaluation: boolean;
  assignedCouncilId: string | null;
}

export const councilScheduleKey = (id: string) => ['council-schedule', id] as const;

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.error.message || 'Không thể lưu thay đổi.');
  return response.data;
}

export const councilPlanningApi = {
  async create(input: CreateCouncilInput): Promise<CouncilWriteResponse> {
    const response = await httpClient.post<ApiResponse<CouncilWriteResponse>>('/api/councils', input);
    return unwrap(response.data);
  },
  async updateSchedule(id: string, input: CouncilScheduleInput): Promise<CouncilWriteResponse> {
    const response = await httpClient.put<ApiResponse<CouncilWriteResponse>>(`/api/councils/${id}`, input);
    return unwrap(response.data);
  },
  async schedule(id: string, signal?: AbortSignal): Promise<CouncilScheduleResponseDto> {
    const response = await httpClient.get<ApiResponse<CouncilScheduleResponseDto>>(`/api/councils/schedule/${id}`, { signal });
    return unwrap(response.data);
  },
  async defenseEligibility(groupId: string): Promise<DefenseEligibility> {
    const response = await httpClient.get<ApiResponse<DefenseEligibility>>(`/api/councils/group-candidates/${groupId}/eligibility`);
    return unwrap(response.data);
  },
  async addGroup(councilId: string, groupId: string, orderNumber: number): Promise<void> {
    const response = await httpClient.post<ApiResponse<unknown>>(`/api/councils/${councilId}/assignments`, {
      assignments: [{ groupId, orderNumber }],
    });
    unwrap(response.data);
  },
  async reorderGroups(councilId: string, assignments: { groupId: string; orderNumber: number }[]): Promise<void> {
    const response = await httpClient.put<ApiResponse<unknown>>(`/api/councils/${councilId}/assignments/order`, { assignments });
    unwrap(response.data);
  },
  async removeGroup(councilId: string, assignmentId: string): Promise<void> {
    const response = await httpClient.delete<ApiResponse<unknown>>(`/api/councils/${councilId}/assignments/${assignmentId}`);
    unwrap(response.data);
  },
  async groupCandidates(batchId: string, signal?: AbortSignal): Promise<CouncilGroupCandidate[]> {
    const rows: CouncilGroupCandidate[] = [];
    for (let pageNumber = 1; pageNumber <= 20; pageNumber += 1) {
      const response = await httpClient.get<ApiResponse<PagedResult<CouncilGroupCandidate>>>(
        '/api/councils/group-candidates',
        { params: { batchId, pageNumber, pageSize: 100 }, signal },
      );
      const page = unwrap(response.data);
      rows.push(...page.items);
      if (!page.hasNextPage) return rows;
    }
    throw new Error('Có quá nhiều nhóm trong đợt. Hãy dùng tìm kiếm hoặc liên hệ quản trị hệ thống.');
  },
};

const ELIGIBILITY_REASONS: Record<string, string> = {
  GroupNotReady: 'Nhóm chưa sẵn sàng để bảo vệ.',
  AssignmentMissing: 'Nhóm chưa được phân công giảng viên phản biện.',
  EvaluationMissing: 'Giảng viên phản biện chưa hoàn tất đánh giá.',
  EvaluationNotSubmitted: 'Giảng viên phản biện chưa nộp kết quả đánh giá.',
  DefenseRejected: 'Giảng viên phản biện không đồng ý cho nhóm bảo vệ.',
  ScoreBelowMinimum: 'Điểm phản biện chưa đạt mức tối thiểu của đợt.',
  ConfigurationMissing: 'Đợt chưa có cấu hình điều kiện bảo vệ.',
  ConfigurationAmbiguous: 'Cấu hình điều kiện bảo vệ của đợt đang mâu thuẫn.',
  ConfigurationInvalid: 'Cấu hình điều kiện bảo vệ của đợt chưa hợp lệ.',
  AdvisorConflict: 'Giảng viên phản biện trùng với giảng viên hướng dẫn.',
  BindingInvalid: 'Kết quả phản biện không còn khớp với hồ sơ đã duyệt.',
  EvaluationExcluded: 'Kết quả phản biện đã bị loại khỏi tính toán.',
  LegacyAssignment: 'Phân công phản biện cũ cần được xử lý trước.',
  LegacyEvaluation: 'Phiếu phản biện cũ cần được xử lý trước.',
  ContextInvalid: 'Thông tin nhóm hoặc hồ sơ chưa hợp lệ.',
};

export function eligibilityMessage(reasonCode: string | null): string {
  return (reasonCode && ELIGIBILITY_REASONS[reasonCode]) || 'Nhóm chưa đủ điều kiện bảo vệ. Hãy kiểm tra hồ sơ và kết quả phản biện.';
}
