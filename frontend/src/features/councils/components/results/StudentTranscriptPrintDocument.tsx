import React from 'react';
import type { StudentScoreSummaryDto } from '../../types/studentResults.types';
import { formatDateTimeVi, formatScoreDecimal } from '../../utils/scoreSummaryFormatting';
import {
  computeWeightSharePercent,
  formatScale4Score,
  getScoreClassificationMeta,
} from '../../utils/studentResults';
import type { StudentResultMemberItem } from './StudentResultsSidebar';

export interface StudentTranscriptPrintDocumentProps {
  summary: StudentScoreSummaryDto;
  topicCode: string;
  groupCode: string;
  topicTitle: string;
  topicTitleEn: string | null;
  batchName: string;
  advisorName: string;
  studentItems: readonly StudentResultMemberItem[];
  feedbackComment: string;
}

/**
 * Monochrome A4 printable official student thesis transcript rendered only during `@media print`.
 */
export const StudentTranscriptPrintDocument: React.FC<StudentTranscriptPrintDocumentProps> = ({
  summary,
  topicCode,
  groupCode,
  topicTitle,
  topicTitleEn,
  batchName,
  advisorName,
  studentItems,
  feedbackComment,
}) => {
  const totalWeight = summary.components.reduce((acc, item) => acc + Number(item.weight || 0), 0);
  const rank = getScoreClassificationMeta(summary.finalScore);

  return (
    <article
      aria-label="Phiếu công bố kết quả khóa luận bản in A4"
      className="hidden print:block bg-white text-black text-[12px] leading-relaxed"
    >
      <header className="border-b-2 border-slate-900 pb-3 mb-5 flex items-start justify-between gap-4">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-700">
            Trường Đại học Công nghệ Thông tin · ĐHQG-HCM
          </p>
          <p className="text-xs font-bold uppercase text-slate-900">
            Hệ thống Quản lý Khóa luận Tốt nghiệp (UGTM)
          </p>
        </div>
        <div className="text-right text-[11px] text-slate-700 tabular-nums">
          <p>Mã đề tài: {topicCode}</p>
          <p>Mã nhóm: {groupCode}</p>
        </div>
      </header>

      <div className="text-center space-y-1 mb-5">
        <h1 className="text-lg font-bold uppercase tracking-tight text-black">
          Phiếu Kết Quả Khóa Luận Tốt Nghiệp
        </h1>
        <p className="text-xs text-slate-700">
          {batchName} · Công bố lúc {formatDateTimeVi(summary.publishedAt)}
        </p>
      </div>

      <section className="mb-5 border border-slate-300 rounded-lg p-3.5 space-y-2">
        <div>
          <p className="text-[11px] font-semibold text-slate-600 uppercase">Đề tài thực hiện</p>
          <p className="text-sm font-bold text-black">{topicTitle}</p>
          {topicTitleEn && <p className="text-xs text-slate-700 italic">{topicTitleEn}</p>}
        </div>
        <div className="grid grid-cols-2 gap-4 pt-2 border-t border-slate-200 text-xs">
          <div>
            <p className="text-[11px] font-semibold text-slate-600">Sinh viên thực hiện</p>
            <ul className="mt-0.5 space-y-0.5">
              {studentItems.map((student) => (
                <li key={student.id} className="font-medium text-black tabular-nums">
                  {student.name}
                  {student.userCode ? ` (${student.userCode})` : ''}
                </li>
              ))}
            </ul>
          </div>
          <div>
            <p className="text-[11px] font-semibold text-slate-600">Giảng viên hướng dẫn</p>
            <p className="mt-0.5 font-medium text-black">{advisorName}</p>
          </div>
        </div>
      </section>

      <section className="mb-5 grid grid-cols-3 gap-3">
        <div className="border border-slate-300 rounded-lg p-3">
          <p className="text-[11px] text-slate-600">Điểm tổng kết (Thang 10)</p>
          <p className="text-2xl font-bold text-black tabular-nums mt-0.5">
            {formatScoreDecimal(summary.finalScore, 2)}
          </p>
        </div>
        <div className="border border-slate-300 rounded-lg p-3">
          <p className="text-[11px] text-slate-600">Quy đổi (Thang 4)</p>
          <p className="text-2xl font-bold text-black tabular-nums mt-0.5">
            {formatScale4Score(summary.finalScore)}
          </p>
        </div>
        <div className="border border-slate-300 rounded-lg p-3">
          <p className="text-[11px] text-slate-600">Xếp loại (Tham khảo)</p>
          <p className="text-lg font-bold text-black mt-1">{rank.label}</p>
        </div>
      </section>

      <section className="mb-5 space-y-2">
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-xs font-bold uppercase text-black">Chi tiết các đầu điểm thành phần</h2>
          {summary.calculationFormula && (
            <span className="text-[11px] text-slate-700 tabular-nums">
              Công thức: {summary.calculationFormula}
            </span>
          )}
        </div>
        <table className="w-full border-collapse border border-slate-300 text-xs">
          <thead>
            <tr className="bg-slate-100 text-left">
              <th className="border border-slate-300 px-2.5 py-2 w-10 text-center">STT</th>
              <th className="border border-slate-300 px-2.5 py-2">Nguồn / Thành phần</th>
              <th className="border border-slate-300 px-2.5 py-2 text-right">Điểm</th>
              <th className="border border-slate-300 px-2.5 py-2 text-right">Hệ số</th>
              <th className="border border-slate-300 px-2.5 py-2 text-right">Tỷ trọng</th>
            </tr>
          </thead>
          <tbody>
            {summary.components.map((component, idx) => (
              <tr key={`${component.componentName}-${idx}`}>
                <td className="border border-slate-300 px-2.5 py-2 text-center tabular-nums">
                  {idx + 1}
                </td>
                <td className="border border-slate-300 px-2.5 py-2 font-medium">
                  {component.componentName}
                </td>
                <td className="border border-slate-300 px-2.5 py-2 text-right font-semibold tabular-nums">
                  {formatScoreDecimal(component.score, 2)}
                </td>
                <td className="border border-slate-300 px-2.5 py-2 text-right tabular-nums">
                  {component.weight}
                </td>
                <td className="border border-slate-300 px-2.5 py-2 text-right tabular-nums">
                  {computeWeightSharePercent(component.weight, totalWeight)}%
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {feedbackComment && (
        <section className="mb-5 border border-slate-300 rounded-lg p-3.5 space-y-1">
          <h2 className="text-xs font-bold uppercase text-black">Góp ý sau bảo vệ</h2>
          <p className="text-xs text-slate-800 whitespace-pre-line">{feedbackComment}</p>
        </section>
      )}

      <footer className="pt-3 border-t border-slate-300 text-[11px] text-slate-600 flex items-center justify-between">
        <span>Tài liệu trích xuất từ hệ thống UGTM — Dành cho sinh viên đối chiếu.</span>
        <span className="tabular-nums">Đã công bố: {formatDateTimeVi(summary.publishedAt)}</span>
      </footer>
    </article>
  );
};
