import type { FinalReportEvaluationStartedDto } from '../types/finalReport.types';

interface AdvisorReportSnapshot {
  finalReportId?: string | null;
  reportVersionNo?: number | null;
  reportVersionType?: string | null;
  reportStatus?: string | null;
}

// Only the version loaded in the grading workspace may be locked and scored.
export async function prepareAdvisorReport(
  report: AdvisorReportSnapshot,
  beginEvaluation: (id: string, version: number) => Promise<FinalReportEvaluationStartedDto>,
): Promise<{ finalReportId: string; reportVersionNo: number; reportStatus: 'UnderEvaluation' }> {
  const { finalReportId, reportVersionNo, reportVersionType, reportStatus } = report;
  if (!finalReportId || !reportVersionNo || !Number.isInteger(reportVersionNo) || reportVersionNo < 1 || reportVersionType !== 'InitialFinal') {
    throw new Error('Chưa tải được thông tin báo cáo cuối kỳ ban đầu. Vui lòng tải lại dữ liệu trước khi chấm.');
  }
  if (reportStatus === 'Submitted') {
    const started = await beginEvaluation(finalReportId, reportVersionNo);
    if (started.finalReportId !== finalReportId || started.versionNo !== reportVersionNo ||
      started.versionType !== 'InitialFinal' || started.status !== 'UnderEvaluation') {
      throw new Error('Phiên bản báo cáo đã thay đổi. Vui lòng tải lại dữ liệu trước khi lưu điểm.');
    }
  } else if (reportStatus !== 'UnderEvaluation') {
    throw new Error('Báo cáo hiện không ở trạng thái cho phép GVHD chấm. Vui lòng tải lại dữ liệu.');
  }
  return { finalReportId, reportVersionNo, reportStatus: 'UnderEvaluation' };
}
