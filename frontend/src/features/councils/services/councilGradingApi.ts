import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type { CouncilOverviewApiItemDto } from '../types/councilApproval.types';
import type {
  CriterionScoreInputDto,
  EvaluationDetailResponseDto,
  GradingRubricDto,
  StudentCriterionScoresInputDto,
  StudentEvaluationScoreDto,
} from '@/features/thesis/services/lecturerGradingApi';

export interface CouncilScheduleStudentDto {
  studentId: string;
  fullName: string;
  email: string;
  userCode: string | null;
}

export interface CouncilScheduleGroupDto {
  groupId: string;
  orderNumber: number;
  topicId: string;
  topicTitleVi: string;
  topicTitleEn: string | null;
  advisorId: string;
  advisorName: string | null;
  reviewerId: string | null;
  reviewerName: string | null;
  isReviewerInCouncil: boolean;
  students: CouncilScheduleStudentDto[];
  assignmentId?: string | null;
}

export interface CouncilScheduleMemberDto {
  userId: string;
  fullName: string;
  email: string;
  userCode: string | null;
  role: string;
}

export interface CouncilScheduleResponseDto {
  councilId: string;
  batchId: string;
  councilCode: string;
  defenseDate: string;
  timeSlot: string;
  room: string;
  status: string;
  decisionNumber: string | null;
  signedDate: string | null;
  members: CouncilScheduleMemberDto[];
  groups: CouncilScheduleGroupDto[];
}

export interface CouncilScoreResponseDto {
  id: string;
  councilAssignmentId: string;
  councilMemberId: string;
  userId: string;
  score: number;
  feedback: string | null;
  evaluatedAt: string;
  details: EvaluationDetailResponseDto[];
  studentScores?: StudentEvaluationScoreDto[] | null;
}

export interface SubmitCouncilScoreRequest {
  details?: CriterionScoreInputDto[] | null;
  feedback?: string | null;
  studentScores?: StudentCriterionScoresInputDto[] | null;
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const councilGradingApi = {
  async getCouncilOverviewRows(
    batchId?: string,
    signal?: AbortSignal,
  ): Promise<CouncilOverviewApiItemDto[]> {
    const response = await httpClient.get<ApiResponse<CouncilOverviewApiItemDto[]>>(
      '/api/councils',
      {
        params: {
          batchId: batchId && batchId !== 'all' ? batchId : undefined,
          pageNumber: 1,
          pageSize: 100,
        },
        signal,
      },
    );
    return unwrap(response.data);
  },

  async getCouncilSchedule(
    councilId: string,
    signal?: AbortSignal,
  ): Promise<CouncilScheduleResponseDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<CouncilScheduleResponseDto>>(
        `/api/councils/schedule/${councilId}`,
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

  async getGradingRubric(
    batchId: string,
    signal?: AbortSignal,
  ): Promise<GradingRubricDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<GradingRubricDto>>(
        `/api/thesis/batches/${batchId}/grading-rubric`,
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

  async submitCouncilScore(
    assignmentId: string,
    payload: SubmitCouncilScoreRequest,
  ): Promise<CouncilScoreResponseDto> {
    const response = await httpClient.post<ApiResponse<CouncilScoreResponseDto>>(
      `/api/councils/assignments/${assignmentId}/scores`,
      payload,
    );
    return unwrap(response.data);
  },
};
