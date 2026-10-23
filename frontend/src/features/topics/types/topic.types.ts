export type TopicStatus =
  | 'Draft'
  | 'Submitted'
  | 'NeedRevision'
  | 'Approved'
  | 'Rejected'
  | 'Published'
  | 'Closed'
  | 'Archived';

export const REVIEW_TOPIC_STATUSES = [
  'Submitted',
  'NeedRevision',
  'Approved',
  'Rejected',
  'Published',
  'Closed',
  'Archived',
] as const satisfies readonly TopicStatus[];

export const TOPIC_STATUS_LABELS: Record<TopicStatus, string> = {
  Draft: 'Bản nháp',
  Submitted: 'Chờ duyệt',
  NeedRevision: 'Yêu cầu chỉnh sửa',
  Approved: 'Đã duyệt',
  Rejected: 'Không được duyệt',
  Published: 'Đã công bố',
  Closed: 'Đã đóng',
  Archived: 'Đã lưu trữ',
};

export const TOPIC_STATUS_HINTS: Record<TopicStatus, string> = {
  Draft: 'Bạn có thể tiếp tục soạn thảo và gửi Khoa thẩm định khi sẵn sàng.',
  Submitted: 'Đề tài đã gửi đến Khoa. Nội dung đang được khóa trong thời gian chờ duyệt.',
  NeedRevision: 'Khoa yêu cầu chỉnh sửa. Cập nhật nội dung rồi gửi duyệt lại.',
  Approved: 'Khoa đã duyệt đề tài. Đề tài đang chờ công bố cho sinh viên tra cứu.',
  Rejected: 'Đề tài không được duyệt. Bạn có thể tạo một đề xuất mới.',
  Published: 'Đề tài đã được công bố cho sinh viên tra cứu.',
  Closed: 'Đề tài đã đóng. Nội dung chỉ được xem.',
  Archived: 'Đề tài đã được lưu trữ. Nội dung chỉ được xem.',
};

export interface Topic {
  id: string;
  batchId: string;
  advisorId: string;
  titleVi: string;
  titleEn: string | null;
  description: string | null;
  requirements: string | null;
  studentNumber: number | null;
  status: TopicStatus;
  createdAt: string;
  isPublished: boolean;
  reviewNote?: string | null;
}

export interface TopicFormValues {
  batchId: string;
  titleVi: string;
  titleEn: string;
  description: string;
  requirements: string;
  studentNumber: 1 | 2;
}

export interface TopicFilters {
  keyword: string;
  batchId: string;
  status: TopicStatus | '';
}

export interface TopicStatistics {
  total: number;
  submitted: number;
  needRevision: number;
}

export type { AcademicBatch as TopicBatch } from '@/shared/academic-periods/types';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export function canEditTopic(status: TopicStatus): boolean {
  return status === 'Draft' || status === 'NeedRevision';
}
