import type {
  CouncilCandidate,
  CouncilMemberResponse,
  CouncilRole,
} from '../services/councilMembersApi';
import type { CouncilDraftItemDto, CouncilOverviewMemberDto } from '../types/councilApproval.types';

export const COUNCIL_ROLES: readonly { value: CouncilRole; label: string }[] = [
  { value: 'Chairman', label: 'Chủ tịch' },
  { value: 'Secretary', label: 'Thư ký' },
  { value: 'Member', label: 'Ủy viên' },
];

const ROLE_ORDER: Record<CouncilRole, number> = { Chairman: 0, Secretary: 1, Member: 2 };

export function roleLabel(role: string): string {
  return COUNCIL_ROLES.find((item) => item.value === role)?.label ?? role;
}

export interface RosterMember {
  userId: string;
  fullName: string;
  role: CouncilRole;
  isReviewer: boolean;
}

export type RoleAvailability = { allowed: true } | { allowed: false; reason: string };

function candidateReason(candidate: CouncilCandidate): string {
  if (candidate.isAdvisorConflict) {
    return 'Giảng viên này đang hướng dẫn một nhóm bảo vệ trong hội đồng, nên không thể tham gia.';
  }
  if (candidate.isScheduleConflict) {
    return 'Giảng viên này đã có lịch ở hội đồng khác cùng ca. Hãy chọn người khác hoặc đổi lịch bảo vệ.';
  }
  if (candidate.isReviewerForCouncilGroups) {
    return 'Giảng viên này phản biện cho một nhóm trong hội đồng, nên chỉ có thể làm Ủy viên.';
  }
  return candidate.conflictReason || 'Chưa thể phân công giảng viên này. Hãy kiểm tra điều kiện của hội đồng.';
}

function sortRoster(roster: RosterMember[]): RosterMember[] {
  return [...roster].sort(
    (a, b) =>
      ROLE_ORDER[a.role] - ROLE_ORDER[b.role] || a.fullName.localeCompare(b.fullName, 'vi'),
  );
}

/** Roster shown on screen. Derived from the council overview, so no extra request is needed. */
export function rosterFromCouncil(council: CouncilDraftItemDto): RosterMember[] {
  const roster: RosterMember[] = [];
  const push = (member: CouncilOverviewMemberDto | null | undefined, role: CouncilRole) => {
    if (member && !roster.some((item) => item.userId === member.userId)) {
      roster.push({
        userId: member.userId,
        fullName: member.fullName,
        role,
        isReviewer: member.isReviewer,
      });
    }
  };
  push(council.chairman, 'Chairman');
  push(council.secretary, 'Secretary');
  for (const member of council.members) push(member, 'Member');
  return sortRoster(roster);
}

export function rosterFromResponse(
  response: readonly CouncilMemberResponse[],
  previous: readonly RosterMember[] = [],
): RosterMember[] {
  return sortRoster(
    response.map((item) => ({
      userId: item.userId,
      fullName: item.fullName || previous.find((m) => m.userId === item.userId)?.fullName || '',
      role: item.role,
      isReviewer: previous.find((m) => m.userId === item.userId)?.isReviewer ?? false,
    })),
  );
}

/** Roster after an add response (the API returns only the members that were added). */
export function mergeAddedMembers(
  current: readonly RosterMember[],
  added: readonly CouncilMemberResponse[],
): RosterMember[] {
  const addedIds = new Set(added.map((item) => item.userId));
  return sortRoster([
    ...current.filter((item) => !addedIds.has(item.userId)),
    ...rosterFromResponse(added, current),
  ]);
}

export function summarizeRoster(roster: readonly RosterMember[]): string {
  const parts: string[] = [];
  if (roster.some((m) => m.role === 'Chairman')) parts.push('Chủ tịch');
  if (roster.some((m) => m.role === 'Secretary')) parts.push('Thư ký');
  if (roster.some((m) => m.role === 'Member')) parts.push('Ủy viên');
  return parts.length > 0 ? parts.join(' · ') : 'Chưa có thành viên';
}

function toOverviewMember(
  member: RosterMember,
  advisorConflictIds: ReadonlySet<string>,
): CouncilOverviewMemberDto {
  return {
    userId: member.userId,
    fullName: member.fullName,
    role: member.role,
    isReviewer: member.isReviewer,
    isAdvisorConflict: advisorConflictIds.has(member.userId),
    note: null,
  };
}

/** Mirrors the server: any roster change withdraws a submitted dossier (ResetSubmission). */
export function applyRosterToCouncil(
  council: CouncilDraftItemDto,
  roster: readonly RosterMember[],
): CouncilDraftItemDto {
  const chairman = roster.find((m) => m.role === 'Chairman');
  const secretary = roster.find((m) => m.role === 'Secretary');
  const members = roster.filter((m) => m.role === 'Member');
  const advisorConflictIds = new Set(
    [council.chairman, council.secretary, ...council.members]
      .filter((m): m is CouncilOverviewMemberDto => Boolean(m?.isAdvisorConflict))
      .map((m) => m.userId),
  );
  const toOverview = (member: RosterMember) => toOverviewMember(member, advisorConflictIds);
  return {
    ...council,
    // Cờ xung đột tính lại từ danh sách mới để nút Gửi PĐT phản ánh đúng ngay; đồng bộ nền sẽ xác nhận với server.
    hasAdvisorConflict: roster.some((m) => advisorConflictIds.has(m.userId)),
    hasReviewerRoleConflict: roster.some((m) => m.isReviewer && m.role !== 'Member'),
    conflictingMemberNames: council.conflictingMemberNames.filter((name) =>
      roster.some((m) => name.includes(m.fullName)),
    ),
    chairman: chairman ? toOverview(chairman) : null,
    secretary: secretary ? toOverview(secretary) : null,
    members: members.map(toOverview),
    chairmanName: chairman?.fullName,
    secretaryName: secretary?.fullName,
    memberNames: members.map((m) => (m.isReviewer ? `${m.fullName} (GVPB kiêm UV)` : m.fullName)),
    membersSummary: summarizeRoster(roster),
    isSubmittedToAcademicAffairs: false,
    submittedAt: null,
    submissionNotes: null,
  };
}

export function applyRosterToCandidates(
  candidates: readonly CouncilCandidate[],
  roster: readonly RosterMember[],
): CouncilCandidate[] {
  const roleByUser = new Map(roster.map((m) => [m.userId, m.role]));
  return candidates.map((candidate) => {
    const role = roleByUser.get(candidate.userId) ?? null;
    return { ...candidate, isAlreadyInCouncil: role !== null, currentRoleInCouncil: role };
  });
}

export function markCouncilSubmitted(
  council: CouncilDraftItemDto,
  notes: string,
  submittedAt: string,
): CouncilDraftItemDto {
  return {
    ...council,
    isSubmittedToAcademicAffairs: true,
    submittedAt,
    submissionNotes: notes.trim() || null,
  };
}

function leadershipHolder(roster: readonly RosterMember[], role: CouncilRole, exceptUserId?: string) {
  if (role === 'Member') return undefined;
  return roster.find((m) => m.role === role && m.userId !== exceptUserId);
}

/** Can `role` be given to this person now? The server stays the final authority. */
export function getRoleAvailability(
  roster: readonly RosterMember[],
  candidate: CouncilCandidate | undefined,
  role: CouncilRole,
  userId: string,
): RoleAvailability {
  if (candidate) {
    if (!candidate.isEligible) {
      return { allowed: false, reason: candidateReason(candidate) };
    }
    if (!candidate.allowedRoles.includes(role)) {
      return {
        allowed: false,
        reason: candidate.isReviewerForCouncilGroups
          ? candidateReason(candidate)
          : candidate.conflictReason || `Giảng viên này không thể làm ${roleLabel(role)} trong hội đồng này.`,
      };
    }
  }
  const holder = leadershipHolder(roster, role, userId);
  if (holder) {
    return {
      allowed: false,
      reason: `Đã có ${roleLabel(role)}: ${holder.fullName}. Hãy đổi vai trò của người đó trước.`,
    };
  }
  return { allowed: true };
}

export type CandidateStatus =
  | { kind: 'selectable' }
  | { kind: 'in-council'; role: string }
  | { kind: 'blocked'; reason: string }
  | { kind: 'restricted'; reason: string };

export function getCandidateStatus(candidate: CouncilCandidate): CandidateStatus {
  if (candidate.isAlreadyInCouncil) {
    return { kind: 'in-council', role: candidate.currentRoleInCouncil || '' };
  }
  if (!candidate.isEligible || candidate.allowedRoles.length === 0) {
    return { kind: 'blocked', reason: candidateReason(candidate) };
  }
  if (candidate.allowedRoles.length < COUNCIL_ROLES.length) {
    return {
      kind: 'restricted',
      reason: candidateReason(candidate),
    };
  }
  return { kind: 'selectable' };
}

export function filterCandidates(
  candidates: readonly CouncilCandidate[],
  keyword: string,
): CouncilCandidate[] {
  const needle = keyword.trim().toLocaleLowerCase('vi');
  return candidates
    .filter((item) => {
      if (!needle) return true;
      return [item.fullName, item.userCode, item.email].some((value) =>
        value?.toLocaleLowerCase('vi').includes(needle),
      );
    })
    .sort((a, b) => {
      const rank = (c: CouncilCandidate) => {
        const kind = getCandidateStatus(c).kind;
        return kind === 'selectable' ? 0 : kind === 'restricted' ? 1 : kind === 'blocked' ? 2 : 3;
      };
      return rank(a) - rank(b) || a.fullName.localeCompare(b.fullName, 'vi');
    });
}

/** Candidates shown in the add-member dialog. Advisors of assigned groups are never offered. */
export function pickerCandidates(
  candidates: readonly CouncilCandidate[],
  keyword: string,
  role: CouncilRole,
  roster: readonly RosterMember[],
): CouncilCandidate[] {
  const hasSearch = Boolean(keyword.trim());
  if (!hasSearch && role !== 'Member') return [];

  const assignedIds = new Set(roster.map((member) => member.userId));
  const isAssigned = (candidate: CouncilCandidate) =>
    candidate.isAlreadyInCouncil || assignedIds.has(candidate.userId);
  const canAdd = (candidate: CouncilCandidate) =>
    !isAssigned(candidate) && getRoleAvailability(roster, candidate, role, candidate.userId).allowed;
  const isUnassignedReviewer = (candidate: CouncilCandidate) =>
    role === 'Member' && Boolean(candidate.isReviewerForCouncilGroups) && !isAssigned(candidate);

  return filterCandidates(candidates, keyword)
    .filter((candidate) =>
      !candidate.isAdvisorConflict && (hasSearch || isUnassignedReviewer(candidate)),
    )
    .sort((a, b) =>
      Number(isUnassignedReviewer(b)) - Number(isUnassignedReviewer(a)) ||
      Number(canAdd(b)) - Number(canAdd(a)),
    );
}

export interface SubmitReadiness {
  /** Mọi việc còn phải làm trước khi gửi (vai trò, nhóm và xung đột). */
  missing: string[];
  /** Các xung đột về lịch, giảng viên và vai trò. */
  conflicts: string[];
  /** Điều kiện của nhóm được trả về dưới dạng cờ tổng hợp, chưa có tên nhóm cụ thể. */
  groupEligibilityIssue: string | null;
  hasChairman: boolean;
  hasSecretary: boolean;
  canSubmit: boolean;
}

/** Faculty-side checklist before sending to PĐT. Mirrors the backend submit rules. */
export function evaluateSubmitReadiness(
  council: CouncilDraftItemDto,
  roster: readonly RosterMember[],
): SubmitReadiness {
  const hasChairman = roster.some((m) => m.role === 'Chairman');
  const hasSecretary = roster.some((m) => m.role === 'Secretary');
  const conflicts: string[] = [];
  if (council.hasRoomConflict) {
    conflicts.push(`Phòng ${council.room} đã có hội đồng khác trong cùng ngày và ca. Hãy đổi phòng hoặc lịch bảo vệ.`);
  }
  if (council.conflictingMemberNames.length > 0) {
    conflicts.push(`${council.conflictingMemberNames.join(', ')} đã có lịch ở hội đồng khác cùng ca. Hãy đổi lịch hoặc phân công giảng viên khác.`);
  }
  if (council.hasAdvisorConflict) {
    conflicts.push('Một giảng viên trong hội đồng đang hướng dẫn nhóm bảo vệ tại đây. Hãy phân công người khác.');
  }
  const groupEligibilityIssue = council.hasIneligibleGroups
    ? 'Ít nhất một nhóm đã gán chưa được phép bảo vệ. Hãy kiểm tra kết quả phản biện và điều kiện bảo vệ của các nhóm.'
    : null;
  if (council.hasReviewerRoleConflict) {
    conflicts.push('Một giảng viên phản biện đang làm Chủ tịch hoặc Thư ký. Hãy đổi người đó sang vai trò Ủy viên.');
  }
  const missing: string[] = [];
  if (!hasChairman) missing.push('Chưa có Chủ tịch.');
  if (!hasSecretary) missing.push('Chưa có Thư ký.');
  if (!roster.some((member) => member.role === 'Member')) missing.push('Chưa có Ủy viên. Thêm GVPB kiêm nhiệm hoặc chọn giảng viên khác.');
  if (council.groupCodes.length === 0) {
    missing.push('Chưa có nhóm bảo vệ nào được gán vào hội đồng.');
  }
  missing.push(...conflicts);
  if (groupEligibilityIssue) missing.push(groupEligibilityIssue);
  return {
    missing,
    conflicts,
    groupEligibilityIssue,
    hasChairman,
    hasSecretary,
    canSubmit:
      council.status === 'Draft' && !council.isSubmittedToAcademicAffairs && missing.length === 0,
  };
}

export type CouncilPhase = 'preparing' | 'submitted' | 'approved';

export function councilPhase(council: CouncilDraftItemDto): CouncilPhase {
  if (council.status !== 'Draft') return 'approved';
  return council.isSubmittedToAcademicAffairs ? 'submitted' : 'preparing';
}

export const PHASE_LABEL: Record<CouncilPhase, string> = {
  preparing: 'Đang chuẩn bị',
  submitted: 'Đã gửi PĐT',
  approved: 'Đã phê duyệt',
};

export function phaseLabelFor(council: CouncilDraftItemDto): string {
  return council.status === 'Completed' ? 'Đã hoàn thành' : PHASE_LABEL[councilPhase(council)];
}
