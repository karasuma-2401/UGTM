import axios from 'axios';
import { httpClient } from '@/shared/api';
import type { ApiResponse } from '@/shared/api/types';
import type {
  CouncilDraftItemDto,
  CouncilOverviewApiItemDto,
  CouncilOverviewMemberDto,
  CouncilScheduleResponseDto,
  ApproveCouncilRequest,
  CouncilApprovalResponseDto,
} from '../types/councilApproval.types';

const PAGE_SIZE = 100;
const MAX_PAGES = 20;

const STATUS_PARAM_MAP: Record<string, string | undefined> = {
  draft: 'Draft',
  active: 'Active',
  completed: 'Completed',
  all: undefined,
};

const STATUS_LABEL_MAP: Record<'Draft' | 'Active' | 'Completed', string> = {
  Draft: 'Chờ phê duyệt',
  Active: 'Đã phê duyệt',
  Completed: 'Đã hoàn thành',
};

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    throw new Error(response.error.message || 'Thao tác không thành công.');
  }
  return response.data;
}

function toApiError(error: unknown, fallback: string): Error {
  if (axios.isCancel(error)) {
    return error instanceof Error ? error : new Error('Request canceled');
  }
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const apiMessage = error.response?.data?.error?.message;
    if (apiMessage) {
      return new Error(apiMessage);
    }
    if (error.response?.status === 403) {
      return new Error('Bạn không có quyền thực hiện thao tác phê duyệt Hội đồng.');
    }
    if (!error.response) {
      return new Error('Không thể kết nối đến máy chủ. Vui lòng thử lại.');
    }
  }
  if (error instanceof Error && error.message) {
    return error;
  }
  return new Error(fallback);
}

function normalizeCouncilStatus(status: string): 'Draft' | 'Active' | 'Completed' {
  if (status === 'Active' || status === 'Completed') {
    return status;
  }
  return 'Draft';
}

function buildMembersSummary(row: CouncilOverviewApiItemDto): string {
  const parts: string[] = [];
  if (row.chairman) {
    parts.push('Chủ tịch');
  }
  if (row.secretary) {
    parts.push('Thư ký');
  }
  if (row.members.length > 0) {
    parts.push('Ủy viên');
  }
  return parts.length > 0 ? parts.join(' · ') : 'Chưa có thành viên';
}

function formatMemberDisplayNames(row: CouncilOverviewApiItemDto): string[] {
  const names: string[] = [];
  for (const member of row.members) {
    const label = member.isReviewer ? `${member.fullName} (GVPB kiêm UV)` : member.fullName;
    if (!names.includes(label)) {
      names.push(label);
    }
  }
  return names;
}

function hasRowAdvisorConflict(row: CouncilOverviewApiItemDto): boolean {
  return Boolean(
    row.chairman?.isAdvisorConflict ||
      row.secretary?.isAdvisorConflict ||
      row.members.some((m) => m.isAdvisorConflict),
  );
}

function mergeMembers(
  existing: CouncilOverviewMemberDto[],
  incoming: CouncilOverviewMemberDto[],
): CouncilOverviewMemberDto[] {
  const byUserId = new Map<string, CouncilOverviewMemberDto>();
  for (const member of existing) {
    byUserId.set(member.userId, { ...member });
  }
  for (const member of incoming) {
    const current = byUserId.get(member.userId);
    if (!current) {
      byUserId.set(member.userId, { ...member });
    } else {
      current.isReviewer = current.isReviewer || member.isReviewer;
      current.isAdvisorConflict = current.isAdvisorConflict || member.isAdvisorConflict;
      if (member.isReviewer && member.note) {
        current.note = member.note;
      }
    }
  }
  return Array.from(byUserId.values());
}

function groupCouncilsById(rows: CouncilOverviewApiItemDto[]): CouncilDraftItemDto[] {
  const grouped = new Map<string, CouncilDraftItemDto>();

  for (const row of rows) {
    const normalizedStatus = normalizeCouncilStatus(row.status);
    const existing = grouped.get(row.id);

    if (!existing) {
      const groupCodes = row.groupCode ? [row.groupCode] : [];
      const topicTitles = row.topicTitle ? [row.topicTitle] : [];
      const memberNames = formatMemberDisplayNames(row);
      const assignments = row.groupCode
        ? [
            {
              assignmentId: row.assignmentId ?? null,
              groupCode: row.groupCode,
              topicTitle: row.topicTitle ?? null,
            },
          ]
        : [];

      grouped.set(row.id, {
        id: row.id,
        batchId: row.batchId,
        councilCode: row.councilCode,
        facultyName: row.facultyName || 'Khoa CNTT',
        groupCode: groupCodes.length > 0 ? groupCodes.join(', ') : 'Chưa gán nhóm',
        groupCodes,
        topicTitles,
        assignments,
        room: row.room,
        timeSlot: row.timeSlot,
        defenseDate: row.defenseDate,
        status: normalizedStatus,
        statusLabel: row.statusLabel || STATUS_LABEL_MAP[normalizedStatus],
        rosterModel: row.rosterModel,
        rosterModelLabel: row.rosterModelLabel,
        groupConditionLabel: groupCodes.length > 0 ? 'Đủ điều kiện bảo vệ' : 'Chưa gán nhóm',
        membersSummary: buildMembersSummary(row),
        chairman: row.chairman ?? null,
        secretary: row.secretary ?? null,
        members: row.members.map((m) => ({ ...m })),
        chairmanName: row.chairman?.fullName,
        secretaryName: row.secretary?.fullName,
        memberNames,
        isSubmittedToAcademicAffairs: Boolean(row.isSubmittedToAcademicAffairs),
        submittedAt: row.submittedAt ?? null,
        submissionNotes: row.submissionNotes ?? null,
        decisionNumber: row.decisionNumber ?? null,
        signedDate: row.signedDate ?? null,
        decisionFileUrl: row.decisionFileUrl ?? null,
        approvedAt: row.approvedAt ?? null,
        hasRoomConflict: Boolean(row.hasRoomConflict),
        conflictingMemberNames: row.conflictingMemberNames ? [...row.conflictingMemberNames] : [],
        hasIneligibleGroups: Boolean(row.hasIneligibleGroups),
        hasReviewerRoleConflict: Boolean(row.hasReviewerRoleConflict),
        hasAdvisorConflict: hasRowAdvisorConflict(row),
      });
      continue;
    }

    if (row.groupCode && !existing.groupCodes.includes(row.groupCode)) {
      existing.groupCodes.push(row.groupCode);
      existing.groupCode = existing.groupCodes.join(', ');
      existing.groupConditionLabel = 'Đủ điều kiện bảo vệ';
      existing.assignments.push({
        assignmentId: row.assignmentId ?? null,
        groupCode: row.groupCode,
        topicTitle: row.topicTitle ?? null,
      });
    }

    if (row.topicTitle && !existing.topicTitles.includes(row.topicTitle)) {
      existing.topicTitles.push(row.topicTitle);
    }

    existing.members = mergeMembers(existing.members, row.members);
    if (existing.chairman && row.chairman) {
      existing.chairman.isReviewer = existing.chairman.isReviewer || row.chairman.isReviewer;
      existing.chairman.isAdvisorConflict =
        existing.chairman.isAdvisorConflict || row.chairman.isAdvisorConflict;
    }
    if (existing.secretary && row.secretary) {
      existing.secretary.isReviewer = existing.secretary.isReviewer || row.secretary.isReviewer;
      existing.secretary.isAdvisorConflict =
        existing.secretary.isAdvisorConflict || row.secretary.isAdvisorConflict;
    }

    for (const label of formatMemberDisplayNames(row)) {
      if (existing.memberNames && !existing.memberNames.includes(label)) {
        existing.memberNames.push(label);
      }
    }

    existing.hasIneligibleGroups = existing.hasIneligibleGroups || Boolean(row.hasIneligibleGroups);
    existing.hasReviewerRoleConflict =
      existing.hasReviewerRoleConflict || Boolean(row.hasReviewerRoleConflict);
    existing.hasAdvisorConflict = existing.hasAdvisorConflict || hasRowAdvisorConflict(row);
    if (existing.hasIneligibleGroups) {
      existing.groupConditionLabel = 'Có nhóm chưa đủ điều kiện';
    }
  }

  return Array.from(grouped.values());
}

export const councilApprovalApi = {
  async getCouncilDrafts(
    params?: {
      batchId?: string;
      keyword?: string;
      status?: string;
    },
    signal?: AbortSignal,
  ): Promise<CouncilDraftItemDto[]> {
    try {
      const allRows: CouncilOverviewApiItemDto[] = [];
      let pageNumber = 1;
      const mappedStatus = params?.status ? STATUS_PARAM_MAP[params.status] : undefined;

      while (pageNumber <= MAX_PAGES) {
        const response = await httpClient.get<ApiResponse<CouncilOverviewApiItemDto[]>>(
          '/api/councils',
          {
            params: {
              batchId: params?.batchId && params.batchId !== 'all' ? params.batchId : undefined,
              keyword: params?.keyword?.trim() || undefined,
              status: mappedStatus,
              pageNumber,
              pageSize: PAGE_SIZE,
            },
            signal,
          },
        );
        const pageItems = unwrap(response.data);
        allRows.push(...pageItems);
        if (pageItems.length < PAGE_SIZE) {
          break;
        }
        pageNumber += 1;
      }

      return groupCouncilsById(allRows);
    } catch (error) {
      throw toApiError(error, 'Không thể tải danh sách Hội đồng.');
    }
  },

  async getCouncilSchedule(
    councilId: string,
    signal?: AbortSignal,
  ): Promise<CouncilScheduleResponseDto> {
    try {
      const response = await httpClient.get<ApiResponse<CouncilScheduleResponseDto>>(
        `/api/councils/schedule/${councilId}`,
        { signal },
      );
      return unwrap(response.data);
    } catch (error) {
      throw toApiError(error, 'Không thể tải chi tiết lịch và nhóm của Hội đồng.');
    }
  },

  async approveCouncil(
    id: string,
    request: ApproveCouncilRequest,
  ): Promise<CouncilApprovalResponseDto> {
    try {
      const response = await httpClient.put<ApiResponse<CouncilApprovalResponseDto>>(
        `/api/councils/${id}/approve`,
        request,
      );
      return unwrap(response.data);
    } catch (error) {
      throw toApiError(error, 'Phê duyệt Hội đồng thất bại. Vui lòng thử lại.');
    }
  },
};
