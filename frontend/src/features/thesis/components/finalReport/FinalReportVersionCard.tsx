import { MessageSquareQuote } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import type { FinalReportHistoryItemDto } from '../../types/finalReport.types';
import { finalReportApi } from '../../services/finalReportApi';
import { VersionHistoryTimeline, type TimelineVersionItem } from '../shared';

interface FinalReportVersionCardProps {
  versions: FinalReportHistoryItemDto[];
  currentStatus: string;
  currentVersionNo?: number;
  onDownload: (fileId: string) => void;
  onOpenFeedback?: () => void;
  isLoading?: boolean;
}

const statusToneMap: Record<string, { tone: StatusTone; label: string }> = {
  Submitted: { tone: 'info', label: 'Đã nộp' },
  UnderEvaluation: { tone: 'warning', label: 'Đang chấm' },
  Approved: { tone: 'success', label: 'Chuyển phản biện' },
  Rejected: { tone: 'error', label: 'Cần chỉnh sửa' },
};

export function FinalReportVersionCard({
  versions,
  currentStatus,
  currentVersionNo,
  onDownload,
  onOpenFeedback,
  isLoading = false,
}: FinalReportVersionCardProps) {
  const highestNo = currentVersionNo || versions[versions.length - 1]?.versionNo;
  const latestStatusMeta = statusToneMap[currentStatus] || {
    tone: 'neutral' as StatusTone,
    label: 'Đã nộp',
  };

  const items: TimelineVersionItem[] = versions.map((version) => {
    const isCurrent = version.versionNo === highestNo;
    return {
      key: version.versionNo,
      versionNo: version.versionNo,
      fileId: version.file.id,
      fileName: version.file.fileName,
      fileSize: version.file.fileSize,
      timestampUtc: version.file.uploadedAtUtc,
      productLinks: version.productLinks,
      attachments: version.attachments,
      statusBadge: isCurrent ? (
        <StatusBadge tone={latestStatusMeta.tone}>{latestStatusMeta.label}</StatusBadge>
      ) : (
        <StatusBadge tone="neutral">Bản cũ</StatusBadge>
      ),
    };
  });

  return (
    <VersionHistoryTimeline
      title="Phiên bản báo cáo cuối kỳ"
      items={items}
      onDownload={onDownload}
      resolveAttachmentUrl={(attachmentId) =>
        finalReportApi.getAttachmentUrl(attachmentId).then((res) => res.downloadUrl)
      }
      downloadLabel="Tải tệp"
      isLoading={isLoading}
      emptyTitle="Chưa có phiên bản báo cáo cuối kỳ"
      emptyDescription="Sau khi hoàn thành Cổng 2 (Báo cáo giữa kỳ), tải lên báo cáo toàn văn để GVHD chấm và hệ thống đối chiếu tương đồng."
      headerAction={
        onOpenFeedback ? (
          <Button type="button" variant="outline" size="sm" onClick={onOpenFeedback}>
            <MessageSquareQuote className="size-3.5 mr-1.5 text-[#2054a5]" />
            Xem góp ý GVPB
          </Button>
        ) : undefined
      }
    />
  );
}
