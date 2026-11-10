export type CouncilApprovalStatus = 'all' | 'draft' | 'active' | 'completed';

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
}

export interface CouncilDraftItemDto {
  id: string;
  batchId: string;
  councilCode: string;
  facultyName: string;
  groupCode: string;
  groupCodes: string[];
  topicTitles: string[];
  room: string;
  timeSlot: string;
  defenseDate: string;
  status: 'Draft' | 'Active' | 'Completed';
  statusLabel: string;
  groupConditionLabel: string;
  membersSummary: string;
  chairmanName?: string;
  secretaryName?: string;
  memberNames?: string[];
  decisionNumber?: string | null;
  decisionFileUrl?: string | null;
  feedback?: string | null;
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