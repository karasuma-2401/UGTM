import type { StatusTone } from '@/components/ui/status-badge';
import type { AssignedCouncilReport } from '../types/similarity.types';

export type SimilarityFilterKey =
  | 'ALL'
  | 'NeedsAttention'
  | 'LowSimilarity'
  | 'Pending'
  | 'IssueOrUnscanned';

export type SimilaritySortField = 'groupCode' | 'topicTitle' | 'version' | 'coverage' | 'status';
export type SimilaritySortDirection = 'asc' | 'desc';

export interface SimilaritySortState {
  field: SimilaritySortField;
  direction: SimilaritySortDirection;
}

export interface SimilarityRowView extends AssignedCouncilReport {
  groupCode: string;
  normalizedCoverage: number | null;
  citedCount: number;
  uncitedCount: number;
  suspectedCount: number;
  attentionCount: number;
  totalFindingsCount: number;
  scanLabel: string;
  scanTone: StatusTone;
  riskTier: 'high' | 'warning' | 'low' | 'pending' | 'issue';
  studentsText: string;
}

export interface SimilarityStats {
  total: number;
  needsAttention: number;
  lowSimilarity: number;
  pending: number;
  issueOrUnscanned: number;
}

export const PENDING_SCAN_STATUSES = new Set([
  'PendingSubmission',
  'Queued',
  'Processing',
  'WaitingForCompute',
]);

export const ISSUE_SCAN_STATUSES = new Set([
  'Failed',
  'ComputeUnavailable',
  'UnsupportedFormat',
]);

export function normalizeSimilarityPercent(value: number): number {
  const percent = value <= 1 ? value * 100 : value;
  return Math.max(0, Math.min(100, percent));
}

export function formatSimilarityPercent(value: number): string {
  return `${normalizeSimilarityPercent(value).toLocaleString('vi-VN', {
    maximumFractionDigits: 1,
  })}%`;
}

export function formatRerankerScore(value: number): string {
  return value.toLocaleString('vi-VN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

export function resolveSimilarityGroupCode(groupId: string): string {
  const clean = groupId.trim();
  if (!clean) return 'NH-0000';
  const lastSegment = clean.split('-').pop() ?? clean;
  const paddedSuffixMatch = /0{3,}([1-9][0-9]{0,3})$/.exec(lastSegment);
  if (paddedSuffixMatch) {
    const digits = paddedSuffixMatch[1].padStart(4, '0');
    return `NH-${digits}`;
  }
  const compact = clean.replace(/[^a-zA-Z0-9]/g, '');
  if (!compact) return 'NH-0000';
  return `NH-${compact.slice(0, 4).toUpperCase()}`;
}

export function getSimilarityScanDisplay(status?: string | null): {
  label: string;
  tone: StatusTone;
} {
  switch (status) {
    case 'Completed':
      return { label: 'Đã hoàn tất', tone: 'success' };
    case 'PendingSubmission':
      return { label: 'Đã tiếp nhận', tone: 'info' };
    case 'Queued':
      return { label: 'Đang chờ xử lý', tone: 'info' };
    case 'Processing':
      return { label: 'Đang phân tích AI', tone: 'info' };
    case 'WaitingForCompute':
      return { label: 'Đang chờ GPU', tone: 'warning' };
    case 'ComputeUnavailable':
      return { label: 'GPU tạm gián đoạn', tone: 'warning' };
    case 'UnsupportedFormat':
      return { label: 'Định dạng chưa hỗ trợ', tone: 'error' };
    case 'Failed':
      return { label: 'Xử lý thất bại', tone: 'error' };
    default:
      return { label: 'Chưa có lượt quét', tone: 'neutral' };
  }
}

export function buildSimilarityRowView(item: AssignedCouncilReport): SimilarityRowView {
  const groupCode = resolveSimilarityGroupCode(item.groupId);
  const scanDisplay = getSimilarityScanDisplay(item.scanStatus);
  const citedCount = item.citedFindings ?? 0;
  const uncitedCount = item.uncitedFindings ?? 0;
  const suspectedCount = item.suspectedFindings ?? 0;
  const attentionCount = uncitedCount + suspectedCount;
  const totalFindingsCount = item.totalFindings ?? citedCount + attentionCount;

  const normalizedCoverage =
    item.scanStatus === 'Completed' && item.similarityCoverage != null
      ? normalizeSimilarityPercent(item.similarityCoverage)
      : null;

  let riskTier: SimilarityRowView['riskTier'] = 'pending';
  if (!item.scanStatus || ISSUE_SCAN_STATUSES.has(item.scanStatus)) {
    riskTier = 'issue';
  } else if (PENDING_SCAN_STATUSES.has(item.scanStatus)) {
    riskTier = 'pending';
  } else if (
    (normalizedCoverage != null && normalizedCoverage >= 30) ||
    suspectedCount > 0
  ) {
    riskTier = 'high';
  } else if (
    (normalizedCoverage != null && normalizedCoverage >= 15) ||
    uncitedCount > 0
  ) {
    riskTier = 'warning';
  } else {
    riskTier = 'low';
  }

  const students = item.students ?? [];
  const studentsText =
    students.length > 0
      ? students
          .map((s) => (s.userCode ? `${s.fullName} (${s.userCode})` : s.fullName))
          .join(' · ')
      : '';

  return {
    ...item,
    groupCode,
    normalizedCoverage,
    citedCount,
    uncitedCount,
    suspectedCount,
    attentionCount,
    totalFindingsCount,
    scanLabel: scanDisplay.label,
    scanTone: scanDisplay.tone,
    riskTier,
    studentsText,
  };
}

export function matchesSimilarityFilter(
  row: SimilarityRowView,
  filterKey: SimilarityFilterKey,
): boolean {
  switch (filterKey) {
    case 'NeedsAttention':
      return row.scanStatus === 'Completed' && (row.riskTier === 'high' || row.riskTier === 'warning');
    case 'LowSimilarity':
      return row.scanStatus === 'Completed' && row.riskTier === 'low';
    case 'Pending':
      return Boolean(row.scanStatus && PENDING_SCAN_STATUSES.has(row.scanStatus));
    case 'IssueOrUnscanned':
      return !row.scanStatus || ISSUE_SCAN_STATUSES.has(row.scanStatus);
    case 'ALL':
    default:
      return true;
  }
}

export function computeSimilarityStats(rows: readonly SimilarityRowView[]): SimilarityStats {
  let needsAttention = 0;
  let lowSimilarity = 0;
  let pending = 0;
  let issueOrUnscanned = 0;

  for (const row of rows) {
    if (matchesSimilarityFilter(row, 'NeedsAttention')) needsAttention++;
    else if (matchesSimilarityFilter(row, 'LowSimilarity')) lowSimilarity++;
    else if (matchesSimilarityFilter(row, 'Pending')) pending++;
    else if (matchesSimilarityFilter(row, 'IssueOrUnscanned')) issueOrUnscanned++;
  }

  return {
    total: rows.length,
    needsAttention,
    lowSimilarity,
    pending,
    issueOrUnscanned,
  };
}

export function filterSimilarityRows(
  rows: readonly SimilarityRowView[],
  options: {
    batchId: string;
    filterKey: SimilarityFilterKey;
    keyword: string;
  },
): SimilarityRowView[] {
  const trimmed = options.keyword.trim().toLowerCase();
  return rows.filter((row) => {
    if (options.batchId !== 'ALL' && row.batchId !== options.batchId) {
      return false;
    }
    if (!matchesSimilarityFilter(row, options.filterKey)) {
      return false;
    }
    if (!trimmed) return true;
    const haystack = [
      row.groupCode,
      row.groupId,
      row.topicTitle,
      row.topicTitleEn ?? '',
      row.studentsText,
      row.scanLabel,
    ]
      .join(' ')
      .toLowerCase();
    return haystack.includes(trimmed);
  });
}

function statusSortRank(row: SimilarityRowView): number {
  if (row.riskTier === 'high') return 0;
  if (row.riskTier === 'warning') return 1;
  if (row.riskTier === 'low') return 2;
  if (row.riskTier === 'pending') return 3;
  return 4;
}

export function sortSimilarityRows(
  rows: readonly SimilarityRowView[],
  sort: SimilaritySortState,
): SimilarityRowView[] {
  const dir = sort.direction === 'asc' ? 1 : -1;
  return [...rows].sort((a, b) => {
    switch (sort.field) {
      case 'groupCode':
        return a.groupCode.localeCompare(b.groupCode, 'vi') * dir;
      case 'topicTitle':
        return a.topicTitle.localeCompare(b.topicTitle, 'vi') * dir;
      case 'version':
        return (a.versionNo - b.versionNo) * dir;
      case 'status':
        return (statusSortRank(a) - statusSortRank(b)) * dir;
      case 'coverage':
      default: {
        const aCov = a.normalizedCoverage ?? -1;
        const bCov = b.normalizedCoverage ?? -1;
        if (aCov !== bCov) {
          return (aCov - bCov) * dir;
        }
        return (a.attentionCount - b.attentionCount) * dir;
      }
    }
  });
}
