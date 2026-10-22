import type { PagedResult } from './user.types';

export type AuditOutcome = 'Success' | 'Denied' | 'Failed';

export interface AuditLogItem {
  id: number;
  actorUserId: string | null;
  actorEmail: string | null;
  actorFullName: string | null;
  actorSessionId: string | null;
  targetType: string;
  targetId: string;
  action: string;
  outcome: AuditOutcome;
  reasonCode: string | null;
  requestId: string;
  metadata: Record<string, any> | null;
  timestamp: string; // ISO 8601 UTC
}

export interface AuditLogQueryParams {
  actorUserId?: string;
  action?: string;
  targetType?: string;
  outcome?: string;
  fromUtc?: string;
  toUtc?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface AuditLogFilterState {
  searchTerm: string;
  action: string;
  fromUtc: string;
  toUtc: string;
  academicPeriod: string;
}

export interface AuditLogDiffData {
  fieldTitle: string;
  beforeValue: string;
  afterValue: string;
  reason?: string;
  hasDiff: boolean;
}

export type PagedAuditLogResult = PagedResult<AuditLogItem>;

