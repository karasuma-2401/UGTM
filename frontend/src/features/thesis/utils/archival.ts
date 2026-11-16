import type { StatusTone } from '@/components/ui/status-badge';
import type {
  AcademicResultDetailDto,
  AcademicResultListItemDto,
} from '@/features/councils/types/results.types';
import type { AdvisingGroup } from '@/features/topics/types/advising.types';
import type {
  ArchivalFilterKey,
  ArchivalMetadataInput,
  ArchivalReviewRow,
  ArchivalStats,
  FinalSubmissionSummaryDto,
} from '../types/archival.types';
import type { StudentGroupDetailDto } from '../types/syllabus.types';

export const ARCHIVAL_MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;
export const ARCHIVAL_ABSTRACT_MAX_LENGTH = 10_000;
export const ARCHIVAL_KEYWORDS_MAX_LENGTH = 1_000;
export const ARCHIVAL_REASON_MAX_LENGTH = 1_000;

const BLOCKING_REASON_MESSAGES: Record<string, string> = {
  'FinalSubmission.BatchNotWritable': 'Đợt khóa luận hiện không mở để nộp hồ sơ lưu chiểu.',
  'FinalSubmission.SubmissionWindowClosed': 'Hiện đang ngoài thời gian mở cổng nộp lưu chiểu.',
  'FinalSubmission.DefenseNotLocked':
    'Nhóm cần có biên bản bảo vệ Hội đồng đã khóa (Locked) trước khi nộp lưu chiểu.',
  'FinalSubmission.Locked':
    'Hồ sơ đã được GVHD xác nhận hoặc Khoa nghiệm thu hoàn thành, không thể nộp lại.',
  'FinalSubmission.ContextInvalid': 'Thông tin nhóm, đề tài hoặc đợt chưa hợp lệ.',
};

const STATUS_DISPLAY: Record<string, { label: string; tone: StatusTone; shortStepNote: string }> = {
  NotSubmitted: {
    label: 'Chưa nộp',
    tone: 'neutral',
    shortStepNote: 'Bạn đang thực hiện',
  },
  Submitted: {
    label: 'Chờ GVHD xác nhận',
    tone: 'warning',
    shortStepNote: 'Đã nộp, chờ GVHD xác nhận',
  },
  NeedRevision: {
    label: 'Cần bổ sung',
    tone: 'error',
    shortStepNote: 'Cần chỉnh sửa và nộp lại',
  },
  AdvisorConfirmed: {
    label: 'Chờ nghiệm thu',
    tone: 'info',
    shortStepNote: 'GVHD đã xác nhận',
  },
  ArchiveConfirmed: {
    label: 'Đã nghiệm thu',
    tone: 'success',
    shortStepNote: 'Hoàn thành lưu chiểu',
  },
};

export function getArchivalStatusDisplay(status: string | null | undefined): {
  label: string;
  tone: StatusTone;
  shortStepNote: string;
} {
  if (!status || !(status in STATUS_DISPLAY)) {
    return STATUS_DISPLAY.NotSubmitted;
  }
  return STATUS_DISPLAY[status];
}

export function formatArchivalBlockingReason(code: string): string {
  return BLOCKING_REASON_MESSAGES[code] ?? code;
}

export function validateArchivalFile(
  file: File,
  expectedExtension: '.pdf' | '.docx',
): string | null {
  const lowerName = file.name.toLowerCase();
  if (!lowerName.endsWith(expectedExtension)) {
    return `Tệp không đúng định dạng ${expectedExtension.toUpperCase()} yêu cầu.`;
  }
  if (file.size <= 0) {
    return 'Tệp rỗng, vui lòng chọn tệp hợp lệ.';
  }
  if (file.size > ARCHIVAL_MAX_FILE_SIZE_BYTES) {
    return 'Dung lượng tệp vượt quá giới hạn tối đa 20 MiB.';
  }
  return null;
}

export function formatArchivalFileSize(bytes: number | null | undefined): string {
  if (bytes === null || bytes === undefined || Number.isNaN(bytes) || bytes <= 0) {
    return '—';
  }
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

export function validateArchivalMetadata(input: ArchivalMetadataInput): {
  valid: boolean;
  abstractsReady: boolean;
  keywordsReady: boolean;
} {
  const vi = input.abstractVi.trim();
  const en = input.abstractEn.trim();
  const kvi = input.keywordsVi.trim();
  const ken = input.keywordsEn.trim();

  const abstractsReady =
    vi.length > 0 &&
    vi.length <= ARCHIVAL_ABSTRACT_MAX_LENGTH &&
    en.length > 0 &&
    en.length <= ARCHIVAL_ABSTRACT_MAX_LENGTH;
  const keywordsReady =
    kvi.length > 0 &&
    kvi.length <= ARCHIVAL_KEYWORDS_MAX_LENGTH &&
    ken.length > 0 &&
    ken.length <= ARCHIVAL_KEYWORDS_MAX_LENGTH;

  return {
    valid: abstractsReady && keywordsReady,
    abstractsReady,
    keywordsReady,
  };
}

export function computeArchivalStats(
  items: readonly FinalSubmissionSummaryDto[],
): ArchivalStats {
  const stats: ArchivalStats = {
    all: items.length,
    advisorConfirmed: 0,
    submitted: 0,
    needRevision: 0,
    archiveConfirmed: 0,
  };

  for (const item of items) {
    if (item.status === 'AdvisorConfirmed') stats.advisorConfirmed += 1;
    else if (item.status === 'Submitted') stats.submitted += 1;
    else if (item.status === 'NeedRevision') stats.needRevision += 1;
    else if (item.status === 'ArchiveConfirmed') stats.archiveConfirmed += 1;
  }

  return stats;
}

interface ResolvedGroupMeta {
  groupCode?: string | null;
  topicTitleVi?: string | null;
  topicTitleEn?: string | null;
  studentNames: string[];
  studentCodes: string[];
  advisorName?: string | null;
}

const ACADEMIC_PREFIX_PATTERN = /^(?:PGS\.?\s*TS\.?|GS\.?\s*TS\.?|TS\.?|ThS\.?|KS\.?|CN\.?|GV\.?)\s+/i;

export function resolveArchivalGroupCode(
  groupId: string,
  rawGroupCode?: string | null,
): string {
  const trimmed = rawGroupCode?.trim() ?? '';
  const genericPrefixFallback = `Nhóm ${groupId.slice(0, 4).toUpperCase()}`;
  if (trimmed && trimmed.toUpperCase() !== genericPrefixFallback.toUpperCase()) {
    return trimmed;
  }
  const suffix = groupId.replace(/-/g, '').slice(-4).toUpperCase();
  return suffix ? `NH-${suffix}` : 'NH';
}

export function formatAdvisorDisplayName(
  fullName?: string | null,
  academicTitle?: string | null,
): string {
  const cleanName = fullName?.trim() ?? '';
  if (!cleanName || cleanName === 'Chưa phân công') {
    return 'Giảng viên hướng dẫn';
  }
  const cleanTitle = academicTitle?.trim() ?? '';
  if (!cleanTitle) {
    return cleanName;
  }
  if (ACADEMIC_PREFIX_PATTERN.test(cleanName)) {
    return cleanName;
  }
  return `${cleanTitle} ${cleanName}`.trim();
}

export function parseArchivalKeywords(raw?: string | null): string[] {
  if (!raw) return [];
  return raw
    .split(/[;,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function buildArchivalRows(
  summaries: readonly FinalSubmissionSummaryDto[],
  resolveMeta: (groupId: string) => ResolvedGroupMeta,
): ArchivalReviewRow[] {
  return summaries.map((summary) => {
    const meta = resolveMeta(summary.groupId);
    const display = getArchivalStatusDisplay(summary.status);

    return {
      ...summary,
      groupCode: resolveArchivalGroupCode(summary.groupId, meta.groupCode),
      topicTitleVi: meta.topicTitleVi || summary.topicTitle,
      topicTitleEn: meta.topicTitleEn ?? null,
      studentNames: meta.studentNames,
      studentCodes: meta.studentCodes,
      advisorName: meta.advisorName ?? null,
      statusLabel: display.label,
      statusTone: display.tone,
    };
  });
}

export function mergeArchivalSummaryRows(
  summaries: readonly FinalSubmissionSummaryDto[],
  academicItems: readonly AcademicResultListItemDto[],
  detailByGroupId: Readonly<Record<string, AcademicResultDetailDto | undefined>>,
): ArchivalReviewRow[] {
  const academicByGroupId = new Map(academicItems.map((item) => [item.groupId, item]));

  return buildArchivalRows(summaries, (groupId) => {
    const academic = academicByGroupId.get(groupId);
    const detail = detailByGroupId[groupId];

    return {
      groupCode: detail?.groupCode || academic?.groupCode,
      topicTitleVi: detail?.topicTitleVi || academic?.topicTitleVi,
      topicTitleEn: detail?.topicTitleEn ?? academic?.topicTitleEn ?? null,
      studentNames:
        detail && detail.students.length > 0
          ? detail.students.map((s) => s.fullName)
          : (academic?.studentNames ?? []),
      studentCodes:
        detail && detail.students.length > 0
          ? detail.students.map((s) => s.studentCode)
          : (academic?.studentCodes ?? []),
      advisorName: detail?.advisorName ?? null,
    };
  });
}

export function mergeAdvisorArchivalRows(
  summaries: readonly FinalSubmissionSummaryDto[],
  advisingGroups: readonly AdvisingGroup[],
  detailByGroupId: Readonly<Record<string, StudentGroupDetailDto | undefined>>,
): ArchivalReviewRow[] {
  const advisingByGroupId = new Map(advisingGroups.map((group) => [group.groupId, group]));

  return buildArchivalRows(summaries, (groupId) => {
    const advising = advisingByGroupId.get(groupId);
    const detail = detailByGroupId[groupId];

    const detailMembers = detail?.members.filter((m) => m.confirmationStatus === 'Confirmed') ?? [];
    const fallbackMembers = detail?.members ?? [];
    const activeDetailMembers = detailMembers.length > 0 ? detailMembers : fallbackMembers;

    const advisingConfirmed =
      advising?.students.filter((s) => s.confirmationStatus === 'Confirmed') ?? [];
    const advisingAll = advising?.students ?? [];
    const activeAdvising = advisingConfirmed.length > 0 ? advisingConfirmed : advisingAll;

    const studentNames =
      activeDetailMembers.length > 0
        ? activeDetailMembers.map((m) => m.fullName)
        : activeAdvising.map((s) => s.fullName || 'Chưa cập nhật');
    const studentCodes =
      activeDetailMembers.length > 0
        ? activeDetailMembers.map((m) => m.userCode)
        : activeAdvising.map((s) => s.userCode || '');

    return {
      groupCode: detail?.groupCode,
      topicTitleVi: detail?.topicTitleVi || advising?.titleVi,
      topicTitleEn: detail?.topicTitleEn ?? advising?.titleEn ?? null,
      studentNames,
      studentCodes,
      advisorName: detail?.advisor?.fullName ?? null,
    };
  });
}

export function filterArchivalRows(
  rows: readonly ArchivalReviewRow[],
  filterKey: ArchivalFilterKey,
  keyword: string,
): ArchivalReviewRow[] {
  const normalizedKeyword = keyword.trim().toLowerCase();

  return rows.filter((row) => {
    if (filterKey !== 'ALL' && row.status !== filterKey) {
      return false;
    }

    if (!normalizedKeyword) {
      return true;
    }

    const haystack = [
      row.groupCode,
      row.topicTitleVi,
      row.topicTitleEn ?? '',
      row.advisorName ?? '',
      ...row.studentNames,
      ...row.studentCodes,
    ]
      .join(' ')
      .toLowerCase();

    return haystack.includes(normalizedKeyword);
  });
}

export type ArchivalSortField =
  | 'groupCode'
  | 'topicTitle'
  | 'version'
  | 'submittedAt'
  | 'status';

export interface ArchivalSortState {
  field: ArchivalSortField;
  direction: 'asc' | 'desc';
}

const ARCHIVAL_STATUS_ORDER: Record<string, number> = {
  Submitted: 1,
  NeedRevision: 2,
  AdvisorConfirmed: 3,
  ArchiveConfirmed: 4,
  NotSubmitted: 5,
};

export function sortArchivalRows(
  rows: readonly ArchivalReviewRow[],
  sort: ArchivalSortState,
): ArchivalReviewRow[] {
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
        cmp = a.currentVersionNo - b.currentVersionNo;
        break;
      case 'submittedAt': {
        const ta = a.submittedAt ? Date.parse(a.submittedAt) : 0;
        const tb = b.submittedAt ? Date.parse(b.submittedAt) : 0;
        cmp = (Number.isNaN(ta) ? 0 : ta) - (Number.isNaN(tb) ? 0 : tb);
        break;
      }
      case 'status': {
        const oa = ARCHIVAL_STATUS_ORDER[a.status] ?? 99;
        const ob = ARCHIVAL_STATUS_ORDER[b.status] ?? 99;
        cmp = oa !== ob ? oa - ob : a.statusLabel.localeCompare(b.statusLabel, 'vi');
        break;
      }
    }
    if (cmp !== 0) return cmp * factor;
    return a.submissionId.localeCompare(b.submissionId);
  });
}

export function formatReviewActionLabel(action: string, stage: string): string {
  if (action === 'AdvisorConfirm') return 'GVHD xác nhận bản sửa';
  if (action === 'FacultyConfirm') return 'Khoa nghiệm thu lưu chiểu';
  if (action === 'RequestRevision') {
    return stage === 'Advisor' ? 'GVHD yêu cầu chỉnh sửa' : 'Khoa yêu cầu chỉnh sửa';
  }
  return action;
}

export function triggerBrowserDownload(url: string, filename: string): void {
  const link = document.createElement('a');
  link.href = url;
  link.target = '_blank';
  link.rel = 'noopener noreferrer';
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
}
