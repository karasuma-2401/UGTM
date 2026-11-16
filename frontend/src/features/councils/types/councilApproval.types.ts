export type CouncilApprovalStatus = 'all' | 'draft' | 'active' | 'completed';

export type CouncilReadinessFilter = 'all' | 'ready' | 'needs_action' | 'approved';

export interface CouncilOverviewMemberDto {
  userId: string;
  fullName: string;
  role: string;
  isReviewer: boolean;
  isAdvisorConflict: boolean;
  note?: string | null;
}

export interface CouncilOverviewApiItemDto {
  id: string;
  batchId: string;
  councilCode: string;
  facultyName: string;
  groupCode?: string | null;
  topicTitle?: string | null;
  room: string;
  timeSlot: string;
  defenseDate: string;
  status: 'Draft' | 'Active' | 'Completed';
  statusLabel: string;
  rosterModel: string;
  rosterModelLabel: string;
  profileStatus: string;
  profileStatusLabel: string;
  minuteLockStatus: string;
  minuteLockStatusLabel: string;
  assignmentId?: string | null;
  chairman?: CouncilOverviewMemberDto | null;
  secretary?: CouncilOverviewMemberDto | null;
  members: CouncilOverviewMemberDto[];
  isSubmittedToAcademicAffairs?: boolean;
  submittedAt?: string | null;
  submissionNotes?: string | null;
  decisionNumber?: string | null;
  signedDate?: string | null;
  decisionFileUrl?: string | null;
  approvedAt?: string | null;
  hasRoomConflict?: boolean;
  conflictingMemberNames?: string[] | null;
  hasIneligibleGroups?: boolean;
  hasReviewerRoleConflict?: boolean;
}

export interface CouncilDraftAssignmentSummary {
  assignmentId?: string | null;
  groupCode: string;
  topicTitle?: string | null;
}

export interface CouncilDraftItemDto {
  id: string;
  batchId: string;
  councilCode: string;
  facultyName: string;
  groupCode: string;
  groupCodes: string[];
  topicTitles: string[];
  assignments: CouncilDraftAssignmentSummary[];
  room: string;
  timeSlot: string;
  defenseDate: string;
  status: 'Draft' | 'Active' | 'Completed';
  statusLabel: string;
  rosterModel: string;
  rosterModelLabel: string;
  groupConditionLabel: string;
  membersSummary: string;
  chairman?: CouncilOverviewMemberDto | null;
  secretary?: CouncilOverviewMemberDto | null;
  members: CouncilOverviewMemberDto[];
  chairmanName?: string;
  secretaryName?: string;
  memberNames?: string[];
  isSubmittedToAcademicAffairs: boolean;
  submittedAt?: string | null;
  submissionNotes?: string | null;
  decisionNumber?: string | null;
  signedDate?: string | null;
  decisionFileUrl?: string | null;
  approvedAt?: string | null;
  hasRoomConflict: boolean;
  conflictingMemberNames: string[];
  hasIneligibleGroups: boolean;
  hasReviewerRoleConflict: boolean;
  hasAdvisorConflict: boolean;
}

export interface CouncilScheduleStudentDto {
  studentId: string;
  fullName: string;
  email: string;
  userCode?: string | null;
}

export interface CouncilScheduleGroupDto {
  groupId: string;
  orderNumber: number;
  topicId: string;
  topicTitleVi: string;
  topicTitleEn?: string | null;
  advisorId: string;
  advisorName?: string | null;
  reviewerId?: string | null;
  reviewerName?: string | null;
  isReviewerInCouncil: boolean;
  students: CouncilScheduleStudentDto[];
  assignmentId?: string | null;
}

export interface CouncilScheduleMemberDto {
  userId: string;
  fullName: string;
  email: string;
  userCode?: string | null;
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
  decisionNumber?: string | null;
  signedDate?: string | null;
  members: CouncilScheduleMemberDto[];
  groups: CouncilScheduleGroupDto[];
}

export interface ApproveCouncilRequest {
  decisionNumber: string;
  signedDate?: string | null;
  decisionFileUrl?: string | null;
}

export interface CouncilApprovalResponseDto {
  id: string;
  batchId: string;
  councilCode: string;
  defenseDate: string;
  timeSlot: string;
  room: string;
  status: string;
  decisionNumber?: string | null;
  signedDate?: string | null;
  decisionFileUrl?: string | null;
  approvedBy?: string | null;
  approvedAt?: string | null;
}
