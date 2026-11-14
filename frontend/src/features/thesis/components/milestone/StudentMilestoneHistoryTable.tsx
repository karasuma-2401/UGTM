import type { ProgressReportVersionDto } from '../../types/milestone.types';
import { milestoneApi } from '../../services/milestoneApi';
import { VersionHistoryTimeline, type TimelineVersionItem } from '../shared';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';

interface StudentMilestoneHistoryTableProps {
  versions: ProgressReportVersionDto[];
  deadlineUtc?: string | null;
  onDownload: (fileId: string) => void;
  isLoading?: boolean;
  omitLatestReview?: boolean;
}

export function StudentMilestoneHistoryTable({
  versions,
  deadlineUtc,
  onDownload,
  isLoading = false,
  omitLatestReview = false,
}: StudentMilestoneHistoryTableProps) {
  const items: TimelineVersionItem[] = versions.map((version) => ({
    key: version.versionNo,
    versionNo: version.versionNo,
    fileId: version.file?.id ?? null,
    fileName: version.file?.fileName || `Báo cáo tiến độ v${version.versionNo}`,
    fileSize: version.file?.fileSize ?? null,
    timestampUtc: version.submittedAtUtc,
    actorLabel: 'GVHD đánh giá',
    statusBadge: <MilestoneStatusBadge status={version.status} versionNo={null} />,
    gitUrl: version.gitUrl,
    productLinks: version.productLinks,
    attachments: version.attachments,
    studentNote: version.studentNote,
    deadlineUtc,
    isLate: version.isLate,
    review: version.review
      ? {
          decisionLabel:
            version.review.decision === 'Accepted'
              ? 'GVHD xác nhận Đạt'
              : 'GVHD yêu cầu chỉnh sửa',
          tone: version.review.decision === 'Accepted' ? 'success' : 'warning',
          feedback: version.review.feedback,
          reviewedAtUtc: version.review.reviewedAtUtc,
        }
      : null,
  }));

  return (
    <VersionHistoryTimeline
      title="Lịch sử nộp sản phẩm"
      items={items}
      onDownload={onDownload}
      resolveAttachmentUrl={(attachmentId) =>
        milestoneApi.getAttachmentUrl(attachmentId).then((res) => res.downloadUrl)
      }
      downloadLabel="Tải tệp"
      isLoading={isLoading}
      omitLatestReview={omitLatestReview}
      emptyTitle="Chưa có sản phẩm nào được nộp cho mốc này"
      emptyDescription="Hãy tải lên tệp báo cáo tiến độ kèm liên kết sản phẩm hoặc ảnh minh chứng ở khung phía trên để gửi cho GVHD."
    />
  );
}
