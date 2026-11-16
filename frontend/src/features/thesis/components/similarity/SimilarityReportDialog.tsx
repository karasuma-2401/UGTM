import React from 'react';
import { ChevronLeft, ChevronRight, X } from 'lucide-react';
import { AvatarGroup } from '@/components/ui/avatar-group';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatDateTimeVi } from '@/features/councils/utils/scoreSummaryFormatting';
import type { SimilarityRowView } from '../../utils/similarity';
import { SimilarityReportPanel } from '../SimilarityReportPanel';

interface SimilarityReportDialogProps {
  open: boolean;
  inspectedRow: SimilarityRowView | null;
  inspectedIndex: number;
  filteredTotalCount: number;
  onClose: () => void;
  onNavigate: (delta: -1 | 1) => void;
}

export const SimilarityReportDialog: React.FC<SimilarityReportDialogProps> = ({
  open,
  inspectedRow,
  inspectedIndex,
  filteredTotalCount,
  onClose,
  onNavigate,
}) => {
  if (!open || !inspectedRow) {
    return null;
  }

  const canGoPrev = inspectedIndex > 0;
  const canGoNext = inspectedIndex >= 0 && inspectedIndex < filteredTotalCount - 1;

  const studentItems = (inspectedRow.students ?? []).map((s) => ({
    id: s.id,
    name: s.fullName,
    code: s.userCode,
  }));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      closeOnBackdropClick
      hideHeader
      closeLabel="Đóng kết quả tương đồng"
      className="max-w-6xl w-[calc(100vw-1.5rem)] sm:w-[calc(100vw-3rem)] max-h-[92vh] flex flex-col overflow-hidden rounded-2xl bg-surface-card p-0 shadow-2xl border border-line/80"
    >
      <div className="shrink-0 border-b border-line/80 bg-surface-card px-5 py-4 sm:px-6 space-y-2.5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="min-w-0 flex flex-wrap items-center gap-2">
            <span className="text-sm font-bold text-primary tabular-nums">
              {inspectedRow.groupCode}
            </span>
            <Badge variant="outline" className="text-[11px] tabular-nums">
              v{inspectedRow.versionNo}
            </Badge>
            <StatusBadge tone={inspectedRow.scanTone}>{inspectedRow.scanLabel}</StatusBadge>
            {inspectedRow.submittedAt && (
              <span className="text-xs text-ink-muted tabular-nums">
                · Nộp lúc {formatDateTimeVi(inspectedRow.submittedAt)}
              </span>
            )}
          </div>

          <div className="flex items-center gap-2 shrink-0">
            <div className="inline-flex items-center rounded-lg border border-line-input/80 bg-surface-muted p-0.5">
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="size-7 rounded-md"
                disabled={!canGoPrev}
                aria-label="Báo cáo trước"
                title="Báo cáo trước"
                onClick={() => onNavigate(-1)}
              >
                <ChevronLeft className="size-4" />
              </Button>
              <span className="px-2 text-xs font-semibold text-ink-body tabular-nums">
                {inspectedIndex + 1}/{filteredTotalCount}
              </span>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="size-7 rounded-md"
                disabled={!canGoNext}
                aria-label="Báo cáo tiếp theo"
                title="Báo cáo tiếp theo"
                onClick={() => onNavigate(1)}
              >
                <ChevronRight className="size-4" />
              </Button>
            </div>

            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="size-8 rounded-lg text-ink-muted hover:bg-surface-muted hover:text-ink-heading"
              aria-label="Đóng cửa sổ"
              onClick={onClose}
            >
              <X className="size-4" />
            </Button>
          </div>
        </div>

        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <h2 className="text-base sm:text-lg font-bold text-ink-primary leading-snug">
              {inspectedRow.topicTitle}
            </h2>
            {inspectedRow.topicTitleEn && (
              <p className="text-xs italic text-ink-muted mt-0.5 truncate">
                {inspectedRow.topicTitleEn}
              </p>
            )}
          </div>

          {studentItems.length > 0 && (
            <div className="flex items-center gap-2.5 shrink-0">
              <AvatarGroup items={studentItems} max={3} size="sm" ariaLabel="Sinh viên thực hiện" />
              <div className="text-xs text-ink-body max-w-xs truncate" title={inspectedRow.studentsText}>
                {inspectedRow.studentsText}
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-y-auto bg-surface-subtle/50 p-5 sm:p-6">
        <SimilarityReportPanel
          key={`${inspectedRow.finalReportId}-${inspectedRow.versionNo}`}
          reportId={inspectedRow.finalReportId}
          versionNo={inspectedRow.versionNo}
          embeddedInModal
        />
      </div>
    </Dialog>
  );
};
