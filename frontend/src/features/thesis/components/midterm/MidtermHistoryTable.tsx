import type {
  MidtermReportSubmissionHistoryItemDto,
  MidtermReportStatus,
} from '../../types/midterm.types';
import { VersionHistoryTimeline, type TimelineVersionItem } from '../shared';
import { MidtermStatusBadge } from './MidtermStatusBadge';

interface MidtermHistoryTableProps {
  versions: MidtermReportSubmissionHistoryItemDto[];
  currentStatus: MidtermReportStatus;
  groupCode?: string;
  onDownload: (fileId: string) => void;
  isLoading?: boolean;
}

export function MidtermHistoryTable({
  versions,
  currentStatus,
  groupCode,
  onDownload,
  isLoading = false,
}: MidtermHistoryTableProps) {
  const items: TimelineVersionItem[] = versions.map((version) => {
    let versionStatus: MidtermReportStatus = currentStatus;
    if (version.review) {
      versionStatus = version.review.decision as MidtermReportStatus;
    } else if (version.versionNo < versions.length) {
      versionStatus = 'NeedRevision';
    }

    return {
      key: version.versionNo,
      versionNo: version.versionNo,
      fileId: version.file.id,
      fileName: version.file.fileName,
      fileSize: version.file.fileSize,
      timestampUtc: version.file.uploadedAtUtc,
      actorLabel: groupCode ? `Nhóm ${groupCode.replace(/^Nhóm\s*/i, '')}` : null,
      statusBadge: <MidtermStatusBadge status={versionStatus} />,
      review: version.review
        ? {
            decisionLabel:
              version.review.decision === 'Approved'
                ? 'Văn phòng Khoa phê duyệt Đạt Cổng 2'
                : version.review.decision === 'NeedRevision'
                  ? 'Khoa yêu cầu chỉnh sửa giữa kỳ'
                  : 'Khoa đánh giá Không đạt',
            tone:
              version.review.decision === 'Approved'
                ? 'success'
                : version.review.decision === 'NeedRevision'
                  ? 'warning'
                  : 'error',
            feedback: version.review.feedback,
            reviewedAtUtc: version.review.reviewedAtUtc,
          }
        : null,
    };
  });

  return (
    <VersionHistoryTimeline
      title="Phiên bản báo cáo giữa kỳ"
      items={items}
      onDownload={onDownload}
      downloadLabel="Tải tệp"
      isLoading={isLoading}
      emptyTitle="Chưa có phiên bản báo cáo giữa kỳ nào"
      emptyDescription="Sau khi hoàn thành Cổng 1 (Đề cương chi tiết), nhóm tải lên báo cáo giữa kỳ tại khung nộp bài để thẩm định Cổng 2."
    />
  );
}
