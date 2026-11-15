import { RichTextContent } from '@/components/ui/rich-text-content';
import { cn } from '@/lib/utils';
import type { CouncilScheduleGroupDto, CouncilScheduleResponseDto } from '../../services/councilGradingApi';
import type { CouncilOverviewApiItemDto } from '../../types/councilApproval.types';
import type {
  CouncilDiscrepancyResponseDto,
  CouncilMinuteWorkspaceResponseDto,
  MinuteCalculationPreviewDto,
  MinuteDiscrepancySummaryDto,
  MinuteScoreItemDto,
} from '../../types/councilMinute.types';
import {
  formatDefenseDateVi,
  getComponentFullLabel,
  getMinuteLockStatusMeta,
} from '../../utils/councilMinuteFormatting';
import { formatScoreDecimal } from '../../utils/scoreSummaryFormatting';

interface CouncilMinutePrintDocumentProps {
  workspace: CouncilMinuteWorkspaceResponseDto;
  overviewRow: CouncilOverviewApiItemDto | null;
  schedule: CouncilScheduleResponseDto | null;
  currentGroup: CouncilScheduleGroupDto | null;
  discrepancyDetail: CouncilDiscrepancyResponseDto | null;
  scoreItems: MinuteScoreItemDto[];
  discrepancySummary: MinuteDiscrepancySummaryDto;
  calculationPreview: MinuteCalculationPreviewDto;
  studentFeedback: string;
}

/**
 * Monochrome A4 printable official Vietnamese thesis defense council minute.
 * Rendered for both Print Preview mode on screen and `@media print` browser output.
 */
export function CouncilMinutePrintDocument({
  workspace,
  overviewRow,
  schedule,
  currentGroup,
  discrepancyDetail,
  scoreItems,
  discrepancySummary,
  calculationPreview,
  studentFeedback,
}: CouncilMinutePrintDocumentProps) {
  const { assignmentInfo } = workspace;
  const lockMeta = getMinuteLockStatusMeta(assignmentInfo.lockStatus);

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
  const facultyName = overviewRow?.facultyName ?? 'Khoa Công nghệ Thông tin';

  const chairmanName =
    assignmentInfo.chairmanName || overviewRow?.chairman?.fullName || '—';
  const secretaryName =
    assignmentInfo.secretaryName || overviewRow?.secretary?.fullName || '—';

  const students = currentGroup?.students ?? [];

  const memberFeedbackByRole = new Map<string, string>();
  if (discrepancyDetail?.memberScores) {
    for (const member of discrepancyDetail.memberScores) {
      if (!member.feedback) continue;
      if (member.isReviewerInCouncil) {
        memberFeedbackByRole.set('REVIEWER', member.feedback);
      }
      const code =
        member.role === 'Chairman'
          ? 'COUNCIL_CHAIRMAN'
          : member.role === 'Secretary'
            ? 'COUNCIL_SECRETARY'
            : 'COUNCIL_MEMBER';
      if (!member.isBlockedFromScoring) {
        memberFeedbackByRole.set(code, member.feedback);
      }
    }
  }

  const finalScoreText =
    calculationPreview.finalRoundedScore !== null
      ? formatScoreDecimal(calculationPreview.finalRoundedScore, 2)
      : '—';

  const rawAverageText =
    calculationPreview.rawDivisor > 0
      ? formatScoreDecimal(calculationPreview.rawAverage, 2)
      : '—';

  const hasAdjustedDivisor =
    calculationPreview.adjustedDivisor !== calculationPreview.rawDivisor &&
    calculationPreview.rawDivisor > 0;

  return (
    <article
      aria-label="Bản in A4 biên bản chấm điểm hội đồng"
      className={cn(
        'mx-auto w-full max-w-[794px] bg-white text-black',
        'border border-slate-300 shadow-[0_6px_28px_rgba(15,23,42,0.08)]',
        'px-7 py-8 sm:px-12 sm:py-11',
        'print:max-w-none print:border-0 print:shadow-none print:p-0',
      )}
    >
      <div className="space-y-5 text-[13px] leading-relaxed text-black">
        {/* Official Administrative Masthead */}
        <header className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 print:grid-cols-2 gap-4 text-center">
            <div className="space-y-0.5">
              <p className="text-[11px] font-medium uppercase tracking-wide">
                Đại học Quốc gia TP. Hồ Chí Minh
              </p>
              <p className="text-[11.5px] font-bold uppercase">
                Trường Đại học Công nghệ Thông tin
              </p>
              <p className="text-[11px] font-semibold">{facultyName}</p>
              <div className="mx-auto mt-1 h-px w-28 bg-black" aria-hidden="true" />
            </div>

            <div className="space-y-0.5">
              <p className="text-[11.5px] font-bold uppercase">
                Cộng hòa Xã hội Chủ nghĩa Việt Nam
              </p>
              <p className="text-[12px] font-semibold">Độc lập - Tự do - Hạnh phúc</p>
              <div className="mx-auto mt-1 h-px w-36 bg-black" aria-hidden="true" />
              <p className="pt-1.5 text-[11px] italic">
                {defenseDate !== '—'
                  ? `TP. Hồ Chí Minh, ngày bảo vệ ${defenseDate}`
                  : 'TP. Hồ Chí Minh, ngày ..... tháng ..... năm 20....'}
              </p>
            </div>
          </div>

          <div className="border-b border-black pb-3.5 text-center space-y-1">
            <h2 className="text-base sm:text-[17px] font-bold uppercase tracking-tight">
              Biên bản chấm điểm hội đồng bảo vệ khóa luận tốt nghiệp
            </h2>
            <p className="text-xs">
              Hội đồng: <strong className="font-semibold">{assignmentInfo.councilCode}</strong>
              {' · '}
              Nhóm: <strong className="font-semibold">{assignmentInfo.groupCode}</strong>
              {' · '}
              Phòng: <strong className="font-semibold">{room}</strong>
              {' · '}
              Thời gian: {timeSlot} ({defenseDate})
            </p>
            {decisionNumber && (
              <p className="text-[11.5px] text-slate-800 print:text-black">
                Căn cứ Quyết định số <strong className="font-semibold">{decisionNumber}</strong>
                {signedDate !== '—' ? ` ngày ${signedDate}` : ''} về việc thành lập Hội đồng chấm khóa luận tốt nghiệp.
              </p>
            )}
          </div>
        </header>

        {/* Section I: Topic & Participants */}
        <section className="space-y-2.5">
          <h3 className="text-xs font-bold uppercase tracking-wide">
            I. Thông tin đề tài và thành phần tham dự
          </h3>

          <table className="w-full border-collapse border border-slate-700 text-xs">
            <tbody>
              <tr>
                <td className="w-40 border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold align-top">
                  Tên đề tài
                </td>
                <td colSpan={3} className="border border-slate-700 px-3 py-2 align-top">
                  <p className="font-semibold leading-snug">{topicTitleVi}</p>
                  {topicTitleEn && (
                    <p className="mt-0.5 text-[11px] italic text-slate-700 print:text-black">
                      {topicTitleEn}
                    </p>
                  )}
                </td>
              </tr>
              <tr>
                <td className="border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold align-top">
                  Sinh viên thực hiện
                </td>
                <td colSpan={3} className="border border-slate-700 px-3 py-2 align-top">
                  {students.length > 0 ? (
                    <ul className="space-y-0.5">
                      {students.map((st, idx) => (
                        <li key={st.studentId}>
                          {idx + 1}. <span className="font-semibold">{st.fullName}</span>
                          {st.userCode ? (
                            <span className="tabular-nums"> — MSSV: {st.userCode}</span>
                          ) : null}
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <span>
                      Nhóm {assignmentInfo.groupCode} (mã #{assignmentInfo.groupId.slice(0, 8)})
                    </span>
                  )}
                </td>
              </tr>
              <tr>
                <td className="border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold">
                  Chủ tịch Hội đồng
                </td>
                <td className="border border-slate-700 px-3 py-2 font-medium">{chairmanName}</td>
                <td className="w-36 border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold">
                  Thư ký Hội đồng
                </td>
                <td className="border border-slate-700 px-3 py-2 font-medium">{secretaryName}</td>
              </tr>
              {(currentGroup?.advisorName || currentGroup?.reviewerName) && (
                <tr>
                  <td className="border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold">
                    GV hướng dẫn
                  </td>
                  <td className="border border-slate-700 px-3 py-2">
                    {currentGroup?.advisorName || '—'}
                  </td>
                  <td className="border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold">
                    GV phản biện
                  </td>
                  <td className="border border-slate-700 px-3 py-2">
                    {currentGroup?.reviewerName
                      ? `${currentGroup.reviewerName}${
                          currentGroup.isReviewerInCouncil ? ' (kiêm Ủy viên HĐ)' : ''
                        }`
                      : '—'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </section>

        {/* Section II: Score Breakdown Table */}
        <section className="space-y-2">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h3 className="text-xs font-bold uppercase tracking-wide">
              II. Bảng tổng hợp đầu điểm và trọng số áp dụng
            </h3>
            {calculationPreview.configurationVersion && (
              <span className="text-[11px] font-medium">
                Cấu hình công thức điểm: v{calculationPreview.configurationVersion}
              </span>
            )}
          </div>

          <table className="w-full border-collapse border border-slate-700 text-xs">
            <thead>
              <tr className="bg-slate-100 print:bg-slate-100 text-left font-bold">
                <th className="w-11 border border-slate-700 px-2.5 py-2 text-center whitespace-nowrap">
                  STT
                </th>
                <th className="border border-slate-700 px-3 py-2 whitespace-nowrap">
                  Thành phần / Vai trò
                </th>
                <th className="border border-slate-700 px-3 py-2 whitespace-nowrap">
                  Giảng viên chấm
                </th>
                <th className="w-20 border border-slate-700 px-2.5 py-2 text-center whitespace-nowrap">
                  Trọng số
                </th>
                <th className="w-16 border border-slate-700 px-2.5 py-2 text-right whitespace-nowrap">
                  Điểm
                </th>
                <th className="w-36 border border-slate-700 px-2.5 py-2 whitespace-nowrap">
                  Hiệu lực / Ghi chú
                </th>
              </tr>
            </thead>
            <tbody>
              {scoreItems.map((item, idx) => {
                const isAttendOnly =
                  item.isDualRoleReviewer && item.componentCode === 'COUNCIL_MEMBER';
                const isExcluded = item.isIgnoredInCalculation;
                const memberFeedback = memberFeedbackByRole.get(item.componentCode);

                const statusLabel = isExcluded
                  ? 'Loại theo biên bản'
                  : isAttendOnly
                    ? 'Chỉ tham dự (GVPB trong HĐ)'
                    : item.score !== null
                      ? item.statusText || 'Đã chấm'
                      : item.statusText || 'Chưa chấm';

                return (
                  <tr key={`${item.componentCode}-print-${idx}`} className="align-top">
                    <td className="border border-slate-700 px-2.5 py-2 text-center tabular-nums">
                      {idx + 1}
                    </td>
                    <td className="border border-slate-700 px-3 py-2">
                      <p className="font-semibold">
                        {getComponentFullLabel(item.componentCode, item.roleLabel)}
                      </p>
                      <p className="text-[11px] text-slate-700 print:text-black">{item.category}</p>
                    </td>
                    <td className="border border-slate-700 px-3 py-2">
                      <p className="font-medium">{item.lecturerName}</p>
                      {memberFeedback && (
                        <p className="mt-0.5 text-[11px] italic text-slate-700 print:text-black">
                          Nhận xét: “{memberFeedback}”
                        </p>
                      )}
                    </td>
                    <td
                      className={cn(
                        'border border-slate-700 px-2.5 py-2 text-center tabular-nums',
                        isExcluded && 'line-through text-slate-500 print:text-black',
                      )}
                    >
                      {isAttendOnly ? '0' : item.weight}
                    </td>
                    <td
                      className={cn(
                        'border border-slate-700 px-2.5 py-2 text-right tabular-nums font-bold',
                        isExcluded && 'line-through font-normal text-slate-500 print:text-black',
                      )}
                    >
                      {item.score !== null ? formatScoreDecimal(item.score, 2) : '—'}
                    </td>
                    <td className="border border-slate-700 px-2.5 py-2 text-[11px]">
                      <span className={cn(isExcluded && 'font-semibold underline')}>
                        {statusLabel}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </section>

        {/* Section III: Calculation & Discrepancy Resolution */}
        <section className="space-y-2.5 break-inside-avoid">
          <h3 className="text-xs font-bold uppercase tracking-wide">
            III. Kết quả tổng hợp điểm và xử lý sai biệt
          </h3>

          <table className="w-full border-collapse border border-slate-700 text-xs">
            <tbody>
              <tr>
                <td className="w-48 border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold">
                  Tổng điểm có trọng số / Mẫu số
                </td>
                <td className="border border-slate-700 px-3 py-2 tabular-nums">
                  {calculationPreview.adjustedDivisor > 0 ? (
                    <span>
                      {calculationPreview.adjustedTotal.toFixed(2)} /{' '}
                      {calculationPreview.adjustedDivisor} ={' '}
                      {calculationPreview.adjustedAverage.toFixed(6)}
                      {hasAdjustedDivisor
                        ? ` (trước điều chỉnh: ${rawAverageText}, mẫu số ${calculationPreview.rawDivisor})`
                        : ''}
                    </span>
                  ) : (
                    'Chưa đủ dữ liệu tính điểm'
                  )}
                </td>
                <td className="w-48 border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 text-center">
                  <span className="block text-[11px] font-semibold uppercase whitespace-nowrap">
                    Điểm tổng kết chính thức
                  </span>
                  <span className="mt-0.5 block text-lg font-bold tabular-nums">
                    {finalScoreText}
                  </span>
                  <span className="block text-[10px]">(Làm tròn AwayFromZero)</span>
                </td>
              </tr>
              <tr>
                <td className="border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold">
                  Kiểm tra sai biệt điểm
                </td>
                <td colSpan={2} className="border border-slate-700 px-3 py-2">
                  <span>
                    Chênh lệch lớn nhất:{' '}
                    <strong className="tabular-nums">
                      {discrepancySummary.scoreDifference !== null
                        ? `${formatScoreDecimal(discrepancySummary.scoreDifference, 2)} điểm`
                        : '—'}
                    </strong>
                    {' — '}
                    {discrepancySummary.hasDiscrepancy
                      ? 'Phát hiện sai biệt ≥ 2.00 điểm giữa các thành viên chấm.'
                      : 'Trong ngưỡng cho phép (< 2.00 điểm).'}
                  </span>
                </td>
              </tr>
              <tr>
                <td className="border border-slate-700 bg-slate-50 print:bg-white px-3 py-2 font-semibold">
                  Kết luận của Hội đồng
                </td>
                <td colSpan={2} className="border border-slate-700 px-3 py-2">
                  <span>
                    {discrepancySummary.chairmanConclusion ||
                      'Chưa ghi nhận kết luận xử lý chính thức.'}
                  </span>
                  <span className="ml-2 text-[11px]">
                    (Trạng thái biên bản: <strong>{lockMeta.label}</strong>)
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        {/* Section IV: Student Feedback */}
        <section className="space-y-2 break-inside-avoid">
          <h3 className="text-xs font-bold uppercase tracking-wide">
            IV. Yêu cầu chỉnh sửa và góp ý cho sinh viên sau bảo vệ
          </h3>
          <div className="min-h-[68px] border border-slate-700 px-3.5 py-2.5 text-xs">
            {studentFeedback.trim() ? (
              <RichTextContent content={studentFeedback} />
            ) : (
              <p className="italic">
                Chưa ghi nhận yêu cầu chỉnh sửa hoặc góp ý bổ sung cho sinh viên.
              </p>
            )}
          </div>
        </section>

        {/* Section V: Official Handwritten / Verified Signatures */}
        <section className="pt-4 grid grid-cols-2 gap-8 text-center text-xs break-inside-avoid">
          <div className="flex flex-col justify-between min-h-[118px]">
            <div>
              <p className="font-bold uppercase tracking-wide">Thư ký Hội đồng</p>
              <p className="mt-0.5 text-[11px] italic">(Ký và ghi rõ họ tên)</p>
            </div>
            <p className="font-bold">{secretaryName}</p>
          </div>
          <div className="flex flex-col justify-between min-h-[118px]">
            <div>
              <p className="font-bold uppercase tracking-wide">Chủ tịch Hội đồng</p>
              <p className="mt-0.5 text-[11px] italic">(Ký và ghi rõ họ tên)</p>
            </div>
            <p className="font-bold">{chairmanName}</p>
          </div>
        </section>
      </div>
    </article>
  );
}
