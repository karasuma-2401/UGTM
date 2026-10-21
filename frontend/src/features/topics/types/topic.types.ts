export type TopicStatus =
  | 'Draft'
  | 'Submitted'
  | 'NeedRevision'
  | 'Approved'
  | 'Rejected'
  | 'Published'
  | 'Closed'
  | 'Archived';

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

export interface TopicBatch {
  id: string;
  batchName: string;
  academicYear: string;
  semester: number;
  status: 'Draft' | 'Open' | 'Closed' | 'Archived';
}

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
