import type {
  RenameRequest,
  RenameRequestStatus,
} from './types/renameRequest.types';

export type RenameReviewLevel = 'advisor' | 'faculty' | 'academic-affairs';
export type RenameQueueScope = 'pending' | 'completed' | 'rejected' | 'all';
export type RenameSortField = 'createdAt' | 'newTitleVi' | 'status';

export interface RenameLevelConfig {
  level: RenameReviewLevel;
  number: 1 | 2 | 3;
  tabLabel: string;
  actorLabel: string;
  description: string;
  status: RenameRequestStatus;
  priorHint: string;
}

export const RENAME_LEVEL_CONFIGS: Record<RenameReviewLevel, RenameLevelConfig> = {
  advisor: {
    level: 'advisor',
    number: 1,
    tabLabel: 'Cấp 1 · GVHD',
    actorLabel: 'Giảng viên hướng dẫn',
    description:
      'Xem xét, đối chiếu tên đề tài cũ — mới và phê duyệt ở cấp Giảng viên hướng dẫn.',
    status: 'Created',
    priorHint: 'Đơn mới do sinh viên gửi.',
  },
  faculty: {
    level: 'faculty',
    number: 2,
    tabLabel: 'Cấp 2 · Khoa',
    actorLabel: 'Khoa',
    description:
      'Thẩm định các yêu cầu đã được Giảng viên hướng dẫn xác nhận trước khi chuyển Phòng đào tạo.',
    status: 'AdvisorConfirmed',
    priorHint: 'Đã được GVHD xác nhận ở bước 1.',
  },
  'academic-affairs': {
    level: 'academic-affairs',
    number: 3,
    tabLabel: 'Cấp 3 · Phòng đào tạo',
    actorLabel: 'Phòng đào tạo',
    description:
      'Phê duyệt cuối để cập nhật tên đề tài chính thức và mở khóa nộp lại đề cương cho nhóm.',
    status: 'FacultyApproved',
    priorHint: 'Đã qua GVHD và Khoa thẩm định.',
  },
};

export const RENAME_SCOPE_OPTIONS: Array<{ value: RenameQueueScope; label: string }> = [
  { value: 'pending', label: 'Chờ duyệt cấp hiện tại' },
  { value: 'completed', label: 'Đã hoàn tất' },
  { value: 'rejected', label: 'Đã từ chối' },
  { value: 'all', label: 'Tất cả đơn' },
];

export const REJECTED_RENAME_STATUSES: ReadonlySet<RenameRequestStatus> = new Set([
  'RejectedByAdvisor',
  'RejectedByFaculty',
  'RejectedByAA',
]);

export function isRejectedRenameStatus(status: RenameRequestStatus): boolean {
  return REJECTED_RENAME_STATUSES.has(status);
}

export function getPendingLevelForStatus(status: RenameRequestStatus): RenameReviewLevel | null {
  if (status === 'Created') return 'advisor';
  if (status === 'AdvisorConfirmed') return 'faculty';
  if (status === 'FacultyApproved') return 'academic-affairs';
  return null;
}

export interface RenameQueueStats {
  pendingCount: number;
  completedCount: number;
  rejectedCount: number;
  totalCount: number;
  levelCounts: Record<RenameReviewLevel, number>;
}

export function computeRenameQueueStats(
  items: readonly RenameRequest[],
  activeStatus: RenameRequestStatus,
): RenameQueueStats {
  let pendingCount = 0;
  let completedCount = 0;
  let rejectedCount = 0;
  const levelCounts: Record<RenameReviewLevel, number> = {
    advisor: 0,
    faculty: 0,
    'academic-affairs': 0,
  };

  for (const item of items) {
    if (item.status === activeStatus) pendingCount += 1;
    if (item.status === 'Completed') completedCount += 1;
    if (isRejectedRenameStatus(item.status)) rejectedCount += 1;

    if (item.status === 'Created') levelCounts.advisor += 1;
    else if (item.status === 'AdvisorConfirmed') levelCounts.faculty += 1;
    else if (item.status === 'FacultyApproved') levelCounts['academic-affairs'] += 1;
  }

  return {
    pendingCount,
    completedCount,
    rejectedCount,
    totalCount: items.length,
    levelCounts,
  };
}

export interface RenameFilterCriteria {
  activeStatus: RenameRequestStatus;
  scope: RenameQueueScope;
  keyword: string;
  batchId?: string;
  sortBy: RenameSortField;
  sortOrder: 'asc' | 'desc';
}

const STATUS_SORT_RANK: Record<RenameRequestStatus, number> = {
  Created: 1,
  AdvisorConfirmed: 2,
  FacultyApproved: 3,
  Completed: 4,
  RejectedByAdvisor: 5,
  RejectedByFaculty: 6,
  RejectedByAA: 7,
  Closed: 8,
};

export function filterAndSortRenameRequests(
  items: readonly RenameRequest[],
  criteria: RenameFilterCriteria,
): RenameRequest[] {
  const normalizedKeyword = criteria.keyword.trim().toLowerCase();

  const filtered = items.filter((item) => {
    if (criteria.scope === 'pending' && item.status !== criteria.activeStatus) return false;
    if (criteria.scope === 'completed' && item.status !== 'Completed') return false;
    if (criteria.scope === 'rejected' && !isRejectedRenameStatus(item.status)) return false;

    if (criteria.batchId && item.batchId !== criteria.batchId) return false;

    if (!normalizedKeyword) return true;

    const haystacks = [
      item.newTitleVi,
      item.oldTitleVi,
      item.newTitleEn ?? '',
      item.oldTitleEn ?? '',
      item.reason,
      item.advisorName ?? '',
      item.batchName ?? '',
      ...(item.members?.flatMap((m) => [m.fullName ?? '', m.userCode ?? '']) ?? []),
    ];

    return haystacks.some((value) => value.toLowerCase().includes(normalizedKeyword));
  });

  const factor = criteria.sortOrder === 'asc' ? 1 : -1;

  return [...filtered].sort((a, b) => {
    if (criteria.sortBy === 'newTitleVi') {
      return a.newTitleVi.localeCompare(b.newTitleVi, 'vi') * factor;
    }
    if (criteria.sortBy === 'status') {
      const rankDiff = (STATUS_SORT_RANK[a.status] - STATUS_SORT_RANK[b.status]) * factor;
      if (rankDiff !== 0) return rankDiff;
    }
    const timeDiff = Date.parse(a.createdAt) - Date.parse(b.createdAt);
    return (Number.isNaN(timeDiff) ? 0 : timeDiff) * factor;
  });
}

export type ApprovalStepState = 'approved' | 'current' | 'rejected' | 'pending';

export interface RenameApprovalStep {
  level: RenameReviewLevel;
  number: 1 | 2 | 3;
  actorLabel: string;
  state: ApprovalStepState;
  stateLabel: string;
  timestamp?: string | null;
}

export function getRenameApprovalSteps(item: RenameRequest): RenameApprovalStep[] {
  const advisorState: ApprovalStepState =
    item.status === 'RejectedByAdvisor'
      ? 'rejected'
      : item.status === 'Created'
        ? 'current'
        : item.advisorConfirmedAt ||
            ['AdvisorConfirmed', 'FacultyApproved', 'Completed', 'RejectedByFaculty', 'RejectedByAA'].includes(
              item.status,
            )
          ? 'approved'
          : 'pending';

  const facultyState: ApprovalStepState =
    item.status === 'RejectedByFaculty'
      ? 'rejected'
      : item.status === 'AdvisorConfirmed'
        ? 'current'
        : item.facultyApprovedAt ||
            ['FacultyApproved', 'Completed', 'RejectedByAA'].includes(item.status)
          ? 'approved'
          : 'pending';

  const aaState: ApprovalStepState =
    item.status === 'RejectedByAA'
      ? 'rejected'
      : item.status === 'FacultyApproved'
        ? 'current'
        : item.status === 'Completed' || item.aaApprovedAt
          ? 'approved'
          : 'pending';

  const labelFor = (state: ApprovalStepState): string => {
    if (state === 'approved') return 'Đã phê duyệt';
    if (state === 'current') return 'Đang chờ xử lý';
    if (state === 'rejected') return 'Đã từ chối';
    return 'Chưa đến bước';
  };

  return [
    {
      level: 'advisor',
      number: 1,
      actorLabel: 'Giảng viên hướng dẫn',
      state: advisorState,
      stateLabel: labelFor(advisorState),
      timestamp:
        advisorState === 'rejected' ? item.rejectedAt : item.advisorConfirmedAt,
    },
    {
      level: 'faculty',
      number: 2,
      actorLabel: 'Khoa thẩm định',
      state: facultyState,
      stateLabel: labelFor(facultyState),
      timestamp:
        facultyState === 'rejected' ? item.rejectedAt : item.facultyApprovedAt,
    },
    {
      level: 'academic-affairs',
      number: 3,
      actorLabel: 'Phòng đào tạo',
      state: aaState,
      stateLabel: labelFor(aaState),
      timestamp: aaState === 'rejected' ? item.rejectedAt : item.aaApprovedAt,
    },
  ];
}

export function formatRenameDateTime(value?: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatRenameDate(value?: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

export type TitleDiffKind = 'unchanged' | 'removed' | 'added';

export interface TitleDiffSegment {
  text: string;
  kind: TitleDiffKind;
}

export interface TitleWordDiffResult {
  hasDiff: boolean;
  oldSegments: TitleDiffSegment[];
  newSegments: TitleDiffSegment[];
}

function mergeDiffTokens(tokens: TitleDiffSegment[]): TitleDiffSegment[] {
  const merged: TitleDiffSegment[] = [];
  for (const token of tokens) {
    const prev = merged[merged.length - 1];
    if (prev && prev.kind === token.kind) {
      prev.text = `${prev.text} ${token.text}`;
    } else {
      merged.push({ ...token });
    }
  }
  return merged;
}

/**
 * Computes word-level diff segments between old and new topic titles so reviewers
 * can spot replaced or added phrases immediately without scanning full sentences.
 */
export function computeTitleWordDiff(
  oldText?: string | null,
  newText?: string | null,
): TitleWordDiffResult {
  const oldTrimmed = (oldText ?? '').trim();
  const newTrimmed = (newText ?? '').trim();

  if (!oldTrimmed || !newTrimmed || oldTrimmed === newTrimmed) {
    return {
      hasDiff: false,
      oldSegments: oldTrimmed ? [{ text: oldTrimmed, kind: 'unchanged' }] : [],
      newSegments: newTrimmed ? [{ text: newTrimmed, kind: 'unchanged' }] : [],
    };
  }

  const oldWords = oldTrimmed.split(/\s+/);
  const newWords = newTrimmed.split(/\s+/);
  const m = oldWords.length;
  const n = newWords.length;

  // Fallback to plain rendering for unusually long inputs
  if (m > 80 || n > 80) {
    return {
      hasDiff: false,
      oldSegments: [{ text: oldTrimmed, kind: 'unchanged' }],
      newSegments: [{ text: newTrimmed, kind: 'unchanged' }],
    };
  }

  const dp: number[][] = Array.from({ length: m + 1 }, () => Array(n + 1).fill(0));
  for (let i = m - 1; i >= 0; i -= 1) {
    for (let j = n - 1; j >= 0; j -= 1) {
      dp[i][j] =
        oldWords[i] === newWords[j]
          ? dp[i + 1][j + 1] + 1
          : Math.max(dp[i + 1][j], dp[i][j + 1]);
    }
  }

  const commonCount = dp[0][0];
  // If titles share under 25% of words, treat as an entirely new topic rather than noisy inline diff
  if (commonCount / Math.max(m, n) < 0.25) {
    return {
      hasDiff: false,
      oldSegments: [{ text: oldTrimmed, kind: 'unchanged' }],
      newSegments: [{ text: newTrimmed, kind: 'unchanged' }],
    };
  }

  const oldTokens: TitleDiffSegment[] = [];
  const newTokens: TitleDiffSegment[] = [];
  let i = 0;
  let j = 0;

  while (i < m && j < n) {
    if (oldWords[i] === newWords[j]) {
      oldTokens.push({ text: oldWords[i], kind: 'unchanged' });
      newTokens.push({ text: newWords[j], kind: 'unchanged' });
      i += 1;
      j += 1;
    } else if (dp[i + 1][j] >= dp[i][j + 1]) {
      oldTokens.push({ text: oldWords[i], kind: 'removed' });
      i += 1;
    } else {
      newTokens.push({ text: newWords[j], kind: 'added' });
      j += 1;
    }
  }

  while (i < m) {
    oldTokens.push({ text: oldWords[i], kind: 'removed' });
    i += 1;
  }
  while (j < n) {
    newTokens.push({ text: newWords[j], kind: 'added' });
    j += 1;
  }

  return {
    hasDiff: true,
    oldSegments: mergeDiffTokens(oldTokens),
    newSegments: mergeDiffTokens(newTokens),
  };
}
