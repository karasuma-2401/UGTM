import type {
  AcademicResultListItemDto,
  MergedResultRow,
  MergedResultStatusBadgeTone,
  ResultFilterKey,
  ScoreListItemDto,
} from '../types/results.types';

const BLOCKING_REASON_MESSAGES: Record<string, string> = {
  'ScoreSummary.AlreadyPublished': 'Bảng điểm đã công bố chính thức và bị khóa.',
  'ScoreSummary.BatchNotWritable': 'Đợt khóa luận đang ở trạng thái Nháp hoặc Đã lưu trữ, không thể tính/công bố mới.',
  'ScoreSummary.NotEligible': 'Chưa đủ điều kiện (biên bản hội đồng chưa khóa hoặc hồ sơ lưu chiểu chưa được Khoa nghiệm thu).',
  'ScoreSummary.MissingRequiredSource': 'Thiếu đầu điểm bắt buộc (GVHD, GVPB hoặc thành viên Hội đồng).',
  'ScoreSummary.NoPositiveWeight': 'Tổng hệ số/trọng số hợp lệ bằng 0 sau khi loại trừ điểm không công nhận.',
  'ScoreSummary.ConfigurationInvalid': 'Đợt chưa có cấu hình điểm Active hợp lệ.',
  'ScoreSummary.AmbiguousSource': 'Nguồn điểm cấu hình bị trùng lặp hoặc có nhiều Ủy viên gây mơ hồ.',
  'ScoreSummary.UnsupportedSource': 'Cấu hình có nguồn điểm Custom chưa hỗ trợ tự động tính.',
  'ScoreSummary.ContextInvalid': 'Ngữ cảnh nhóm, đề tài hoặc thành viên hội đồng không hợp lệ.',
  'ScoreSummary.Stale': 'Dữ liệu điểm, biên bản, lưu chiểu hoặc cấu hình đã thay đổi sau lần tính gần nhất — cần tính lại.',
  'ScoreSummary.Legacy': 'Bảng điểm dữ liệu cũ cần được tính lại theo cấu hình hiện hành.',
  'ScoreSummary.VersionConflict': 'Phiên bản bảng điểm đã thay đổi trên hệ thống; hãy tải lại.',
  'ScoreSummary.NotCalculated': 'Chưa có bảng điểm dự thảo hợp lệ để công bố.',
  'ScoreSummary.BatchMismatch': 'Nhóm không thuộc đợt khóa luận đang chọn.',
  'ScoreSummary.NotFound': 'Không tìm thấy bản ghi bảng điểm của nhóm.',
};

const SOURCE_LABELS: Record<string, string> = {
  Advisor: 'GVHD',
  Reviewer: 'GVPB',
  CouncilChairman: 'Chủ tịch HĐ',
  CouncilSecretary: 'Thư ký HĐ',
  CouncilMember: 'Ủy viên HĐ',
  CouncilAverage: 'TB Hội đồng',
  Custom: 'Tùy chỉnh',
};

export function formatScoreReason(code: string): string {
  return BLOCKING_REASON_MESSAGES[code] ?? code;
}

export function formatScoreSourceLabel(source: string): string {
  return SOURCE_LABELS[source] ?? source;
}

export function formatScoreDecimal(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }
  return value.toFixed(digits);
}

export function formatDateTimeVi(isoString: string | null | undefined): string {
  if (!isoString) {
    return '—';
  }
  const date = new Date(isoString);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

function resolveDisplayState(
  scoreItem: ScoreListItemDto,
  academicItem: AcademicResultListItemDto | undefined,
): {
  filterKey: Exclude<ResultFilterKey, 'all'>;
  displayStatusLabel: string;
  displayStatusTone: MergedResultStatusBadgeTone;
  conditionSummary: string;
} {
  if (scoreItem.status === 'PublishedToStudent') {
    return {
      filterKey: 'published',
      displayStatusLabel: 'Đã công bố',
      displayStatusTone: 'success',
      conditionSummary: academicItem?.detailLabel || 'Lưu chiểu đạt · Đã mở cho sinh viên',
    };
  }

  if (scoreItem.status === 'Calculated') {
    if (scoreItem.isStale || scoreItem.isLegacy) {
      return {
        filterKey: 'needsCalculation',
        displayStatusLabel: 'Cần tính lại',
        displayStatusTone: 'warning',
        conditionSummary: scoreItem.isLegacy
          ? 'Dữ liệu cũ · Cần tính lại theo cấu hình đợt'
          : 'Nguồn điểm hoặc cấu hình thay đổi',
      };
    }

    if (scoreItem.isReadyForPublication) {
      return {
        filterKey: 'readyToPublish',
        displayStatusLabel: 'Đủ điều kiện',
        displayStatusTone: 'info',
        conditionSummary: 'Đủ điểm · Lưu chiểu đạt · Chờ công bố',
      };
    }
  }

  if (scoreItem.status === 'Pending' && scoreItem.isReadyForCalculation) {
    return {
      filterKey: 'needsCalculation',
      displayStatusLabel: 'Sẵn sàng tính',
      displayStatusTone: 'orange',
      conditionSummary: 'Đủ điểm & lưu chiểu đạt · Chưa tính dự thảo',
    };
  }

  if (academicItem?.statusCode === 'MISSING_ARCHIVE') {
    return {
      filterKey: 'needsAction',
      displayStatusLabel: 'Chờ lưu chiểu',
      displayStatusTone: 'warning',
      conditionSummary: 'Đủ điểm chấm · Chưa nghiệm thu lưu chiểu',
    };
  }

  return {
    filterKey: 'needsAction',
    displayStatusLabel: 'Cần bổ sung',
    displayStatusTone: 'neutral',
    conditionSummary: academicItem?.statusLabel || 'Chưa đủ điểm hoặc biên bản chưa khóa',
  };
}

export function mergeScoreAndAcademicRows(
  scoreItems: readonly ScoreListItemDto[],
  academicItems: readonly AcademicResultListItemDto[],
): MergedResultRow[] {
  const academicByGroupId = new Map(academicItems.map((item) => [item.groupId, item]));
  const scoreByGroupId = new Map(scoreItems.map((item) => [item.groupId, item]));
  const rows: MergedResultRow[] = [];

  for (let index = 0; index < scoreItems.length; index += 1) {
    const scoreItem = scoreItems[index];
    const academicItem = academicByGroupId.get(scoreItem.groupId);
    const resolved = resolveDisplayState(scoreItem, academicItem);
    const fallbackCode = `N${String(index + 1).padStart(2, '0')}`;

    rows.push({
      groupId: scoreItem.groupId,
      groupCode: academicItem?.groupCode || fallbackCode,
      topicId: academicItem?.topicId ?? null,
      topicTitleVi: academicItem?.topicTitleVi || scoreItem.topicTitle || 'Chưa có tên đề tài',
      topicTitleEn: academicItem?.topicTitleEn ?? null,
      studentNames: academicItem?.studentNames ?? [],
      studentCodes: academicItem?.studentCodes ?? [],
      facultyName: academicItem?.facultyName ?? 'Khoa CNTT',
      summaryId: scoreItem.summaryId,
      summaryVersion: scoreItem.summaryVersion,
      summaryStatus: scoreItem.status,
      academicStatusCode: academicItem?.statusCode ?? null,
      detailLabel: academicItem?.detailLabel ?? resolved.conditionSummary,
      finalScore: scoreItem.finalScore ?? academicItem?.finalScore ?? null,
      isLegacy: scoreItem.isLegacy,
      isStale: scoreItem.isStale,
      isReadyForCalculation: scoreItem.isReadyForCalculation,
      isReadyForPublication: scoreItem.isReadyForPublication,
      calculatedAt: scoreItem.calculatedAt,
      publishedAt: scoreItem.publishedAt,
      ...resolved,
    });
  }

  for (const academicItem of academicItems) {
    if (scoreByGroupId.has(academicItem.groupId)) {
      continue;
    }

    const fallbackScoreItem: ScoreListItemDto = {
      groupId: academicItem.groupId,
      topicTitle: academicItem.topicTitleVi,
      summaryId: null,
      summaryVersion: null,
      status: academicItem.statusCode === 'PUBLISHED' ? 'PublishedToStudent' : 'Pending',
      finalScore: academicItem.finalScore,
      isLegacy: false,
      isStale: false,
      isReadyForCalculation: academicItem.statusCode === 'READY_TO_PUBLISH',
      isReadyForPublication: false,
      calculatedAt: null,
      publishedAt: null,
    };
    const resolved = resolveDisplayState(fallbackScoreItem, academicItem);

    rows.push({
      groupId: academicItem.groupId,
      groupCode: academicItem.groupCode,
      topicId: academicItem.topicId,
      topicTitleVi: academicItem.topicTitleVi,
      topicTitleEn: academicItem.topicTitleEn,
      studentNames: academicItem.studentNames,
      studentCodes: academicItem.studentCodes,
      facultyName: academicItem.facultyName,
      summaryId: null,
      summaryVersion: null,
      summaryStatus: fallbackScoreItem.status,
      academicStatusCode: academicItem.statusCode,
      detailLabel: academicItem.detailLabel,
      finalScore: academicItem.finalScore,
      isLegacy: false,
      isStale: false,
      isReadyForCalculation: fallbackScoreItem.isReadyForCalculation,
      isReadyForPublication: false,
      calculatedAt: null,
      publishedAt: null,
      ...resolved,
    });
  }

  return rows.sort((a, b) => a.groupCode.localeCompare(b.groupCode, 'vi'));
}

export function extractPublicationFailureMessage(
  errorPayload: { message?: string; errors?: Record<string, string[]> | null } | undefined,
  rowsByGroupId: ReadonlyMap<string, MergedResultRow>,
): string {
  const baseMessage =
    errorPayload?.message || 'Không thể công bố danh sách nhóm đã chọn. Vui lòng kiểm tra lại.';
  const groupErrors = errorPayload?.errors;
  if (!groupErrors || Object.keys(groupErrors).length === 0) {
    return baseMessage;
  }

  const details = Object.entries(groupErrors).map(([groupId, codes]) => {
    const groupCode = rowsByGroupId.get(groupId)?.groupCode ?? groupId.slice(0, 8);
    const reasonText = (codes ?? []).map(formatScoreReason).join('; ');
    return `${groupCode}: ${reasonText}`;
  });

  return `${baseMessage} (${details.join(' | ')})`;
}

export function mapFilterKeyToExportStatus(filterKey: ResultFilterKey): string | undefined {
  switch (filterKey) {
    case 'published':
      return 'PUBLISHED';
    case 'readyToPublish':
      return 'READY_TO_PUBLISH';
    default:
      return undefined;
  }
}
