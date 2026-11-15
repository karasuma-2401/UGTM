import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse, PagedResult } from '@/shared/api/types';

export interface GradingCriterionDto {
  id: string;
  code: string;
  name: string;
  maxScore: number;
  order: number;
  sectionCode: string | null;
  sectionName: string | null;
  sectionOrder: number;
  elo: string | null;
}

export interface GradingRubricDto {
  batchId: string;
  locked: boolean;
  criteria: GradingCriterionDto[];
}

export interface CriterionScoreInputDto {
  criteriaId: string;
  score: number;
  studentId?: string | null;
}

export interface StudentCriterionScoresInputDto {
  studentId: string;
  details: CriterionScoreInputDto[];
}

export interface EvaluationDetailResponseDto {
  id: string;
  criteriaId: string;
  criteriaCode: string;
  criteriaName: string;
  score: number;
  maxScore: number;
  studentId?: string | null;
}

export interface StudentEvaluationScoreDto {
  studentId: string;
  score: number | null;
  completedCriteriaCount: number;
  totalCriteriaCount: number;
  details: EvaluationDetailResponseDto[];
}

export interface AdvisorEvaluationDto {
  id: string;
  finalReportId: string;
  score: number;
  comment: string | null;
  status: 'Draft' | 'Submitted' | string;
  isLocked: boolean;
  evaluationVersion: number;
  evaluatedAt: string;
  details?: EvaluationDetailResponseDto[] | null;
  studentScores?: StudentEvaluationScoreDto[] | null;
}

export interface SaveAdvisorEvaluationRequest {
  finalReportId: string;
  expectedReportVersionNo: number;
  expectedEvaluationVersion?: number | null;
  score?: number | null;
  comment?: string | null;
  details?: CriterionScoreInputDto[] | null;
  studentScores?: StudentCriterionScoresInputDto[] | null;
  action?: 'SaveDraft' | 'Submit';
}

export interface DecideAdvisorEvaluationRequest {
  expectedReportVersionNo: number;
  expectedEvaluationVersion?: number | null;
  allowsReviewerAssignment: boolean;
  rejectionReason?: string | null;
}

export interface ReviewerAssignmentStudentDto {
  studentId: string;
  fullName: string;
  userCode: string | null;
}

export interface ReviewerAssignmentTaskDto {
  id: string;
  groupId: string;
  topicId: string;
  batchId: string;
  topicTitleVi: string;
  topicTitleEn: string | null;
  assignedAtUtc: string;
  assignmentVersion: number;
  isLegacy: boolean;
  finalReportId: string | null;
  reportVersionNo: number | null;
  submissionFileId: string | null;
  canDownloadReport: boolean;
  accessBlockCode: string | null;
  students?: ReviewerAssignmentStudentDto[] | null;
}

export interface ReviewerEvaluationDto {
  id: string;
  assignmentId: string;
  topicId: string;
  batchId: string;
  score: number | null;
  comment: string | null;
  defenseApproval: 'Approved' | 'Rejected' | null;
  status: 'Draft' | 'Submitted' | string;
  isLocked: boolean;
  evaluationVersion: number;
  evaluatedAt: string | null;
  details?: EvaluationDetailResponseDto[] | null;
  studentScores?: StudentEvaluationScoreDto[] | null;
}

export interface SaveReviewerEvaluationRequest {
  assignmentId: string;
  action: 'SaveDraft' | 'Submit';
  expectedAssignmentVersion: number;
  expectedEvaluationVersion?: number | null;
  score?: number | null;
  comment?: string | null;
  defenseApproval?: 'Approved' | 'Rejected' | null;
  details?: CriterionScoreInputDto[] | null;
  studentScores?: StudentCriterionScoresInputDto[] | null;
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

export const lecturerGradingApi = {
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

  async getAdvisorEvaluation(
    groupId: string,
    signal?: AbortSignal,
  ): Promise<AdvisorEvaluationDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<AdvisorEvaluationDto>>(
        `/api/thesis/groups/${groupId}/advisor-evaluation`,
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

  async saveAdvisorEvaluation(
    payload: SaveAdvisorEvaluationRequest,
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<AdvisorEvaluationDto> {
    const response = await httpClient.post<ApiResponse<AdvisorEvaluationDto>>(
      '/api/thesis/advisor-evaluations',
      payload,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },

  async decideAdvisorEvaluation(
    finalReportId: string,
    payload: DecideAdvisorEvaluationRequest,
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<AdvisorEvaluationDto> {
    const response = await httpClient.put<ApiResponse<AdvisorEvaluationDto>>(
      `/api/thesis/final-reports/${finalReportId}/approval`,
      payload,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },

  async getMyReviewerTasks(
    batchId?: string,
    signal?: AbortSignal,
  ): Promise<ReviewerAssignmentTaskDto[]> {
    const response = await httpClient.get<ApiResponse<PagedResult<ReviewerAssignmentTaskDto>>>(
      '/api/thesis/me/reviewer-assignments',
      {
        params: {
          batchId: batchId || undefined,
          pageNumber: 1,
          pageSize: 100,
        },
        signal,
      },
    );
    return unwrap(response.data).items ?? [];
  },

  async getReviewerEvaluation(
    assignmentId: string,
    signal?: AbortSignal,
  ): Promise<ReviewerEvaluationDto | null> {
    try {
      const response = await httpClient.get<ApiResponse<ReviewerEvaluationDto>>(
        `/api/thesis/reviewer-assignments/${assignmentId}/evaluation`,
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

  async saveReviewerEvaluation(
    payload: SaveReviewerEvaluationRequest,
    idempotencyKey: string = crypto.randomUUID(),
  ): Promise<ReviewerEvaluationDto> {
    const response = await httpClient.post<ApiResponse<ReviewerEvaluationDto>>(
      '/api/thesis/reviewer-evaluations',
      payload,
      {
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    );
    return unwrap(response.data);
  },
};
