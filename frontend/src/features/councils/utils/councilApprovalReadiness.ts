import type { CouncilDraftItemDto } from '../types/councilApproval.types';

export type ReadinessCheckState = 'passed' | 'warning' | 'blocked';

export interface CouncilReadinessCheck {
  key: 'submitted' | 'structure' | 'groups' | 'eligibility' | 'conflicts';
  label: string;
  detail: string;
  state: ReadinessCheckState;
}

export interface CouncilReadinessResult {
  category: 'approved' | 'ready' | 'needs_action';
  isReady: boolean;
  summaryLabel: string;
  blockingReasons: string[];
  checks: CouncilReadinessCheck[];
}

export function isCouncilVisibleForApproval(council: CouncilDraftItemDto): boolean {
  return council.status !== 'Draft' || council.isSubmittedToAcademicAffairs;
}

export function formatViDate(isoDate?: string | null): string {
  if (!isoDate) return '—';
  const normalized = isoDate.slice(0, 10);
  const parts = normalized.split('-');
  if (parts.length !== 3) return isoDate;
  const [year, month, day] = parts;
  return `${day}/${month}/${year}`;
}

export function formatViDateTime(isoDateTime?: string | null): string {
  if (!isoDateTime) return '—';
  const parsed = new Date(isoDateTime);
  if (Number.isNaN(parsed.getTime())) return isoDateTime;
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(parsed);
}

export function evaluateCouncilReadiness(council: CouncilDraftItemDto): CouncilReadinessResult {
  const isApproved = council.status === 'Active' || council.status === 'Completed';

  const hasChairman = Boolean(council.chairman);
  const hasSecretary = Boolean(council.secretary);
  const structurePassed = hasChairman && hasSecretary;

  const missingRoles: string[] = [];
  if (!hasChairman) missingRoles.push('Chủ tịch');
  if (!hasSecretary) missingRoles.push('Thư ký');

  const hasGroups = council.groupCodes.length > 0;
  const eligibilityPassed = hasGroups && !council.hasIneligibleGroups;

  const conflictReasons: string[] = [];
  if (council.hasRoomConflict) {
    conflictReasons.push(`Trùng phòng ${council.room} (${council.timeSlot}, ${formatViDate(council.defenseDate)})`);
  }
  if (council.conflictingMemberNames.length > 0) {
    conflictReasons.push(`Trùng lịch thành viên: ${council.conflictingMemberNames.join(', ')}`);
  }
  if (council.hasAdvisorConflict) {
    conflictReasons.push('Có giảng viên hướng dẫn của nhóm ngồi trong hội đồng');
  }
  if (council.hasReviewerRoleConflict) {
    conflictReasons.push('Giảng viên phản biện đang giữ vai trò Chủ tịch hoặc Thư ký');
  }

  const submittedPassed = isApproved || council.isSubmittedToAcademicAffairs;
  const conflictsPassed = conflictReasons.length === 0;

  const checks: CouncilReadinessCheck[] = [
    {
      key: 'submitted',
      label: 'Hồ sơ từ Khoa',
      detail: submittedPassed
        ? council.submittedAt
          ? `Khoa đã nộp lúc ${formatViDateTime(council.submittedAt)}`
          : 'Khoa đã gửi hồ sơ lên Phòng Đào tạo'
        : 'Văn phòng Khoa chưa bấm nộp hồ sơ lên Phòng Đào tạo',
      state: submittedPassed ? 'passed' : 'warning',
    },
    {
      key: 'structure',
      label: 'Cơ cấu thành viên',
      detail: structurePassed
        ? `Đủ 1 Chủ tịch, 1 Thư ký${council.members.length > 0 ? ` và ${council.members.length} Ủy viên` : ''}`
        : `Thiếu ${missingRoles.join(' và ')}`,
      state: structurePassed ? 'passed' : 'blocked',
    },
    {
      key: 'groups',
      label: 'Nhóm bảo vệ',
      detail: hasGroups
        ? `Đã phân công ${council.groupCodes.length} nhóm (${council.groupCodes.join(', ')})`
        : 'Chưa có nhóm đề tài nào được gán vào hội đồng',
      state: hasGroups ? 'passed' : 'blocked',
    },
    {
      key: 'eligibility',
      label: 'Điều kiện bảo vệ',
      detail: !hasGroups
        ? 'Chờ phân công nhóm đề tài'
        : council.hasIneligibleGroups
          ? 'Có nhóm chưa đủ điều kiện hoặc bị phản biện từ chối bảo vệ'
          : 'Tất cả nhóm được gán đều đủ điều kiện bảo vệ',
      state: !hasGroups ? 'warning' : eligibilityPassed ? 'passed' : 'blocked',
    },
    {
      key: 'conflicts',
      label: 'Xung đột lịch & vai trò',
      detail: conflictsPassed ? 'Không phát hiện trùng phòng, trùng lịch hay xung đột GVHD' : conflictReasons.join(' · '),
      state: conflictsPassed ? 'passed' : 'blocked',
    },
  ];

  const blockingReasons = checks
    .filter((check) => check.state !== 'passed')
    .map((check) => check.detail);

  if (isApproved) {
    return {
      category: 'approved',
      isReady: true,
      summaryLabel: council.status === 'Completed' ? 'Đã hoàn thành' : 'Đã phê duyệt',
      blockingReasons: [],
      checks,
    };
  }

  const isReady =
    submittedPassed &&
    structurePassed &&
    hasGroups &&
    eligibilityPassed &&
    conflictsPassed;

  return {
    category: isReady ? 'ready' : 'needs_action',
    isReady,
    summaryLabel: isReady
      ? 'Sẵn sàng duyệt'
      : !council.isSubmittedToAcademicAffairs
        ? 'Chờ Khoa nộp'
        : 'Chưa đủ điều kiện',
    blockingReasons,
    checks,
  };
}
