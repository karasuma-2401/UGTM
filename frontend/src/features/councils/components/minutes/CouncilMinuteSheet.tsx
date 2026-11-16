import {
  Ban,
  Calendar,
  Clock,
  FileCheck2,
  Lock,
  MapPin,
  RotateCcw,
  ShieldAlert,
  Users,
} from 'lucide-react';
import { Avatar } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { RichTextContent } from '@/components/ui/rich-text-content';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { StatusBadge } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type { CouncilScheduleGroupDto, CouncilScheduleResponseDto } from '../../services/councilGradingApi';
import type { CouncilOverviewApiItemDto } from '../../types/councilApproval.types';
import type {
  CouncilDiscrepancyResponseDto,
  CouncilMinuteWorkspaceResponseDto,
  MinuteCalculationPreviewDto,
  MinuteDiscrepancySummaryDto,
  MinuteScoreItemDto,
  SecretaryMarking,
} from '../../types/councilMinute.types';
import {
  formatDefenseDateVi,
  getComponentFullLabel,
} from '../../utils/councilMinuteFormatting';
import { formatDateTimeVi, formatScoreDecimal } from '../../utils/scoreSummaryFormatting';
import { CouncilMinutePrintDocument } from './CouncilMinutePrintDocument';

interface CouncilMinuteSheetProps {
  workspace: CouncilMinuteWorkspaceResponseDto;
  overviewRow: CouncilOverviewApiItemDto | null;
  schedule: CouncilScheduleResponseDto | null;
  currentGroup: CouncilScheduleGroupDto | null;
  discrepancyDetail: CouncilDiscrepancyResponseDto | null;
  scoreItems: MinuteScoreItemDto[];
  discrepancySummary: MinuteDiscrepancySummaryDto;
  calculationPreview: MinuteCalculationPreviewDto;
  selectableTargets: MinuteScoreItemDto[];
  secretaryMarking: SecretaryMarking;
  targetComponentCode: string;
  studentFeedback: string;
  exclusionReason: string;
  requiresExclusionReason: boolean;
  canEditMinute: boolean;
  isLocked: boolean;
  isPrintPreview: boolean;
  isPreviewing: boolean;
  onChangeMarking: (marking: SecretaryMarking) => void;
  onChangeTargetComponent: (componentCode: string) => void;
  onChangeStudentFeedback: (value: string) => void;
  onChangeExclusionReason: (value: string) => void;
}

const MARKING_OPTIONS: Array<{
  value: SecretaryMarking;
  label: string;
  hint: string;
}> = [
  {
    value: 'None',
    label: 'Chưa quyết định',
    hint: 'Giữ trạng thái chờ Hội đồng thảo luận',
  },
  {
    value: 'AcceptedAsPerMinute',
    label: 'Giữ nguyên tất cả',
    hint: 'Công nhận toàn bộ đầu điểm hiện có',
  },
  {
    value: 'RejectedAsPerMinute',
    label: 'Loại một đầu điểm',
    hint: 'Loại khỏi tử số và mẫu số công thức',
  },
];

export function CouncilMinuteSheet({
  workspace,
  overviewRow,
  schedule,
  currentGroup,
  discrepancyDetail,
  scoreItems,
  discrepancySummary,
  calculationPreview,
  selectableTargets,
  secretaryMarking,
  targetComponentCode,
  studentFeedback,
  exclusionReason,
  requiresExclusionReason,
  canEditMinute,
  isLocked,
  isPrintPreview,
  isPreviewing,
  onChangeMarking,
  onChangeTargetComponent,
  onChangeStudentFeedback,
  onChangeExclusionReason,
}: CouncilMinuteSheetProps) {
  const { assignmentInfo } = workspace;

  const topicTitleVi =
    currentGroup?.topicTitleVi ??
    overviewRow?.topicTitle ??
    'Đề tài khóa luận tốt nghiệp';
  const topicTitleEn = currentGroup?.topicTitleEn ?? null;
  const defenseDate = formatDefenseDateVi(schedule?.defenseDate ?? overviewRow?.defenseDate);
  const timeSlot = schedule?.timeSlot ?? overviewRow?.timeSlot ?? '—';
  const room = schedule?.room ?? overviewRow?.room ?? '—';
  const decisionNumber = schedule?.decisionNumber ?? null;
  const signedDate = formatDefenseDateVi(schedule?.signedDate);

  const students = currentGroup?.students ?? [];
  const memberFeedbackByRole = new Map<string, { feedback?: string | null; evaluatedAt?: string | null }>();
  if (discrepancyDetail?.memberScores) {
    for (const member of discrepancyDetail.memberScores) {
      if (member.isReviewerInCouncil) {
        memberFeedbackByRole.set('REVIEWER', {
          feedback: member.feedback,
          evaluatedAt: member.evaluatedAt,
        });
      }
      const code =
        member.role === 'Chairman'
          ? 'COUNCIL_CHAIRMAN'
          : member.role === 'Secretary'
            ? 'COUNCIL_SECRETARY'
            : 'COUNCIL_MEMBER';
      if (!member.isBlockedFromScoring) {
        memberFeedbackByRole.set(code, {
          feedback: member.feedback,
          evaluatedAt: member.evaluatedAt,
        });
      }
    }
  }

  const trimmedReasonLen = exclusionReason.trim().length;
  const isReasonLengthValid = trimmedReasonLen >= 10 && trimmedReasonLen <= 1000;

  const councilMetaRows: Array<{
    key: string;
    roleLabel: string;
    name: string | null | undefined;
    suffix?: string;
    emphasize?: boolean;
  }> = [
    {
      key: 'chairman',
      roleLabel: 'Chủ tịch Hội đồng:',
      name: assignmentInfo.chairmanName || overviewRow?.chairman?.fullName,
      emphasize: true,
    },
    {
      key: 'secretary',
      roleLabel: 'Thư ký Hội đồng:',
      name: assignmentInfo.secretaryName || overviewRow?.secretary?.fullName,
      emphasize: true,
    },
    ...(currentGroup?.advisorName
      ? [
          {
            key: 'advisor',
            roleLabel: 'Giảng viên hướng dẫn:',
            name: currentGroup.advisorName,
          },
        ]
      : []),
    ...(currentGroup?.reviewerName
      ? [
          {
            key: 'reviewer',
            roleLabel: 'Giảng viên phản biện:',
            name: currentGroup.reviewerName,
            suffix: currentGroup.isReviewerInCouncil ? ' (kiêm UV HĐ)' : undefined,
          },
        ]
      : []),
  ];

  const printDocument = (
    <CouncilMinutePrintDocument
      workspace={workspace}
      overviewRow={overviewRow}
      schedule={schedule}
      currentGroup={currentGroup}
      discrepancyDetail={discrepancyDetail}
      scoreItems={scoreItems}
      discrepancySummary={discrepancySummary}
      calculationPreview={calculationPreview}
      studentFeedback={studentFeedback}
    />
  );

  if (isPrintPreview) {
    return printDocument;
  }

  return (
    <>
      <article className="relative overflow-hidden rounded-2xl border border-slate-200/90 bg-white shadow-[0_4px_20px_-4px_rgba(15,23,42,0.05)] print:hidden">
      {/* Brand top accent rule matching DESIGN.md */}
      <div
        aria-hidden="true"
        className="h-1 bg-[linear-gradient(90deg,#2054a5_0%,#2563eb_55%,#fa7921_100%)] print:hidden"
      />

      <div className="p-5 sm:p-7 space-y-6">
        {/* Administrative Minute Header */}
        <header className="border-b border-slate-200/80 pb-5">
          <div className="space-y-1.5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-xs font-medium text-slate-500">
                Trường Đại học Công nghệ Thông tin · ĐHQG-HCM — {overviewRow?.facultyName ?? 'Khoa CNTT'}
              </p>
              {isLocked && (
                <span className="inline-flex items-center gap-1 rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 text-[11px] font-medium text-slate-600 print:hidden">
                  <Lock className="size-3" aria-hidden="true" />
                  Chế độ chỉ đọc
                </span>
              )}
            </div>
            <h2 className="text-lg sm:text-xl font-bold tracking-tight text-slate-900">
              Biên bản chấm điểm hội đồng bảo vệ khóa luận
            </h2>
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 pt-1 text-xs text-slate-600">
              <span className="font-semibold text-slate-800">
                Hội đồng: {assignmentInfo.councilCode} · Nhóm {assignmentInfo.groupCode}
              </span>
              {decisionNumber && (
                <span>
                  QĐ: <strong className="font-semibold text-slate-800">{decisionNumber}</strong>
                  {signedDate !== '—' ? ` (${signedDate})` : ''}
                </span>
              )}
              <span className="inline-flex items-center gap-1">
                <Calendar className="size-3.5 text-slate-400" aria-hidden="true" />
                {defenseDate}
              </span>
              <span className="inline-flex items-center gap-1">
                <Clock className="size-3.5 text-slate-400" aria-hidden="true" />
                {timeSlot}
              </span>
              <span className="inline-flex items-center gap-1">
                <MapPin className="size-3.5 text-slate-400" aria-hidden="true" />
                Phòng {room}
              </span>
            </div>
          </div>
        </header>

        {/* Topic, Students và Council Composition Metadata */}
        <section
          aria-label="Thông tin đề tài và thành phần hội đồng"
          className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 sm:p-5 space-y-4"
        >
          <div>
            <h3 className="text-sm sm:text-base font-semibold text-slate-900 leading-snug">
              {topicTitleVi}
            </h3>
            {topicTitleEn && (
              <p className="mt-0.5 text-xs italic text-slate-500">{topicTitleEn}</p>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)] gap-4 border-t border-slate-200/70 pt-3.5 text-xs">
            <div className="space-y-2">
              <p className="font-semibold text-slate-600 flex items-center gap-1.5">
                <Users className="size-3.5 text-slate-400" aria-hidden="true" />
                Sinh viên thực hiện ({students.length > 0 ? students.length : assignmentInfo.groupCode})
              </p>
              {students.length > 0 ? (
                <ul className="space-y-1.5">
                  {students.map((st) => (
                    <li key={st.studentId} className="flex items-center gap-2 text-slate-700">
                      <Avatar name={st.fullName} className="size-6 shrink-0 text-[10px]" />
                      <span className="font-medium text-slate-800">{st.fullName}</span>
                      {st.userCode && (
                        <span className="tabular-nums text-slate-500">({st.userCode})</span>
                      )}
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-slate-600">
                  Nhóm {assignmentInfo.groupCode} (mã nhóm #{assignmentInfo.groupId.slice(0, 8)})
                </p>
              )}
            </div>

            <div className="space-y-1.5 text-slate-600">
              {councilMetaRows.map((row) => (
                <div
                  key={row.key}
                  className="grid grid-cols-[136px_minmax(0,1fr)] items-center gap-x-2"
                >
                  <span className="text-slate-500">{row.roleLabel}</span>
                  <div className="flex items-center gap-2 min-w-0">
                    {row.name && (
                      <Avatar name={row.name} className="size-6 shrink-0 text-[10px]" />
                    )}
                    <span
                      className={cn(
                        'leading-snug',
                        row.emphasize ? 'font-semibold text-slate-900' : 'font-medium text-slate-800',
                      )}
                    >
                      {row.name || '—'}
                      {row.suffix ?? ''}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Score Breakdown Table */}
        <section aria-label="Bảng tổng hợp các đầu điểm thành phần" className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="text-sm font-semibold text-slate-900">
              Bảng tổng hợp đầu điểm &amp; Trọng số áp dụng
            </h3>
            {calculationPreview.configurationVersion && (
              <span className="inline-flex items-center gap-1.5 rounded-full border border-blue-200/80 bg-blue-50/70 px-2.5 py-0.5 text-xs font-medium text-blue-700 tabular-nums">
                <FileCheck2 className="size-3.5" aria-hidden="true" />
                Cấu hình điểm v{calculationPreview.configurationVersion}
              </span>
            )}
          </div>

          {/* Mobile (<md) clean stacked score cards so zero horizontal clipping */}
          <div className="divide-y divide-slate-100 rounded-xl border border-slate-200/80 bg-white md:hidden print:hidden">
            {scoreItems.map((item, idx) => {
              const isAttendOnly = item.isDualRoleReviewer && item.componentCode === 'COUNCIL_MEMBER';
              const isExcluded = item.isIgnoredInCalculation;
              const canExcludeThis =
                canEditMinute &&
                selectableTargets.some((t) => t.componentCode === item.componentCode);
              const isSelectedTarget =
                secretaryMarking === 'RejectedAsPerMinute' &&
                targetComponentCode === item.componentCode;
              const memberMeta = memberFeedbackByRole.get(item.componentCode);

              return (
                <div
                  key={`mobile-${item.componentCode}-${idx}`}
                  className={cn('p-3.5 space-y-2', isExcluded && 'bg-rose-50/45')}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="text-xs font-semibold text-slate-900">
                        {getComponentFullLabel(item.componentCode, item.roleLabel)}
                      </p>
                      <p className="text-[11px] text-slate-500">
                        {item.lecturerName}
                        {item.isDualRoleReviewer && item.componentCode === 'REVIEWER'
                          ? ' · GVPB trong HĐ'
                          : ''}
                      </p>
                    </div>
                    <div className="text-right shrink-0">
                      <div
                        className={cn(
                          'text-sm font-bold tabular-nums',
                          isExcluded
                            ? 'line-through text-slate-400'
                            : item.score !== null
                              ? 'text-slate-900'
                              : 'text-slate-400',
                        )}
                      >
                        {item.score !== null ? formatScoreDecimal(item.score, 2) : '—'}
                      </div>
                      <div className="text-[11px] text-slate-500 tabular-nums">
                        TS: {isAttendOnly ? '0' : item.weight}
                      </div>
                    </div>
                  </div>

                  {memberMeta?.feedback && (
                    <p className="text-xs text-slate-500 line-clamp-2">“{memberMeta.feedback}”</p>
                  )}

                  <div className="flex flex-wrap items-center justify-between gap-2 pt-0.5">
                    {isExcluded ? (
                      <StatusBadge tone="error">Loại theo biên bản</StatusBadge>
                    ) : isAttendOnly ? (
                      <StatusBadge tone="neutral">Chỉ tham dự</StatusBadge>
                    ) : item.score !== null ? (
                      <StatusBadge tone="success">{item.statusText || 'Đã chấm'}</StatusBadge>
                    ) : (
                      <StatusBadge tone="warning">{item.statusText || 'Chưa chấm'}</StatusBadge>
                    )}

                    {canEditMinute && !isPrintPreview && canExcludeThis && (
                      isSelectedTarget || isExcluded ? (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => onChangeMarking('AcceptedAsPerMinute')}
                          disabled={isPreviewing}
                          className="h-7 rounded-lg px-2 text-xs border-slate-200 text-slate-700"
                          leftIcon={<RotateCcw className="size-3" />}
                        >
                          Khôi phục
                        </Button>
                      ) : (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => onChangeTargetComponent(item.componentCode)}
                          disabled={isPreviewing}
                          className="h-7 rounded-lg px-2 text-xs border-rose-200 text-rose-700"
                          leftIcon={<Ban className="size-3" />}
                        >
                          Loại điểm
                        </Button>
                      )
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Desktop (>=md) compact 6-column table fitted cleanly inside left sheet */}
          <div className="hidden md:block print:block overflow-x-auto rounded-xl border border-slate-200/80">
            <table className="w-full border-collapse text-left text-xs">
              <thead>
                <tr className="border-b border-slate-200/80 bg-slate-50/90 text-[11px] font-semibold text-slate-600">
                  <th className="px-3 py-2.5 whitespace-nowrap">Thành phần</th>
                  <th className="px-3.5 py-2.5">Giảng viên</th>
                  <th className="w-20 px-3 py-2.5 whitespace-nowrap text-center">Trọng số</th>
                  <th className="w-20 px-3.5 py-2.5 whitespace-nowrap text-right">Điểm</th>
                  <th className="w-28 pl-4 pr-3 py-2.5 whitespace-nowrap">Hiệu lực</th>
                  {canEditMinute && !isPrintPreview && (
                    <th className="px-3 py-2.5 whitespace-nowrap text-right w-24 print:hidden">
                      Điều chỉnh
                    </th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {scoreItems.map((item, idx) => {
                  const isAttendOnly = item.isDualRoleReviewer && item.componentCode === 'COUNCIL_MEMBER';
                  const isExcluded = item.isIgnoredInCalculation;
                  const canExcludeThis =
                    canEditMinute &&
                    selectableTargets.some((t) => t.componentCode === item.componentCode);
                  const isSelectedTarget =
                    secretaryMarking === 'RejectedAsPerMinute' &&
                    targetComponentCode === item.componentCode;
                  const memberMeta = memberFeedbackByRole.get(item.componentCode);
                  const hasMemberMeta = Boolean(memberMeta?.feedback || memberMeta?.evaluatedAt);

                  return (
                    <tr
                      key={`${item.componentCode}-${idx}`}
                      className={cn(
                        'transition-colors',
                        isExcluded ? 'bg-rose-50/45' : 'hover:bg-slate-50/70',
                      )}
                    >
                      <td className="px-3 py-2.5 align-top">
                        <div className="font-semibold text-slate-900 leading-snug">
                          {getComponentFullLabel(item.componentCode, item.roleLabel)}
                        </div>
                        <div className="mt-0.5 text-[11px] text-slate-500">
                          {item.category}
                          {item.isDualRoleReviewer && item.componentCode === 'REVIEWER'
                            ? ' · GVPB trong HĐ'
                            : ''}
                        </div>
                      </td>

                      <td
                        className={cn(
                          'px-3.5 py-2.5',
                          hasMemberMeta ? 'align-top' : 'align-middle',
                        )}
                      >
                        <div
                          className={cn(
                            'flex gap-2',
                            hasMemberMeta ? 'items-start' : 'items-center',
                          )}
                        >
                          <Avatar name={item.lecturerName} className="size-6 text-[10px] shrink-0" />
                          <div className="min-w-0">
                            <div className="font-medium text-slate-800 leading-6">
                              {item.lecturerName}
                            </div>
                            {memberMeta?.feedback && (
                              <p className="mt-0.5 text-[11px] text-slate-500 line-clamp-2 max-w-[280px]">
                                “{memberMeta.feedback}”
                              </p>
                            )}
                            {memberMeta?.evaluatedAt && (
                              <p className="mt-0.5 text-[10px] text-slate-400">
                                Chấm lúc {formatDateTimeVi(memberMeta.evaluatedAt)}
                              </p>
                            )}
                          </div>
                        </div>
                      </td>

                      <td
                        className={cn(
                          'px-3 py-2.5 align-top text-center tabular-nums font-medium leading-6',
                          isExcluded ? 'line-through text-slate-400' : 'text-slate-700',
                        )}
                      >
                        {isAttendOnly ? '0' : item.weight}
                      </td>

                      <td
                        className={cn(
                          'px-3.5 py-2.5 align-top text-right tabular-nums text-sm font-bold leading-6',
                          isExcluded
                            ? 'line-through text-slate-400'
                            : item.score !== null
                              ? 'text-slate-900'
                              : 'text-slate-400',
                        )}
                      >
                        {item.score !== null ? formatScoreDecimal(item.score, 2) : '—'}
                      </td>

                      <td className="pl-4 pr-3 py-2.5 align-top whitespace-nowrap">
                        {isExcluded ? (
                          <StatusBadge tone="error">Loại điểm</StatusBadge>
                        ) : isAttendOnly ? (
                          <StatusBadge tone="neutral">Chỉ tham dự</StatusBadge>
                        ) : item.score !== null ? (
                          <StatusBadge tone="success">{item.statusText || 'Đã chấm'}</StatusBadge>
                        ) : (
                          <StatusBadge tone="warning">{item.statusText || 'Chưa chấm'}</StatusBadge>
                        )}
                      </td>

                      {canEditMinute && !isPrintPreview && (
                        <td className="px-3 py-2.5 align-top text-right whitespace-nowrap print:hidden">
                          {canExcludeThis ? (
                            isSelectedTarget || isExcluded ? (
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => onChangeMarking('AcceptedAsPerMinute')}
                                disabled={isPreviewing}
                                className="h-7 rounded-lg px-2 text-[11px] border-slate-200 text-slate-700 hover:bg-slate-100"
                                leftIcon={<RotateCcw className="size-3" />}
                              >
                                Khôi phục
                              </Button>
                            ) : (
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => onChangeTargetComponent(item.componentCode)}
                                disabled={isPreviewing}
                                className="h-7 rounded-lg px-2 text-[11px] border-rose-200 text-rose-700 hover:bg-rose-50"
                                leftIcon={<Ban className="size-3" />}
                              >
                                Loại điểm
                              </Button>
                            )
                          ) : (
                            <span className="text-xs text-slate-400">—</span>
                          )}
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>

        {/* Discrepancy Analysis và Secretary Decision */}
        <section
          aria-label="Phân tích sai biệt điểm và kết luận hội đồng"
          className="border-t border-slate-200/80 pt-5 space-y-3.5"
        >
          <h3 className="text-sm font-semibold text-slate-900">
            Đối soát sai biệt điểm &amp; Kết luận của Hội đồng
          </h3>

          <div className="divide-y divide-slate-200/75 rounded-xl border border-slate-200/80 bg-slate-50/55 text-xs">
            <div className="flex flex-wrap items-center justify-between gap-2 px-3.5 py-2.5">
              <span className="font-medium text-slate-500">Độ lệch điểm tối đa</span>
              {discrepancySummary.scoreDifference !== null ? (
                <div className="flex flex-wrap items-center gap-1.5">
                  <strong className="font-semibold tabular-nums text-slate-900">
                    {formatScoreDecimal(discrepancySummary.scoreDifference, 2)} điểm
                  </strong>
                  <span className="text-slate-300" aria-hidden="true">
                    ·
                  </span>
                  <span
                    className={cn(
                      'font-semibold',
                      discrepancySummary.hasDiscrepancy ? 'text-amber-700' : 'text-emerald-700',
                    )}
                  >
                    {discrepancySummary.hasDiscrepancy
                      ? 'Vượt ngưỡng 2.00 — cần xác nhận'
                      : 'Trong ngưỡng cho phép (< 2.00)'}
                  </span>
                </div>
              ) : (
                <span className="text-slate-400">Chưa đủ đầu điểm để đối soát</span>
              )}
            </div>

            <div className="flex flex-col sm:flex-row sm:items-baseline sm:justify-between gap-1.5 sm:gap-4 px-3.5 py-2.5">
              <span className="font-medium text-slate-500 shrink-0">Kết luận áp dụng</span>
              <span className="font-medium text-slate-800 sm:text-right leading-relaxed">
                {(discrepancySummary.chairmanConclusion ?? '')
                  .replace(/>=/g, '≥')
                  .replace(/<=/g, '≤')
                  .trim() || 'Chưa có kết luận chính thức.'}
              </span>
            </div>
          </div>

          {/* Interactive Secretary Marking controls (hidden in print preview or read-only) */}
          {canEditMinute && !isPrintPreview && (
            <div className="space-y-3 print:hidden">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5" role="radiogroup" aria-label="Đánh dấu xử lý sai biệt của Thư ký">
                {MARKING_OPTIONS.map((option) => {
                  const selected = secretaryMarking === option.value;
                  return (
                    <button
                      key={option.value}
                      type="button"
                      role="radio"
                      aria-checked={selected}
                      disabled={isPreviewing}
                      onClick={() => onChangeMarking(option.value)}
                      className={cn(
                        'rounded-xl border px-3.5 py-2.5 text-left transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/30',
                        selected
                          ? 'border-blue-500 bg-blue-50/80 text-blue-950 shadow-2xs'
                          : 'border-slate-200/90 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50/70',
                      )}
                    >
                      <div className="text-xs font-semibold flex items-center justify-between gap-2">
                        <span>{option.label}</span>
                        <span
                          className={cn(
                            'size-3.5 rounded-full border flex items-center justify-center',
                            selected ? 'border-blue-600 bg-blue-600' : 'border-slate-300',
                          )}
                          aria-hidden="true"
                        >
                          {selected && <span className="size-1.5 rounded-full bg-white" />}
                        </span>
                      </div>
                      <p className="mt-1 text-[11px] text-slate-500 leading-snug">{option.hint}</p>
                    </button>
                  );
                })}
              </div>

              {secretaryMarking === 'RejectedAsPerMinute' && (
                <div className="rounded-xl border border-rose-200/80 bg-rose-50/40 p-3.5 space-y-2.5">
                  <div className="flex items-center justify-between gap-2">
                    <label className="text-xs font-semibold text-rose-900">
                      Chọn đầu điểm không công nhận theo biên bản <span className="text-rose-600">*</span>
                    </label>
                    <span className="text-[11px] text-rose-700">
                      Chỉ chọn đầu điểm đã chấm hợp lệ
                    </span>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {selectableTargets.map((item) => {
                      const active = targetComponentCode === item.componentCode;
                      return (
                        <button
                          key={item.componentCode}
                          type="button"
                          onClick={() => onChangeTargetComponent(item.componentCode)}
                          className={cn(
                            'inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors',
                            active
                              ? 'border-rose-600 bg-rose-600 text-white shadow-2xs'
                              : 'border-rose-200 bg-white text-rose-800 hover:bg-rose-50',
                          )}
                        >
                          <span>{item.roleLabel}</span>
                          <span className="tabular-nums opacity-90">
                            ({formatScoreDecimal(item.score, 2)})
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}

              {requiresExclusionReason && (
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between gap-2">
                    <label
                      htmlFor="minute-exclusion-reason"
                      className="text-xs font-semibold text-slate-800"
                    >
                      Lý do nội bộ (lưu kiểm toán &amp; nguồn điểm bị loại){' '}
                      <span className="text-rose-600">*</span>
                    </label>
                    <span
                      className={cn(
                        'text-[11px] tabular-nums font-medium',
                        isReasonLengthValid ? 'text-emerald-700' : 'text-amber-700',
                      )}
                    >
                      {trimmedReasonLen}/1000 ký tự (tối thiểu 10)
                    </span>
                  </div>
                  <textarea
                    id="minute-exclusion-reason"
                    rows={2}
                    value={exclusionReason}
                    onChange={(e) => onChangeExclusionReason(e.target.value)}
                    placeholder="Ví dụ: Hội đồng thống nhất không công nhận điểm Ủy viên do đánh giá lệch tiêu chí thực nghiệm của nhóm..."
                    className="w-full rounded-xl border border-slate-200/90 bg-white px-3.5 py-2.5 text-xs sm:text-sm text-slate-800 placeholder:text-slate-400 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/15"
                  />
                  <p className="text-[11px] text-slate-500 flex items-center gap-1">
                    <ShieldAlert className="size-3.5 text-slate-400 shrink-0" aria-hidden="true" />
                    Lý do này được ghi vào AuditLog và nguồn điểm bị loại; không hiển thị thay thế góp ý sinh viên.
                  </p>
                </div>
              )}
            </div>
          )}
        </section>

        {/* Student Feedback và Required Revisions */}
        <section
          aria-label="Góp ý chỉnh sửa gửi sinh viên"
          className="border-t border-slate-200/80 pt-5 space-y-2.5"
        >
          <div className="flex flex-wrap items-center justify-between gap-2">
            <label
              id="minute-student-feedback-label"
              htmlFor="minute-student-feedback"
              className="text-sm font-semibold text-slate-900"
            >
              Yêu cầu chỉnh sửa &amp; Góp ý cho sinh viên sau bảo vệ
            </label>
            <span className="text-xs text-slate-500">
              Sinh viên căn cứ mục này để hoàn thiện báo cáo nộp lưu chiểu
            </span>
          </div>

          {canEditMinute && !isPrintPreview ? (
            <RichTextEditor
              id="minute-student-feedback"
              ariaLabelledBy="minute-student-feedback-label"
              value={studentFeedback}
              onChange={onChangeStudentFeedback}
              maxLength={2000}
              placeholder="Ghi rõ các nội dung sinh viên cần bổ sung, chỉnh sửa trong cuốn báo cáo toàn văn và sản phẩm trước khi nộp lưu chiểu..."
              expandedTitle={`Góp ý biên bản · ${assignmentInfo.councilCode} (${assignmentInfo.groupCode})`}
              expandedSubtitle={topicTitleVi}
            />
          ) : (
            <div className="rounded-xl border border-slate-200/80 bg-slate-50/50 p-4 text-sm text-slate-800">
              {studentFeedback.trim() ? (
                <RichTextContent content={studentFeedback} />
              ) : (
                <p className="text-xs italic text-slate-500">
                  Chưa ghi nhận yêu cầu chỉnh sửa hoặc góp ý bổ sung cho sinh viên.
                </p>
              )}
            </div>
          )}
        </section>
      </div>
    </article>

      <div className="hidden print:block">{printDocument}</div>
    </>
  );
}
