export type CouncilMinuteLockStatus =
  | 'Open'
  | 'AllScoresEntered'
  | 'Locked'
  | 'UnlockedByOverride';

export type SecretaryMarking = 'None' | 'AcceptedAsPerMinute' | 'RejectedAsPerMinute';

export interface MinuteWorkspaceAssignmentInfoDto {
  assignmentId: string;
  councilId: string;
  councilCode: string;
  groupId: string;
  groupCode: string;
  chairmanName?: string | null;
  secretaryName?: string | null;
  lockStatus: CouncilMinuteLockStatus | string;
  studentFeedback?: string | null;
}

export interface MinuteScoreItemDto {
  componentCode: string;
  roleLabel: string;
  lecturerName: string;
  score: number | null;
  weight: number;
  statusText: string;
  category: string;
  isDualRoleReviewer: boolean;
  isIgnoredInCalculation: boolean;
}

export interface MinuteDiscrepancySummaryDto {
  hasDiscrepancy: boolean;
  scoreDifference: number | null;
  chairmanConclusion?: string | null;
  secretaryMarking?: SecretaryMarking | string | null;
  ignoredComponentCode?: string | null;
  isIgnoredInCalculation: boolean;
}

export interface MinuteCalculationPreviewDto {
  rawTotal: number;
  rawDivisor: number;
  rawAverage: number;
  adjustedTotal: number;
  adjustedDivisor: number;
  adjustedAverage: number;
  finalRoundedScore: number | null;
  formulaExplanation: string;
  isReadyToLock: boolean;
  configurationId?: string | null;
  configurationVersion?: number | null;
  calculationErrorCode?: string | null;
}

export interface CouncilMinuteWorkspaceResponseDto {
  assignmentInfo: MinuteWorkspaceAssignmentInfoDto;
  scoreItems: MinuteScoreItemDto[];
  discrepancySummary: MinuteDiscrepancySummaryDto;
  calculationPreview: MinuteCalculationPreviewDto;
}

export interface SaveCouncilMinuteRequest {
  secretaryMarking?: string | null;
  targetComponentCode?: string | null;
  studentFeedback?: string | null;
  chairmanConclusion?: string | null;
  reason?: string | null;
}

export interface SaveCouncilMinuteResponseDto {
  assignmentId: string;
  lockStatus: CouncilMinuteLockStatus | string;
  studentFeedback?: string | null;
  isPreview: boolean;
  discrepancySummary: MinuteDiscrepancySummaryDto;
  calculationPreview: MinuteCalculationPreviewDto;
  scoreItems: MinuteScoreItemDto[];
}

export interface LockCouncilMinuteRequest {
  password: string;
}

export interface UnlockCouncilMinuteRequest {
  overrideReason: string;
}

export interface CouncilMinuteLockResponseDto {
  minuteId?: string | null;
  assignmentId: string;
  lockStatus: CouncilMinuteLockStatus | string;
  lockStatusLabel: string;
  lockedAt?: string | null;
  lockedBy?: string | null;
  unlockedAt?: string | null;
  unlockedBy?: string | null;
  overrideReason?: string | null;
}

export interface MemberScoreDetailDto {
  councilMemberId: string;
  userId: string;
  fullName: string;
  email: string;
  userCode?: string | null;
  role: string;
  score: number | null;
  feedback?: string | null;
  evaluatedAt?: string | null;
  isReviewerInCouncil: boolean;
  isBlockedFromScoring: boolean;
}

export interface CouncilDiscrepancyResponseDto {
  assignmentId: string;
  councilId: string;
  groupId: string;
  isDiscrepancyDetected: boolean;
  warningThreshold: number;
  scoreDifference: number | null;
  maxScore: number | null;
  minScore: number | null;
  warningMessage?: string | null;
  allScoresSubmitted: boolean;
  averageCouncilScore: number | null;
  finalScore: number | null;
  memberScores: MemberScoreDetailDto[];
}
