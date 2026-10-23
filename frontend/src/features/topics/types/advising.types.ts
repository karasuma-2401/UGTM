import type { TopicStatus } from './topic.types';

export type RegistrationStatus = 'PendingEmail' | 'Confirmed' | 'Expired' | 'Cancelled';
export type AdvisingTab = 'topics' | 'registrations' | 'advising';

export const REGISTRATION_LABELS: Record<RegistrationStatus, string> = {
  PendingEmail: 'Chờ xác nhận email',
  Confirmed: 'Đã xác nhận email',
  Expired: 'Hết hạn xác nhận',
  Cancelled: 'Khoa đã hủy',
};

export interface AdvisingStudent {
  id: string;
  code: string;
  name: string;
  email: string;
  phone: string;
  emailConfirmedAt: string | null;
}

export interface AdvisingRegistration {
  id: string;
  code: string;
  groupCode: string;
  topicId: string;
  status: RegistrationStatus;
  registeredAt: string;
  confirmationDeadline: string;
  confirmedAt: string | null;
  stage: string | null;
  cancellationReason: string | null;
  students: AdvisingStudent[];
}

export interface AdvisingTopic {
  id: string;
  code: string;
  batchId: string;
  title: string;
  titleEn: string;
  description: string;
  requirements: string;
  capacity: number;
  status: TopicStatus;
}

export interface AdvisingData {
  batches: { id: string; name: string }[];
  topics: AdvisingTopic[];
  registrations: AdvisingRegistration[];
}
