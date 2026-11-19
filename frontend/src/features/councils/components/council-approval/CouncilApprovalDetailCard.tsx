import { useState } from 'react';
import {
  AlertTriangle,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock3,
  FileCheck2,
  MapPin,
  MessageSquareText,
  Users,
  XCircle,
} from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type {
  CouncilDraftItemDto,
  CouncilOverviewMemberDto,
  CouncilScheduleResponseDto,
} from '../../types/councilApproval.types';
import {
  formatViDate,
  formatViDateTime,
  type CouncilReadinessResult,
  type ReadinessCheckState,
} from '../../utils/councilApprovalReadiness';

interface CouncilApprovalDetailCardProps {
  selectedCouncil: CouncilDraftItemDto | null;
  readiness: CouncilReadinessResult | null;
  schedule?: CouncilScheduleResponseDto;
  isLoadingSchedule?: boolean;
}

function getHeaderBadge(
  council: CouncilDraftItemDto,
  readiness: CouncilReadinessResult | null,
): { tone: StatusTone; label: string } {
  if (council.status === 'Completed') return { tone: 'neutral', label: 'Đã hoàn thành' };
  if (council.status === 'Active') return { tone: 'success', label: 'Đã phê duyệt' };
  if (readiness?.category === 'ready') return { tone: 'info', label: 'Sẵn sàng duyệt' };
  if (!council.isSubmittedToAcademicAffairs) return { tone: 'warning', label: 'Chờ Khoa nộp' };
  return { tone: 'error', label: 'Chưa đủ điều kiện' };
}

function CheckStateIcon({ state }: { state: ReadinessCheckState }) {
  if (state === 'passed') {
    return <CheckCircle2 className="size-4 text-tone-success-icon shrink-0 mt-0.5" />;
  }
  if (state === 'warning') {
    return <AlertTriangle className="size-4 text-tone-warning-icon shrink-0 mt-0.5" />;
  }
  return <XCircle className="size-4 text-tone-danger-icon shrink-0 mt-0.5" />;
}

function roleLabel(role: string, isReviewer?: boolean): string {
  if (role === 'Chairman') return 'Chủ tịch';
  if (role === 'Secretary') return 'Thư ký';
  if (isReviewer) return 'GVPB kiêm UV';
  return 'Ủy viên';
}

export function CouncilApprovalDetailCard({
  selectedCouncil,
  readiness,
  schedule,
  isLoadingSchedule = false,
}: CouncilApprovalDetailCardProps) {
  const [expandedGroupsForId, setExpandedGroupsForId] = useState<string | null>(null);
  const showAllGroups = Boolean(selectedCouncil && expandedGroupsForId === selectedCouncil.id);

  if (!selectedCouncil) {
    return (
      <div className="p-6 text-center space-y-2">
        <div className="mx-auto flex size-10 items-center justify-center rounded-full bg-surface-muted text-ink-subtle">
          <FileCheck2 className="size-5" />
        </div>
        <p className="text-sm font-medium text-ink-heading">Chưa chọn hội đồng</p>
        <p className="text-xs text-ink-muted">
          Chọn một hội đồng ở danh sách bên trái để kiểm tra hồ sơ và phê duyệt quyết định.
        </p>
      </div>
    );
  }

  const badge = getHeaderBadge(selectedCouncil, readiness);

  const memberList: Array<{
    userId: string;
    fullName: string;
    role: string;
    isReviewer: boolean;
    isAdvisorConflict: boolean;
    userCode?: string | null;
    email?: string | null;
  }> = [];

  const scheduleMemberById = new Map(
    (schedule?.members ?? []).map((m) => [m.userId, m]),
  );

  const pushOverviewMember = (member: CouncilOverviewMemberDto | null | undefined, role: string) => {
    if (!member) return;
    const extra = scheduleMemberById.get(member.userId);
    memberList.push({
      userId: member.userId,
      fullName: member.fullName,
      role,
      isReviewer: member.isReviewer,
      isAdvisorConflict: member.isAdvisorConflict,
      userCode: extra?.userCode ?? null,
      email: extra?.email ?? null,
    });
  };

  pushOverviewMember(selectedCouncil.chairman, 'Chairman');
  pushOverviewMember(selectedCouncil.secretary, 'Secretary');
  for (const m of selectedCouncil.members) {
    pushOverviewMember(m, 'Member');
  }

  const conflictingSet = new Set(selectedCouncil.conflictingMemberNames);
  const scheduleGroups = schedule?.groups ?? [];
  const totalGroups = scheduleGroups.length > 0 ? scheduleGroups.length : selectedCouncil.assignments.length;
  const visibleGroups = showAllGroups ? scheduleGroups : scheduleGroups.slice(0, 2);

  return (
    <div className="p-5 space-y-5">
      {/* Header & Schedule */}
      <div className="space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h2 className="text-base font-bold text-ink-heading tracking-tight tabular-nums">
                {selectedCouncil.councilCode}
              </h2>
              <span className="text-xs text-ink-subtle">·</span>
              <span className="text-xs font-medium text-ink-muted">
                {selectedCouncil.facultyName}
              </span>
            </div>
            <p className="text-xs text-ink-muted mt-0.5">{selectedCouncil.rosterModelLabel}</p>
          </div>
          <StatusBadge tone={badge.tone}>{badge.label}</StatusBadge>
        </div>

        <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-3 rounded-xl border border-line/70 bg-surface-subtle/60 px-3 py-2.5 text-xs">
          <div className="min-w-0 space-y-0.5">
            <span className="flex items-center gap-1 text-[11px] text-ink-subtle">
              <CalendarDays className="size-3 shrink-0" />
              Lịch bảo vệ
            </span>
            <p className="font-semibold text-ink-heading tabular-nums">
              {formatViDate(selectedCouncil.defenseDate)} ·{' '}
              <span className="inline-flex items-center gap-1 font-medium text-ink-body">
                <Clock3 className="size-3 text-ink-subtle shrink-0" />
                {selectedCouncil.timeSlot}
              </span>
            </p>
          </div>
          <div className="text-right space-y-0.5 shrink-0">
            <span className="inline-flex items-center gap-1 text-[11px] text-ink-subtle">
              <MapPin className="size-3 shrink-0" />
              Phòng
            </span>
            <p
              className={cn(
                'font-semibold',
                selectedCouncil.hasRoomConflict ? 'text-tone-warning-strong' : 'text-ink-heading',
              )}
            >
              {selectedCouncil.room}
            </p>
          </div>
        </div>
      </div>

      {/* Readiness Checklist (for Draft) */}
      {selectedCouncil.status === 'Draft' && readiness && (
        <div className="space-y-2">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-xs font-semibold text-ink-secondary">
              Kiểm tra điều kiện phê duyệt
            </h3>
            <span
              className={cn(
                'text-[11px] font-semibold',
                readiness.isReady ? 'text-tone-success-text' : 'text-tone-warning-strong',
              )}
            >
              {readiness.checks.filter((c) => c.state === 'passed').length}/{readiness.checks.length} đạt
            </span>
          </div>
          <ul className="rounded-xl border border-line/70 bg-surface-subtle/40 divide-y divide-line-subtle">
            {readiness.checks.map((check) => (
              <li key={check.key} className="px-3 py-2 flex items-start gap-2.5 text-xs">
                <CheckStateIcon state={check.state} />
                <div className="min-w-0 flex-1">
                  <span className="font-medium text-ink-heading">{check.label}</span>
                  <p
                    className={cn(
                      'text-[11px] leading-snug mt-0.5',
                      check.state === 'passed'
                        ? 'text-ink-muted'
                        : check.state === 'warning'
                          ? 'text-tone-warning-strong'
                          : 'text-tone-danger-text',
                    )}
                  >
                    {check.detail}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Faculty Submission Note (read-only) */}
      {(selectedCouncil.submissionNotes || selectedCouncil.submittedAt) && (
        <div className="rounded-xl border border-line/70 bg-surface-subtle/60 p-3 space-y-1.5">
          <div className="flex items-center justify-between gap-2 text-[11px] text-ink-subtle">
            <span className="inline-flex items-center gap-1.5 font-medium text-ink-secondary">
              <MessageSquareText className="size-3.5 text-ink-subtle" />
              Ghi chú từ Văn phòng Khoa
            </span>
            {selectedCouncil.submittedAt && (
              <span className="tabular-nums">{formatViDateTime(selectedCouncil.submittedAt)}</span>
            )}
          </div>
          <p className="text-xs text-ink-body leading-relaxed">
            {selectedCouncil.submissionNotes || 'Khoa đã gửi hồ sơ thẩm định lên Phòng Đào tạo.'}
          </p>
        </div>
      )}

      {/* Members */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-xs font-semibold text-ink-secondary">
            Thành phần Hội đồng ({memberList.length})
          </h3>
        </div>
        {memberList.length === 0 ? (
          <p className="text-xs text-ink-subtle rounded-xl border border-dashed border-line-input p-3">
            Chưa có thành viên nào được phân công vào hội đồng này.
          </p>
        ) : (
          <ul className="space-y-2">
            {memberList.map((member) => {
              const hasScheduleConflict = conflictingSet.has(member.fullName);
              return (
                <li
                  key={`${member.role}-${member.userId}`}
                  className="flex items-center justify-between gap-2.5 rounded-xl border border-line/70 bg-surface-card px-3 py-2"
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <Avatar name={member.fullName} className="size-8 text-[11px]" />
                    <div className="min-w-0">
                      <p className="text-xs font-semibold text-ink-heading truncate">
                        {member.fullName}
                      </p>
                      {(member.userCode || member.email) && (
                        <p className="text-[11px] text-ink-muted truncate tabular-nums">
                          {[member.userCode, member.email].filter(Boolean).join(' · ')}
                        </p>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-1.5 shrink-0">
                    {(member.isAdvisorConflict || hasScheduleConflict) && (
                      <Badge variant="error" className="text-[11px] px-1.5 py-0">
                        {member.isAdvisorConflict ? 'Trùng GVHD' : 'Trùng lịch'}
                      </Badge>
                    )}
                    <Badge
                      variant={member.role === 'Chairman' ? 'info' : 'secondary'}
                      className="text-[11px] px-2 py-0.5"
                    >
                      {roleLabel(member.role, member.isReviewer)}
                    </Badge>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {/* Assigned Groups */}
      <div className="space-y-2">
        <div className="flex items-center justify-between gap-2">
          <h3 className="text-xs font-semibold text-ink-secondary inline-flex items-center gap-1.5">
            <Users className="size-3.5 text-ink-subtle" />
            Nhóm đề tài bảo vệ ({totalGroups})
          </h3>
          {scheduleGroups.length > 2 && (
            <button
              type="button"
              onClick={() =>
                setExpandedGroupsForId(showAllGroups ? null : selectedCouncil.id)
              }
              className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline cursor-pointer"
            >
              {showAllGroups ? (
                <>
                  Thu gọn <ChevronUp className="size-3.5" />
                </>
              ) : (
                <>
                  Xem tất cả ({scheduleGroups.length}) <ChevronDown className="size-3.5" />
                </>
              )}
            </button>
          )}
        </div>

        {isLoadingSchedule ? (
          <div className="space-y-2">
            <Skeleton className="h-16 w-full rounded-xl" />
            <Skeleton className="h-16 w-full rounded-xl" />
          </div>
        ) : totalGroups === 0 ? (
          <p className="text-xs text-ink-subtle rounded-xl border border-dashed border-line-input p-3">
            Chưa có nhóm đề tài nào được gán vào hội đồng này.
          </p>
        ) : scheduleGroups.length > 0 ? (
          <div className="space-y-2">
            {visibleGroups.map((group) => (
              <div
                key={group.groupId}
                className="rounded-xl border border-line/70 bg-surface-subtle/40 p-3 space-y-1.5"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0 flex items-start gap-2">
                    <span className="inline-flex items-center rounded-md bg-surface-card border border-line px-1.5 py-0.5 text-[11px] font-semibold text-ink-heading tabular-nums shrink-0">
                      N{String(group.orderNumber).padStart(2, '0')}
                    </span>
                    <p className="text-xs font-medium text-ink-heading leading-snug line-clamp-2">
                      {group.topicTitleVi || 'Chưa cập nhật tên đề tài'}
                    </p>
                  </div>
                  {group.isReviewerInCouncil && (
                    <Badge variant="info" className="text-[10px] shrink-0">
                      GVPB kiêm UV
                    </Badge>
                  )}
                </div>
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-ink-muted">
                  {group.advisorName && <span>GVHD: {group.advisorName}</span>}
                  {group.reviewerName && <span>GVPB: {group.reviewerName}</span>}
                </div>
                {group.students.length > 0 && (
                  <p className="text-[11px] text-ink-secondary truncate">
                    SV:{' '}
                    {group.students
                      .map((s) => `${s.fullName}${s.userCode ? ` (${s.userCode})` : ''}`)
                      .join(', ')}
                  </p>
                )}
              </div>
            ))}
          </div>
        ) : (
          <div className="space-y-1.5">
            {selectedCouncil.assignments.map((assign) => (
              <div
                key={assign.groupCode}
                className="rounded-xl border border-line/70 bg-surface-subtle/40 px-3 py-2 flex items-start gap-2 text-xs"
              >
                <span className="inline-flex items-center rounded-md bg-surface-card border border-line px-1.5 py-0.5 text-[11px] font-semibold text-ink-heading tabular-nums shrink-0">
                  {assign.groupCode}
                </span>
                <span className="text-ink-body line-clamp-2">
                  {assign.topicTitle || 'Đang đồng bộ thông tin đề tài'}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
