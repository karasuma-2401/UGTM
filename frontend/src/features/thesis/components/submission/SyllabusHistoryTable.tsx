import type {
  SyllabusSubmissionHistoryItemDto,
  SyllabusStatus,
} from '../../types/syllabus.types';
import { VersionHistoryTimeline, type TimelineVersionItem } from '../shared';
import { SyllabusStatusBadge } from './SyllabusStatusBadge';

interface SyllabusHistoryTableProps {
  versions: SyllabusSubmissionHistoryItemDto[];
  currentStatus: SyllabusStatus;
  groupCode?: string;
  onDownload: (fileId: string) => void;
  isLoading?: boolean;
}

export function SyllabusHistoryTable({
  versions,
  currentStatus,
  groupCode,
  onDownload,
  isLoading = false,
}: SyllabusHistoryTableProps) {
  const items: TimelineVersionItem[] = versions.map((version) => {
    let versionStatus: SyllabusStatus = currentStatus;
    if (version.review) {
      versionStatus = version.review.decision as SyllabusStatus;
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
      statusBadge: <SyllabusStatusBadge status={versionStatus} />,
      review: version.review
        ? {
            decisionLabel:
              version.review.decision === 'Approved'
                ? 'Văn phòng Khoa phê duyệt Đạt'
                : version.review.decision === 'NeedRevision'
                  ? 'Khoa yêu cầu chỉnh sửa'
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
      title="Phiên bản đề cương đã nộp"
      items={items}
      onDownload={onDownload}
      downloadLabel="Tải tệp"
      isLoading={isLoading}
      emptyTitle="Chưa có phiên bản đề cương nào"
      emptyDescription="Tải lên bản Đề cương chi tiết (.pdf, .doc, .docx) và nhấn Nộp đề cương để bắt đầu thẩm định Cổng 1."
    />
  );
}
