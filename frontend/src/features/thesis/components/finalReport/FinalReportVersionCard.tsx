import React from 'react';
import { Send, MessageSquareQuote, ExternalLink } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { FinalReportHistoryItemDto } from '../../types/finalReport.types';

interface FinalReportVersionCardProps {
  latestVersion: FinalReportHistoryItemDto | null;
  currentStatus: string;
  hasSelectedFile: boolean;
  isSubmitting: boolean;
  canSubmit: boolean;
  onSubmit: () => void;
  onOpenFeedback: () => void;
  onDownloadFile: (fileId: string) => void;
}

export const FinalReportVersionCard: React.FC<FinalReportVersionCardProps> = ({
  latestVersion,
  currentStatus,
  hasSelectedFile,
  isSubmitting,
  canSubmit,
  onSubmit,
  onOpenFeedback,
  onDownloadFile,
}) => {
  const versionLabel = latestVersion
    ? `v${latestVersion.versionNo} · ${latestVersion.file.fileName}`
    : 'Chưa có phiên bản nào được nộp';

  const statusLabel =
    currentStatus === 'Approved'
      ? 'Đã duyệt chuyển phản biện'
      : currentStatus === 'UnderEvaluation'
        ? 'Đang chấm điểm'
        : currentStatus === 'Submitted'
          ? 'Đã nộp'
          : 'Bản nháp';

  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs space-y-2">
        <h3 className="text-sm font-bold text-slate-800">Phiên bản hồ sơ</h3>
        <p className="text-sm font-semibold text-slate-700">{versionLabel}</p>
        <div className="flex items-center gap-2 text-xs text-slate-500">
          <span>{statusLabel}</span>
          {latestVersion && (
            <>
              <span>·</span>
              <button
                type="button"
                onClick={() => onDownloadFile(latestVersion.file.id)}
                className="font-medium text-blue-600 hover:text-blue-700 hover:underline inline-flex items-center gap-1"
              >
                <span>Xem trước</span>
                <ExternalLink className="size-3" />
              </button>
            </>
          )}
        </div>
      </div>

      <div className="flex items-center gap-3">
        <Button
          type="button"
          onClick={onSubmit}
          disabled={!canSubmit || !hasSelectedFile || isSubmitting}
          isLoading={isSubmitting}
          loadingText="Đang nộp báo cáo…"
          className="bg-[#2054A5] hover:bg-[#1a4485] text-white font-semibold px-5 h-10 shadow-xs"
        >
          <Send className="size-4 mr-2" />
          Nộp cuối kỳ
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={onOpenFeedback}
          className="font-semibold text-slate-700 border-slate-200 bg-white hover:bg-slate-50 h-10 px-4"
        >
          <MessageSquareQuote className="size-4 mr-2 text-slate-500" />
          Xem góp ý
        </Button>
      </div>
    </div>
  );
};