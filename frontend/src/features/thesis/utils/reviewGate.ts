import type { StatusTone } from '@/components/ui/status-badge';
import type {
  ReviewDecision,
  ReviewGateItem,
  ReviewGateSortState,
  ReviewGateStatus,
} from '../types/review.types';

export const REVIEW_FEEDBACK_MAX_LENGTH = 2000;

const STATUS_DISPLAY: Record<ReviewGateStatus, { label: string; tone: StatusTone }> = {
  Draft: { label: 'Bản nháp', tone: 'neutral' },
  Submitted: { label: 'Chờ thẩm định', tone: 'info' },
  NeedRevision: { label: 'Yêu cầu chỉnh sửa', tone: 'warning' },
  Approved: { label: 'Đạt', tone: 'success' },
  Rejected: { label: 'Không đạt', tone: 'error' },
};

const STATUS_ORDER: Record<string, number> = {
  Submitted: 1,
  NeedRevision: 2,
  Approved: 3,
  Rejected: 4,
  Draft: 5,
};

export function getReviewGateStatusDisplay(status: string | null | undefined): {
  label: string;
  tone: StatusTone;
} {
  if (!status || !(status in STATUS_DISPLAY)) {
    return { label: status || 'Không xác định', tone: 'neutral' };
  }
  return STATUS_DISPLAY[status as ReviewGateStatus];
}

export function getReviewDecisionDisplay(decision: ReviewDecision): {
  label: string;
  tone: StatusTone;
  historyLabel: string;
} {
  switch (decision) {
    case 'Approved':
      return {
        label: 'Đạt',
        tone: 'success',
        historyLabel: 'Văn phòng Khoa phê duyệt Đạt',
      };
    case 'NeedRevision':
      return {
        label: 'Yêu cầu chỉnh sửa',
        tone: 'warning',
        historyLabel: 'Khoa yêu cầu chỉnh sửa',
      };
    case 'Rejected':
      return {
        label: 'Không đạt',
        tone: 'error',
        historyLabel: 'Khoa đánh giá Không đạt',
      };
  }
}

export function isPdfSubmissionFile(
  fileName?: string | null,
  fileExtension?: string | null,
): boolean {
  const ext = fileExtension?.trim().toLowerCase();
  if (ext === '.pdf' || ext === 'pdf') return true;
  return Boolean(fileName?.trim().toLowerCase().endsWith('.pdf'));
}

export function formatReviewGateStudentsInline(row: ReviewGateItem): string {
  if (row.students && row.students.length > 0) {
    return row.students
      .map((s) => {
        const name = s.fullName || s.userCode || 'Sinh viên';
        return s.userCode ? `${name} (${s.userCode})` : name;
      })
      .join(' · ');
  }
  return row.studentNames || 'Chưa có thành viên';
}

export function sortReviewGateItems<T extends ReviewGateItem>(
  rows: readonly T[],
  sort: ReviewGateSortState,
): T[] {
  const factor = sort.direction === 'asc' ? 1 : -1;
  return [...rows].sort((a, b) => {
    let cmp = 0;
    switch (sort.field) {
      case 'groupCode':
        cmp = a.groupCode.localeCompare(b.groupCode, 'vi', { numeric: true, sensitivity: 'base' });
        break;
      case 'topicTitle':
        cmp = a.topicTitleVi.localeCompare(b.topicTitleVi, 'vi', { sensitivity: 'base' });
        break;
      case 'version':
        cmp = a.latestVersionNo - b.latestVersionNo;
        break;
      case 'submittedAt': {
        const ta = a.submittedAtUtc ? Date.parse(a.submittedAtUtc) : 0;
        const tb = b.submittedAtUtc ? Date.parse(b.submittedAtUtc) : 0;
        cmp = (Number.isNaN(ta) ? 0 : ta) - (Number.isNaN(tb) ? 0 : tb);
        break;
      }
      case 'status': {
        const oa = STATUS_ORDER[a.currentStatus] ?? 99;
        const ob = STATUS_ORDER[b.currentStatus] ?? 99;
        cmp =
          oa !== ob
            ? oa - ob
            : getReviewGateStatusDisplay(a.currentStatus).label.localeCompare(
                getReviewGateStatusDisplay(b.currentStatus).label,
                'vi',
              );
        break;
      }
    }
    if (cmp !== 0) return cmp * factor;
    return a.groupId.localeCompare(b.groupId);
  });
}
