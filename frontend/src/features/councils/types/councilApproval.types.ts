export type CouncilApprovalStatus = 'all' | 'draft' | 'active' | 'completed';

export interface CouncilDraftItemDto {
  id: string;
  batchId: string;
  councilCode: string;
  facultyName: string;
  groupCode: string;
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
