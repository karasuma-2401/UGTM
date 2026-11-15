import type { StatusTone } from '@/components/ui/status-badge';
import type {
  AcademicResultDetailDto,
  AcademicResultListItemDto,
} from '@/features/councils/types/results.types';
import type {
  ArchivalFilterKey,
  ArchivalMetadataInput,
  ArchivalReviewRow,
  ArchivalStats,
  FinalSubmissionSummaryDto,
} from '../types/archival.types';

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
    shortStepNote: 'Cần chỉnh sửa & nộp lại',
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

export function mergeArchivalSummaryRows(
  summaries: readonly FinalSubmissionSummaryDto[],
  academicItems: readonly AcademicResultListItemDto[],
  detailByGroupId: Readonly<Record<string, AcademicResultDetailDto | undefined>>,
): ArchivalReviewRow[] {
  const academicByGroupId = new Map(academicItems.map((item) => [item.groupId, item]));

  return summaries.map((summary, index) => {
    const academic = academicByGroupId.get(summary.groupId);
    const detail = detailByGroupId[summary.groupId];
    const display = getArchivalStatusDisplay(summary.status);
    const fallbackCode = `N${String(index + 1).padStart(2, '0')}`;

    const studentNames =
      detail && detail.students.length > 0
        ? detail.students.map((s) => s.fullName)
        : (academic?.studentNames ?? []);
    const studentCodes =
      detail && detail.students.length > 0
        ? detail.students.map((s) => s.studentCode)
        : (academic?.studentCodes ?? []);

    return {
      ...summary,
      groupCode: detail?.groupCode || academic?.groupCode || fallbackCode,
      topicTitleVi: detail?.topicTitleVi || academic?.topicTitleVi || summary.topicTitle,
      topicTitleEn: detail?.topicTitleEn ?? academic?.topicTitleEn ?? null,
      studentNames,
      studentCodes,
      advisorName: detail?.advisorName ?? null,
      statusLabel: display.label,
      statusTone: display.tone,
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
