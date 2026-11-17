export interface ReviewerAssignmentEligibilityItemDto {
  groupId: string;
  topicId: string;
  batchId: string;
  topicTitleVi: string;
  topicTitleEn?: string | null;
  advisorId: string;
  advisorFullName?: string | null;
  advisorEvaluationId: string;
  finalReportId: string;
  submissionFileId: string;
  reportVersionNo: number;
  submissionChecksumSha256: string;
  approvedAtUtc: string;
  hasActiveAssignment: boolean;
  activeAssignmentId?: string | null;
  canAssign: boolean;
}

export interface ReviewerCandidateContractDto {
  id: string;
  fullName: string;
  userCode?: string | null;
}

export interface ReviewerAssignmentDto {
  id: string;
  groupId: string;
  topicId: string;
  batchId: string;
  reviewerId: string;
  advisorIdAtAssignment?: string;
  finalReportId?: string;
  reportVersionNo?: number;
  submissionFileId?: string;
  submissionChecksumSha256?: string;
  isActive: boolean;
  version: number;
  assignedAtUtc: string;
  assignedBy: string;
}

export interface CreateReviewerAssignmentRequest {
  groupId: string;
  reviewerId: string;
  expectedAdvisorEvaluationId: string;
  expectedReportVersionNo: number;
}

export interface CancelReviewerAssignmentRequest {
  expectedAssignmentVersion: number;
  reason?: string;
}

export type ReviewerAssignmentFilterStatus = 'all' | 'pending' | 'assigned';