import type { Topic } from './topic.types';

export type CatalogAvailabilityFilter = '' | 'available' | 'full';
export type CatalogSortField = 'createdAt' | 'title' | 'availableSlots';

export interface PublishedTopic extends Omit<Topic, 'group' | 'reviewNote'> {
  // Optional for compatibility with backend versions predating availability.
  advisorName?: string | null;
  confirmedStudentsCount?: number | null;
  reservedSlots?: number | null;
  availableSlots?: number | null;
}

export interface StudentRegistrationStatus {
  hasRegistered: boolean;
  groupId: string | null;
  topicId: string | null;
  topicTitleVi: string | null;
  confirmationStatus: string | null;
  confirmationExpiresAtUtc: string | null;
  violationCount: number;
  isRegistrationBlocked: boolean;
  blockedAtUtc: string | null;
  batchId?: string | null;
}

export interface RegistrationResult {
  groupId: string;
  topicId: string;
  batchId: string;
  isPendingConfirmation: boolean;
  confirmationExpiresAtUtc: string | null;
  message: string;
}

export interface ParticipationResult {
  groupId: string;
  topicId: string;
  studentId: string;
  confirmedAtUtc: string;
  message: string;
}

export interface RegistrationTimeline {
  batchId: string;
  status: string;
  currentLocalDate: string;
  stages: { orderIndex: number; stageCode: string; startDate: string; deadlineAtUtc: string }[];
}

export interface RegistrationRequest {
  topic: PublishedTopic;
  partnerUserCode: string;
}

export interface RegistrationReceipt extends RegistrationResult {
  topicTitle: string;
  partnerUserCode: string;
}

export interface StudentTopicGroupDetail {
  groupId: string;
  topicId: string;
  topicTitleVi: string;
  topicTitleEn: string | null;
  batchId: string;
  batchName: string | null;
  advisor: { advisorId: string; fullName: string | null };
  members: {
    studentId: string;
    userCode: string | null;
    fullName: string | null;
    email: string | null;
    confirmationStatus: string;
  }[];
  groupStatus: string;
}
